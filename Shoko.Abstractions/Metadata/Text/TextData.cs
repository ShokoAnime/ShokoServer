using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Text;

/// <summary>
///   A new text to store on an entry through
///   <see cref="IMetadataTextManager.AddText"/>.
/// </summary>
public sealed class TextData
{
    /// <summary>
    ///   Whether the text is a title or an overview.
    /// </summary>
    public required TextKind Kind { get; init; }

    /// <summary>
    ///   The text itself. Must not be blank.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    ///   The language code: alpha 2, alpha 3 or an <c>x-</c> prefixed custom
    ///   code, or <c>unk</c> when unknown.
    /// </summary>
    public required string LanguageCode { get; init; }

    /// <summary>
    ///   The alpha 2 or alpha 3 country code, when there is one.
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>
    ///   The ISO 15924 script code, such as <c>Latn</c>, when it is known.
    /// </summary>
    public string? ScriptCode { get; init; }

    /// <summary>
    ///   The language. Worked out from <see cref="LanguageCode"/> and
    ///   <see cref="CountryCode"/> when left out.
    /// </summary>
    public TitleLanguage? Language { get; init; }

    /// <summary>
    ///   The kind of title. Ignored for an overview.
    /// </summary>
    public TitleType TitleType { get; init; } = TitleType.None;

    /// <summary>
    ///   Who the text is from. <see cref="MetadataSource.User"/> when left out.
    /// </summary>
    public MetadataSource? Source { get; init; }

    /// <summary>
    ///   How strongly the text is preferred for its entry. Setting a
    ///   preference clears the same one on the entry's other texts.
    /// </summary>
    public TextPreference Preference { get; init; } = TextPreference.None;

    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
