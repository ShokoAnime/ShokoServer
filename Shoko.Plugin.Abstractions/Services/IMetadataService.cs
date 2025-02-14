using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Provides functionality for interacting with metadata from various providers,
/// including Shoko and other providers.
/// </summary>
public interface IMetadataService
{
    #region Episode

    /// <summary>
    /// Dispatched when episode metadata from any provider is added.
    /// </summary>
    event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeAdded;

    /// <summary>
    /// Dispatched when episode metadata from any provider is updated.
    /// </summary>
    event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeUpdated;

    /// <summary>
    /// Dispatched when episode metadata from any provider is removed.
    /// </summary>
    event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeRemoved;

    /// <summary>
    /// Looks up all episodes for a given provider as a queryable list.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>A collection of episodes if found, otherwise an empty collection.</returns>
    IEnumerable<IEpisode> GetAllEpisodesForProvider(ProviderName providerName);

    /// <summary>
    /// Looks up an episode by its provider ID.
    /// </summary>
    /// <param name="providerID">The provider ID of the episode.</param>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The episode if found, otherwise <see langword="null"/>.</returns>
    IEpisode? GetEpisodeByProviderID(int providerID, ProviderName providerName);

    /// <summary>
    /// Looks up all shoko episodes as a queryable list.
    /// </summary>
    /// <returns>A collection of episodes if found, otherwise an empty collection.</returns>
    IEnumerable<IShokoEpisode> GetAllShokoEpisodes();

    /// <summary>
    /// Looks up a shoko episode by its ID.
    /// </summary>
    /// <param name="episodeID">The ID of the episode.</param>
    /// <returns>The episode if found, otherwise <see langword="null"/>.</returns>
    IShokoEpisode? GetShokoEpisodeByID(int episodeID);

    /// <summary>
    /// Looks up a shoko episode by its AniDB ID.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB ID of the episode.</param>
    /// <returns>The episode if found, otherwise <see langword="null"/>.</returns>
    IShokoEpisode? GetShokoEpisodeByAnidbID(int anidbEpisodeID);

    #endregion

    #region Movie

    /// <summary>
    /// Dispatched when movie metadata from any provider is added.
    /// </summary>
    event EventHandler<MovieInfoUpdatedEventArgs> MovieAdded;

    /// <summary>
    /// Dispatched when movie metadata from any provider is updated.
    /// </summary>
    event EventHandler<MovieInfoUpdatedEventArgs> MovieUpdated;

    /// <summary>
    /// Dispatched when movie metadata from any provider is removed.
    /// </summary>
    event EventHandler<MovieInfoUpdatedEventArgs> MovieRemoved;

    /// <summary>
    /// Looks up all movies for a given provider as a queryable list.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>A collection of movies if found, otherwise an empty collection.</returns>
    IEnumerable<IMovie> GetAllMoviesForProvider(ProviderName providerName);

    /// <summary>
    /// Looks up a movie by its provider ID.
    /// </summary>
    /// <param name="providerID">The provider ID of the movie.</param>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The movie if found, otherwise <see langword="null"/>.</returns>
    IMovie? GetMovieByProviderID(int providerID, ProviderName providerName);

    #endregion

    #region Series

    /// <summary>
    /// Dispatched when series metadata from any provider is added.
    /// </summary>
    event EventHandler<SeriesInfoUpdatedEventArgs> SeriesAdded;

    /// <summary>
    /// Dispatched when series metadata from any provider is updated.
    /// </summary>
    event EventHandler<SeriesInfoUpdatedEventArgs> SeriesUpdated;

    /// <summary>
    /// Dispatched when series metadata from any provider is removed.
    /// </summary>
    event EventHandler<SeriesInfoUpdatedEventArgs> SeriesRemoved;

    /// <summary>
    /// Looks up all series for a given provider as a queryable list.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>A collection of series if found, otherwise an empty collection.</returns>
    IEnumerable<ISeries> GetAllSeriesForProvider(ProviderName providerName);

    /// <summary>
    /// Looks up a series by its provider ID.
    /// </summary>
    /// <param name="providerID">The provider ID of the series.</param>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The series if found, otherwise <see langword="null"/>.</returns>
    ISeries? GetSeriesByProviderID(int providerID, ProviderName providerName);

    /// <summary>
    /// Looks up all shoko series as a queryable list.
    /// </summary>
    /// <returns>A collection of series if found, otherwise an empty collection.</returns>
    IEnumerable<IShokoSeries> GetAllShokoSeries();

    /// <summary>
    /// Looks up a shoko series by its ID.
    /// </summary>
    /// <param name="seriesID">The ID of the series.</param>
    /// <returns>The series if found, otherwise <see langword="null"/>.</returns>
    IShokoSeries? GetShokoSeriesByID(int seriesID);

    /// <summary>
    /// Looks up a shoko series by its AniDB ID.
    /// </summary>
    /// <param name="anidbSeriesID">The AniDB ID of the series.</param>
    /// <returns>The series if found, otherwise <see langword="null"/>.</returns>
    IShokoSeries? GetShokoSeriesByAnidbID(int anidbSeriesID);

    #endregion

    #region Group

    /// <summary>
    /// Looks up all shoko groups as a queryable list.
    /// </summary>
    /// <returns>A collection of groups if found, otherwise an empty collection.</returns>
    IEnumerable<IShokoGroup> GetAllShokoGroups();

    /// <summary>
    /// Looks up a shoko group by its ID.
    /// </summary>
    /// <param name="groupID">The ID of the group.</param>
    /// <returns>The group if found, otherwise <see langword="null"/>.</returns>
    IShokoGroup? GetShokoGroupByID(int groupID);

    #endregion

    /// <summary>
    /// Represents the name of a provider.
    /// </summary>
    public enum ProviderName
    {
        /// <summary>
        /// Shoko.
        /// </summary>
        Shoko = 0,
        /// <summary>
        /// AniDB.
        /// </summary>
        AniDB = 1,
        /// <summary>
        /// The Movie DataBase (TMDB).
        /// </summary>
        TMDB = 2,
    }
}
