
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
        ///   or <c>0</c> if not present or not a valid positive integer.
        /// </summary>
        public int AnidbEpisodeID
            => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Episode, out var v) && int.TryParse(v, out var id) && id > 0 ? id : 0;

        /// <summary>
        ///   Returns the AniDB anime ID from
        ///   <see cref="IReleaseVideoCrossReference.ProviderIDs"/>,
        ///   or <c>null</c> if not present or not a valid positive integer.
        /// </summary>
        public int? AnidbAnimeID
            => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Anime, out var v) && int.TryParse(v, out var id) && id > 0 ? id : null;
    }

    extension(ReleaseVideoCrossReference xref)
    {
        /// <summary>
        ///   Gets or sets the AniDB episode ID in
        ///   <see cref="ReleaseVideoCrossReference.ProviderIDs"/>. Reads
        ///   <c>0</c> if not present or not a valid positive integer, and
        ///   setting <c>0</c> or less removes it.
        /// </summary>
        public int AnidbEpisodeID
        {
            get => xref.ProviderIDs.TryGetValue(CrossReferenceIDs.AniDB_Episode, out var v) && int.TryParse(v, out var id) && id > 0 ? id : 0;
            set
            {
                if (value is { } and > 0)
                    xref.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = value.ToString()!;
                else
                    xref.ProviderIDs.Remove(CrossReferenceIDs.AniDB_Episode);
            }
        }

        /// <summary>
        ///   Gets or sets the AniDB anime ID in
        ///   <see cref="ReleaseVideoCrossReference.ProviderIDs"/>. Reads
        ///   <c>null</c> if not present or not a valid positive integer, and
        ///   setting <c>null</c>, <c>0</c> or less removes it.
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
        /// <param name="episodeID">
        ///   The AniDB episode ID.
        /// </param>
        /// <param name="animeID">
        ///   The AniDB anime ID, if known.
        /// </param>
        /// <param name="percentStart">
        ///   Where in the episode the video starts, in percent, if it covers only part of it.
        /// </param>
        /// <param name="percentEnd">
        ///   Where in the episode the video ends, in percent, if it covers only part of it.
        /// </param>
        /// <returns>
        ///   The new cross-reference.
        /// </returns>
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
