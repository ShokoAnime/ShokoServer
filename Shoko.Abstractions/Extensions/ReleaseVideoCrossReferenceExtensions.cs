
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Extensions;

/// <summary>
///   Convenience extensions for <see cref="IReleaseVideoCrossReference"/> and
///   <see cref="ReleaseVideoCrossReference"/>: reading well-known provider IDs
///   and provider-specific factory helpers.
/// </summary>
public static class ReleaseVideoCrossReferenceExtensions
{
    extension(IReleaseVideoCrossReference xref)
    {
        /// <summary>
        ///   Returns the AniDB episode ID from
        ///   <see cref="IReleaseVideoCrossReference.ProviderIDs"/>,
        ///   or <c>null</c> if not present or not a valid integer.
        /// </summary>
        public int? AnidbEpisodeID
            => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Episode, out var v) && int.TryParse(v, out var id) && id > 0 ? id : null;

        /// <summary>
        ///   Returns the AniDB anime ID from
        ///   <see cref="IReleaseVideoCrossReference.ProviderIDs"/>,
        ///   or <c>null</c> if not present or not a valid integer.
        /// </summary>
        public int? AnidbAnimeID
            => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Anime, out var v) && int.TryParse(v, out var id) && id > 0 ? id : null;
    }

    extension(ReleaseVideoCrossReference xref)
    {
        /// <summary>
        ///   Returns the AniDB episode ID from
        ///   <see cref="ReleaseVideoCrossReference.ProviderIDs"/>,
        ///   or <c>null</c> if not present or not a valid integer.
        /// </summary>
        public int? AnidbEpisodeID
        {
            get => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Episode, out var v) && int.TryParse(v, out var id) && id > 0 ? id : null;
            set
            {
                if (value is { } and > 0)
                    xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = value.ToString()!;
                else
                    xref.ProviderIDs.Remove(CrossReferenceIDs.AniDB_Episode);
            }
        }

        /// <summary>
        ///   Returns the AniDB anime ID from
        ///   <see cref="ReleaseVideoCrossReference.ProviderIDs"/>,
        ///   or <c>null</c> if not present or not a valid integer.
        /// </summary>
        public int? AnidbAnimeID
        {
            get => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Anime, out var v) && int.TryParse(v, out var id) && id > 0 ? id : null;
            set
            {
                if (value is { } and > 0)
                    xref.ProviderIDs[CrossReferenceIDs.AniDB_Anime] = value.ToString()!;
                else
                    xref.ProviderIDs.Remove(CrossReferenceIDs.AniDB_Anime);
            }
        }

        /// <summary>
        ///   Populates a new cross-reference with AniDB episode and optional
        ///   anime provider IDs, and returns it to support fluent chaining.
        /// </summary>
        public static ReleaseVideoCrossReference ForAniDB(int episodeID, int? animeID = null, int? percentStart = null, int? percentEnd = null)
            => new()
            {
                AnidbEpisodeID = episodeID,
                AnidbAnimeID = animeID,
                PercentageStart = percentStart,
                PercentageEnd = percentEnd,
            };
    }
}
