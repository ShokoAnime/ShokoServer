using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

using TitleLanguage = Shoko.Abstractions.Metadata.Enums.TitleLanguage;

#pragma warning disable CS0618
// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

public partial class TmdbMetadataUpdater
{
    #region Update (Shows)

    /// <summary>
    ///   Brings a show up to date from TMDB, with its seasons, episodes and
    ///   whatever else the options ask for, and matches the episodes of the
    ///   anime linked to it.
    /// </summary>
    /// <remarks>
    ///   A show that is not forced and not new only fetches the seasons and
    ///   episodes TMDB recorded changes to since it was last updated, within
    ///   the changes window.
    /// </remarks>
    /// <param name="showId">The TMDB show ID.</param>
    /// <param name="options">What to fetch; a switch left out goes by the settings.</param>
    /// <param name="forceRefresh">Whether to fetch everything again, the people included, however recently it was fetched.</param>
    /// <returns><see langword="true"/> when anything changed.</returns>
    /// <exception cref="TmdbApiKeyUnavailableException">No API key is available.</exception>
    public async Task<bool> UpdateShow(int showId, MetadataRefreshOptions options, bool forceRefresh)
    {
        var downloadImages = options.DownloadImages;
        var quickRefresh = options.QuickRefresh;
        var settings = _settingsProvider.GetSettings();
        var downloadCrewAndCast = options.DownloadCrewAndCast ?? settings.TMDB.AutoDownloadCrewAndCast;
        var downloadAlternateOrdering = options.DownloadAlternateOrdering ?? settings.TMDB.AutoDownloadAlternateOrdering;
        var downloadNetworks = options.DownloadNetworks ?? settings.TMDB.AutoDownloadNetworks;

        if (showId <= 0)
            return false;

        var tmdbShow = _tmdbShows.GetByTmdbShowID(showId) ?? new(showId);
        var newlyAdded = tmdbShow.CreatedAt == tmdbShow.LastUpdatedAt;
        var xrefs = _xrefAnidbTmdbShows.GetByTmdbShowID(showId);
        var methods = TvShowMethods.ContentRatings | TvShowMethods.Translations | TvShowMethods.AlternativeTitles | TvShowMethods.ExternalIds |
            TvShowMethods.Keywords | TvShowMethods.Recommendations | TvShowMethods.Similar;
        if (downloadAlternateOrdering && !quickRefresh)
            methods |= TvShowMethods.EpisodeGroups;
        var show = await _client.UseClient(c => c.GetTvShowAsync(showId, methods, "en-US"), $"Get Show {showId}").ConfigureAwait(false);
        if (show is null)
            return false;

        // A null result (window exceeded or API failure) makes UpdateShowSeasonsAndEpisodes refresh everything.
        TmdbShowChangedItems? changedItems = null;
        if (!newlyAdded && !forceRefresh && !quickRefresh)
        {
            try
            {
                changedItems = await _client.GetShowChangedItemsAsync(showId, tmdbShow.LastUpdatedAt).ConfigureAwait(false);
            }
            catch (Exception ex) when (TmdbApiClient.IsTmdbTransient(ex))
            {
                _logger.LogWarning(ex, "TMDB: Transient error checking changes for show {ShowId}; proceeding with full refresh.", showId);
                changedItems = null;
            }
        }

        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();
        var contentRatingLanguages = settings.TMDB.DownloadAllContentRatings
            ? null
            : Languages.PreferredNamingLanguages.Select(a => a.Language)
                .Concat(Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language))
                .Except([TitleLanguage.Main, TitleLanguage.Unknown, TitleLanguage.None])
                .ToHashSet();
        var shouldFireEvents = !quickRefresh || xrefs.Count > 0;
        var updated = tmdbShow.Populate(show, contentRatingLanguages);
        var (titlesUpdated, overviewsUpdated) = UpdateTitlesAndOverviewsWithTuple(tmdbShow, show.Translations, preferredTitleLanguages, preferredOverviewLanguages,
            alternativeTitles: show.AlternativeTitles?.Results);
        updated = titlesUpdated || overviewsUpdated || updated;
        updated = UpdateShowExternalIDs(tmdbShow, show.ExternalIds!) || updated;
        updated = await UpdateCompanies(tmdbShow, show.ProductionCompanies!) || updated;
        updated = UpdateSuggestions(MetadataEntityType.Series, tmdbShow.TmdbShowID,
        [
            (SuggestionKind.Recommended, show.Recommendations?.Results?.Select(result => result.Id) ?? []),
            (SuggestionKind.Similar, show.Similar?.Results?.Select(result => result.Id) ?? []),
        ]) || updated;
        var (episodesOrSeasonsUpdated, updatedSeasons, updatedEpisodes, episodeCount, hiddenEpisodeCount) =
            await UpdateShowSeasonsAndEpisodes(show, downloadCrewAndCast, forceRefresh, downloadImages, quickRefresh, shouldFireEvents, changedItems);
        updated = episodesOrSeasonsUpdated || updated;
        if (tmdbShow.EpisodeCount != episodeCount)
        {
            tmdbShow.EpisodeCount = episodeCount;
            updated = true;
        }
        if (tmdbShow.HiddenEpisodeCount != hiddenEpisodeCount)
        {
            tmdbShow.HiddenEpisodeCount = hiddenEpisodeCount;
            updated = true;
        }
        if (downloadAlternateOrdering && !quickRefresh)
            updated = await UpdateShowAlternateOrdering(tmdbShow, show) || updated;
        if (downloadNetworks && !quickRefresh)
            updated = await UpdateShowNetworks(tmdbShow, show) || updated;
        if (newlyAdded || updated)
        {
            if (shouldFireEvents)
                tmdbShow.LastUpdatedAt = DateTime.Now;
            _tmdbShows.Save(tmdbShow);
        }

        // Don't do the auto-matching if we're just doing a quick refresh.
        if (!quickRefresh)
            foreach (var xref in xrefs)
                await MatchShowEpisodes(xref.AnidbAnimeID, xref.TmdbShowID).ConfigureAwait(false);

        if (shouldFireEvents && (newlyAdded || updated || updatedEpisodes.Count > 0))
            ShokoEventHandler.Instance.OnSeriesUpdated(
                tmdbShow,
                newlyAdded ? UpdateReason.Added : UpdateReason.Updated,
                updatedSeasons,
                updatedEpisodes
            );

