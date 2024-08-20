using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents a text description from a data source.
/// </summary>
public class TextDescription
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
    /// The country code.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// The value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

