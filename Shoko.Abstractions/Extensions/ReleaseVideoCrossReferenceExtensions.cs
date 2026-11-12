
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Extensions;

/// <summary>
///   Convenience extensions for <see cref="IReleaseVideoCrossReference"/> and
///   <see cref="ReleaseVideoCrossReference"/>: reading the main entry and the
///   AniDB IDs among their <see cref="IReleaseVideoCrossReference.IDs"/>, and provider-specific
///   factory helpers.
/// </summary>
public static class ReleaseVideoCrossReferenceExtensions
{
    extension(IReleaseVideoCrossReference xref)
    {
        /// <summary>
        ///   The cross-reference's main entry, the one a video is linked to
        ///   through it: the first of its
        ///   <see cref="IReleaseVideoCrossReference.IDs"/> by
        ///   <see cref="ReleaseCrossReferenceIDComparer"/>, or <c>null</c>
        ///   when it names none.
        /// </summary>
        public MetadataGuid? MainID
        {
            get
            {
                MetadataGuid? main = null;
                foreach (var id in xref.IDs)
                {
                    if (main is null || ReleaseCrossReferenceIDComparer.Instance.Compare(id, main) < 0)
                        main = id;
                }

                return main;
            }
        }

        /// <summary>
        ///   Whether the cross-reference's main entry is an AniDB one, which
        ///   it is whenever it names any AniDB entry, AniDB coming first.
        /// </summary>
        public bool IsAnidb
            => xref.MainID?.Source == MetadataSource.AniDB;

        /// <summary>
        ///   Returns the first AniDB episode ID in
        ///   <see cref="IReleaseVideoCrossReference.IDs"/>, or <c>0</c> if
        ///   there is none or it is not a positive integer.
        /// </summary>
        public int AnidbEpisodeID
            => FindPositiveID(xref.IDs, MetadataEntityType.Episode) ?? 0;

        /// <summary>
        ///   Returns the first AniDB anime ID in
        ///   <see cref="IReleaseVideoCrossReference.IDs"/>, or <c>null</c> if
        ///   there is none or it is not a positive integer.
        /// </summary>
        public int? AnidbAnimeID
            => FindPositiveID(xref.IDs, MetadataEntityType.Series);
    }

    extension(ReleaseVideoCrossReference xref)
    {
        /// <summary>
        ///   Gets or sets the AniDB episode ID in
        ///   <see cref="ReleaseVideoCrossReference.IDs"/>. Reads <c>0</c> if
        ///   there is none or it is not a positive integer. Setting it replaces
        ///   every AniDB episode ID with the new one, put first, and setting
        ///   <c>0</c> or less removes them.
        /// </summary>
        public int AnidbEpisodeID
        {
            get => FindPositiveID(xref.IDs, MetadataEntityType.Episode) ?? 0;
            set
            {
                xref.IDs.RemoveAll(id => IsAnidb(id, MetadataEntityType.Episode));
                if (value > 0)
                    xref.IDs.Insert(0, new(MetadataSource.AniDB, MetadataEntityType.Episode, value.ToString()));
            }
        }

        /// <summary>
        ///   Gets or sets the AniDB anime ID in
        ///   <see cref="ReleaseVideoCrossReference.IDs"/>. Reads <c>null</c> if
        ///   there is none or it is not a positive integer. Setting it replaces
        ///   every AniDB anime ID with the new one, put last, and setting
        ///   <c>null</c>, <c>0</c> or less removes them.
        /// </summary>
        public int? AnidbAnimeID
        {
            get => FindPositiveID(xref.IDs, MetadataEntityType.Series);
            set
            {
                xref.IDs.RemoveAll(id => IsAnidb(id, MetadataEntityType.Series));
                if (value is > 0)
                    xref.IDs.Add(new(MetadataSource.AniDB, MetadataEntityType.Series, value.Value.ToString()));
            }
        }

        /// <summary>
        ///   Populates a new cross-reference with an AniDB episode ID and an
        ///   optional AniDB anime ID, and returns it to support fluent
        ///   chaining.
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

        /// <summary>
        ///   Populates a new cross-reference with entries of any source, and
        ///   returns it to support fluent chaining.
        /// </summary>
        /// <param name="ids">
        ///   The entries the video is linked to.
        /// </param>
        /// <param name="percentStart">
        ///   Where in the entries the video starts, in percent, if it covers only part of them.
        /// </param>
        /// <param name="percentEnd">
        ///   Where in the entries the video ends, in percent, if it covers only part of them.
        /// </param>
        /// <returns>
        ///   The new cross-reference.
        /// </returns>
        public static ReleaseVideoCrossReference For(IEnumerable<MetadataGuid> ids, int? percentStart = null, int? percentEnd = null)
            => new()
            {
                IDs = [.. ids],
                PercentageStart = percentStart,
                PercentageEnd = percentEnd,
            };
    }

    /// <summary>
    ///   Whether an ID is an AniDB ID of a kind.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <param name="entityType">The kind.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsAnidb(MetadataGuid id, MetadataEntityType entityType)
        => id.Source == MetadataSource.AniDB && id.EntityType == entityType;

    /// <summary>
    ///   Finds the first AniDB ID of a kind, read as a positive integer.
    /// </summary>
    /// <param name="ids">The IDs.</param>
    /// <param name="entityType">The kind.</param>
    /// <returns>The ID, or <c>null</c> when the first one is missing or not a positive integer.</returns>
    private static int? FindPositiveID(IReadOnlyList<MetadataGuid> ids, MetadataEntityType entityType)
    {
        foreach (var id in ids)
        {
            if (IsAnidb(id, entityType))
                return id.TryGetNumericID<int>(out var value) && value > 0 ? value : null;
        }

        return null;
    }
}
