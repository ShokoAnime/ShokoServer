
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
using Shoko.Commons.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.WebUI;

public static class WebUIThemeProvider
{
    private static readonly Regex VersionRegex = new Regex(@"^\s*(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+)(?:\.(?<revision>\d+))?)?)?\s*$", RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static readonly Regex FileNameRegex = new Regex(@"^\b[A-Za-z][A-Za-z0-9_\-]*\b$", RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static readonly ISet<string> AllowedJsonMime = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "application/json", "text/json", "text/plain" };

    private static readonly ISet<string> AllowedCssMime = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "text/css", "text/plain" };

    private static DateTime? NextRefreshAfter = null;

    private static Dictionary<string, ThemeDefinition>? ThemeDict = null;

    private static Dictionary<string, ThemeDefinition> RefreshThemes(bool forceRefresh = false)
    {
        if (ThemeDict == null || forceRefresh || DateTime.UtcNow > NextRefreshAfter)
        {
            NextRefreshAfter = DateTime.UtcNow.AddMinutes(10);
            ThemeDict = ThemeDefinition.FromDirectory("themes").ToDictionary(theme => theme.ID);
        }
        return ThemeDict;
    }

    /// <summary>
    /// Get the themes from the theme folder.
    /// </summary>
    /// <param name="forceRefresh"></param>
    /// <returns></returns>
    public static IEnumerable<ThemeDefinition> GetThemes(bool forceRefresh = false)
    {
        return RefreshThemes(forceRefresh).Values;
    }

    /// <summary>
    /// Get a specified theme from the theme folder.
    /// </summary>
    /// <param name="themeId">The id of the theme to get.</param>
    /// <param name="forceRefresh">Forcefully refresh the theme dict. before checking for the theme.</param>
    /// <returns></returns>
    public static ThemeDefinition? GetTheme(string themeId, bool forceRefresh)
    {
        return RefreshThemes(forceRefresh).TryGetValue(themeId, out var themeDefinition) ? themeDefinition : null;
    }

    /// <summary>
    /// Remove a theme from the theme folder.
    /// </summary>
    /// <param name="theme">The theme to remove.</param>
    /// <returns>A boolean indicating the success status of the operation.</returns>
    public static bool RemoveTheme(ThemeDefinition theme)
    {
        var filePath = Path.Combine(Utils.ApplicationPath, "themes", theme.FileName);
        if (!File.Exists(filePath))
            return false;

        File.Delete(filePath);
        ThemeDict?.Remove(theme.ID);

        return true;
    }

    /// <summary>
    /// Update an existing theme, or preview an update to an existing theme.
    /// </summary>
    /// <param name="theme">The theme to update.</param>
    /// <param name="preview">Flag indicating whether to enable preview mode.</param>
    /// <returns>The updated theme metadata.</returns>
    public static async Task<ThemeDefinition> UpdateTheme(ThemeDefinition theme, bool preview = false)
    {
        // Return the local theme if we don't have an update url.
        if (string.IsNullOrEmpty(theme.UpdateUrl))
            if (preview)
                throw new ValidationException("No update URL in existing theme definition.");
            else
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
        if (string.IsNullOrEmpty(contentType) || !AllowedJsonMime.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected JSON.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync();
        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Invalid theme file format.");

        // Try to parse the updated theme.
        var updatedTheme = ThemeDefinition.FromJson(content, theme.ID, theme.FileName, preview) ??
            throw new HttpRequestException("Failed to parse the updated theme.");

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

            var cssResponse = await httpClient.GetAsync(theme.CssUrl);
            if (cssResponse.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Failed to retrieve CSS file with status code {cssResponse.StatusCode}.", null, cssResponse.StatusCode);

            var cssContentType = cssResponse.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(cssContentType) || !AllowedCssMime.Contains(cssContentType))
                throw new ValidationException("Invalid css content-type for resource. Expected \"text/css\" or \"text/plain\".");

            var cssContent = (await cssResponse.Content.ReadAsStringAsync())?.Trim();
            if (string.IsNullOrEmpty(cssContent))
                throw new ValidationException("The css url cannot resolve to an empty resource if it is provided in the theme definition.");

            updatedTheme.CssContent = cssContent;
        }

        // Save the updated theme file if we're not pre-viewing.
        if (!preview)
        {
            var dirPath = Path.Combine(Utils.ApplicationPath, "themes");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            if (!string.IsNullOrEmpty(theme.CssContent))
            {
                var cssFilePath = Path.Combine(dirPath, theme.CssFileName);
                await File.WriteAllTextAsync(cssFilePath, theme.CssContent);
            }

            var filePath = Path.Combine(dirPath, theme.FileName);
            await File.WriteAllTextAsync(filePath, content);

            if (ThemeDict != null && !ThemeDict.TryAdd(theme.ID, updatedTheme))
                ThemeDict[theme.ID] = updatedTheme;
        }

        return updatedTheme;
    }

