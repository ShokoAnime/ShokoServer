using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Shoko series metadata.
/// </summary>
public interface IShokoSeries : ISeries, IWithCreationDate, IWithUpdateDate
{
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
    IReadOnlyList<IShokoTagForSeries> Tags { get; }

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
    ///   Wether or not AniList auto matching is disabled for the Shoko series.
    /// </summary>
    bool AnilistAutoMatchingDisabled { get; }

    /// <summary>
    /// A direct link to all AniList anime linked to the Shoko series.
    /// </summary>
    IReadOnlyList<IAnilistAnime> AnilistAnime { get; }

    /// <summary>
    /// All Shoko series ↔ AniList anime cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<IAnilistAnimeCrossReference> AnilistAnimeCrossReferences { get; }

    /// <summary>
    ///   Wether or not TMDB auto matching is disabled for the Shoko series.
    /// </summary>
    bool TmdbAutoMatchingDisabled { get; }

    /// <summary>
    /// A direct link to all TMDB shows linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbShow> TmdbShows { get; }

    /// <summary>
    /// A direct link to all TMDB seasons linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbSeason> TmdbSeasons { get; }

    /// <summary>
    /// A direct link to all TMDB movies linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbMovie> TmdbMovies { get; }

    /// <summary>
    /// All Shoko series ↔ TMDB show cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbShowCrossReference> TmdbShowCrossReferences { get; }

    /// <summary>
    /// All Shoko series ↔ TMDB season cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB episode cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB movie cross references linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }

    /// <summary>
    /// All series linked to the Shoko series.
    /// </summary>
    IReadOnlyList<ISeries> LinkedSeries { get; }

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
    /// All known fake "seasons" for the Shoko series.
    /// </summary>
    new IReadOnlyList<IShokoSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the the Shoko series.
    /// </summary>
    new IReadOnlyList<IShokoEpisode> Episodes { get; }

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
}
