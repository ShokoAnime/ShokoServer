
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Server.Extensions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public partial class CssThemeService
{
    [GeneratedRegex(@"^\s*(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+)(?:\.(?<revision>\d+))?)?)?\s*$", RegexOptions.ECMAScript | RegexOptions.Compiled)]
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
            _themeDict = ThemeDefinition.FromThemesDirectory().ToDictionary(theme => theme.ID);
        }
        return _themeDict;
    }

    /// <summary>
    /// Get the themes from the theme folder.
    /// </summary>
    /// <param name="forceRefresh"></param>
    /// <returns></returns>
    public IEnumerable<ThemeDefinition> GetThemes(bool forceRefresh = false)
    {
        return RefreshThemes(forceRefresh).Values;
    }

    /// <summary>
    /// Get a specified theme from the theme folder.
    /// </summary>
    /// <param name="themeId">The id of the theme to get.</param>
    /// <param name="forceRefresh">Forcefully refresh the theme dict. before checking for the theme.</param>
    /// <returns></returns>
    public ThemeDefinition? GetTheme(string themeId, bool forceRefresh)
    {
        return RefreshThemes(forceRefresh).TryGetValue(themeId, out var themeDefinition) ? themeDefinition : null;
    }

    /// <summary>
    /// Remove a theme from the theme folder.
    /// </summary>
    /// <param name="theme">The theme to remove.</param>
    /// <returns>A boolean indicating the success status of the operation.</returns>
    public bool RemoveTheme(ThemeDefinition theme)
    {
        var jsonFilePath = Path.Combine(Utils.ApplicationPath, "themes", theme.JsonFileName);
        var cssFilePath = Path.Combine(Utils.ApplicationPath, "themes", theme.CssFileName);
        if (!File.Exists(jsonFilePath) && !File.Exists(cssFilePath))
            return false;

        _themeDict?.Remove(theme.ID);
        if (File.Exists(jsonFilePath))
            File.Delete(jsonFilePath);
        if (File.Exists(cssFilePath))
            File.Delete(cssFilePath);

        return true;
    }

    /// <summary>
    /// Update an existing theme, or preview an update to an existing theme.
    /// </summary>
    /// <param name="theme">The theme to update.</param>
    /// <param name="preview">Flag indicating whether to enable preview mode.</param>
    /// <returns>The updated theme metadata.</returns>
    public async Task<ThemeDefinition> UpdateThemeOnline(ThemeDefinition theme, bool preview = false)
    {
        // Return the local theme if we don't have an update url.
        if (string.IsNullOrEmpty(theme.UpdateUrl))
            return theme;

        if (!(Uri.TryCreate(theme.UpdateUrl, UriKind.Absolute, out var updateUrl) && (updateUrl.Scheme == Uri.UriSchemeHttp || updateUrl.Scheme == Uri.UriSchemeHttps)))
            throw new ValidationException("Invalid update URL in existing theme definition.");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(1);
        var response = await httpClient.GetAsync(updateUrl.AbsoluteUri);

        // Check if the response was a success.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Failed to retrieve theme file.");

        // Check if the response is using the correct content-type.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(contentType) || !_allowedJsonMime.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected JSON.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync();
        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Invalid theme file format.");

        // Try to parse the updated theme.
        var updatedTheme = ThemeDefinition.FromJson(content, theme.ID, preview) ??
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

            var cssResponse = await httpClient.GetAsync(theme.CssUrl);
            if (cssResponse.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Failed to retrieve CSS file with status code {cssResponse.StatusCode}.", null, cssResponse.StatusCode);

            var cssContentType = cssResponse.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(cssContentType) || !_allowedCssMime.Contains(cssContentType))
                throw new ValidationException("Invalid css content-type for resource. Expected \"text/css\" or \"text/plain\".");

            var cssContent = (await cssResponse.Content.ReadAsStringAsync())?.Trim();
            if (string.IsNullOrEmpty(cssContent))
                throw new ValidationException("The css url cannot resolve to an empty resource if it is provided in the theme definition.");

            updatedTheme.CssContent = cssContent;
        }

        // Save the updated theme file if we're not pre-viewing.
        if (!preview)
            await SaveTheme(updatedTheme);

        return updatedTheme;
    }

    /// <summary>
    /// Install a new theme, or preview a new theme before installation.
    /// </summary>
    /// <param name="url">The URL leading to where the theme lives online.</param>
    /// <param name="preview">Flag indicating whether to enable preview mode.</param>
    /// <returns>The new or updated theme metadata.</returns>
    public async Task<ThemeDefinition> InstallThemeFromUrl(string url, bool preview = false)
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
        var response = await httpClient.GetAsync(updateUrl.AbsoluteUri);

        // Check if the response was a success.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Failed to retrieve theme file.");

        // Check if the response is using the correct content-type.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(contentType) || !_allowedJsonMime.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected 'application/json', 'text/json', or 'text/plain.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync();
        return await InstallOrUpdateThemeFromJson(content, fileName, preview);
    }

    public async Task<ThemeDefinition> InstallOrUpdateThemeFromJson(string? content, string fileName, bool preview = false)
    {
        fileName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(fileName) || !FileNameRegex().IsMatch(fileName))
            throw new ValidationException("Invalid theme file name.");

        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Pre-validation failed. Resource is not a valid JSON object.");

        // Try to parse the new theme.
        var id = FileNameToID(fileName);
        var theme = ThemeDefinition.FromJson(content, id, preview) ??
            throw new HttpRequestException("Failed to parse the theme from resource.");

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
            var cssResponse = await httpClient.GetAsync(theme.CssUrl);
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

    public async Task<ThemeDefinition> CreateOrUpdateThemeFromCss(string content, string fileName, bool preview = false)
    {
        if (string.IsNullOrEmpty(fileName) || !FileNameRegex().IsMatch(fileName))
            throw new ValidationException("Invalid theme file name.");

        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("The theme definition cannot be empty.");

        var id = FileNameToID(fileName);
        var theme = GetTheme(id, true) ?? new(id, preview);
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
        var dirPath = Path.Combine(Utils.ApplicationPath, "themes");
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
            await File.WriteAllTextAsync(jsonFilePath, theme.ToJson());
        else if (File.Exists(jsonFilePath))
            File.Delete(jsonFilePath);

        if (_themeDict != null)
            _themeDict[theme.ID] = theme;
    }

    public class ThemeDefinitionInput
    {
        /// <summary>
        ///  The display name of the theme. Will be inferred from the filename if omitted.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; set; } = null;

        /// <summary>
        /// The theme version.
        /// </summary>
        [Required]
        [MinLength(1)]
        [RegularExpression(@"^(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+)(?:\.(?<revision>\d+))?)?)?$")]
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Optional description for the theme, if any.
        /// </summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Optional tags to make it easier to search for the theme.
        /// </summary>
        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<string>? Tags { get; set; } = null;

        /// <summary>
        /// The author's name.
        /// </summary>
        [Required]
        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// The CSS variables defined in the theme.
        /// </summary>
        [JsonProperty("values", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, string>? Values { get; set; } = null;

        /// <summary>
        /// The CSS overrides defined in the theme, if any.
        /// </summary>
        [JsonProperty("css", NullValueHandling = NullValueHandling.Ignore)]
        public string? CssContent { get; set; }

        /// <summary>
        /// The URL for where the theme CSS overrides file lives. Will be downloaded locally if provided. It must end in ".css" and the content type must be "text/plain" or "text/css".
        /// </summary>
        [Url]
        [JsonProperty("cssUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string? CssUrl { get; set; }

        /// <summary>
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        [Url]
        [JsonProperty("updateUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string? UpdateUrl { get; set; }

    }

    public class ThemeDefinition
    {
        /// <summary>
        /// The theme id is inferred from the filename of the theme definition file.
        /// </summary>
        /// <remarks>
        /// Only JSON-files with an alphanumerical filename will be checked if they're themes. All other files will be skipped outright.
        /// </remarks>
        public readonly string ID;

        /// <summary>
        /// The file name associated with the theme.
        /// </summary>
        public readonly string JsonFileName;

        /// <summary>
        /// The name of the CSS file associated with the theme.
        /// </summary>
        public string CssFileName => JsonFileName[..^Path.GetExtension(JsonFileName).Length] + ".css";

        /// <summary>
        /// The display name of the theme.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// A short description about the theme, if available.
        /// </summary>
        public readonly string? Description;

        /// <summary>
        /// Author-defined tags associated with the theme.
        /// </summary>
        public readonly IReadOnlyList<string> Tags;

        /// <summary>
        /// The name of the author of the theme definition.
        /// </summary>
        public readonly string? Author;

        /// <summary>
        /// The theme version.
        /// </summary>
        public readonly Version Version;

        /// <summary>
        /// The CSS variables to define for the theme.
        /// </summary>
        /// <remarks>
        /// Not sent to the client as a dictionary. The user should hot-reload the `themes.css` file to load the CSS for the theme if it's not already available.
        /// </remarks>
        [JsonIgnore]
        public readonly IReadOnlyDictionary<string, string> Values;

        /// <summary>
        /// The CSS content to define for the theme.
        /// </summary>
        [JsonIgnore]
        public string? CssContent { get; internal set; } = string.Empty;

        /// <summary>
        /// The URL for where the theme CSS overrides file lives. Will be downloaded locally if provided. It must end in ".css" and the content type must be "text/plain" or "text/css".
        /// </summary>
        public string? CssUrl { get; internal set; }

        /// <summary>
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        public readonly string? UpdateUrl;

        /// <summary>
        /// Indicates this is only a preview of the theme metadata and the theme
        /// might not actually be installed yet.
        /// </summary>
        public readonly bool IsPreview;

        private bool? _isInstalled;

        /// <summary>
        /// Indicates the theme is installed locally.
        /// </summary>
        public bool IsInstalled => _isInstalled ??= File.Exists(Path.Combine(Utils.ApplicationPath, "themes", JsonFileName)) || File.Exists(Path.Combine(Utils.ApplicationPath, "themes", CssFileName));

        public ThemeDefinition(string id, bool preview = false)
        {
            ID = id;
            JsonFileName = $"{id}.json";
            Name = NameFromID(ID);
            Tags = [];
            Version = new Version(1, 0, 0, 0);
            Values = new Dictionary<string, string>();
            IsPreview = preview;
        }

        public ThemeDefinition(ThemeDefinitionInput input, string id, bool preview = false)
        {
            // We use a regex match and parse the result instead of using the built-in version parer
            // directly because the built-in parser is more rigged then what we want to support.
            var versionMatch = VersionRegex().Match(input.Version);
            var major = int.Parse(versionMatch.Groups["major"].Value);
            var minor = versionMatch.Groups["minor"].Success ? int.Parse(versionMatch.Groups["minor"].Value) : 0;
            var build = versionMatch.Groups["build"].Success ? int.Parse(versionMatch.Groups["build"].Value) : 0;
            var revision = versionMatch.Groups["revision"].Success ? int.Parse(versionMatch.Groups["build"].Value) : 0;

            ID = id;
            JsonFileName = $"{id}.json";
            Name = string.IsNullOrEmpty(input.Name) ? NameFromID(ID) : input.Name;
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description;
            Tags = input.Tags ?? [];
            Author = input.Author;
            Version = new Version(major, minor, build, revision);
            Values = input.Values ?? new Dictionary<string, string>();
            CssUrl = input.CssUrl;
            CssContent = input.CssContent;
            UpdateUrl = input.UpdateUrl;
            IsPreview = preview;
        }

        public string ToCSS()
        {
            var cssFile = Path.Combine(Utils.ApplicationPath, "themes", CssFileName);
            var css = new StringBuilder()
                .Append('\n')
                .Append($".theme-{ID} {{\n");
            if (Values.Count > 0)
                css.Append("  " + Values.Select(pair => $" --{pair.Key}: {pair.Value};").Join("\n  ") + "\n");

            if (Values.Count > 0 && !string.IsNullOrWhiteSpace(CssContent))
                css.Append('\n');

            if (!string.IsNullOrWhiteSpace(CssContent))
                css
                    .Append("  " + CssContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : $"  {line.TrimEnd()}").Join("\n  ") + "\n");

            return css
                .AppendLine("}\n")
                .ToString();
        }

        public string? ToJson() => !string.IsNullOrEmpty(Author)
            ? JsonConvert.SerializeObject(new ThemeDefinitionInput()
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

        internal static ThemeDefinition? FromJson(string? json, string id, bool preview = false)
        {
            try
            {
                // Simple sanity check before parsing the file contents.
                if (string.IsNullOrWhiteSpace(json) || json[0] != '{' || json[^1] != '}')
                    return null;
                var input = JsonConvert.DeserializeObject<ThemeDefinitionInput>(json);
                if (input == null)
                    return null;

                var theme = new ThemeDefinition(input, id, preview);
                return theme;
            }
            catch
            {
                return null;
            }
        }

        internal static IReadOnlyList<ThemeDefinition> FromThemesDirectory()
        {
            var dirPath = Path.Combine(Utils.ApplicationPath, "themes");
            if (!Directory.Exists(dirPath))
                return new List<ThemeDefinition>();

            var allowedExtensions = new HashSet<string>() { ".json", ".css" };
            return Directory.GetFiles(dirPath)
                .GroupBy(a => Path.GetFileNameWithoutExtension(a))
                .Where(a => !string.IsNullOrEmpty(a.Key) && FileNameRegex().IsMatch(a.Key) && a.Any(b => allowedExtensions.Contains(Path.GetExtension(b))))
                .Select(FromPath)
                .WhereNotNull()
                .DistinctBy(theme => theme.ID)
                .OrderBy(theme => theme.ID)
                .ToList();
        }

        private static ThemeDefinition? FromPath(IGrouping<string, string> fileDetails)
        {
            // Check file extension.
            var id = FileNameToID(fileDetails.Key);
            var jsonFile = fileDetails.FirstOrDefault(a => string.Equals(Path.GetExtension(a), ".json", StringComparison.InvariantCultureIgnoreCase));
            var theme = string.IsNullOrEmpty(jsonFile) ? new(id) : FromJson(File.ReadAllText(jsonFile)?.Trim(), id);
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
        => fileName.ToLowerInvariant().Replace('_', '-');

    private static string NameFromID(string id)
        => string.Join(
            ' ',
            id.Replace('_', '-')
                .Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => segment[0..1].ToUpperInvariant() + segment[1..].ToLowerInvariant())
        );
}