    /// <summary>
    /// Install a new theme, or preview a new theme before installation.
    /// </summary>
    /// <param name="url">The URL leading to where the theme lives online.</param>
    /// <param name="preview">Flag indicating whether to enable preview mode.</param>
    /// <returns>The new or updated theme metadata.</returns>
    public static async Task<ThemeDefinition> InstallTheme(string url, bool preview = false)
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
            throw new ValidationException("Invalid theme file format. Expected JSON.");

        // Check if the file name conforms to our specified format.
        var fileName = Path.GetFileNameWithoutExtension(lastFragment);
        if (string.IsNullOrEmpty(fileName) || !FileNameRegex.IsMatch(fileName))
            throw new ValidationException("Invalid theme file name.");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(1);
        var response = await httpClient.GetAsync(updateUrl.AbsoluteUri);

        // Check if the response was a success.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Failed to retrieve theme file.");

        // Check if the response is using the correct content-type.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(contentType) || !AllowedJsonMime.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected JSON.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync();
        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Invalid theme file format.");

        // Try to parse the new theme.
        var id = FileNameToID(fileName);
        var theme = ThemeDefinition.FromJson(content, id, fileName + extName, preview) ??
            throw new HttpRequestException("Failed to parse the new theme.");

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

            var cssResponse = await httpClient.GetAsync(theme.CssUrl);
            if (cssResponse.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Failed to retrieve CSS file with status code {cssResponse.StatusCode}.", null, cssResponse.StatusCode);

            var cssContentType = cssResponse.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(cssContentType) || !AllowedCssMime.Contains(cssContentType))
                throw new ValidationException("Invalid css content-type for resource. Expected \"text/css\" or \"text/plain\".");

            var cssContent = (await cssResponse.Content.ReadAsStringAsync())?.Trim();
            if (string.IsNullOrEmpty(cssContent))
                throw new ValidationException("The css url cannot resolve to an empty resource if it is provided in the theme definition.");

