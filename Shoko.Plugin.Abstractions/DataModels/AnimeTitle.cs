
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents a title from a data source.
/// </summary>
public class AnimeTitle
{
    /// <summary>
    /// The source.
    /// </summary>
    public DataSourceEnum Source { get; set; }

    /// <summary>
    /// The language.
    /// </summary>
    public TitleLanguage Language { get; set; }

    /// <summary>
    /// The language code.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// The country code, if available and applicable.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// The title value.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The type.
    /// </summary>
    public TitleType Type { get; set; }
}

