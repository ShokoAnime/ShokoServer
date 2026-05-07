using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Web;

/// <summary>
///   Input model for creating a new web theme or representing the original
///   state of a theme before an update.
/// </summary>
/// <remarks>
///   Serialized to/from JSON by theme providers and plugin authors.
///   All properties except <see cref="Values"/> are sent as JSON keys.
/// </remarks>
public class WebThemeDefinitionData
{
    /// <summary>
    ///   The display name of the theme. Will be inferred from the filename if omitted.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///   The theme version in semver-like format (major.minor.build.revision).
    /// </summary>
    [JsonProperty("version")]
    [JsonPropertyName("version")]
    [RegularExpression(@"^\d+(\.\d+){0,2}$", ErrorMessage = "Invalid theme version format.")]
    public string Version { get; set; } = "1";

    /// <summary>
    ///   Optional description for the theme.
    /// </summary>
    [JsonProperty("description")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///   Optional tags to make it easier to search for the theme.
    /// </summary>
    [JsonProperty("tags")]
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>
    ///   The author's name.
    /// </summary>
    [JsonProperty("author")]
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    ///   The CSS custom properties (variables) defined in the theme.
    /// </summary>
    [JsonProperty("values")]
    [JsonPropertyName("values")]
    public IReadOnlyDictionary<string, string>? Values { get; set; }

    /// <summary>
    ///   The CSS overrides defined in the theme.
    /// </summary>
    /// <remarks>
    ///   This content is wrapped inside the <c>.theme-{ID}</c> CSS selector block
    ///   when rendered.
    /// </remarks>
    [JsonProperty("css")]
    [JsonPropertyName("css")]
    public string? CssContent { get; set; }

    /// <summary>
    ///   The URL for the theme CSS overrides file. Will be downloaded and stored
    ///   locally when set.
    /// </summary>
    [JsonProperty("cssUrl")]
    [JsonPropertyName("cssUrl")]
    public string? CssUrl { get; set; }

    /// <summary>
    ///   The URL for the theme definition. Used for fetching new versions of the
    ///   theme definition (JSON).
    /// </summary>
    [JsonProperty("updateUrl")]
    [JsonPropertyName("updateUrl")]
    public string? UpdateUrl { get; set; }
}
