using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   How a caller wants the films a search offered judged.
/// </summary>
public sealed record MovieMatchOptions
{
    /// <summary>
    ///   The title the source was searched with, to judge the candidates
    ///   against. Left out, every title of the anime and every title of the
    ///   episode that is not a placeholder is tried, short titles and
    ///   synonyms only as exact matches of a whole name.
    /// </summary>
    /// <remarks>
    ///   The query is tried as it is and without a sequel suffix such as
    ///   "2nd Season".
    /// </remarks>
    public string? Query { get; init; }

    /// <summary>
    ///   Whether adult candidates may be taken. Rejected otherwise.
    /// </summary>
    public bool IncludeRestricted { get; init; }

    /// <summary>
    ///   Entries another source names as the anime's own, such as the ones
    ///   its AniDB resources link to.
    /// </summary>
    /// <remarks>
    ///   A hint only settles a tie: between films rated alike, a hinted one
    ///   wins before the source's order does, and the ones it beat say so.
    /// </remarks>
    public IReadOnlyCollection<MetadataGuid> HintedIDs { get; init; } = [];
}
