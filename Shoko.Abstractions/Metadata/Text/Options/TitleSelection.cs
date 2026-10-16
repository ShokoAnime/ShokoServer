using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Text.Options;

/// <summary>
///   How <see cref="IMetadataTextManager.ChooseTitle"/> picks a title out of
///   candidates: steps walked in order, the first one a title answers wins.
///   <see cref="IMetadataTextManager.GetTitleSelection"/> builds one from the
///   settings, to use as is or to change.
/// </summary>
public sealed record TitleSelection
{
    /// <summary>
    ///   The steps, best first.
    /// </summary>
    public required IReadOnlyList<TitleSelectionStep> Steps { get; init; }

    /// <summary>
    ///   Whether every step is first walked for real titles only, and again
    ///   for generic ones such as <c>Episode 5</c>, as for episodes, so a real
    ///   title in a later step beats a generic one in an earlier step.
    /// </summary>
    public bool RankGenericTitlesLast { get; init; }

    /// <summary>
    ///   Whether a user's picks count: a title a user prefers overall wins
    ///   outright, and one a user prefers for a language wins before the
    ///   first step in that language. Defaults to <c>true</c>.
    /// </summary>
    public bool UsePreferences { get; init; } = true;
}

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
