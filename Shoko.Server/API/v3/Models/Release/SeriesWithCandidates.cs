using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
/// A series paired with its ranked release candidates.
/// </summary>
public class SeriesWithCandidates
{
    /// <summary>Shoko series ID.</summary>
    [Required]
    public required int SeriesID { get; init; }

    /// <summary>Display title for the series.</summary>
    [Required]
    public required string SeriesTitle { get; init; }

    /// <summary>AniDB anime ID.</summary>
    [Required]
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    /// True when the series has no end date or its end date is in the future.
    /// Affects whether per-file deletion is applied for airing series.
    /// </summary>
    [Required]
    public required bool IsAiring { get; init; }

    /// <summary>
    /// True when at least one candidate is fully covered by the primary candidate
    /// (rank 1) and could be safely deleted.
    /// </summary>
    [Required]
    public required bool HasRedundantCandidates { get; init; }

    /// <summary>
    /// Ranked release candidates for the series, best-first (rank 1 is the primary).
    /// </summary>
    [Required]
    public required IReadOnlyList<ReleaseCandidate> Candidates { get; init; }

    /// <summary>
    /// All release groups for the series, including groups with partial episode
    /// coverage that are excluded from <see cref="Candidates"/>. Used as the data
    /// source for the release override (Mix &amp; Match) view. Only populated on
    /// the single-series detail endpoint; empty on the list endpoint.
    /// </summary>
    [Required]
    public IReadOnlyList<ReleaseOverride> Overrides { get; init; } = [];
}
