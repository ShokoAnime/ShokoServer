using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Text.Options;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Mapping;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   The half of the plugin that fetches shows, movies and collections from
///   TMDB and writes them into the core's stores.
/// </summary>
/// <remarks>
///   The core's refresh job calls in through the provider once an entry is
///   due, holding its lock, so nothing here checks freshness. What did not
///   change on TMDB since the last refresh is carried over from the stores
///   rather than fetched again, by TMDB's changes feed.
/// </remarks>
/// <param name="apiClient">The TMDB client.</param>
/// <param name="stores">The core's stores.</param>
/// <param name="configurationProvider">The plugin's configuration.</param>
/// <param name="logger">The logger.</param>
public sealed class TmdbRefreshService(
    TmdbApiClient apiClient,
    TmdbStores stores,
    ConfigurationProvider<TmdbConfiguration> configurationProvider,
    ILogger<TmdbRefreshService> logger
)
{
    #region Shows

    /// <summary>
    ///   Fetches a show, its seasons, episodes and whatever the options and
    ///   settings ask for, and writes it into the stores.
    /// </summary>
    /// <remarks>
    ///   The show's titles, overviews, content ratings, tags, studios,
    ///   networks and suggestions are always written, and the credits unless
    ///   the refresh is quick; the core fetches the people, studios and
    ///   networks they name for the kinds turned on. The episode groups as
    ///   orderings follow the options, or the settings where they leave it
    ///   open. A quick refresh leaves out the credits and the episode groups
    ///   and fetches no episode on its own. A refresh with a last refresh
    ///   time inside the changes window only fetches the seasons and episodes
    ///   TMDB changed since. A show TMDB no longer has is left as it was
    ///   stored. The core matches the linked anime's episodes again once the
    ///   refresh is done.
    /// </remarks>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="options">What kind of refresh it is.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether TMDB had the show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    /// <exception cref="InvalidOperationException">TMDB listed a season it then did not give.</exception>
    public async Task<bool> RefreshShow(int showID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (showID <= 0)
            return false;

        var configuration = configurationProvider.Load();
        var quick = options.QuickRefresh;
        var downloadCredits = !quick;
        var downloadOrderings = !quick && (options.DownloadAlternateOrdering ?? configuration.AutoDownloadAlternateOrdering);
        var methods = TvShowMethods.ContentRatings | TvShowMethods.Translations | TvShowMethods.AlternativeTitles | TvShowMethods.ExternalIds |
            TvShowMethods.Keywords | TvShowMethods.Recommendations | TvShowMethods.Similar;
        if (downloadOrderings)
            methods |= TvShowMethods.EpisodeGroups;

        logger.LogInformation("Refreshing TMDB show {ShowID}.", showID);
        if (await apiClient.GetShow(showID, methods, cancellationToken).ConfigureAwait(false) is not { } show)
        {
            logger.LogWarning("TMDB has no show with ID {ShowID}. Keeping what is stored.", showID);
            return false;
        }

        var seriesID = TmdbIds.Series(show.Id);
        var stored = stores.Series.GetSeries(seriesID);
        var changes = stored is null || quick || options.LastRefreshedAt is not { } lastRefreshedAt
            ? null
            : await GetShowChanges(show.Id, lastRefreshedAt, cancellationToken).ConfigureAwait(false);
        var languages = TmdbTextLanguages.From(configuration, stores.Texts);
        var storedSeasons = stored?.Seasons.ToDictionary(season => season.ID) ?? [];
        var storedEpisodes = stored?.Episodes.ToDictionary(episode => episode.ID) ?? [];

        var seasons = new List<MetadataSeasonData>();
        var episodes = new List<MetadataEpisodeData>();
        var credits = new List<EpisodeCredits>();
        foreach (var listedSeason in (show.Seasons ?? []).OrderBy(season => season.SeasonNumber))
        {
            var seasonID = TmdbIds.Season(listedSeason.Id);
            if (changes is not null && !changes.SeasonNumbers.Contains(listedSeason.SeasonNumber) && storedSeasons.TryGetValue(seasonID, out var unchangedSeason))
            {
                logger.LogDebug("Keeping season {SeasonNumber} of TMDB show {ShowID}, which did not change.", listedSeason.SeasonNumber, show.Id);
                seasons.Add(CarrySeason(unchangedSeason));
                foreach (var episode in storedEpisodes.Values.Where(episode => episode.SeasonID == seasonID).OrderBy(episode => episode.EpisodeNumber))
                {
                    episodes.Add(CarryEpisode(episode));
                    credits.Add(new(seasonID, episode.ID, null, null));
                }

                continue;
            }

            var season = await apiClient.GetSeason(show.Id, listedSeason.SeasonNumber, TvSeasonMethods.Translations, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"TMDB listed season {listedSeason.SeasonNumber} of show {show.Id}, then gave none.");
            seasons.Add(TmdbEntityMapper.ToSeasonData(season, languages) with { ID = seasonID });
            foreach (var listed in season.Episodes ?? [])
            {
                var episodeID = TmdbIds.Episode(listed.Id);
                storedEpisodes.TryGetValue(episodeID, out var known);
                if (known is not null && changes is not null && !changes.Episodes.Contains((listed.SeasonNumber, (int)listed.EpisodeNumber)))
                {
                    episodes.Add(CarryEpisode(known) with { SeasonID = seasonID });
                    credits.Add(new(seasonID, episodeID, null, null));
                    continue;
                }

                if (quick)
                {
                    var listedOnly = TmdbEntityMapper.ToEpisodeData(listedSeason.Id, listed, null, languages);
                    episodes.Add(known is null
                        ? listedOnly
                        : listedOnly with { Titles = StoredTitles(known.ID), Overviews = StoredOverviews(known.ID), CrossSourceIDs = known.CrossSourceIDs });
                    credits.Add(new(seasonID, episodeID, null, null));
                    continue;
                }

                var episodeMethods = TvEpisodeMethods.ExternalIds | TvEpisodeMethods.Translations;
                if (downloadCredits)
                    episodeMethods |= TvEpisodeMethods.Credits;
                var details = await apiClient.GetEpisode(show.Id, listed.SeasonNumber, (int)listed.EpisodeNumber, episodeMethods, cancellationToken).ConfigureAwait(false);
                episodes.Add(TmdbEntityMapper.ToEpisodeData(listedSeason.Id, listed, details, languages));
                credits.Add(details?.Credits is { } episodeCredits && downloadCredits
                    ? new(
                        seasonID,
                        episodeID,
                        TmdbCredits.EpisodeCast(episodeCredits.Cast, episodeCredits.GuestStars, show.OriginalLanguage),
                        TmdbCredits.Crew(episodeCredits.Crew, show.OriginalLanguage)
                    )
                    : new(seasonID, episodeID, null, null));
            }
        }

        var data = TmdbEntityMapper.ToSeriesData(show, seasons, episodes, languages);
        var changed = stores.Series.SaveSeries(data);

        var (tags, entryTags) = TmdbEntityMapper.Tags(show.Genres, show.Keywords?.Results);
        stores.Tags.SaveTags(tags);
        stores.Tags.SetTags(seriesID, entryTags);
        var (studios, entryStudios) = TmdbEntityMapper.Studios(show.ProductionCompanies);
        stores.Studios.SaveStudios(studios);
        stores.Studios.SetStudios(seriesID, entryStudios);
        stores.Suggestions.SetSuggestions(seriesID, TmdbEntityMapper.Suggestions(
            show.Id,
            TmdbEntityMapper.IDs(show.Recommendations?.Results),
            TmdbEntityMapper.IDs(show.Similar?.Results),
            TmdbIds.Series
        ));
        var (networks, entryNetworks) = TmdbEntityMapper.Networks(show.Networks);
        stores.Studios.SaveNetworks(networks);
        stores.Studios.SetNetworks(seriesID, entryNetworks);

        if (downloadCredits)
            WriteCredits(seriesID, credits);

        if (downloadOrderings)
            await UpdateOrderings(show, data.Episodes.Select(episode => episode.ID).ToHashSet(), cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Refreshed TMDB show {ShowID} ({Title}): {Changes} changes to the show and its {Seasons} seasons and {Episodes} episodes.",
            show.Id,
            show.Name,
            changed,
            seasons.Count,
            episodes.Count
        );
        return true;
    }

    /// <summary>
    ///   What TMDB changed of a show since its last refresh, or
    ///   <c>null</c> to fetch it whole, as when TMDB cannot say.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="since">The last refresh, in UTC.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>What changed, or <c>null</c>.</returns>
    private async Task<TmdbShowChanges?> GetShowChanges(int showID, DateTime since, CancellationToken cancellationToken)
    {
        try
        {
            return await apiClient.GetShowChanges(showID, since, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (TmdbApiClient.IsTransient(ex))
        {
            logger.LogWarning(ex, "Unable to ask TMDB what changed of show {ShowID}; fetching it whole.", showID);
            return null;
        }
    }

    /// <summary>
    ///   Writes the credits fetched for the episodes, and the season's and
    ///   show's credits worked out from every episode's, the ones not fetched
    ///   read back from the store.
    /// </summary>
    /// <param name="seriesID">The show.</param>
    /// <param name="episodes">Each episode's credits, in order, <c>null</c> where they were not fetched.</param>
    private void WriteCredits(MetadataGuid seriesID, IReadOnlyList<EpisodeCredits> episodes)
    {
        var resolved = new List<(int EpisodeID, MetadataGuid SeasonID, IReadOnlyList<MetadataCastData> Cast, IReadOnlyList<MetadataCrewData> Crew)>(episodes.Count);
        foreach (var episode in episodes)
        {
            var episodeNumber = int.TryParse(episode.EpisodeID.ID, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
            if (episode.Cast is { } cast && episode.Crew is { } crew)
            {
                stores.People.SetCast(episode.EpisodeID, cast);
                stores.People.SetCrew(episode.EpisodeID, crew);
                resolved.Add((episodeNumber, episode.SeasonID, cast, crew));
                continue;
            }

            resolved.Add((
                episodeNumber,
                episode.SeasonID,
                [.. stores.People.GetCast(episode.EpisodeID).Select(TmdbCredits.FromStored)],
                [.. stores.People.GetCrew(episode.EpisodeID).Select(TmdbCredits.FromStored)]
            ));
        }

        // A credit takes its place from the first episode it is on by TMDB's ID, as it always has.
        resolved = [.. resolved.OrderBy(episode => episode.EpisodeID)];
        foreach (var season in resolved.GroupBy(episode => episode.SeasonID))
        {
            stores.People.SetCast(season.Key, TmdbCredits.AggregateCast(season.Select(episode => episode.Cast), season: true));
            stores.People.SetCrew(season.Key, TmdbCredits.AggregateCrew(season.Select(episode => episode.Crew), season: true));
        }

        stores.People.SetCast(seriesID, TmdbCredits.AggregateCast(resolved.Select(episode => episode.Cast)));
        stores.People.SetCrew(seriesID, TmdbCredits.AggregateCrew(resolved.Select(episode => episode.Crew)));
    }

    /// <summary>
    ///   Stores each of a show's episode group collections as an ordering of
    ///   it, and removes the orderings of collections it no longer has.
    /// </summary>
    /// <remarks>
    ///   A collection none of whose episodes is stored keeps whatever was
    ///   stored for it, and one the core will not take is logged and kept as
    ///   it was, rather than failing the refresh.
    /// </remarks>
    /// <param name="show">The show, with its episode groups.</param>
    /// <param name="storedEpisodes">The show's stored episodes.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the orderings are written.</returns>
    /// <exception cref="InvalidOperationException">TMDB listed an episode group it then did not give.</exception>
    private async Task UpdateOrderings(TvShow show, IReadOnlySet<MetadataGuid> storedEpisodes, CancellationToken cancellationToken)
    {
        var seriesID = TmdbIds.Series(show.Id);
        var kept = new HashSet<MetadataGuid>();
        foreach (var listed in show.EpisodeGroups?.Results ?? [])
        {
            if (string.IsNullOrWhiteSpace(listed.Id))
                continue;

            // The show only lists a collection; its groups come on their own.
            var collection = await apiClient.GetEpisodeGroup(listed.Id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"TMDB listed episode group {listed.Id} of show {show.Id}, then gave none.");
            kept.Add(TmdbIds.Ordering(listed.Id));
            if (collection.Network is { Id: > 0 } network)
                stores.Studios.SaveNetworks([TmdbEntityMapper.ToNetworkData(network)]);
            if (TmdbOrderings.ToOrderingData(show.Id, collection, storedEpisodes) is not { } ordering)
                continue;

            try
            {
                stores.Orderings.SaveOrdering(ordering);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Unable to store episode group {GroupID} of TMDB show {ShowID}.", listed.Id, show.Id);
            }
        }

        var gone = stores.Orderings.GetOrderings(seriesID)
            .Where(ordering => ordering.ID.Source == MetadataSource.TMDB && !ordering.IsDefault && !kept.Contains(ordering.ID))
            .ToList();
        foreach (var ordering in gone)
        {
            logger.LogInformation("Removing ordering {OrderingID}, which TMDB no longer has.", ordering.ID);
            stores.Orderings.RemoveOrdering(ordering.ID);
        }
    }

    /// <summary>
    ///   A stored season, as it is written again.
    /// </summary>
    /// <param name="season">The stored season.</param>
    /// <returns>The season.</returns>
    private MetadataSeasonData CarrySeason(ISeason season)
        => new()
        {
            ID = season.ID,
            SeasonNumber = season.SeasonNumber,
            Titles = StoredTitles(season.ID),
            Overviews = StoredOverviews(season.ID),
        };

    /// <summary>
    ///   A stored episode, as it is written again.
    /// </summary>
    /// <param name="episode">The stored episode.</param>
    /// <returns>The episode.</returns>
    private MetadataEpisodeData CarryEpisode(IEpisode episode)
        => new()
        {
            ID = episode.ID,
            SeasonID = episode.SeasonID,
            SeasonNumber = episode.SeasonNumber,
            EpisodeNumber = episode.EpisodeNumber,
            Type = episode.Type,
            Rating = episode.Rating,
            RatingVotes = episode.RatingVotes,
            Runtime = episode.Runtime,
            AirDate = episode.AirDate,
            CrossSourceIDs = episode.CrossSourceIDs,
            Titles = StoredTitles(episode.ID),
            Overviews = StoredOverviews(episode.ID),
        };

    private IReadOnlyList<ITitle> StoredTitles(MetadataGuid entryID)
        => stores.Texts.GetTitles(entryID, StoredTexts());

    private IReadOnlyList<IText> StoredOverviews(MetadataGuid entryID)
        => stores.Texts.GetOverviews(entryID, StoredTexts());

    // TMDB's own texts of the entry, disabled ones included so a user's choice is kept.
    private static TextFilteringOptions StoredTexts()
        => new() { Source = MetadataSource.TMDB, IsEnabled = null, IncludeInlineDefault = false };

    /// <summary>
    ///   An episode's credits, or <c>null</c> ones when they were
    ///   not fetched and are read back from the store.
    /// </summary>
    /// <param name="SeasonID">The episode's season.</param>
    /// <param name="EpisodeID">The episode.</param>
    /// <param name="Cast">The cast fetched, or <c>null</c>.</param>
    /// <param name="Crew">The crew fetched, or <c>null</c>.</param>
    private sealed record EpisodeCredits(MetadataGuid SeasonID, MetadataGuid EpisodeID, IReadOnlyList<MetadataCastData>? Cast, IReadOnlyList<MetadataCrewData>? Crew);

    #endregion

    #region Movies

    /// <summary>
    ///   Fetches a movie and whatever the options and settings ask for, and
    ///   writes it into the stores, unless TMDB changed nothing since its
    ///   last refresh.
    /// </summary>
    /// <remarks>
    ///   The credits are written unless the refresh is quick, naming the
    ///   people by ID. The movie names its collection by ID, and the core
    ///   fetches the collection through <see cref="RefreshCollection"/>, and
    ///   the people and studios through the entity provider, for the kinds
    ///   turned on. A movie TMDB no longer has is left as it was stored.
    /// </remarks>
    /// <param name="movieID">The TMDB movie ID.</param>
    /// <param name="options">What kind of refresh it is.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether the movie was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public async Task<bool> RefreshMovie(int movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (movieID <= 0)
            return false;

        var configuration = configurationProvider.Load();
        var quick = options.QuickRefresh;
        var downloadCredits = !quick;
        var movieGuid = TmdbIds.Movie(movieID);
        if (stores.Movies.GetMovie(movieGuid) is not null && options.LastRefreshedAt is { } lastRefreshedAt &&
            !await HasMovieChanged(movieID, lastRefreshedAt, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Skipping TMDB movie {MovieID}, which did not change since {LastRefreshedAt}.", movieID, lastRefreshedAt);
            return false;
        }

        var methods = MovieMethods.Translations | MovieMethods.AlternativeTitles | MovieMethods.ReleaseDates | MovieMethods.ExternalIds |
            MovieMethods.Keywords | MovieMethods.Recommendations | MovieMethods.Similar;
        if (downloadCredits)
            methods |= MovieMethods.Credits;

        logger.LogInformation("Refreshing TMDB movie {MovieID}.", movieID);
        if (await apiClient.GetMovie(movieID, methods, cancellationToken).ConfigureAwait(false) is not { } movie)
        {
            logger.LogWarning("TMDB has no movie with ID {MovieID}. Keeping what is stored.", movieID);
            return false;
        }

        var languages = TmdbTextLanguages.From(configuration, stores.Texts);
        stores.Movies.SaveMovie(TmdbEntityMapper.ToMovieData(movie, languages));

        var (tags, entryTags) = TmdbEntityMapper.Tags(movie.Genres, movie.Keywords?.Keywords);
        stores.Tags.SaveTags(tags);
        stores.Tags.SetTags(movieGuid, entryTags);
        var (studios, entryStudios) = TmdbEntityMapper.Studios(movie.ProductionCompanies);
        stores.Studios.SaveStudios(studios);
        stores.Studios.SetStudios(movieGuid, entryStudios);
        stores.Suggestions.SetSuggestions(movieGuid, TmdbEntityMapper.Suggestions(
            movie.Id,
            TmdbEntityMapper.IDs(movie.Recommendations?.Results),
            TmdbEntityMapper.IDs(movie.Similar?.Results),
            TmdbIds.Movie
        ));
        if (downloadCredits && movie.Credits is { } movieCredits)
        {
            stores.People.SetCast(movieGuid, TmdbCredits.MovieCast(movieCredits.Cast, movie.OriginalLanguage));
            stores.People.SetCrew(movieGuid, TmdbCredits.Crew(movieCredits.Crew, movie.OriginalLanguage));
        }

        logger.LogInformation("Refreshed TMDB movie {MovieID} ({Title}).", movie.Id, movie.Title);
        return true;
    }

    private async Task<bool> HasMovieChanged(int movieID, DateTime since, CancellationToken cancellationToken)
    {
        try
        {
            return await apiClient.HasMovieChanged(movieID, since, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (TmdbApiClient.IsTransient(ex))
        {
            logger.LogWarning(ex, "Unable to ask TMDB whether movie {MovieID} changed; fetching it whole.", movieID);
            return true;
        }
    }

    #endregion

    #region Collections

    /// <summary>
    ///   Fetches a collection with its titles, overviews and the movies it
    ///   holds, and writes it into the store. One TMDB no longer has is
    ///   removed from it.
    /// </summary>
    /// <param name="collectionID">The TMDB collection ID.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether TMDB had the collection.</returns>
    /// <exception cref="MetadataProviderNotConfiguredException">No API key is configured.</exception>
    public async Task<bool> RefreshCollection(int collectionID, CancellationToken cancellationToken = default)
    {
        if (collectionID <= 0)
            return false;

        var collectionGuid = TmdbIds.Collection(collectionID);
        if (await apiClient.GetCollection(collectionID, cancellationToken).ConfigureAwait(false) is not { } collection)
        {
            logger.LogWarning("TMDB has no collection with ID {CollectionID}. Removing what is stored.", collectionID);
            if (stores.Collections.GetCollection(collectionGuid) is not null)
                stores.Collections.RemoveCollection(collectionGuid);
            return false;
        }

        var languages = TmdbTextLanguages.From(configurationProvider.Load(), stores.Texts);
        stores.Collections.SaveCollection(TmdbEntityMapper.ToCollectionData(collection, languages));
        logger.LogDebug("Refreshed TMDB collection {CollectionID} ({Title}).", collection.Id, collection.Name);
        return true;
    }

    #endregion
}
