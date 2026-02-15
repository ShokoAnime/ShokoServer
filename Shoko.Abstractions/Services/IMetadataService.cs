using System;
using System.Collections.Generic;
using System.Data;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Services;

/// <summary>
/// Provides functionality for interacting with metadata from various providers,
/// including Shoko and other providers.
/// </summary>
public interface IMetadataService
{
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
    /// Looks up all movies for a given provider as an enumerable list.
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
    /// Looks up all episodes for a given provider as an enumerable list.
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
    /// Looks up all shoko episodes as an enumerable list.
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

    #region Season

    /// <summary>
    /// Dispatched when season metadata from any provider is added.
    /// </summary>
    event EventHandler<SeasonInfoUpdatedEventArgs> SeasonAdded;

    /// <summary>
    /// Dispatched when season metadata from any provider is updated.
    /// </summary>
    event EventHandler<SeasonInfoUpdatedEventArgs> SeasonUpdated;

    /// <summary>
    /// Dispatched when season metadata from any provider is removed.
    /// </summary>
    event EventHandler<SeasonInfoUpdatedEventArgs> SeasonRemoved;

    /// <summary>
    /// Looks up all seasons for a given provider as an enumerable list.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="includeAlternativeSeasons">Determines if alternative seasons should be included.</param>
    /// <returns>A collection of seasons if found, otherwise an empty collection.</returns>
    IEnumerable<ISeason> GetAllSeasonsForProvider(ProviderName providerName, bool includeAlternativeSeasons = false);

    /// <summary>
    /// Looks up an season by its provider ID.
    /// </summary>
    /// <param name="providerID">The provider ID of the season.</param>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The season if found, otherwise <see langword="null"/>.</returns>
    ISeason? GetSeasonByProviderID(string providerID, ProviderName providerName);

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
    /// Looks up all series for a given provider as an enumerable list.
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
    /// Looks up all shoko series as an enumerable list.
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

    #region Series | Custom Tags

    /// <summary>
    /// Looks up all custom tags as an enumerable list.
    /// </summary>
    /// <returns>A collection of custom tags if found, otherwise an empty collection.</returns>
    IEnumerable<IShokoTag> GetAllCustomTags();

    /// <summary>
    /// Looks up a custom tag by its ID.
    /// </summary>
    /// <param name="tagID">The ID of the custom tag.</param>
    /// <returns>The custom tag if found, otherwise <see langword="null"/>.</returns>
    IShokoTag? GetCustomTagByID(int tagID);

    /// <summary>
    /// Creates a custom tag with the given name and optional description.
    /// </summary>
    /// <param name="name">The name of the custom tag.</param>
    /// <param name="description">The description of the custom tag.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="DuplicateNameException">Thrown when a tag with the same name already exists.</exception>
    /// <returns>The created custom tag.</returns>
    IShokoTag CreateCustomTag(string name, string? description = null);

    /// <summary>
    /// Updates a custom tag with the given name and optional description.
    /// </summary>
    /// <param name="tag">The custom tag to update.</param>
    /// <param name="name">Optional. The new name of the custom tag.</param>
    /// <param name="description">Optional. The new description of the custom tag.</param>
    /// <returns>The updated custom tag.</returns>
    IShokoTag UpdateCustomTag(IShokoTag tag, string? name = null, string? description = null);

    /// <summary>
    /// Deletes a custom tag, and removes it from all series.
    /// </summary>
    /// <param name="tag">The custom tag to delete.</param>
    void DeleteCustomTag(IShokoTag tag);

    /// <summary>
    /// Adds custom tags to a series.
    /// </summary>
    /// <param name="series">The series to add the custom tags to.</param>
    /// <param name="tags">The custom tags to add.</param>
    /// <returns><see langword="true"/> if any custom tags were added, otherwise <see langword="false"/>.</returns>
    bool AddCustomTagsToSeries(IShokoSeries series, IEnumerable<IShokoTag> tags);

    /// <summary>
    /// Removes custom tags from a series.
    /// </summary>
    /// <param name="series">The series to remove the custom tags from.</param>
    /// <param name="tags">The custom tags to remove.</param>
    /// <returns><see langword="true"/> if any custom tags were removed, otherwise <see langword="false"/>.</returns>
    bool RemoveCustomTagsFromSeries(IShokoSeries series, IEnumerable<IShokoTag> tags);

    /// <summary>
    /// Clears all custom tags for a series.
    /// </summary>
    /// <param name="series">The series to clear the custom tags for.</param>
    /// <returns><see langword="true"/> if any custom tags were cleared, otherwise <see langword="false"/>.</returns>
    bool ClearCustomTagsForSeries(IShokoSeries series);

    #endregion

    #endregion

    #region Collection

    /// <summary>
    ///   Looks up all collections for a given provider as an enumerable list.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>A collection of collections if found, otherwise an empty collection.</returns>
    IEnumerable<ICollection> GetAllCollectionsForProvider(ProviderName providerName);

    /// <summary>
    /// Looks up a collection by its provider ID.
    /// </summary>
    /// <param name="providerID">The provider ID of the collection.</param>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The collection if found, otherwise <see langword="null"/>.</returns>
    ICollection? GetCollectionByProviderID(int providerID, ProviderName providerName);

    /// <summary>
    /// Looks up all shoko groups as an enumerable list.
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