            theme.CssContent = cssContent;
        }

        // Save the new theme file if we're not pre-viewing.
        if (!preview)
        {
            var dirPath = Path.Combine(Utils.ApplicationPath, "themes");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            if (!string.IsNullOrEmpty(theme.CssContent))
            {
                var cssFilePath = Path.Combine(dirPath, theme.CssFileName);
                await File.WriteAllTextAsync(cssFilePath, theme.CssContent);
            }

            var filePath = Path.Combine(dirPath, fileName + extName);
            await File.WriteAllTextAsync(filePath, content);

            if (ThemeDict != null && !ThemeDict.TryAdd(id, theme))
                ThemeDict[id] = theme;
        }

        return theme;
    }

    public class ThemeDefinitionInput
    {
        /// <summary>
        ///  The display name of the theme. Will be inferred from the filename if omitted.
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// The theme version.
        /// </summary>
        [Required]
        [RegularExpression(@"^\s*(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+)(?:\.(?<revision>\d+))?)?)?\s*$")]
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Optional description for the theme, if any.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Optional tags to make it easier to search for the theme.
        /// </summary>
        [JsonProperty("tags")]
        public IReadOnlyList<string>? Tags { get; set; }

        /// <summary>
        /// The author's name.
        /// </summary>
        [Required]
        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// The CSS variables defined in the theme.
        /// </summary>
        [Required]
        [MinLength(1)]
        [JsonProperty("values")]
        public IReadOnlyDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        [Url]
        [JsonProperty("update")]
        public string? UpdateUrl { get; set; }

        /// <summary>
        /// The URL for where the theme CSS overrides file lives. Will be downloaded locally if provided. It must end in ".css" and the content type must be "text/plain" or "text/css".
        /// </summary>
        [Url]
        [JsonProperty("css")]
        public string? CssUrl { get; set; }
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
        public readonly string FileName;

        /// <summary>
        /// The name of the CSS file associated with the theme.
        /// </summary>
        public string CssFileName => FileName[..^Path.GetExtension(FileName).Length] + ".css";

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
        public readonly string Author;

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
        public bool IsInstalled => _isInstalled ??= File.Exists(Path.Combine(Utils.ApplicationPath, "themes", FileName)) || File.Exists(Path.Combine(Utils.ApplicationPath, "themes", CssFileName));

        public ThemeDefinition(string id)
        {
            ID = id;
            FileName = $"{id}.json";
            Name = NameFromID(ID);
            Tags = [];
            Author = "<unknown>";
            Version = new Version(1, 0, 0, 0);
            Values = new Dictionary<string, string>();
            IsPreview = false;
        }

        public ThemeDefinition(ThemeDefinitionInput input, string id, string fileName, bool preview = false)
        {
            // We use a regex match and parse the result instead of using the built-in version parer
            // directly because the built-in parser is more rigged then what we want to support.
            var versionMatch = VersionRegex.Match(input.Version);
            var major = int.Parse(versionMatch.Groups["major"].Value);
            var minor = versionMatch.Groups["minor"].Success ? int.Parse(versionMatch.Groups["minor"].Value) : 0;
            var build = versionMatch.Groups["build"].Success ? int.Parse(versionMatch.Groups["build"].Value) : 0;
            var revision = versionMatch.Groups["revision"].Success ? int.Parse(versionMatch.Groups["build"].Value) : 0;
            if (string.IsNullOrEmpty(fileName))
                fileName = $"{id}.json";

            ID = id;
            FileName = fileName;
            Name = string.IsNullOrEmpty(input.Name) ? NameFromID(ID) : input.Name;
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description;
            Tags = input.Tags ?? [];
            Author = input.Author;
            Version = new Version(major, minor, build, revision);
            Values = input.Values ?? new Dictionary<string, string>();
            CssUrl = input.CssUrl;
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

            if (Values.Count == 0 && !string.IsNullOrWhiteSpace(CssContent))
                css.Append('\n');

            if (!string.IsNullOrWhiteSpace(CssContent))
                css
                    .Append("  " + CssContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : $"  {line.TrimEnd()}").Join("\n  ") + "\n");

            return css
                .AppendLine("}\n")
                .ToString();
        }

        internal static IReadOnlyList<ThemeDefinition> FromDirectory(string dirPath)
        {
            // Resolve path relative to the application directory and check if
            // it exists.
            dirPath = Path.Combine(Utils.ApplicationPath, dirPath);
            if (!Directory.Exists(dirPath))
                return new List<ThemeDefinition>();

            var allowedExtensions = new HashSet<string>() { ".json", ".css" };
            return Directory.GetFiles(dirPath)
                .GroupBy(a => Path.GetFileNameWithoutExtension(a))
                .Where(a => !string.IsNullOrEmpty(a.Key) && FileNameRegex.IsMatch(a.Key) && a.Any(b => allowedExtensions.Contains(Path.GetExtension(b))))
                .Select(FromPath)
                .WhereNotNull()
                .DistinctBy(theme => theme.ID)
                .OrderBy(theme => theme.ID)
                .ToList();
        }

        internal static ThemeDefinition? FromPath(IGrouping<string, string> fileDetails)
        {
            // Check file extension.
            var id = FileNameToID(fileDetails.Key);
            var jsonFile = fileDetails.FirstOrDefault(a => string.Equals(Path.GetExtension(a), ".json", StringComparison.InvariantCultureIgnoreCase));
            var theme = string.IsNullOrEmpty(jsonFile) ? new(id) : FromJson(File.ReadAllText(jsonFile)?.Trim(), id, Path.GetFileName(jsonFile));
            if (theme is not null)
            {
                var cssFileName = fileDetails.FirstOrDefault(a => string.Equals(Path.GetExtension(a), ".css", StringComparison.InvariantCultureIgnoreCase)) ??
                    Path.Combine(Path.GetDirectoryName(jsonFile)!, theme.CssFileName);
                if (File.Exists(cssFileName))
                    theme.CssContent = File.ReadAllText(cssFileName);
            }

            return theme;
        }

        internal static ThemeDefinition? FromJson(string? json, string id, string fileName, bool preview = false)
        {
            try
            {
                // Simple sanity check before parsing the file contents.
                if (string.IsNullOrWhiteSpace(json) || json[0] != '{' || json[^1] != '}')
                    return null;
                var input = JsonConvert.DeserializeObject<ThemeDefinitionInput>(json);
                if (input == null)
                    return null;

                var theme = new ThemeDefinition(input, id, fileName, preview);
                return theme;
            }
            catch
            {
                return null;
            }
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

    public static string ToCSS(this IEnumerable<ThemeDefinition> list)
        => list.Select(theme => theme.ToCSS()).Join("");
}
