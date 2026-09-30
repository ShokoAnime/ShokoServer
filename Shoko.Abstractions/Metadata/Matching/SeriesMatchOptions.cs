using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   How a caller wants the series a search offered judged.
/// </summary>
/// <remarks>
///   Titles first, then dates, then episode counts to settle what those leave
///   level. The options describe the source and its search, not which source
///   it is.
/// </remarks>
public sealed record SeriesMatchOptions
{
    /// <summary>
    ///   The title the source was searched with, to judge the candidates
    ///   against. Left out, every title the anime has is tried, its short
    ///   titles and synonyms only as exact matches of a whole name.
    /// </summary>
    /// <remarks>
    ///   Tried as is, without a sequel suffix such as "Season 2", and, as a
    ///   close match only, without its subtitle.
    /// </remarks>
    public string? Query { get; init; }

    /// <summary>
    ///   The language <see cref="Query"/> is written in, which decides where
    ///   its subtitle starts: at the first space for Japanese, and at the
    ///   first colon otherwise. Left out, it is read as not Japanese.
    /// </summary>
    public TitleLanguage? QueryLanguage { get; init; }

    /// <summary>
    ///   Whether adult candidates may be taken. Rejected otherwise.
    /// </summary>
    public bool IncludeRestricted { get; init; }

    /// <summary>
    ///   Whether the source keeps each season or cour as an entry of its own,
    ///   rather than one entry holding every season.
    /// </summary>
    /// <remarks>
    ///   Such a source keeps season one under the bare title, so a title that
    ///   only matches without its sequel suffix counts as a close match
    ///   unless the dates agree too.
    /// </remarks>
    public bool SeasonsAreSeparateEntries { get; init; }

    /// <summary>
    ///   Entries another source names as the anime's own, such as the ones
    ///   its AniDB resources link to.
    /// </summary>
    /// <remarks>
    ///   A hint only settles a tie: between series rated alike, a hinted one
    ///   wins before the episode count and the source's order do, and the
    ///   ones it beat say so.
    /// </remarks>
    public IReadOnlyCollection<MetadataGuid> HintedIDs { get; init; } = [];
}
