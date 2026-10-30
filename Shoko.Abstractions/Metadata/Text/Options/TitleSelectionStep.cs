using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Text.Options;

/// <summary>
///   One step of a <see cref="TitleSelection"/>: a source's title in a
///   language.
/// </summary>
public sealed record TitleSelectionStep
{
    /// <summary>
    ///   The source to read, or <c>null</c> for any source.
    /// </summary>
    public MetadataSource? Source { get; init; }

    /// <summary>
    ///   The language wanted. <see cref="TitleLanguage.Main"/> reads the
    ///   source's main title instead, whatever its language.
    /// </summary>
    public required TitleLanguage Language { get; init; }

    /// <summary>
    ///   Whether a title other than a main, official or untyped one may
    ///   answer when the source has none of those in the language: a short
    ///   title, synonym, title card or kanji reading.
    /// </summary>
    public bool AllowSynonyms { get; init; }
}
