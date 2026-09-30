using System;
using System.Collections.Generic;
using System.Data;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
/// Provides functionality for interacting with metadata from various providers,
/// including Shoko and other providers.
/// </summary>
public interface IMetadataService
{
    #region Resource Providers

    /// <summary>
    ///   Gets a read-only list of the resource providers registered with the
    ///   service.
    /// </summary>
    IReadOnlyList<IResourceResolver> ResourceResolvers { get; }

    /// <summary>
    ///   Collects additional <see cref="Resource"/> entries for
    ///   <paramref name="entity"/> from all registered
    ///   <see cref="IResourceResolver"/> instances.
    /// </summary>
    /// <param name="entity">
    ///   The entity to gather resources for.  The entity's own
    ///   <see cref="IWithResources.Resources"/> getter should call this method
    ///   and append the result to its built-in list.
    /// </param>
    /// <returns>
    ///   A flattened sequence of resources contributed by all applicable
    ///   providers, or an empty sequence if none apply.
    /// </returns>
    IEnumerable<Resource> GatherResourcesForEntity(IWithResources entity);

    #endregion

    #region Lookup

    /// <summary>
    ///   Gets a read-only list of the metadata resolvers registered with the
    ///   service, in plugin load order. Each source and kind is taken by the
    ///   first resolver claiming it; a pair on a core source, or one another
    ///   resolver already took, is refused, and a resolver left with no pair
    ///   is left out.
    /// </summary>
    IReadOnlyList<IMetadataResolver> MetadataResolvers { get; }

    /// <summary>
    ///   Looks up any entry by its identifier, of any kind and from whichever
    ///   source it names.
    /// </summary>
    /// <remarks>
    ///   The core answers <c>shoko</c>, <c>user</c>, <c>generated</c>,
    ///   <c>anidb</c> and <c>tmdb</c> itself. Other sources ask the
    ///   <see cref="IMetadataResolver"/> that took the source and kind, then the
    ///   metadata stores; orderings and their groups come from
    ///   <see cref="IMetadataOrderingService"/>. A removed plugin's stored
    ///   entries still resolve; a source that only links resolves to nothing.
    /// </remarks>
    /// <param name="id">The entry, e.g. <c>anidb://series/1</c>.</param>
    /// <returns>The entry, or <see langword="null"/> when nothing holds it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    IMetadata? GetEntry(MetadataGuid id);

    /// <summary>
    ///   Looks up an entry by its identifier, as
    ///   <see cref="GetEntry(MetadataGuid)"/> does, when it is a
    ///   <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <typeparam name="TMetadata">
    ///   The type wanted, e.g. <see cref="ISeries"/> or a plugin's own entry
    ///   type. An ID of another of the core's kinds than the one the type is
    ///   finds nothing without a lookup; an ID of a kind a plugin registered
    ///   is always looked up.
    /// </typeparam>
    /// <param name="id">The entry, e.g. <c>tmdb://studio/1</c>.</param>
    /// <returns>
    ///   The entry, or <see langword="null"/> when nothing holds it or it is
    ///   not a <typeparamref name="TMetadata"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    TMetadata? GetEntry<TMetadata>(MetadataGuid id) where TMetadata : class, IMetadata;

    /// <summary>
    ///   Looks up a series by its identifier, as
    ///   <see cref="GetEntry{TMetadata}(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="id">The series, e.g. <c>tmdb://series/1</c>.</param>
    /// <returns>The series, or <see langword="null"/> when nothing holds it or the ID names another kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    ISeries? GetSeries(MetadataGuid id);

    /// <summary>
    ///   Looks up a season by its identifier, as
    ///   <see cref="GetEntry{TMetadata}(MetadataGuid)"/> does: one of a
    ///   series' own seasons, or a group of a stored ordering.
    /// </summary>
    /// <param name="id">The season.</param>
    /// <returns>The season, or <see langword="null"/> when nothing holds it or the ID names another kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    ISeason? GetSeason(MetadataGuid id);

    /// <summary>
    ///   Looks up an episode by its identifier, as
    ///   <see cref="GetEntry{TMetadata}(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="id">The episode.</param>
    /// <returns>The episode, or <see langword="null"/> when nothing holds it or the ID names another kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    IEpisode? GetEpisode(MetadataGuid id);

    /// <summary>
    ///   Looks up a movie by its identifier, as
    ///   <see cref="GetEntry{TMetadata}(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="id">The movie.</param>
    /// <returns>The movie, or <see langword="null"/> when nothing holds it or the ID names another kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    IMovie? GetMovie(MetadataGuid id);

    /// <summary>
    ///   Looks up a collection by its identifier, as
    ///   <see cref="GetEntry{TMetadata}(MetadataGuid)"/> does.
    /// </summary>
    /// <param name="id">The collection.</param>
    /// <returns>The collection, or <see langword="null"/> when nothing holds it or the ID names another kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    ICollection? GetCollection(MetadataGuid id);