        return updated;
    }

    /// <summary>
    ///   Matches an anime's episodes with a refreshed show's again, keeping
    ///   the links already there, and saves the result.
    /// </summary>
    /// <remarks>
    ///   Nothing is matched while TMDB's episodes may not be linked, and a
    ///   matching that cannot run is no reason to fail the show's refresh.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="showID">The TMDB show ID.</param>
    /// <returns>A task that completes once the links are saved.</returns>
    private async Task MatchShowEpisodes(int anidbAnimeID, int showID)
    {
        try
        {
            using var linkChanges = _linkChanges.Begin(MetadataLinkChangeReason.AutoLink);
            await _linkingService.Value
                .MatchEpisodes(anidbAnimeID, new(MetadataSource.TMDB, MetadataEntityType.Series, showID.ToString()), useExisting: true, save: true)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogDebug(ex, "Not matching the episodes of AniDB anime {AnidbAnimeID} with TMDB show {ShowID}.", anidbAnimeID, showID);
        }
    }

    private async Task<TmdbSeasonEpisodeUpdateResult> UpdateShowSeasonsAndEpisodes(TvShow show, bool downloadCrewAndCast = false, bool forceRefresh = false, bool downloadImages = false, bool quickRefresh = false, bool shouldFireEvents = false, TmdbShowChangedItems? changedItems = null)
    {
        var settings = _settingsProvider.GetSettings();
        var preferredTitleLanguages = settings.TMDB.DownloadAllTitles ? null : Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language).ToHashSet();
        var preferredOverviewLanguages = settings.TMDB.DownloadAllOverviews ? null : Languages.PreferredDescriptionNamingLanguages.Select(a => a.Language).ToHashSet();

        var existingSeasons = _tmdbSeasons.GetByTmdbShowID(show.Id).ToDictionary(season => season.Id);
        var existingEpisodes = _tmdbEpisodes.GetByTmdbShowID(show.Id).ToDictionary(episode => episode.Id);
        var state = new ShowSyncState
        {
            DownloadCrewAndCast = downloadCrewAndCast,
            QuickRefresh = quickRefresh,
            ShouldFireEvents = shouldFireEvents,
            PreferredTitleLanguages = preferredTitleLanguages,
            PreferredOverviewLanguages = preferredOverviewLanguages,
            ChangedItems = changedItems,
        };
        var episodePeople = BuildEpisodePeopleLookup(show, state);

        foreach (var reducedSeason in show.Seasons!)
            await ProcessShowSeasonAsync(show, reducedSeason, existingSeasons, existingEpisodes, episodePeople, state).ConfigureAwait(false);

        var seasonsToRemove = existingSeasons.Values.ExceptBy(state.SeasonsToSkip, s => s.Id).ToList();
        var episodesToRemove = existingEpisodes.Values.ExceptBy(state.EpisodesToSkip, e => e.Id).ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {Added}/{Updated}/{Removed}/{Skipped} seasons for show {ShowTitle} (Show={ShowId})",
            state.SeasonsAdded,
            state.SeasonsToSave.Count - state.SeasonsAdded,
            seasonsToRemove.Count,
            existingSeasons.Count + state.SeasonsAdded - seasonsToRemove.Count - state.SeasonsToSave.Count,
            show.Name,
            show.Id);
        _tmdbSeasons.Save(state.SeasonsToSave);

        foreach (var season in seasonsToRemove)
        {
            PurgeShowSeason(season);
            state.SeasonEvents.TryAdd(season, UpdateReason.Removed);
        }

        _tmdbSeasons.Delete(seasonsToRemove);

        _logger.LogDebug(
            "Added/updated/removed/skipped {Added}/{Updated}/{Removed}/{Skipped} episodes for show {ShowTitle} (Show={ShowId})",
            state.EpisodesAdded,
            state.EpisodesToSave.Count - state.EpisodesAdded,
            episodesToRemove.Count,
            existingEpisodes.Count + state.EpisodesAdded - episodesToRemove.Count - state.EpisodesToSave.Count,
            show.Name,
            show.Id);
        _tmdbEpisodes.Save(state.EpisodesToSave);

        foreach (var episode in episodesToRemove)
        {
            PurgeShowEpisode(episode);
            state.EpisodeEvents.TryAdd(episode, UpdateReason.Removed);
        }

        _tmdbEpisodes.Delete(episodesToRemove);

        if (quickRefresh)
            return new TmdbSeasonEpisodeUpdateResult(
                state.SeasonsToSave.Count > 0 || seasonsToRemove.Count > 0 || state.EpisodesToSave.Count > 0 || episodesToRemove.Count > 0,
                state.SeasonEvents,
                state.EpisodeEvents,
                state.TotalEpisodeCount,
                state.TotalHiddenEpisodeCount);

        var anyPeopleChanged = await UpdateShowPeopleAsync(show, forceRefresh, downloadImages, state.PeopleToAddOrKeep, state.PeopleToPotentiallyRemove).ConfigureAwait(false);

        return new TmdbSeasonEpisodeUpdateResult(
            state.SeasonsToSave.Count > 0 || seasonsToRemove.Count > 0 || state.EpisodesToSave.Count > 0 || episodesToRemove.Count > 0 || anyPeopleChanged,
            state.SeasonEvents,
            state.EpisodeEvents,
            state.TotalEpisodeCount,
            state.TotalHiddenEpisodeCount);
    }

    private ILookup<int, int> BuildEpisodePeopleLookup(TvShow show, ShowSyncState state)
        => state.DownloadCrewAndCast
            ? _tmdbEpisodeCast.GetByTmdbShowID(show.Id).Select(c => (c.TmdbEpisodeID, c.TmdbPersonID))
                .Concat(_tmdbEpisodeCrew.GetByTmdbShowID(show.Id).Select(c => (c.TmdbEpisodeID, c.TmdbPersonID)))
                .ToLookup(x => x.TmdbEpisodeID, x => x.TmdbPersonID)
            : Enumerable.Empty<TMDB_Episode_Cast>().ToLookup(c => c.TmdbEpisodeID, c => c.TmdbPersonID);

    private async Task ProcessShowSeasonAsync(
        TvShow show,
        SearchTvSeason reducedSeason,
        Dictionary<int, TMDB_Season> existingSeasons,
        Dictionary<int, TMDB_Episode> existingEpisodes,
        ILookup<int, int> episodePeople,
        ShowSyncState state)
    {
        _logger.LogDebug("Checking season {SeasonNumber} for show {ShowTitle} (Show={ShowId})", reducedSeason.SeasonNumber, show.Name, show.Id);

        if (TryAccumulateUnchangedSeason(show, reducedSeason, existingSeasons, episodePeople, state))
            return;

        var season = await _client.UseClient(c => c.GetTvSeasonAsync(show.Id, reducedSeason.SeasonNumber, TvSeasonMethods.Translations), $"Get season {reducedSeason.SeasonNumber} for show {show.Id} \"{show.Name}\"").ConfigureAwait(false) ??
            throw new Exception($"Unable to fetch season {reducedSeason.SeasonNumber} for show \"{show.Name}\".");

        if (!existingSeasons.TryGetValue(reducedSeason.Id, out var tmdbSeason))
        {
            state.SeasonsAdded++;
            tmdbSeason = new(reducedSeason.Id);
        }
        var newlyAddedSeason = tmdbSeason.CreatedAt == tmdbSeason.LastUpdatedAt;

        var seasonUpdated = tmdbSeason.Populate(show, season);
        seasonUpdated = UpdateTitlesAndOverviews(tmdbSeason, season.Translations, state.PreferredTitleLanguages, state.PreferredOverviewLanguages) || seasonUpdated;

        var seasonAlreadySaved = false;
        if ((newlyAddedSeason && state.ShouldFireEvents) || seasonUpdated)
        {
            state.SeasonEvents.TryAdd(tmdbSeason, newlyAddedSeason ? UpdateReason.Added : UpdateReason.Updated);
            if (state.ShouldFireEvents)
                tmdbSeason.LastUpdatedAt = DateTime.Now;
            state.SeasonsToSave.Add(tmdbSeason);
            seasonAlreadySaved = true;
        }

        state.SeasonsToSkip.Add(tmdbSeason.Id);

        var (episodeCount, hiddenEpisodeCount) = await ProcessSeasonEpisodesAsync(show, season, existingEpisodes, episodePeople, state).ConfigureAwait(false);

        if (tmdbSeason.EpisodeCount != episodeCount)
        {
            tmdbSeason.EpisodeCount = episodeCount;
            seasonUpdated = true;
        }
        if (tmdbSeason.HiddenEpisodeCount != hiddenEpisodeCount)
        {
            tmdbSeason.HiddenEpisodeCount = hiddenEpisodeCount;
            seasonUpdated = true;
        }
        if (seasonUpdated)
        {
            tmdbSeason.LastUpdatedAt = DateTime.Now;
            if (!seasonAlreadySaved)
            {
                state.SeasonEvents.TryAdd(tmdbSeason, UpdateReason.Updated);
                state.SeasonsToSave.Add(tmdbSeason);
            }
        }

        state.TotalEpisodeCount += episodeCount;
        state.TotalHiddenEpisodeCount += hiddenEpisodeCount;
    }

    // Skipped seasons still need their episode counts registered so season totals stay accurate.
    private bool TryAccumulateUnchangedSeason(
        TvShow show,
        SearchTvSeason reducedSeason,
        Dictionary<int, TMDB_Season> existingSeasons,
        ILookup<int, int> episodePeople,
        ShowSyncState state)
    {
        if (!state.ChangedItems.HasValue || state.ChangedItems.Value.SeasonNumbers.Contains(reducedSeason.SeasonNumber) || !existingSeasons.TryGetValue(reducedSeason.Id, out var unchangedSeason))
            return false;

        state.SeasonsToSkip.Add(unchangedSeason.Id);
        var localEpisodes = _tmdbEpisodes.GetByTmdbSeasonID(unchangedSeason.Id);
        var visibleCount = 0;
        foreach (var ep in localEpisodes)
        {
            state.EpisodesToSkip.Add(ep.Id);
            if (!ep.IsHidden) visibleCount++;
        }
        state.TotalEpisodeCount += visibleCount;
        state.TotalHiddenEpisodeCount += localEpisodes.Count - visibleCount;
        if (state.DownloadCrewAndCast)
            foreach (var ep in localEpisodes)
                foreach (var personId in episodePeople[ep.Id])
                    state.PeopleToAddOrKeep.Add(personId);
        _logger.LogDebug("Skipping unchanged season {SeasonNumber} for show {ShowTitle} (Show={ShowId}).", reducedSeason.SeasonNumber, show.Name, show.Id);
        return true;
    }

    private async Task<TmdbSeasonEpisodeCounts> ProcessSeasonEpisodesAsync(
        TvShow show,
        TvSeason season,
        Dictionary<int, TMDB_Episode> existingEpisodes,
        ILookup<int, int> episodePeople,
        ShowSyncState state)
    {
        var episodeBag = new List<TMDB_Episode>();
        var hiddenEpisodeBag = new List<TMDB_Episode>();

        foreach (var reducedEpisode in season.Episodes!)
        {
            _logger.LogDebug("Checking episode {EpisodeNumber} in season {SeasonNumber} for show {ShowTitle} (Show={ShowId})", reducedEpisode.EpisodeNumber, season.SeasonNumber, show.Name, show.Id);
            if (!existingEpisodes.TryGetValue(reducedEpisode.Id, out var tmdbEpisode))
            {
                state.EpisodesAdded++;
                tmdbEpisode = new(reducedEpisode.Id);
            }
            var newlyAddedEpisode = tmdbEpisode.CreatedAt == tmdbEpisode.LastUpdatedAt;

            if (AccumulateUnchangedEpisode(tmdbEpisode, season, reducedEpisode, episodePeople, episodeBag, hiddenEpisodeBag, state))
                continue;

            var episodeUpdated = await FetchAndPopulateEpisodeAsync(show, season, reducedEpisode, tmdbEpisode, state).ConfigureAwait(false);

            TrySaveEpisode(tmdbEpisode, newlyAddedEpisode, episodeUpdated, state);
            state.EpisodesToSkip.Add(tmdbEpisode.Id);
            (tmdbEpisode.IsHidden ? hiddenEpisodeBag : episodeBag).Add(tmdbEpisode);
        }

        return new TmdbSeasonEpisodeCounts(episodeBag.Count, hiddenEpisodeBag.Count);
    }

    private static void TrySaveEpisode(TMDB_Episode tmdbEpisode, bool newlyAdded, bool updated, ShowSyncState state)
    {
        if (!(newlyAdded && state.ShouldFireEvents) && !updated)
            return;

        state.EpisodeEvents.TryAdd(tmdbEpisode, newlyAdded ? UpdateReason.Added : UpdateReason.Updated);
        if (state.ShouldFireEvents)
            tmdbEpisode.LastUpdatedAt = DateTime.Now;
        state.EpisodesToSave.Add(tmdbEpisode);
    }

    private static bool AccumulateUnchangedEpisode(
        TMDB_Episode tmdbEpisode,
        TvSeason season,
        TvSeasonEpisode reducedEpisode,
        ILookup<int, int> episodePeople,
        List<TMDB_Episode> episodeBag,
        List<TMDB_Episode> hiddenEpisodeBag,
        ShowSyncState state)
    {
        var newlyAdded = tmdbEpisode.CreatedAt == tmdbEpisode.LastUpdatedAt;
        if (!state.ChangedItems.HasValue || newlyAdded || state.ChangedItems.Value.Episodes.Contains((season.SeasonNumber, (int)reducedEpisode.EpisodeNumber)))
            return false;

        state.EpisodesToSkip.Add(tmdbEpisode.Id);
        (tmdbEpisode.IsHidden ? hiddenEpisodeBag : episodeBag).Add(tmdbEpisode);
        if (state.DownloadCrewAndCast)
            foreach (var personId in episodePeople[tmdbEpisode.Id])
                state.PeopleToAddOrKeep.Add(personId);
        return true;
    }

    private async Task<bool> FetchAndPopulateEpisodeAsync(
        TvShow show,
        TvSeason season,
        TvSeasonEpisode reducedEpisode,
        TMDB_Episode tmdbEpisode,
        ShowSyncState state)
    {
        if (state.QuickRefresh)
        {
            var baseUpdated = tmdbEpisode.Populate(show, season, reducedEpisode, null);
            return UpdateTitlesAndOverviews(tmdbEpisode, null, state.PreferredTitleLanguages, state.PreferredOverviewLanguages) || baseUpdated;
        }

        var episodeMethods = TvEpisodeMethods.ExternalIds | TvEpisodeMethods.Translations;
        if (state.DownloadCrewAndCast)
            episodeMethods |= TvEpisodeMethods.Credits;

        var episode = await _client.UseClient(c => c.GetTvEpisodeAsync(show.Id, season.SeasonNumber, reducedEpisode.EpisodeNumber, episodeMethods), $"Get episode {reducedEpisode.EpisodeNumber} in season {season.SeasonNumber} for show {show.Id} \"{show.Name}\"").ConfigureAwait(false);
        if (episode is null)
            return false;

        var episodeUpdated = tmdbEpisode.Populate(show, season, reducedEpisode, episode.Translations);
        episodeUpdated = UpdateTitlesAndOverviews(tmdbEpisode, episode.Translations, state.PreferredTitleLanguages, state.PreferredOverviewLanguages) || episodeUpdated;
        episodeUpdated = UpdateEpisodeExternalIDs(tmdbEpisode, episode.ExternalIds!) || episodeUpdated;
        if (state.DownloadCrewAndCast)
        {
            var (castOrCrewUpdated, peopleToAddOrKeep, peopleToPotentiallyRemove) = UpdateEpisodeCastAndCrew(tmdbEpisode, episode.Credits!);
            episodeUpdated |= castOrCrewUpdated;
            AccumulateEpisodePeople(peopleToAddOrKeep, peopleToPotentiallyRemove, state);
        }
        return episodeUpdated;
    }

    private static void AccumulateEpisodePeople(IEnumerable<int> toAddOrKeep, IEnumerable<int> toPotentiallyRemove, ShowSyncState state)
    {
        foreach (var personId in toAddOrKeep)
            state.PeopleToAddOrKeep.Add(personId);
        foreach (var personId in toPotentiallyRemove)
            state.PeopleToPotentiallyRemove.Add(personId);
    }

    private async Task<bool> UpdateShowPeopleAsync(TvShow show, bool forceRefresh, bool downloadImages, HashSet<int> allPeopleToAddOrKeep, HashSet<int> allPeopleToPotentiallyRemove)
    {
        var added = 0;
        var updated = 0;
        var purged = 0;
        var peopleToCheck = allPeopleToAddOrKeep;
        var peopleToPurge = allPeopleToPotentiallyRemove.Except(peopleToCheck).ToList();
        var transientlyFailedShowPeople = new ConcurrentBag<int>();
        await ProcessWithConcurrencyAsync(TmdbApiClient.MaxConcurrency, peopleToCheck, async personId =>
        {
            try
            {
                var (personAdded, personUpdated) = await UpdatePerson(personId, forceRefresh, downloadImages, currentShowId: show.Id);
                if (personAdded)
                    Interlocked.Increment(ref added);
                else if (personUpdated)
                    Interlocked.Increment(ref updated);
            }
            catch (Exception ex) when (TmdbApiClient.IsTmdbTransient(ex))
            {
                // Keeps the person's cast and crew rows for the retry.
                transientlyFailedShowPeople.Add(personId);
            }
            catch (Exception ex)
            {
                // Left for CleanupOrphanedCastCrew.
                _logger.LogWarning(ex, "TMDB: Unexpected error updating person {PersonId} for show {ShowId}", personId, show.Id);
            }
        }, onDropped: transientlyFailedShowPeople.Add);
        var transientlyFailedShowSet = transientlyFailedShowPeople.ToHashSet();
        if (transientlyFailedShowSet.Count > 0)
            await Task.WhenAll(transientlyFailedShowSet.Select(personId =>
                _scheduler.Enqueue<UpdateTmdbPersonJob>(j => { j.TmdbPersonID = personId; j.DownloadImages = downloadImages; j.TmdbShowID = show.Id; })));
        CleanupOrphanedCastCrew(peopleToCheck, transientlyFailedShowSet, missingPersonIds =>
        {
            var orphanedCast = _tmdbEpisodeCast.GetByTmdbShowID(show.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            var orphanedCrew = _tmdbEpisodeCrew.GetByTmdbShowID(show.Id)
                .Where(c => missingPersonIds.Contains(c.TmdbPersonID)).ToList();
            if (orphanedCast.Count > 0 || orphanedCrew.Count > 0)
            {
                _logger.LogWarning("TMDB: Removed {CastCount} cast and {CrewCount} crew entries for {PersonCount} people that failed to fetch. (Show={ShowId})",
                    orphanedCast.Count, orphanedCrew.Count, missingPersonIds.Count, show.Id);
                _tmdbEpisodeCast.Delete(orphanedCast);
                _tmdbEpisodeCrew.Delete(orphanedCrew);
            }
        });
        try
        {
            await ProcessWithConcurrencyAsync(TmdbApiClient.MaxConcurrency, peopleToPurge, async personId =>
            {
                if (await PurgePerson(personId))
                    Interlocked.Increment(ref purged);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB: Failed to purge one or more people during show cast/crew update (Show={ShowId})", show.Id);
        }
        _logger.LogDebug("Added/updated/purged/skipped {Added}/{Updated}/{Purged}/{Skipped} staff for show {ShowTitle} (Show={ShowId})",
            added, updated, purged, peopleToPurge.Count + peopleToCheck.Count - added - updated - purged, show.Name, show.Id);
        return added > 0 || purged > 0;
    }

    private void CleanupOrphanedCastCrew(IReadOnlyCollection<int> people, HashSet<int> transientlyFailed, Action<HashSet<int>> deleteOrphansAndLog)
    {
        var candidates = people.Except(transientlyFailed).ToHashSet();
        var existingIds = _tmdbPeople.GetExistingTmdbPersonIDs(candidates);
        var missingPersonIds = candidates.Except(existingIds).ToHashSet();
        if (missingPersonIds.Count > 0)
            deleteOrphansAndLog(missingPersonIds);
    }

    private async Task<bool> UpdateShowAlternateOrdering(TMDB_Show tmdbShow, TvShow show)
    {
        _logger.LogDebug(
            "Checking {count} episode group collections to create alternate orderings for show {ShowTitle} (Show={ShowId})",
            show.EpisodeGroups!.Results!.Count,
            show.Name,
            show.Id);

        var hiddenEpisodes = _tmdbEpisodes.GetByTmdbShowID(show.Id)
            .Where(episode => episode.IsHidden)
            .Select(episode => episode.Id)
            .ToHashSet();
        var existingOrdering = _tmdbAlternateOrdering.GetByTmdbShowID(show.Id)
            .ToDictionary(ordering => ordering.Id);
        var orderingToAdd = 0;
        var orderingToSkip = new HashSet<string>();
        var orderingToSave = new List<TMDB_AlternateOrdering>();

        var existingSeasons = _tmdbAlternateOrderingSeasons.GetByTmdbShowID(show.Id)
            .ToDictionary(season => season.Id);
        var seasonsToAdd = 0;
        var seasonsToSkip = new HashSet<string>();
        var seasonsToSave = new HashSet<TMDB_AlternateOrdering_Season>();

        var existingEpisodes = _tmdbAlternateOrderingEpisodes.GetByTmdbShowID(show.Id)
            .ToDictionary(episode => episode.Id);
        var episodesToAdd = 0;
        var episodesToSkip = new HashSet<string>();
        var episodesToSave = new List<TMDB_AlternateOrdering_Episode>();

        foreach (var reducedCollection in show.EpisodeGroups.Results)
        {
            // The show endpoint leaves the groups out of the collection.
            var collection = await _client.UseClient(c => c.GetTvEpisodeGroupsAsync(reducedCollection.Id!), $"Get alternate ordering {reducedCollection.Id} \"{reducedCollection.Name}\" for show {show.Id} \"{show.Name}\"").ConfigureAwait(false) ??
                throw new Exception($"Unable to fetch alternate ordering \"{reducedCollection.Name}\" for show \"{show.Name}\".");

            if (!existingOrdering.TryGetValue(collection.Id!, out var tmdbOrdering))
            {
                orderingToAdd++;
                tmdbOrdering = new(collection.Id!);
            }

            var orderingUpdated = tmdbOrdering.Populate(collection, show.Id);

            var totalEpisodeCount = 0;
            var totalHiddenEpisodeCount = 0;
            foreach (var episodeGroup in collection.Groups!)
            {
                if (!existingSeasons.TryGetValue(episodeGroup.Id!, out var tmdbSeason))
                {
                    seasonsToAdd++;
                    tmdbSeason = new(episodeGroup.Id!);
                }

                var seasonUpdated = tmdbSeason.Populate(episodeGroup, collection.Id!, show.Id, episodeGroup.Order);

                var episodeCount = 0;
                var hiddenEpisodeCount = 0;
                var episodeNumberCount = 1;
                foreach (var episode in episodeGroup.Episodes!)
                {
                    if (!episode.Id.HasValue)
                        continue;

                    var episodeNumber = episodeNumberCount++;
                    var episodeId = episode.Id.Value;
                    if (hiddenEpisodes.Contains(episodeId))
                        hiddenEpisodeCount++;
                    else
                        episodeCount++;

                    if (!existingEpisodes.TryGetValue($"{episodeGroup.Id}:{episodeId}", out var tmdbEpisode))
                    {
                        episodesToAdd++;
                        tmdbEpisode = new(episodeGroup.Id!, episodeId);
                    }

                    var episodeUpdated = tmdbEpisode.Populate(collection.Id!, show.Id, episodeGroup.Order, episodeNumber);
                    if (episodeUpdated)
                    {
                        tmdbEpisode.LastUpdatedAt = DateTime.Now;
                        episodesToSave.Add(tmdbEpisode);
                    }

                    episodesToSkip.Add(tmdbEpisode.Id);
                }

                if (tmdbSeason.EpisodeCount != episodeCount)
                {
                    tmdbSeason.EpisodeCount = episodeCount;
                    seasonUpdated = true;
                }
                if (tmdbSeason.HiddenEpisodeCount != hiddenEpisodeCount)
                {
                    tmdbSeason.HiddenEpisodeCount = hiddenEpisodeCount;
                    seasonUpdated = true;
                }

                if (seasonUpdated)
                {
                    tmdbSeason.LastUpdatedAt = DateTime.Now;
                    seasonsToSave.Add(tmdbSeason);
                }

                totalEpisodeCount += episodeCount;
                totalHiddenEpisodeCount += hiddenEpisodeCount;
                seasonsToSkip.Add(tmdbSeason.Id);
            }

            if (tmdbOrdering.EpisodeCount != totalEpisodeCount)
            {
                tmdbOrdering.EpisodeCount = totalEpisodeCount;
                orderingUpdated = true;
            }
            if (tmdbOrdering.HiddenEpisodeCount != totalHiddenEpisodeCount)
            {
                tmdbOrdering.HiddenEpisodeCount = totalHiddenEpisodeCount;
                orderingUpdated = true;
            }

            if (orderingUpdated)
            {
                tmdbOrdering.LastUpdatedAt = DateTime.Now;
                orderingToSave.Add(tmdbOrdering);
            }

            orderingToSkip.Add(tmdbOrdering.Id);
        }
        var orderingToRemove = existingOrdering.Values
            .ExceptBy(orderingToSkip, ordering => ordering.Id)
            .ToList();
        var seasonsToRemove = existingSeasons.Values
            .ExceptBy(seasonsToSkip, season => season.Id)
            .ToList();
        var episodesToRemove = existingEpisodes.Values
            .ExceptBy(episodesToSkip, episode => episode.Id)
            .ToList();

        _logger.LogDebug(
            "Added/updated/removed/skipped {oa}/{ou}/{or}/{os} alternate orderings, {sa}/{su}/{sr}/{ss} alternate ordering seasons, and {ea}/{eu}/{er}/{es} alternate ordering episodes for show {ShowTitle} (Show={ShowId})",
            orderingToAdd,
            orderingToSave.Count - orderingToAdd,
            orderingToRemove.Count,
            existingOrdering.Count + orderingToAdd - orderingToRemove.Count - orderingToSave.Count,
            seasonsToAdd,
            seasonsToSave.Count - seasonsToAdd,
            seasonsToRemove.Count,
            existingSeasons.Count + seasonsToAdd - seasonsToRemove.Count - seasonsToSave.Count,
            episodesToAdd,
            episodesToSave.Count - episodesToAdd,
            episodesToRemove.Count,
            existingEpisodes.Count + episodesToAdd - episodesToRemove.Count - episodesToSave.Count,
            show.Name,
            show.Id);

        _tmdbAlternateOrdering.Save(orderingToSave);
        _tmdbAlternateOrdering.Delete(orderingToRemove);

        _tmdbAlternateOrderingSeasons.Save(seasonsToSave);
        _tmdbAlternateOrderingSeasons.Delete(seasonsToRemove);

        _tmdbAlternateOrderingEpisodes.Save(episodesToSave);
        _tmdbAlternateOrderingEpisodes.Delete(episodesToRemove);

        // A show that chose an episode group TMDB no longer has falls back to
        // its default ordering. Only the show can have chosen one of its own.
        var preferredOrderingUpdated = false;
        if (tmdbShow.PreferredOrderingID is { } chosen && orderingToRemove.Any(ordering => ((IMetadata)ordering).ID == chosen))
        {
            tmdbShow.PreferredOrderingID = null;
            preferredOrderingUpdated = true;
        }

        return orderingToSave.Count > 0 ||
            orderingToRemove.Count > 0 ||
            seasonsToSave.Count > 0 ||
            seasonsToRemove.Count > 0 ||
            episodesToSave.Count > 0 ||
            episodesToRemove.Count > 0 ||
            preferredOrderingUpdated;
    }

    private (bool, IEnumerable<int>, IEnumerable<int>) UpdateEpisodeCastAndCrew(TMDB_Episode tmdbEpisode, CreditsWithGuestStars credits)
    {
        var peopleToAddOrKeep = new HashSet<int>();
        var counter = 0;
        var castToAdd = 0;
        var castToKeep = new HashSet<string>();
        var castToSave = new List<TMDB_Episode_Cast>();
        var existingCastDict = _tmdbEpisodeCast.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(cast => cast.TmdbCreditID);
        var guestOffset = credits.Cast!.Count;
        foreach (var cast in credits.Cast.Concat(credits.GuestStars!))
        {
            var ordering = counter++;
            var isGuestRole = ordering >= guestOffset;
            castToKeep.Add(cast.CreditId!);
            peopleToAddOrKeep.Add(cast.Id);

            var roleUpdated = false;
            if (!existingCastDict.TryGetValue(cast.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbShowID = tmdbEpisode.TmdbShowID,
                    TmdbSeasonID = tmdbEpisode.TmdbSeasonID,
                    TmdbEpisodeID = tmdbEpisode.Id,
                    TmdbPersonID = cast.Id,
                    TmdbCreditID = cast.CreditId!,
                    Ordering = ordering,
                    IsGuestRole = isGuestRole,
                };
                castToAdd++;
                roleUpdated = true;
            }

            var characterName = cast.Character!.Replace(" (voice)", "");
            if (role.CharacterName != characterName)
            {
                role.CharacterName = characterName;
                roleUpdated = true;
            }

            if (role.Ordering != ordering)
            {
                role.Ordering = ordering;
                roleUpdated = true;
            }

            if (role.IsGuestRole != isGuestRole)
            {
                role.IsGuestRole = isGuestRole;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                castToSave.Add(role);
            }
        }

        var crewToAdd = 0;
        var crewToKeep = new HashSet<string>();
        var crewToSave = new List<TMDB_Episode_Crew>();
        var existingCrewDict = _tmdbEpisodeCrew.GetByTmdbEpisodeID(tmdbEpisode.Id)
            .ToDictionary(crew => crew.TmdbCreditID);
        foreach (var crew in credits.Crew!)
        {
            peopleToAddOrKeep.Add(crew.Id);
            crewToKeep.Add(crew.CreditId!);
            var roleUpdated = false;
            if (!existingCrewDict.TryGetValue(crew.CreditId!, out var role))
            {
                role = new()
                {
                    TmdbShowID = tmdbEpisode.TmdbShowID,
                    TmdbSeasonID = tmdbEpisode.TmdbSeasonID,
                    TmdbEpisodeID = tmdbEpisode.Id,
                    TmdbPersonID = crew.Id,
                    TmdbCreditID = crew.CreditId!,
                };
                crewToAdd++;
                roleUpdated = true;
            }

            if (role.Department != crew.Department)
            {
                role.Department = crew.Department!;
                roleUpdated = true;
            }

            if (role.Job != crew.Job)
            {
                role.Job = crew.Job!;
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                crewToSave.Add(role);
            }
        }

        var castToRemove = existingCastDict.Values
            .ExceptBy(castToKeep, cast => cast.TmdbCreditID)
            .ToList();
        var crewToRemove = existingCrewDict.Values
            .ExceptBy(crewToKeep, crew => crew.TmdbCreditID)
            .ToList();

        _tmdbEpisodeCast.Save(castToSave);
        _tmdbEpisodeCrew.Save(crewToSave);
        _tmdbEpisodeCast.Delete(castToRemove);
        _tmdbEpisodeCrew.Delete(crewToRemove);

        var peopleToPotentiallyRemove = new HashSet<int>([
            ..existingCastDict.Values.Select(cast => cast.TmdbPersonID),
            ..existingCrewDict.Values.Select(crew => crew.TmdbPersonID),
        ]);

        _logger.LogDebug(
            "Added/updated/removed/skipped {aa}/{au}/{ar}/{as} cast and {ra}/{ru}/{rr}/{rs} crew for episode {EpisodeTitle} (Show={ShowId}, Season={SeasonId}, Episode={EpisodeId})",
            castToAdd,
            castToSave.Count - castToAdd,
            castToRemove.Count,
            existingCastDict.Count - (castToSave.Count - castToAdd),
            crewToAdd,
            crewToSave.Count - crewToAdd,
            crewToRemove.Count,
            existingCrewDict.Count - (crewToSave.Count - crewToAdd),
            tmdbEpisode.EnglishTitle,
            tmdbEpisode.TmdbShowID,
            tmdbEpisode.TmdbSeasonID,
            tmdbEpisode.TmdbEpisodeID
        );
        return (
            castToSave.Count > 0 ||
            castToRemove.Count > 0 ||
            crewToSave.Count > 0 ||
            crewToRemove.Count > 0,
            peopleToAddOrKeep,
            peopleToPotentiallyRemove
        );
    }

    private async Task<bool> UpdateShowNetworks(TMDB_Show tmdbShow, TvShow show)
    {
        var index = 0;
        var xrefsToSkip = new HashSet<int>();
        var xrefsToSave = new List<TMDB_Show_Network>();
        var existingNetworks = tmdbShow.TmdbNetworkCrossReferences
            .ToDictionary(network => network.TmdbNetworkID);
        var existingXref = _xrefTmdbShowNetwork.GetByTmdbShowID(tmdbShow.Id)
            .ToDictionary(xref => xref.TmdbNetworkID);
        foreach (var network in show.Networks!)
        {
            var ordering = index++;
            if (existingXref.TryGetValue(network.Id, out var xref))
            {
                xrefsToSkip.Add(xref.TMDB_Show_NetworkID);
                if (xref.Ordering != ordering)
                {
                    xref.Ordering = ordering;
                    xrefsToSave.Add(xref);
                }
            }
            else
            {
                xref = new()
                {
                    Ordering = ordering,
                    TmdbNetworkID = network.Id,
                    TmdbShowID = tmdbShow.Id,
                };
                xrefsToSave.Add(xref);
            }
        }

        var networksToPurge = new HashSet<int>();
        var xrefsToRemove = existingNetworks.Values.ExceptBy(xrefsToSkip, network => network.TMDB_Show_NetworkID).ToList();
        foreach (var xref in xrefsToRemove)
        {
            if (_xrefTmdbShowNetwork.GetByTmdbNetworkID(xref.TmdbNetworkID).Count <= 1)
                networksToPurge.Add(xref.TmdbNetworkID);
        }

        _xrefTmdbShowNetwork.Save(xrefsToSave);
        _xrefTmdbShowNetwork.Delete(xrefsToRemove);

        foreach (var network in show.Networks)
            await CreateOrUpdateNetwork(network);
        foreach (var networkId in networksToPurge)
            await PurgeShowNetwork(networkId);

        return true;
    }

    private async Task CreateOrUpdateNetwork(NetworkWithLogo network)
    {
        using (await GetLockForEntity(MetadataEntityType.Network, network.Id, "metadata & images", "Update").ConfigureAwait(false))
        {
            var tmdbNetwork = _tmdbNetwork.GetByTmdbNetworkID(network.Id) ??
                new() { TmdbNetworkID = network.Id };
            var updated = tmdbNetwork.TMDB_NetworkID is 0;
            if (!string.Equals(tmdbNetwork.Name, network.Name))
            {
                tmdbNetwork.Name = network.Name!;
                updated = true;
            }
            if (!string.Equals(tmdbNetwork.CountryOfOrigin, network.OriginCountry))
            {
                tmdbNetwork.CountryOfOrigin = network.OriginCountry!;
                updated = true;
            }
            if (_xrefTmdbShowNetwork.GetByTmdbNetworkID(network.Id).Count == 0 && tmdbNetwork.LastOrphanedAt.HasValue)
            {
                tmdbNetwork.LastOrphanedAt = null;
                updated = true;
            }
            if (updated)
            {
                _tmdbNetwork.Save(tmdbNetwork);
                _logger.LogDebug("Updated TMDB Network (Network={NetworkId})", network.Id);
            }

            var settings = _settingsProvider.GetSettings();
            if (!string.IsNullOrEmpty(network.LogoPath))
                await _imageService.DownloadImageByType(network.LogoPath, ImageEntityType.Logo, tmdbNetwork, isDesired: settings.Image.GetMetadataSourceSettings(MetadataSource.TMDB).AutoDownloadStudioImages);
        }
    }

    #endregion

    #region Purge (Shows)

    /// <summary>
    ///   Removes a show from TMDB's tables, with its seasons, episodes,
    ///   alternate orderings, credits, titles, overviews, suggestions and
    ///   image links, and the companies and networks only it used.
    /// </summary>
    /// <remarks>
    ///   The people it credited are stamped as orphaned and removed once they
    ///   have been for long enough. The links to the show are the core's, and
    ///   are gone before this is called. The show's own row goes last, so an
    ///   interrupted purge never leaves seasons or episodes without it.
    /// </remarks>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns>A task that completes once the show is removed.</returns>
    public async Task PurgeShow(int showId)
    {
        var show = _tmdbShows.GetByTmdbShowID(showId);

        PurgeShowCompanies(showId);

        await PurgeShowNetworks(showId);

        PurgeSuggestions(MetadataEntityType.Series, showId);

        PurgeShowEpisodeGroups(showId);

        PurgeShowEpisodes(showId);

        PurgeShowSeasons(showId);

        await PurgeShowCastAndCrew(showId);

        PurgeTitlesAndOverviews(MetadataEntityType.Series, showId);

        _imageService.PurgeImages(show ?? new() { TmdbShowID = showId });

        if (show is not null)
        {
            _logger.LogTrace(
                "Removing show {ShowName} (Show={ShowId})",
                show.EnglishTitle,
                showId
            );
            _tmdbShows.Delete(show);
        }
    }

    private void PurgeShowCompanies(int showId)
    {
        var xrefsToRemove = _xrefTmdbCompanyEntity.GetByTmdbEntityTypeAndID(MetadataEntityType.Series, showId);
        foreach (var xref in xrefsToRemove)
        {
            // Delete xref or purge company.
            var xrefs = _xrefTmdbCompanyEntity.GetByTmdbCompanyID(xref.TmdbCompanyID);
            if (xrefs.Count > 1)
                _xrefTmdbCompanyEntity.Delete(xref);
            else
                PurgeCompany(xref.TmdbCompanyID);
        }
    }

    private async Task PurgeShowNetworks(int showId)
    {
        var xrefsToRemove = _xrefTmdbShowNetwork.GetByTmdbShowID(showId);
        foreach (var xref in xrefsToRemove)
        {
            // The xref goes first so PurgeShowNetwork's re-check sees no link left.
            var xrefs = _xrefTmdbShowNetwork.GetByTmdbNetworkID(xref.TmdbNetworkID);
            _xrefTmdbShowNetwork.Delete(xref);
            if (xrefs.Count == 1)
                await PurgeShowNetwork(xref.TmdbNetworkID);
        }
    }

    /// <summary>
    ///   Stamps the networks no show names any more, and removes the ones
    ///   stamped before a cutoff.
    /// </summary>
    /// <param name="orphanedBefore">
    ///   Remove only the networks orphaned before this time; left out, the
    ///   ones orphaned for longer than the metadata settings say.
    /// </param>
    /// <returns>How many networks were removed.</returns>
    public async Task<int> PurgeUnlinkedShowNetworks(DateTime? orphanedBefore = null)
    {
        var linkedNetworkIds = _xrefTmdbShowNetwork.GetAll().Select(x => x.TmdbNetworkID).ToHashSet();
        var networks = _tmdbNetwork.GetAll().Where(p => !linkedNetworkIds.Contains(p.TmdbNetworkID)).ToList();
        _logger.LogDebug("Checking {Count} orphaned networks if they should be purged.", networks.Count);
        var removed = 0;
        foreach (var network in networks)
        {
            if (await PurgeShowNetwork(network.TmdbNetworkID, orphanedBefore))
                removed++;
        }

        return removed;
    }

    /// <summary>
    ///   Stamps a network no show names any more, or removes it once it was
    ///   stamped before a cutoff.
    /// </summary>
    /// <param name="networkId">The TMDB network ID.</param>
    /// <param name="orphanedBefore">
    ///   Remove the network only when it was orphaned before this time; left
    ///   out, when it was orphaned for longer than the metadata settings say.
    /// </param>
    /// <returns>Whether the network was removed.</returns>
    private async Task<bool> PurgeShowNetwork(int networkId, DateTime? orphanedBefore = null)
    {
        using (await GetLockForEntity(MetadataEntityType.Network, networkId, "metadata & images", "Purge").ConfigureAwait(false))
        {
            // Re-check under the lock: abort if the network was re-linked since the caller's snapshot.
            if (_xrefTmdbShowNetwork.GetByTmdbNetworkID(networkId).Count > 0)
                return false;

            var tmdbNetwork = _tmdbNetwork.GetByTmdbNetworkID(networkId);
            if (tmdbNetwork is not null)
            {
                if (!tmdbNetwork.LastOrphanedAt.HasValue)
                {
                    tmdbNetwork.LastOrphanedAt = DateTime.UtcNow;
                    _tmdbNetwork.Save(tmdbNetwork);
                    _logger.LogDebug("Marked TMDB Network as orphaned. (Network={NetworkId})", networkId);
                    return false;
                }

                // TMDB keeps its stamps in UTC.
                var cutoff = (orphanedBefore ?? DateTime.Now.AddDays(-_settingsProvider.GetSettings().Metadata.PurgeOrphanedAfterDays)).ToUniversalTime();
                if (tmdbNetwork.LastOrphanedAt.Value >= cutoff)
                {
                    _logger.LogDebug("TMDB Network has not been orphaned for long enough yet. Skipping. (Network={NetworkId})", networkId);
                    return false;
                }

                _logger.LogDebug("Removing TMDB Network. (Network={NetworkId})", networkId);
                _tmdbNetwork.Delete(tmdbNetwork);
            }

            _imageService.PurgeImages(tmdbNetwork ?? new() { TmdbNetworkID = networkId });
            return true;
        }
    }

    private void PurgeShowEpisodes(int showId)
    {
        var episodesToRemove = _tmdbEpisodes.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} episodes for show (Show={ShowId})",
            episodesToRemove.Count,
            showId
        );
        foreach (var episode in episodesToRemove)
            PurgeShowEpisode(episode);

        _tmdbEpisodes.Delete(episodesToRemove);
    }

    private void PurgeShowEpisode(TMDB_Episode episode)
    {
        _imageService.PurgeImages(episode);

        PurgeTitlesAndOverviews(MetadataEntityType.Episode, episode.Id);
    }

    private void PurgeShowSeasons(int showId)
    {
        var seasonsToRemove = _tmdbSeasons.GetByTmdbShowID(showId);

        _logger.LogDebug(
            "Removing {count} seasons for show (Show={ShowId})",
            seasonsToRemove.Count,
            showId
        );
        foreach (var season in seasonsToRemove)
            PurgeShowSeason(season);

        _tmdbSeasons.Delete(seasonsToRemove);
    }

    private void PurgeShowSeason(TMDB_Season season)
    {
        _imageService.PurgeImages(season);

        PurgeTitlesAndOverviews(MetadataEntityType.Season, season.Id);
    }

    private async Task PurgeShowCastAndCrew(int showId)
    {
        var castMembers = _tmdbEpisodeCast.GetByTmdbShowID(showId);
        var crewMembers = _tmdbEpisodeCrew.GetByTmdbShowID(showId);

        _tmdbEpisodeCast.Delete(castMembers);
        _tmdbEpisodeCrew.Delete(crewMembers);

        var allPeopleSet = castMembers.Select(c => c.TmdbPersonID)
            .Concat(crewMembers.Select(c => c.TmdbPersonID))
            .Distinct()
            .ToHashSet();
        foreach (var personId in allPeopleSet)
            await PurgePerson(personId);
    }

    private void PurgeShowEpisodeGroups(int showId)
    {
        var episodes = _tmdbAlternateOrderingEpisodes.GetByTmdbShowID(showId);
        var seasons = _tmdbAlternateOrderingSeasons.GetByTmdbShowID(showId);
        var orderings = _tmdbAlternateOrdering.GetByTmdbShowID(showId);

        _logger.LogDebug("Removing {EpisodeCount} episodes and {SeasonCount} seasons across {OrderingCount} alternate orderings for show. (Show={ShowId})", episodes.Count, seasons.Count, orderings.Count, showId);
        _tmdbAlternateOrderingEpisodes.Delete(episodes);
        _tmdbAlternateOrderingSeasons.Delete(seasons);
        _tmdbAlternateOrdering.Delete(orderings);
    }

    /// <summary>
    ///   Removes every show's alternate orderings.
    /// </summary>
    public void PurgeAllShowEpisodeGroups()
    {
        _logger.LogInformation("Purging all show episode groups.");

        var episodes = _tmdbAlternateOrderingEpisodes.GetAll();
        var seasons = _tmdbAlternateOrderingSeasons.GetAll();
        var orderings = _tmdbAlternateOrdering.GetAll();
        var shows = new HashSet<int>([
            ..episodes.Select(e => e.TmdbShowID),
            ..seasons.Select(s => s.TmdbShowID),
            ..orderings.Select(o => o.TmdbShowID),
        ]);

        _logger.LogDebug("Removing {EpisodeCount} episodes and {SeasonCount} seasons across {OrderingCount} alternate orderings for {ShowCount} shows.", episodes.Count, seasons.Count, orderings.Count, shows.Count);
        _tmdbAlternateOrderingEpisodes.Delete(episodes);
        _tmdbAlternateOrderingSeasons.Delete(seasons);
        _tmdbAlternateOrdering.Delete(orderings);
    }

    #endregion
}
