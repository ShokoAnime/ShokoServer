
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Convenience extensions for <see cref="IReleaseVideoCrossReference"/> and
/// <see cref="ReleaseVideoCrossReference"/>: reading well-known provider IDs
/// and provider-specific factory helpers.
/// </summary>
public static class ReleaseVideoCrossReferenceExtensions
{
    /// <summary>
    /// Returns the AniDB episode ID from <see cref="IReleaseVideoCrossReference.ProviderIDs"/>,
    /// or <c>null</c> if not present or not a valid integer.
    /// </summary>
    public static int? GetAnidbEpisodeID(this IReleaseVideoCrossReference xref)
        => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Episode, out var v)
           && int.TryParse(v, out var id) ? id : null;

    /// <summary>
    /// Returns the AniDB anime ID from <see cref="IReleaseVideoCrossReference.ProviderIDs"/>,
    /// or <c>null</c> if not present or not a valid integer.
    /// </summary>
    public static int? GetAnidbAnimeID(this IReleaseVideoCrossReference xref)
        => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Anime, out var v)
           && int.TryParse(v, out var id) ? id : null;

    /// <summary>
    /// Populates <paramref name="xref"/> with AniDB episode and optional anime
    /// provider IDs, and returns it to support fluent chaining.
    /// </summary>
    public static ReleaseVideoCrossReference ForAniDB(this ReleaseVideoCrossReference xref,
        int episodeID, int? animeID = null, int? percentStart = null, int? percentEnd = null)
    {
        xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = episodeID.ToString();
        if (animeID.HasValue)
            xref.ProviderIDs[CrossReferenceIDs.AniDB_Anime] = animeID.Value.ToString();
        xref.PercentageStart = percentStart;
        xref.PercentageEnd = percentEnd;
        return xref;
    }
}
