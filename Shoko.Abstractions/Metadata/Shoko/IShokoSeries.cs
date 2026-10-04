using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Shoko series metadata.
/// </summary>
public interface IShokoSeries : ISeries<IShokoSeries, IShokoEpisode>
{
    /// <summary>
    ///   The Shoko series ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int LocalID { get; }

    /// <summary>
    ///   Always <c>null</c>: a Shoko series is never refreshed from
    ///   a source, and does not take its AniDB anime's time.
    /// </summary>
    DateTime? ISeries.LastRefreshedAt { get => null; }

    /// <summary>
    /// AniDB anime id linked to the Shoko series.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    /// The id of the direct parent group of the Shoko series.
    /// </summary>
    int ParentGroupID { get; }

    /// <summary>
    /// The id of the top-level parent group of the Shoko series.
    /// </summary>
    int TopLevelGroupID { get; }

    /// <summary>
    /// All custom tags for the Shoko series set by the user.
    /// </summary>
    new IReadOnlyList<IShokoTagForSeries> Tags { get; }

    /// <summary>
    ///   The number of missing normal episodes and specials for the Shoko
    ///   series.
    /// </summary>
    int MissingEpisodeCount { get; }

    /// <summary>
    ///   The number of missing normal episodes and specials for the Shoko
    ///   series which have been released by a release group we're collecting.
    /// </summary>
    int MissingCollectingEpisodeCount { get; }

    /// <summary>
    ///   The number of hidden missing normal episodes and specials for the
    ///   Shoko series.
    /// </summary>
    int HiddenMissingEpisodeCount { get; }

    /// <summary>
    ///   The number of hidden missing normal episodes and specials for the
    ///   Shoko series which have been released by a release group we're
    ///   collecting.
    /// </summary>
    int HiddenMissingCollectingEpisodeCount { get; }

    /// <summary>
    /// A direct link to the AniDB anime metadata.
    /// </summary>
    IAnidbAnime AnidbAnime { get; }

    /// <summary>
    /// All series linked to the Shoko series: its AniDB anime first, then
    /// every series another source links to it.
    /// </summary>
    IReadOnlyList<ISeries> LinkedSeries { get; }

    /// <summary>
    /// All seasons of other sources the Shoko series' episodes are linked
    /// into, worked out from their episode links.
    /// </summary>
    IReadOnlyList<ISeason> LinkedSeasons { get; }

    /// <summary>
    /// All movies linked to the Shoko series.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    /// The direct parent group of the series.
    /// </summary>
    IShokoGroup ParentGroup { get; }

    /// <summary>
    /// The top-level parent group of the series. It may or may not be the same
    /// as <see cref="ParentGroup"/> depending on how nested your group
    /// structure is.
    /// </summary>
    IShokoGroup TopLevelGroup { get; }

    /// <summary>
    /// Get an enumerable for all parent groups, starting at the
    /// <see cref="ParentGroup"/> all the way up to the <see cref="TopLevelGroup"/>.
    /// </summary>
    IReadOnlyList<IShokoGroup> AllParentGroups { get; }

    /// <summary>
    /// The local episode counts for the series, broken down by type.
    /// </summary>
    EpisodeCounts LocalEpisodeCounts { get; }

    /// <summary>
    /// The missing episode counts for the series, broken down by type (aired but not locally available).
    /// </summary>
    EpisodeCounts MissingEpisodeCounts { get; }

    /// <summary>
    /// The unaired episode counts for the series, broken down by type (not yet aired and not locally available).
    /// </summary>
    EpisodeCounts UnairedEpisodeCounts { get; }

    /// <summary>
    /// The file source counts for the series.
    /// </summary>
    FileSourceCounts FileSourceCounts { get; }

    /// <summary>
    /// Release provider name to file count mapping for the series.
    /// Provider names are split by '+' before counting.
    /// </summary>
    IReadOnlyDictionary<string, int> ReleaseProviderCounts { get; }

    /// <summary>
    ///   Gets the user-specific data for the Shoko series and user.
    /// </summary>
    /// <param name="user">
    ///   The user to get the data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="user"/> is not stored in the database.
    /// </exception>
    /// <returns>
    ///   The user-specific data for the Shoko series and user.
    /// </returns>
    ISeriesUserData GetUserData(IUser user);

    /// <summary>
    ///   Whether a source has been told to leave this series alone when it
    ///   links on its own.
    /// </summary>
    /// <remarks>
    ///   Set through <see cref="Services.IMetadataLinkingService.SetAutoLinkingDisabled"/>.
    /// </remarks>
    /// <param name="source">The source being asked about.</param>
    /// <returns>
    ///   <c>true</c> when that source must not link this series on
    ///   its own.
    /// </returns>
    bool IsAutoLinkingDisabled(MetadataSource source);
}