    /// <summary>
    ///   Looks up the stored collections a series, movie or group is in, from
    ///   the same places as <see cref="GetEntry(MetadataGuid)"/>.
    /// </summary>
    /// <remarks>
    ///   A TMDB movie is in the TMDB collection it names, a Shoko series in
    ///   its group and a Shoko group in its parent, and a plugin source's
    ///   entry in the collections its collection store holds it in. A
    ///   collection that is not stored is left out.
    /// </remarks>
    /// <param name="member">The series, movie or group, e.g. <c>tmdb://movie/81</c>.</param>
    /// <returns>The collections, or an empty list when it is in none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ICollection> GetCollectionsWith(MetadataGuid member);

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
    /// Looks up all movies from a given metadata source as an enumerable list.
    /// </summary>
    /// <param name="source">The metadata source to look up.</param>
    /// <returns>A collection of movies if found, otherwise an empty collection.</returns>
    IEnumerable<IMovie> GetAllMoviesForSource(MetadataSource source);

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
    /// Looks up all episodes from a given metadata source as an enumerable list.
    /// </summary>
    /// <param name="source">The metadata source to look up.</param>
    /// <returns>A collection of episodes if found, otherwise an empty collection.</returns>
    IEnumerable<IEpisode> GetAllEpisodesForSource(MetadataSource source);

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
    /// Looks up all seasons from a given metadata source as an enumerable list.
    /// </summary>
    /// <param name="source">The metadata source to look up.</param>
    /// <param name="includeAlternativeSeasons">Determines if alternative seasons should be included.</param>
    /// <returns>A collection of seasons if found, otherwise an empty collection.</returns>
    IEnumerable<ISeason> GetAllSeasonsForSource(MetadataSource source, bool includeAlternativeSeasons = false);

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
    /// Looks up all series from a given metadata source as an enumerable list.
    /// </summary>
    /// <param name="source">The metadata source to look up.</param>
    /// <returns>A collection of series if found, otherwise an empty collection.</returns>
    IEnumerable<ISeries> GetAllSeriesForSource(MetadataSource source);

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
    /// Creates a custom tag with the given name and optional overview.
    /// </summary>
    /// <param name="data">The custom tag data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <see langword="null"/> or <see cref="CustomTagData.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="CustomTagData.Name"/> is empty.</exception>
    /// <exception cref="DuplicateNameException">Thrown when a tag with the same name already exists.</exception>
    /// <returns>The created custom tag.</returns>
    IShokoTag CreateCustomTag(CustomTagData data);

    /// <summary>
    /// Updates a custom tag with the given name and optional overview.
    /// </summary>
    /// <param name="tag">The custom tag to update.</param>
    /// <param name="data">The custom tag update data.</param>
    /// <returns>The updated custom tag.</returns>
    IShokoTag UpdateCustomTag(IShokoTag tag, CustomTagUpdateData data);

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

    #region Cross-References

    /// <summary>
    ///   Looks up the series-level cross-references for an anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">Limit the result to one source, or every source when omitted.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesCrossReferences(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   Looks up the film-level cross-references for an episode.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="source">Limit the result to one source, or every source when omitted.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(int anidbEpisodeID, MetadataSource? source = null);

    /// <summary>
    ///   Looks up the film-level cross-references across a whole anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">Limit the result to one source, or every source when omitted.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferencesForSeries(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   Looks up the season-level cross-references for an anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">Limit the result to one source, or every source when omitted.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    IReadOnlyList<IMetadataSeasonCrossReference> GetSeasonCrossReferences(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   Looks up the episode-level cross-references for an episode.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="source">Limit the result to one source, or every source when omitted.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferences(int anidbEpisodeID, MetadataSource? source = null);

    /// <summary>
    ///   Looks up the episode-level cross-references across a whole anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">Limit the result to one source, or every source when omitted.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferencesForSeries(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   Looks up every cross-reference pointing at one provider entry.
    /// </summary>
    /// <remarks>
    ///   The reverse direction of the others: it answers which Shoko entries
    ///   claim a provider entry, without having to load that entry first.
    /// </remarks>
    /// <param name="entry">The provider entry, as the cross-references name it.</param>
    /// <returns>The cross-references, or an empty collection if there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<IMetadataCrossReference> GetCrossReferencesForProviderEntry(MetadataGuid entry);

    #endregion

    #region Collection

    /// <summary>
    ///   Looks up all collections from a given metadata source as an enumerable list.
    /// </summary>
    /// <param name="source">The metadata source to look up.</param>
    /// <returns>A collection of collections if found, otherwise an empty collection.</returns>
    IEnumerable<ICollection> GetAllCollectionsForSource(MetadataSource source);

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

}
