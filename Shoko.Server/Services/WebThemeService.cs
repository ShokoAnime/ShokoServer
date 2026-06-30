using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Web;
using Shoko.Abstractions.Web.Services;

namespace Shoko.Server.Services;

public partial class WebThemeService(IApplicationPaths applicationPaths) : IWebThemeService
{
    [GeneratedRegex(@"^\s*(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+))?)?\s*$", RegexOptions.ECMAScript | RegexOptions.Compiled)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^\b[A-Za-z][A-Za-z0-9_\-]*\b$", RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex FileNameRegex();

    private static readonly ISet<string> _allowedJsonMime = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "application/json", "text/json", "text/plain" };

    private static readonly ISet<string> _allowedCssMime = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "text/css", "text/plain" };

    private DateTime? _nextRefreshAfter = null;

    private Dictionary<string, ThemeDefinition>? _themeDict = null;

    private Dictionary<string, ThemeDefinition> RefreshThemes(bool forceRefresh = false)
    {
        if (_themeDict == null || forceRefresh || DateTime.UtcNow > _nextRefreshAfter)
        {
            _nextRefreshAfter = DateTime.UtcNow.AddMinutes(10);
            _themeDict = ThemeDefinition.FromThemesDirectory(applicationPaths).ToDictionary(theme => theme.ID);
        }
        return _themeDict;
    }

    public IReadOnlyList<IWebThemeDefinition> GetThemes(bool forceRefresh = false)
    {
        return RefreshThemes(forceRefresh).Values.ToList();
    }

    public IWebThemeDefinition? GetTheme(string themeId, bool forceRefresh = false)
    {
        return RefreshThemes(forceRefresh).TryGetValue(themeId, out var themeDefinition) ? themeDefinition : null;
    }

    public bool RemoveTheme(IWebThemeDefinition theme)
    {
        var jsonFilePath = Path.Combine(applicationPaths.ThemesPath, theme.JsonFileName);
        var cssFilePath = Path.Combine(applicationPaths.ThemesPath, theme.CssFileName);
        if (!File.Exists(jsonFilePath) && !File.Exists(cssFilePath))
            return false;

        _themeDict?.Remove(theme.ID);
        if (File.Exists(jsonFilePath))
            File.Delete(jsonFilePath);
        if (File.Exists(cssFilePath))
            File.Delete(cssFilePath);

        return true;
    }

    public async Task<IWebThemeDefinition> UpdateThemeOnline(IWebThemeDefinition theme, bool preview = false, CancellationToken cancellationToken = default)
    {
        // Return the local theme if we don't have an update url.
        if (string.IsNullOrEmpty(theme.UpdateUrl))
            return theme;

        if (!(Uri.TryCreate(theme.UpdateUrl, UriKind.Absolute, out var updateUrl) && (updateUrl.Scheme == Uri.UriSchemeHttp || updateUrl.Scheme == Uri.UriSchemeHttps)))
            throw new ValidationException("Invalid update URL in existing theme definition.");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(1);
        using var response = await httpClient.GetAsync(updateUrl.AbsoluteUri, cancellationToken);

        // Check if the response was a success.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Failed to retrieve theme file.");

        // Check if the response is using the correct content-type.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(contentType) || !_allowedJsonMime.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected JSON.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Invalid theme file format.");

        // Try to parse the updated theme.
        var updatedTheme = ThemeDefinition.FromJson(applicationPaths, content, theme.ID, preview) ??
            throw new HttpRequestException("Failed to parse the updated theme.");

        if (updatedTheme.Version <= theme.Version)
            throw new ValidationException("New theme version is lower than the existing theme version.");

        if (!(Uri.TryCreate(updatedTheme.UpdateUrl, UriKind.Absolute, out updateUrl) && (updateUrl.Scheme == Uri.UriSchemeHttp || updateUrl.Scheme == Uri.UriSchemeHttps)))
            throw new ValidationException("Invalid update URL in new theme definition.");

        if (!string.IsNullOrEmpty(updatedTheme.CssUrl))
        {
            if (!Uri.TryCreate(updatedTheme.CssUrl, UriKind.RelativeOrAbsolute, out var cssUrl))
                throw new ValidationException("Invalid CSS URL in new theme definition.");

            if (!cssUrl.IsAbsoluteUri)
            {
                if (updateUrl is null)
                    throw new ValidationException("Unable to resolve CSS URL in new theme definition because it is relative and no update URL was provided.");
                cssUrl = new Uri(updateUrl, cssUrl);
                updatedTheme.CssUrl = cssUrl.AbsoluteUri;
            }

            if (cssUrl.Scheme != Uri.UriSchemeHttp && cssUrl.Scheme != Uri.UriSchemeHttps)
                throw new ValidationException("Invalid CSS URL in existing theme definition.");

            if (!string.IsNullOrEmpty(theme.CssContent))
                throw new ValidationException("Theme already has CSS overrides inlined. Remove URL or inline CSS first before proceeding.");

            using var cssResponse = await httpClient.GetAsync(theme.CssUrl, cancellationToken);
            if (cssResponse.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Failed to retrieve CSS file with status code {cssResponse.StatusCode}.", null, cssResponse.StatusCode);

            var cssContentType = cssResponse.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(cssContentType) || !_allowedCssMime.Contains(cssContentType))
                throw new ValidationException("Invalid css content-type for resource. Expected \"text/css\" or \"text/plain\".");

            var cssContent = (await cssResponse.Content.ReadAsStringAsync(cancellationToken))?.Trim();
            if (string.IsNullOrEmpty(cssContent))
                throw new ValidationException("The css url cannot resolve to an empty resource if it is provided in the theme definition.");

            updatedTheme.CssContent = cssContent;
        }

        // Save the updated theme file if we're not pre-viewing.
        if (!preview)
            await SaveTheme(updatedTheme);

        return updatedTheme;
    }

    public async Task<IWebThemeDefinition> InstallThemeFromUrl(string url, bool preview = false, CancellationToken cancellationToken = default)
    {
        if (!(Uri.TryCreate(url, UriKind.Absolute, out var updateUrl) && (updateUrl.Scheme == Uri.UriSchemeHttp || updateUrl.Scheme == Uri.UriSchemeHttps)))
            throw new ValidationException("Invalid repository URL.");

        // Try to get the last fragment of the path.
        var lastFragment = updateUrl.AbsolutePath.Split('/').LastOrDefault()?.TrimEnd();
        if (string.IsNullOrEmpty(lastFragment))
            throw new ValidationException("Invalid theme file URL.");

        // Fallback to ".json" if there is no extension at the end.
        var extName = Path.GetExtension(lastFragment);
        if (string.IsNullOrEmpty(extName))
            extName = ".json";

        // We _want_ it to be JSON.
        if (!string.Equals(extName, ".json", StringComparison.InvariantCultureIgnoreCase))
            throw new ValidationException("Invalid theme file name extension. Expected '.json'.");

        // Check if the file name conforms to our specified format.
        var fileName = Path.GetFileNameWithoutExtension(lastFragment);
        if (string.IsNullOrEmpty(fileName) || !FileNameRegex().IsMatch(fileName))
            throw new ValidationException("Invalid theme file name.");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(1);
        using var response = await httpClient.GetAsync(updateUrl.AbsoluteUri, cancellationToken);

        // Check if the response was a success.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Failed to retrieve theme file.");

        // Check if the response is using the correct content-type.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(contentType) || !_allowedJsonMime.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected 'application/json', 'text/json', or 'text/plain.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return await InstallOrUpdateThemeFromJson(content, fileName, preview, cancellationToken);
    }

    public async Task<IWebThemeDefinition> InstallOrUpdateThemeFromJson(string? content, string fileName, bool preview = false, CancellationToken cancellationToken = default)
    {
        fileName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(fileName) || !FileNameRegex().IsMatch(fileName))
            throw new ValidationException("Invalid theme file name.");

        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Pre-validation failed. Resource is not a valid JSON object.");

        // Try to parse the new theme.
        var data = JsonConvert.DeserializeObject<WebThemeDefinitionData>(content) ??
            throw new HttpRequestException("Failed to parse the theme from resource.");
        return await InstallOrUpdateThemeFromData(data, fileName, preview, cancellationToken);
    }

    public async Task<IWebThemeDefinition> InstallOrUpdateThemeFromData(WebThemeDefinitionData data, string? fileName = null, bool preview = false, CancellationToken cancellationToken = default)
    {
        fileName ??= data.Name ?? "unknown-theme";
        var id = FileNameToID(fileName);
        var theme = new ThemeDefinition(applicationPaths, data, id, preview);

        Uri? updateUrl = null;
        if (!string.IsNullOrEmpty(theme.UpdateUrl) && !(Uri.TryCreate(theme.UpdateUrl, UriKind.Absolute, out updateUrl) && (updateUrl.Scheme == Uri.UriSchemeHttp || updateUrl.Scheme == Uri.UriSchemeHttps)))
            throw new ValidationException("Invalid update URL in new theme definition.");

        if (!string.IsNullOrEmpty(theme.CssUrl))
        {
            if (!Uri.TryCreate(theme.CssUrl, UriKind.RelativeOrAbsolute, out var cssUrl))
                throw new ValidationException("Invalid CSS URL in new theme definition.");

            if (!cssUrl.IsAbsoluteUri)
            {
                if (updateUrl is null)
                    throw new ValidationException("Unable to resolve CSS URL in theme definition because it is relative and no update URL was provided.");
                cssUrl = new Uri(updateUrl, cssUrl);
                theme.CssUrl = cssUrl.AbsoluteUri;
            }

            if (cssUrl.Scheme != Uri.UriSchemeHttp && cssUrl.Scheme != Uri.UriSchemeHttps)
                throw new ValidationException("Invalid CSS URL in theme definition.");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(1);
            using var cssResponse = await httpClient.GetAsync(theme.CssUrl);
            if (cssResponse.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Failed to retrieve CSS file with status code {cssResponse.StatusCode}.", null, cssResponse.StatusCode);

            var cssContentType = cssResponse.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(cssContentType) || !_allowedCssMime.Contains(cssContentType))
                throw new ValidationException("Invalid css content-type for resource. Expected \"text/css\" or \"text/plain\".");

            var cssContent = await cssResponse.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(cssContent))
                throw new ValidationException("The css url cannot resolve to an empty resource if it is provided in the theme definition.");

            theme.CssContent = cssContent;
        }

        if (theme.Values.Count == 0 && string.IsNullOrWhiteSpace(theme.CssContent))
            throw new ValidationException("The theme definition cannot be empty.");

        // Save the new theme file if we're not pre-viewing.
        if (!preview)
            await SaveTheme(theme);

        return theme;
    }

    public async Task<IWebThemeDefinition> CreateOrUpdateThemeFromCss(string content, string fileName, bool preview = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fileName) || !FileNameRegex().IsMatch(fileName))
            throw new ValidationException("Invalid theme file name.");

        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("The theme definition cannot be empty.");

        var id = FileNameToID(fileName);
        var theme = RefreshThemes(true).TryGetValue(id, out var themeDefinition)
            ? themeDefinition
            : new(applicationPaths, id, preview);

        if (!string.IsNullOrEmpty(theme.UpdateUrl))
            throw new ValidationException("Unable to manually update a theme with an update URL set.");
        theme.CssContent = content;

        // Save the new theme file if we're not pre-viewing.
        if (!preview)
            await SaveTheme(theme);

        return theme;
    }

    private async Task SaveTheme(ThemeDefinition theme)
    {
        var dirPath = applicationPaths.ThemesPath;
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        var cssContent = theme.CssContent;
        var cssFilePath = Path.Combine(dirPath, theme.CssFileName);
        if (!string.IsNullOrEmpty(cssContent))
            await File.WriteAllTextAsync(cssFilePath, cssContent);
        else if (File.Exists(cssFilePath))
            File.Delete(cssFilePath);

        var jsonContent = theme.ToJson();
        var jsonFilePath = Path.Combine(dirPath, theme.JsonFileName);
        if (!string.IsNullOrEmpty(jsonContent))
            await File.WriteAllTextAsync(jsonFilePath, jsonContent);
        else if (File.Exists(jsonFilePath))
            File.Delete(jsonFilePath);

        if (_themeDict != null)
            _themeDict[theme.ID] = theme;
    }

    internal class ThemeDefinition : IWebThemeDefinition
    {
        private readonly IApplicationPaths _applicationPaths;

        public string ID { get; set; }

        public string JsonFileName => $"{ID}.json";

        public string CssFileName => $"{ID}.css";

        public string Name { get; set; }

        public string? Description { get; set; }

        public IReadOnlyList<string> Tags { get; set; }

        public string? Author { get; set; }

        public Version Version { get; set; }

        public IReadOnlyDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        public string? CssContent { get; set; }

        public string? CssUrl { get; set; }

        public string? UpdateUrl { get; set; }

        public bool IsPreview { get; set; }

        private bool? _isInstalled;

        public bool IsInstalled => _isInstalled ??= File.Exists(Path.Join(_applicationPaths.ThemesPath, JsonFileName)) || File.Exists(Path.Join(_applicationPaths.ThemesPath, CssFileName));

        public ThemeDefinition(IApplicationPaths applicationPaths, string id, bool preview = false)
        {
            _applicationPaths = applicationPaths;

            ID = id;
            Name = NameFromID(id);
            Tags = [];
            Version = new(1, 0);
            Values = new Dictionary<string, string>();
            IsPreview = preview;
        }

        public ThemeDefinition(IApplicationPaths applicationPaths, WebThemeDefinitionData data, string id, bool preview = false)
        {
            // We use a regex match and parse the result instead of using the built-in version parer
            // directly because the built-in parser is more rigged then what we want to support.
            var parsedVersion = ParseVersion(data.Version);

            _applicationPaths = applicationPaths;
            ID = id;
            Name = string.IsNullOrEmpty(data.Name) ? NameFromID(id) : data.Name;
            Description = string.IsNullOrWhiteSpace(data.Description) ? null : data.Description;
            Tags = data.Tags ?? [];
            Author = data.Author;
            Version = parsedVersion;
            Values = data.Values ?? new Dictionary<string, string>();
            CssContent = data.CssContent;
            CssUrl = data.CssUrl;
            UpdateUrl = data.UpdateUrl;
            IsPreview = preview;
        }

        public string? ToJson() => !string.IsNullOrEmpty(Author) || Values.Count > 0 || Name != NameFromID(ID)
            ? JsonConvert.SerializeObject(new WebThemeDefinitionData
            {
                Name = Name,
                Version = Version.ToString(),
                Description = Description,
                Tags = Tags.Count is > 0 ? Tags : null,
                Author = Author,
                Values = Values.Count is > 0 ? Values : null,
                CssUrl = string.IsNullOrEmpty(CssUrl) ? null : CssUrl,
                UpdateUrl = string.IsNullOrEmpty(UpdateUrl) ? null : UpdateUrl,
            })
            : null;

        internal static ThemeDefinition? FromJson(IApplicationPaths applicationPaths, string? json, string id, bool preview = false)
        {
            try
            {
                // Simple sanity check before parsing the file contents.
                if (string.IsNullOrWhiteSpace(json) || json[0] != '{' || json[^1] != '}')
                    return null;
                var input = JsonConvert.DeserializeObject<WebThemeDefinitionData>(json);
                if (input == null)
                    return null;

                return new(applicationPaths, input, id, preview);
            }
            catch
            {
                return null;
            }
        }

        internal static IReadOnlyList<ThemeDefinition> FromThemesDirectory(IApplicationPaths applicationPaths)
        {
            var dirPath = applicationPaths.ThemesPath;
            if (!Directory.Exists(dirPath))
                return [];

            var allowedExtensions = new HashSet<string> { ".json", ".css" };
            return Directory.GetFiles(dirPath)
                .GroupBy(a => Path.GetFileNameWithoutExtension(a))
                .Where(a => !string.IsNullOrEmpty(a.Key) && FileNameRegex().IsMatch(a.Key) && a.Any(b => allowedExtensions.Contains(Path.GetExtension(b))))
                .Select(fileDetails => FromPath(applicationPaths, fileDetails))
                .WhereNotNull()
                .DistinctBy(theme => theme.ID)
                .OrderBy(theme => theme.ID)
                .ToList();
        }

        private static ThemeDefinition? FromPath(IApplicationPaths applicationPaths, IGrouping<string, string> fileDetails)
        {
            // Check file extension.
            var id = FileNameToID(fileDetails.Key);
            var jsonFile = fileDetails.FirstOrDefault(a => string.Equals(Path.GetExtension(a), ".json", StringComparison.InvariantCultureIgnoreCase));
            var theme = string.IsNullOrEmpty(jsonFile) ? new(applicationPaths, id) : FromJson(applicationPaths, File.ReadAllText(jsonFile)?.Trim(), id);
            if (theme is not null)
            {
                var cssFileName = fileDetails.FirstOrDefault(a => string.Equals(Path.GetExtension(a), ".css", StringComparison.InvariantCultureIgnoreCase)) ??
                    Path.Combine(Path.GetDirectoryName(jsonFile)!, theme.CssFileName);
                if (File.Exists(cssFileName))
                    theme.CssContent = File.ReadAllText(cssFileName);
            }

            return theme;
        }
    }

    private static string FileNameToID(string fileName)
        => fileName.ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('_', '-');

    private static string NameFromID(string id)
        => id.Replace('_', '-')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => segment[0..1].ToUpperInvariant() + segment[1..].ToLowerInvariant())
            .Join(' ');

    private static Version ParseVersion(string versionString)
    {
        var match = VersionRegex().Match(versionString);
        if (!match.Success)
            return new(1, 0);

        var major = int.Parse(match.Groups["major"].Value);
        if (!match.Groups["minor"].Success)
            return new(major, 0);

        var minor = int.Parse(match.Groups["minor"].Value);
        if (!match.Groups["build"].Success)
            return new(major, minor);

        var build = int.Parse(match.Groups["build"].Value);
        return new(major, minor, build);
    }
}
