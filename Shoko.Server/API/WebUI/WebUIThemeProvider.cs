
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Commons.Extensions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.WebUI;

public static class WebUIThemeProvider
{
    private static Regex VersionRegex = new Regex(@"^\s*(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+)(?:\.(?<revision>\d+))?)?)?\s*$", RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static Regex FileNameRegex = new Regex(@"^\b[A-Za-z][A-Za-z0-9_\-]*\b$", RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static ISet<string> AllowedMIMEs = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "application/json", "text/json", "text/plain" };

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
    /// Get a spesified theme from the theme folder.
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
        if (string.IsNullOrEmpty(theme.URL))
            if (preview)
                throw new ValidationException("No update URL in existing theme definition.");
            else
                return theme;

        if (!(Uri.TryCreate(theme.URL, UriKind.Absolute, out var updateUrl) && (updateUrl.Scheme == Uri.UriSchemeHttp || updateUrl.Scheme == Uri.UriSchemeHttps)))
            throw new ValidationException("Invalid update URL in existing theme definition.");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(1);
        var response = await httpClient.GetAsync(updateUrl.AbsoluteUri);

        // Check if the response was a success.
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Failed to retrieve theme file.");

        // Check if the response is using the correct content-type.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(contentType) || !AllowedMIMEs.Contains(contentType))
            throw new HttpRequestException("Invalid content-type. Expected JSON.");

        // Simple sanity check before parsing the response content.
        var content = await response.Content.ReadAsStringAsync();
        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content[0] != '{' || content[^1] != '}')
            throw new HttpRequestException("Invalid theme file format.");

        // Try to parse the updated theme.
        var updatedTheme = ThemeDefinition.FromJson(content, theme.ID, theme.FileName, preview) ??
            throw new HttpRequestException("Failed to parse the updated theme.");

        // Save the updated theme file if we're not pre-viewing.
        if (!preview)
        {
            var dirPath = Path.Combine(Utils.ApplicationPath, "themes");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

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
        if (string.IsNullOrEmpty(contentType) || !AllowedMIMEs.Contains(contentType))
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

        // Save the new theme file if we're not pre-viewing.
        if (!preview)
        {
            var dirPath = Path.Combine(Utils.ApplicationPath, "themes");
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

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
        public string? Name = null;

        /// <summary>
        /// The theme version.
        /// </summary>
        [Required]
        [RegularExpression(@"^\s*(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<build>\d+)(?:\.(?<revision>\d+))?)?)?\s*$")]
        public string Version = string.Empty;

        /// <summary>
        /// Optional description for the theme, if any.
        /// </summary>
        public string? Description = null;

        /**
         * Optional tags to make it easier to search for the theme.
         */
        public IReadOnlyList<string>? Tags;

        /// <summary>
        /// The author's name.
        /// </summary>
        [Required]
        public string Author = string.Empty;

        /// <summary>
        /// The CSS variables defined in the theme.
        /// </summary>
        [Required]
        [MinLength(1)]
        public IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>();

        /// <summary>
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        [Url]
        public string? URL;
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
        /// The file name assosiated with the theme.
        /// </summary>
        public readonly string FileName;

        /// <summary>
        /// The display name of the theme.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// A short description about the theme, if available.
        /// </summary>
        public readonly string? Description;

        /// <summary>
        /// Author-defined tags assosiated with the theme.
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
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        public readonly string? URL;

        /// <summary>
        /// Indicates this is only a preview of the theme metadata and the theme
        /// might not actaully be installed yet.
        /// </summary>
        public readonly bool IsPreview;

        /// <summary>
        /// Indicates the theme is installed locally.
        /// </summary>
        public readonly bool IsInstalled;

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
            Tags = input.Tags ?? new List<string>();
            Author = input.Author;
            Version = new Version(major, minor, build, revision);
            Values = input.Values ?? new Dictionary<string, string>();
            URL = input.URL;
            IsPreview = preview;
            IsInstalled = File.Exists(Path.Combine(Utils.ApplicationPath, "themes", fileName));
        }

        public string ToCSS()
            => $".theme-{ID} {{{string.Join(" ", Values.Select(pair => $" --{pair.Key}: {pair.Value};"))} }}";

        internal static IReadOnlyList<ThemeDefinition> FromDirectory(string dirPath)
        {
            // Resolve path relative to the application directory and check if
            // it exists.
            dirPath = Path.Combine(Utils.ApplicationPath, dirPath);
            if (!Directory.Exists(dirPath))
                return new List<ThemeDefinition>();

            return Directory.GetFiles(dirPath)
                .Select(FromPath)
                .WhereNotNull()
                .DistinctBy(theme => theme.ID)
                .OrderBy(theme => theme.ID)
                .ToList();
        }

        internal static ThemeDefinition? FromPath(string filePath)
        {
            // Check file extension.
            var extName = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extName) || !string.Equals(extName, ".json", StringComparison.InvariantCultureIgnoreCase))
                return null;

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(fileName) || !FileNameRegex.IsMatch(fileName))
                return null;

            if (!File.Exists(filePath))
                return null;

            // Safely try to read
            string? fileContents;
            try
            {
                fileContents = File.ReadAllText(filePath)?.Trim();
            }
            catch
            {
                return null;
            }
            // Simple sanity check before parsing the file contents.
            if (string.IsNullOrWhiteSpace(fileContents) || fileContents[0] != '{' || fileContents[^1] != '}')
                return null;

            var id = FileNameToID(fileName);
            return FromJson(fileContents, id, Path.GetFileName(filePath));
        }

        internal static ThemeDefinition? FromJson(string json, string id, string fileName, bool preview = false)
        {
            try
            {
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
        => string.Join(" ", list.Select(theme => theme.ToCSS()));
}
