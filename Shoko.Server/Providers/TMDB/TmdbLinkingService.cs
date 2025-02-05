
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using CrossRefSource = Shoko.Models.Enums.CrossRefSource;
using MatchRating = Shoko.Models.Enums.MatchRating;

// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TmdbLinkingService
{
    private static readonly Dictionary<char, char> _characterReplacementDict = new()
    {
        { '’', '\'' },
        { '”', '"' },
        { '‘', '\'' },
        { '“', '"' },
    };

    private readonly ILogger<TmdbLinkingService> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly ISettingsProvider _settingsProvider;

    private readonly TmdbImageService _imageService;

    private readonly AnimeSeriesRepository _animeSeries;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly AniDB_EpisodeRepository _anidbEpisodes;

    private readonly AniDB_Episode_TitleRepository _anidbEpisodeTitles;

    private readonly TMDB_ShowRepository _tmdbShows;

    private readonly TMDB_EpisodeRepository _tmdbEpisodes;

    private readonly CrossRef_AniDB_TMDB_MovieRepository _xrefAnidbTmdbMovies;

    private readonly CrossRef_AniDB_TMDB_ShowRepository _xrefAnidbTmdbShows;

    private readonly CrossRef_AniDB_TMDB_EpisodeRepository _xrefAnidbTmdbEpisodes;

    public TmdbLinkingService(
        ILogger<TmdbLinkingService> logger,
        ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider,
        TmdbImageService imageService,
        AnimeSeriesRepository animeSeries,
        AniDB_AnimeRepository anidbAnime,
        AniDB_EpisodeRepository anidbEpisodes,
        AniDB_Episode_TitleRepository anidbEpisodeTitles,
        TMDB_ShowRepository tmdbShows,
        TMDB_EpisodeRepository tmdbEpisodes,
        CrossRef_AniDB_TMDB_MovieRepository xrefAnidbTmdbMovies,
        CrossRef_AniDB_TMDB_ShowRepository xrefAnidbTmdbShows,
        CrossRef_AniDB_TMDB_EpisodeRepository xrefAnidbTmdbEpisodes
    )
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _imageService = imageService;
        _animeSeries = animeSeries;
        _anidbAnime = anidbAnime;
        _anidbEpisodes = anidbEpisodes;
        _anidbEpisodeTitles = anidbEpisodeTitles;
        _tmdbShows = tmdbShows;
        _tmdbEpisodes = tmdbEpisodes;
        _xrefAnidbTmdbMovies = xrefAnidbTmdbMovies;
        _xrefAnidbTmdbShows = xrefAnidbTmdbShows;
        _xrefAnidbTmdbEpisodes = xrefAnidbTmdbEpisodes;
    }

    #region Shared
    public void RemoveAllLinks(bool removeShowLinks = true, bool removeMovieLinks = true)
    {
        _logger.LogInformation("Removing AniDB - TMDB links.");
        if (removeShowLinks)
        {
            var showXrefs = _xrefAnidbTmdbShows.GetAll();

            _logger.LogInformation("Removing {Count} TMDB show links.", showXrefs.Count);
            _xrefAnidbTmdbShows.Delete(showXrefs);

            var episodeXrefs = _xrefAnidbTmdbEpisodes.GetAll();

            _logger.LogInformation("Removing {Count} TMDB episode links.", episodeXrefs.Count);
            _xrefAnidbTmdbEpisodes.Delete(episodeXrefs);
        }

        if (removeMovieLinks)
        {
            var movieXrefs = _xrefAnidbTmdbMovies.GetAll();

            _logger.LogInformation("Removing {Count} TMDB movie links.", movieXrefs.Count);
            _xrefAnidbTmdbMovies.Delete(movieXrefs);
        }

        _logger.LogInformation("Done removing AniDB - TMDB links.");
    }

    public void ResetAutoLinkingState(bool disabled = false)
    {
        var series = _animeSeries.GetAll();
        var count = series.Count;
        if (disabled)
            _logger.LogInformation("Disabling auto-linking for {Count} Shoko series.", count);
        else
            _logger.LogInformation("Enabling auto-linking for {Count} Shoko series.", count);

        var itemNo = 0;
        foreach (var seriesItem in series)
        {
            seriesItem.IsTMDBAutoMatchingDisabled = disabled;
            _animeSeries.Save(seriesItem, false, true, false);

            if (++itemNo % 100 == 0)
            {
                if (disabled)
                    _logger.LogInformation("Disabling auto-linking for {Count} Shoko series. (Processed {Processed})", count, itemNo);
                else
                    _logger.LogInformation("Enabling auto-linking for {Count} Shoko series. (Processed {Processed})", count, itemNo);
            }
        }
    }

    #endregion

    #region Movie Links

    public async Task AddMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool additiveLink = false, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            await RemoveAllMovieLinksForEpisode(anidbEpisodeId);

        var episode = _anidbEpisodes.GetByEpisodeID(anidbEpisodeId);
        if (episode == null)
        {
            _logger.LogWarning("AniDB Episode (ID:{AnidbID}) not found", anidbEpisodeId);
            return;
        }

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Movie Link: AniDB episode (EpisodeID={EpisodeID},AnimeID={AnimeID}) → TMDB movie (MovieID={TmdbID})", anidbEpisodeId, episode.AnimeID, tmdbMovieId);
        var xref = _xrefAnidbTmdbMovies.GetByAnidbEpisodeAndTmdbMovieIDs(anidbEpisodeId, tmdbMovieId) ?? new(anidbEpisodeId, episode.AnimeID, tmdbMovieId);
        xref.AnidbAnimeID = episode.AnimeID;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        _xrefAnidbTmdbMovies.Save(xref);
    }

    public async Task RemoveMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = _xrefAnidbTmdbMovies.GetByAnidbEpisodeAndTmdbMovieIDs(anidbEpisodeId, tmdbMovieId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        if (_anidbEpisodes.GetByEpisodeID(anidbEpisodeId) is { } anidbEpisode && _animeSeries.GetByAnimeID(anidbEpisode.AnimeID) is { } series && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, false);
        }

        await RemoveMovieLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllMovieLinksForAnime(int anidbAnimeId, bool purge = false, bool removeImageFiles = true)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByAnidbAnimeID(anidbAnimeId);
        _logger.LogInformation("Removing {Count} TMDB movie links for AniDB anime. (AnimeID={AnimeID})", xrefs.Count, anidbAnimeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        if (_animeSeries.GetByAnimeID(anidbAnimeId) is { } series && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, false);
        }

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllMovieLinksForEpisode(int anidbEpisodeId, bool purge = false, bool removeImageFiles = true)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByAnidbEpisodeID(anidbEpisodeId);
        _logger.LogInformation("Removing {Count} TMDB movie links for AniDB episode. (EpisodeID={EpisodeID})", xrefs.Count, anidbEpisodeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        if (_anidbEpisodes.GetByEpisodeID(anidbEpisodeId) is { } anidbEpisode && _animeSeries.GetByAnimeID(anidbEpisode.AnimeID) is { } series && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, false);
        }

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllMovieLinksForMovie(int tmdbMovieId)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByTmdbMovieID(tmdbMovieId);
        _logger.LogInformation("Removing {Count} TMDB movie links for TMDB movie. (MovieID={MovieID})", xrefs.Count, tmdbMovieId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, false, false);
    }

    private async Task RemoveMovieLink(CrossRef_AniDB_TMDB_Movie xref, bool removeImageFiles = true, bool purge = false)
    {
        _imageService.ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Movie, xref.TmdbMovieID);

        _logger.LogInformation("Removing TMDB movie link: AniDB episode (EpisodeID={EpisodeID},AnimeID={AnimeID}) → TMDB movie (ID:{TmdbID})", xref.AnidbEpisodeID, xref.AnidbAnimeID, xref.TmdbMovieID);
        _xrefAnidbTmdbMovies.Delete(xref);

        if (purge)
            await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<PurgeTmdbMovieJob>(c =>
            {
                c.TmdbMovieID = xref.TmdbMovieID;
                c.RemoveImageFiles = removeImageFiles;
            });
    }

    #endregion

    #region Show Links

    public async Task AddShowLink(int anidbAnimeId, int tmdbShowId, bool additiveLink = true, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            await RemoveAllShowLinksForAnime(anidbAnimeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB show link: AniDB (AnimeID={AnidbID}) → TMDB Show (ID={TmdbID})", anidbAnimeId, tmdbShowId);
        var xref = _xrefAnidbTmdbShows.GetByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId) ??
            new(anidbAnimeId, tmdbShowId);
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        _xrefAnidbTmdbShows.Save(xref);
        await Task.Run(() => MatchAnidbToTmdbEpisodes(anidbAnimeId, tmdbShowId, null, true, true));
    }

    public async Task RemoveShowLink(int anidbAnimeId, int tmdbShowId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = _xrefAnidbTmdbShows.GetByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = _animeSeries.GetByAnimeID(anidbAnimeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, false);
        }

        await RemoveShowLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllShowLinksForAnime(int animeId, bool purge = false, bool removeImageFiles = true)
    {
        var xrefs = _xrefAnidbTmdbShows.GetByAnidbAnimeID(animeId);
        _logger.LogInformation("Removing {Count} TMDB show links for AniDB anime. (AnimeID={AnimeID})", xrefs.Count, animeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = _animeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, false);
        }

        foreach (var xref in xrefs)
            await RemoveShowLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllShowLinksForShow(int showId)
    {
        var xrefs = _xrefAnidbTmdbShows.GetByTmdbShowID(showId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveShowLink(xref, false, false);
    }

    private async Task RemoveShowLink(CrossRef_AniDB_TMDB_Show xref, bool removeImageFiles = true, bool purge = false)
    {
        _imageService.ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Show, xref.TmdbShowID);

        _logger.LogInformation("Removing TMDB show link: AniDB anime (AnimeID={AnidbID}) → TMDB show (ID={TmdbID})", xref.AnidbAnimeID, xref.TmdbShowID);
        _xrefAnidbTmdbShows.Delete(xref);

        var xrefs = _xrefAnidbTmdbEpisodes.GetOnlyByAnidbAnimeAndTmdbShowIDs(xref.AnidbAnimeID, xref.TmdbShowID).ToList();
        if (_xrefAnidbTmdbShows.GetByAnidbAnimeID(xref.AnidbAnimeID).Count > 0 && _xrefAnidbTmdbEpisodes.GetByAnidbAnimeID(xref.AnidbAnimeID) is { Count: > 0 } extraXrefs)
            xrefs.AddRange(extraXrefs);
        _logger.LogInformation("Removing {XRefsCount} episodes cross-references for AniDB anime (AnimeID={AnidbID}) and TMDB show (ID={TmdbID})", xrefs.Count, xref.AnidbAnimeID, xref.TmdbShowID);
        _xrefAnidbTmdbEpisodes.Delete(xrefs);

        var scheduler = await _schedulerFactory.GetScheduler();
        if (purge)
            await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<PurgeTmdbShowJob>(c =>
            {
                c.TmdbShowID = xref.TmdbShowID;
                c.RemoveImageFiles = removeImageFiles;
            });
    }

    #endregion

    #region Episode Links

    public void ResetAllEpisodeLinks(int anidbAnimeId, bool allowAuto)
    {
        var hasXrefs = _xrefAnidbTmdbShows.GetByAnidbAnimeID(anidbAnimeId).Count > 0;
        if (hasXrefs)
        {
            var xrefs = _xrefAnidbTmdbEpisodes.GetByAnidbAnimeID(anidbAnimeId);
            var toSave = new List<CrossRef_AniDB_TMDB_Episode>();
            var toDelete = new List<CrossRef_AniDB_TMDB_Episode>();

            // Reset existing xrefs.
            var existingIDs = new HashSet<int>();
            foreach (var xref in xrefs)
            {
                if (existingIDs.Add(xref.AnidbEpisodeID))
                {
                    xref.TmdbShowID = 0;
                    xref.TmdbEpisodeID = 0;
                    xref.Ordering = 0;
                    xref.MatchRating = allowAuto ? MatchRating.SarahJessicaParker : MatchRating.UserVerified;
                    toSave.Add(xref);
                }
                else
                {
                    toDelete.Add(xref);
                }
            }

            // Add missing xrefs.
            var anidbEpisodesWithoutXrefs = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
                .Where(episode => !existingIDs.Contains(episode.AniDB_EpisodeID) && episode.AbstractEpisodeType is EpisodeType.Episode or EpisodeType.Special)
                .ToList();
            foreach (var anidbEpisode in anidbEpisodesWithoutXrefs)
                toSave.Add(new(anidbEpisode.AniDB_EpisodeID, anidbAnimeId, 0, 0, allowAuto ? MatchRating.SarahJessicaParker : MatchRating.UserVerified));

            // Save the changes.
            _xrefAnidbTmdbEpisodes.Save(toSave);
            _xrefAnidbTmdbEpisodes.Delete(toDelete);
        }
        else
        {
            // Remove all episode cross-references if no show is linked.
            var xrefs = _xrefAnidbTmdbEpisodes.GetByAnidbAnimeID(anidbAnimeId);
            _xrefAnidbTmdbEpisodes.Delete(xrefs);
        }
    }

    public bool SetEpisodeLink(int anidbEpisodeId, int tmdbEpisodeId, bool additiveLink = true, int? index = null)
    {
        var anidbEpisode = _anidbEpisodes.GetByEpisodeID(anidbEpisodeId);
        if (anidbEpisode == null)
            return false;

        // Set an empty link.
        if (tmdbEpisodeId == 0)
        {
            var xrefs = _xrefAnidbTmdbEpisodes.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisodeId, anidbEpisode.AnimeID, 0, 0);
            toSave.TmdbShowID = 0;
            toSave.TmdbEpisodeID = 0;
            toSave.Ordering = 0;
            toSave.MatchRating = MatchRating.UserVerified;
            var toDelete = xrefs.Skip(1).ToList();
            _xrefAnidbTmdbEpisodes.Save(toSave);
            _xrefAnidbTmdbEpisodes.Delete(toDelete);

            return true;
        }

        var tmdbEpisode = _tmdbEpisodes.GetByTmdbEpisodeID(tmdbEpisodeId);
        if (tmdbEpisode == null)
            return false;

        // Add another link
        if (additiveLink)
        {
            var toSave = _xrefAnidbTmdbEpisodes.GetByAnidbEpisodeAndTmdbEpisodeIDs(anidbEpisodeId, tmdbEpisodeId)
                ?? new(anidbEpisodeId, anidbEpisode.AnimeID, tmdbEpisodeId, tmdbEpisode.TmdbShowID);
            var existingAnidbLinks = _xrefAnidbTmdbEpisodes.GetByAnidbEpisodeID(anidbEpisodeId).MaxBy(x => x.Ordering) is { } x1 ? x1.Ordering + 1 : 0;
            var existingTmdbLinks = _xrefAnidbTmdbEpisodes.GetByTmdbEpisodeID(tmdbEpisodeId).MaxBy(x => x.Ordering) is { } x2 ? x2.Ordering + 1 : 0;
            if (toSave.CrossRef_AniDB_TMDB_EpisodeID == 0 && !index.HasValue)
                index = existingAnidbLinks > 0 ? existingAnidbLinks : existingTmdbLinks > 0 ? existingTmdbLinks : 0;
            if (index.HasValue)
                toSave.Ordering = index.Value;
            toSave.MatchRating = MatchRating.UserVerified;
            _xrefAnidbTmdbEpisodes.Save(toSave);
        }
        else
        {
            var xrefs = _xrefAnidbTmdbEpisodes.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisodeId, anidbEpisode.AnimeID, tmdbEpisodeId, tmdbEpisode.TmdbShowID);
            toSave.TmdbShowID = tmdbEpisode.TmdbShowID;
            toSave.TmdbEpisodeID = tmdbEpisode.TmdbEpisodeID;
            if (!index.HasValue && anidbEpisode.EpisodeNumber is > 0 &&
                _anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(anidbEpisode.AnimeID, anidbEpisode.EpisodeTypeEnum, anidbEpisode.EpisodeNumber - 1).FirstOrDefault() is { } previousEpisode)
            {
                var previousXrefs = _xrefAnidbTmdbEpisodes.GetByAnidbEpisodeID(previousEpisode.EpisodeID);
                if (previousXrefs.Count is 1 && previousXrefs[0].TmdbEpisodeID == tmdbEpisodeId)
                    index = previousXrefs[0].Ordering + 1;
            }
            toSave.Ordering = index ?? 0;
            toSave.MatchRating = MatchRating.UserVerified;
            var toDelete = xrefs.Skip(1).ToList();
            _xrefAnidbTmdbEpisodes.Save(toSave);
            _xrefAnidbTmdbEpisodes.Delete(toDelete);
        }

        return true;
    }

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> MatchAnidbToTmdbEpisodes(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId, bool useExisting = false, bool saveToDatabase = false, bool? useExistingOtherShows = null)
    {
        var anime = _anidbAnime.GetByAnimeID(anidbAnimeId);
        if (anime == null)
            return [];

        var show = _tmdbShows.GetByTmdbShowID(tmdbShowId);
        if (show == null)
            return [];

        var startedAt = DateTime.Now;
        _logger.LogTrace("Mapping AniDB Anime {AnidbAnimeId} to TMDB Show {TmdbShowId} (Season: {TmdbSeasonId}, Use Existing: {UseExisting}, Save To Database: {SaveToDatabase})", anidbAnimeId, tmdbShowId, tmdbSeasonId, useExisting, saveToDatabase);

        // Mapping logic
        var isOVA = anime.AbstractAnimeType is AnimeType.OVA;
        var toSkip = new HashSet<int>();
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode>();
        var crossReferences = new List<CrossRef_AniDB_TMDB_Episode>();
        var secondPass = new List<SVR_AniDB_Episode>();
        var fourthPass = new List<SVR_AniDB_Episode>();
        var thirdPass = new List<SVR_AniDB_Episode>();
        var existing = _xrefAnidbTmdbEpisodes.GetAllByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId)
            .GroupBy(xref => xref.AnidbEpisodeID)
            .ToDictionary(grouped => grouped.Key, grouped => grouped.ToList());
        var anidbEpisodes = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
            .Where(episode => episode.AbstractEpisodeType is EpisodeType.Episode or EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeTypeEnum)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToDictionary(episode => episode.EpisodeID);
        var tmdbEpisodeDict = _tmdbEpisodes.GetByTmdbShowID(tmdbShowId)
            .ToDictionary(episode => episode.TmdbEpisodeID);
        var tmdbEpisodes = tmdbEpisodeDict.Values
            .Where(episode => episode.SeasonNumber == 0 || !tmdbSeasonId.HasValue || episode.TmdbSeasonID == tmdbSeasonId.Value)
            .ToList();
        var considerExistingOtherLinks = useExistingOtherShows ?? _settingsProvider.GetSettings().TMDB.ConsiderExistingOtherLinks;
        if (considerExistingOtherLinks)
        {
            var otherShowsExisting = existing.Values.SelectMany(xref => xref).ExceptBy(anidbEpisodes.Keys.Append(0), xref => xref.AnidbEpisodeID).ToList();
            foreach (var link in otherShowsExisting)
            {
                _logger.LogTrace("Skipping existing episode link: AniDB episode (EpisodeID={EpisodeID},AnimeID={AnimeID}) → TMDB episode (EpisodeID={TmdbID})", link.AnidbEpisodeID, link.AnidbAnimeID, link.TmdbEpisodeID);

                // Exclude the linked episodes from the auto-match candidates.
                var index = tmdbEpisodes.FindIndex(episode => episode.TmdbEpisodeID == link.TmdbEpisodeID);
                if (index >= 0)
                    tmdbEpisodes.RemoveAt(index);
            }
        }

        var tmdbNormalEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
            .Where(episode => episode.SeasonNumber != 0)
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();
        var tmdbSpecialEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
            .Where(episode => episode.SeasonNumber == 0)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToList();
        var current = 0;
        foreach (var episode in anidbEpisodes.Values)
        {
            current++;
            _logger.LogTrace("Checking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {AnidbEpisodeID}, Progress: {Current}/{Total}, Pass: 1/4)", episode.EpisodeTypeEnum, episode.EpisodeNumber, episode.EpisodeID, current, anidbEpisodes.Count);
            var shouldAddNewLinks = true;
            if (useExisting && existing.TryGetValue(episode.EpisodeID, out var existingLinks) && existingLinks.Any(link => link.MatchRating is MatchRating.UserVerified or MatchRating.DateAndTitleMatches))
            {
                // Remove empty links if we have one or more empty links and at least one non-empty link.
                if (existingLinks.Any(a => a.TmdbEpisodeID is 0 && a.TmdbShowID is 0) && existingLinks.Any(a => a.TmdbEpisodeID is not 0 || a.TmdbShowID is not 0))
                    existingLinks = existingLinks
                        .Where(link => link.TmdbEpisodeID is not 0 || link.TmdbShowID is not 0)
                        .ToList();

                // Remove duplicates, if any.
                existingLinks = existingLinks.DistinctBy(link => (link.TmdbShowID, link.TmdbEpisodeID)).ToList();

                if (existingLinks.Count == 1 && existingLinks[0].TmdbEpisodeID is 0 && existingLinks[0].TmdbShowID is 0 && existingLinks[0].MatchRating is not MatchRating.UserVerified)
                    goto skipExistingLinks;

                // If hidden and no user verified links, then unset the auto link.
                shouldAddNewLinks = false;
                if ((episode.AnimeEpisode?.IsHidden ?? false) && !existingLinks.Any(link => link.MatchRating is MatchRating.UserVerified))
                {
                    _logger.LogTrace("Skipping hidden episode. (AniDB ID: {AnidbEpisodeID})", episode.EpisodeID);
                    var link = existingLinks[0];
                    if (link.TmdbEpisodeID is 0 && link.TmdbShowID is 0)
                    {
                        crossReferences.Add(link);
                        toSkip.Add(link.CrossRef_AniDB_TMDB_EpisodeID);
                    }
                    else
                    {
                        crossReferences.Add(new(episode.EpisodeID, anidbAnimeId, 0, 0, MatchRating.SarahJessicaParker, 0));
                    }
                    continue;
                }

                // Else return all existing links.
                foreach (var link in existingLinks)
                {
                    _logger.LogTrace("Skipping existing link for episode. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TmdbEpisodeID}, Rating: {MatchRating})", episode.EpisodeID, link.TmdbEpisodeID, link.MatchRating);
                    crossReferences.Add(link);
                    toSkip.Add(link.CrossRef_AniDB_TMDB_EpisodeID);

                    // Exclude the linked episodes from the auto-match candidates.
                    var index = tmdbEpisodes.FindIndex(episode => episode.TmdbEpisodeID == link.TmdbEpisodeID);
                    if (index >= 0)
                        tmdbEpisodes.RemoveAt(index);
                    index = tmdbNormalEpisodes.FindIndex(episode => episode.TmdbEpisodeID == link.TmdbEpisodeID);
                    if (index >= 0)
                        tmdbNormalEpisodes.RemoveAt(index);
                    index = tmdbSpecialEpisodes.FindIndex(episode => episode.TmdbEpisodeID == link.TmdbEpisodeID);
                    if (index >= 0)
                        tmdbSpecialEpisodes.RemoveAt(index);
                }
            }

            skipExistingLinks:;
            if (shouldAddNewLinks)
            {
                // If hidden then skip linking episode.
                if (episode.AnimeEpisode?.IsHidden ?? false)
                {
                    _logger.LogTrace("Skipping hidden episode. (AniDB ID: {AnidbEpisodeID})", episode.EpisodeID);
                    crossReferences.Add(new(episode.EpisodeID, anidbAnimeId, 0, 0, MatchRating.SarahJessicaParker, 0));
                    continue;
                }

                // Else try find a match.
                _logger.LogTrace("Linking episode. (AniDB ID: {AnidbEpisodeID}, Pass: 1/4)", episode.EpisodeID);
                var isSpecial = episode.AbstractEpisodeType is EpisodeType.Special || anime.AbstractAnimeType is not AnimeType.TVSeries and not AnimeType.Web;
                var episodeList = isSpecial ? tmdbSpecialEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(anime, episode, episodeList, isSpecial && !isOVA);
                if (crossRef.MatchRating is MatchRating.DateAndTitleMatches)
                {
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);

                    crossReferences.Add(crossRef);
                    toAdd.Add(crossRef);
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 1/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                }
                else
                {
                    _logger.LogTrace("Skipping episode in the first pass. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 1/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                    secondPass.Add(episode);
                }
            }
        }

        // Run a second pass on the episodes that weren't OV and DT links in the first pass.
        if (secondPass.Count > 0)
        {
            // Filter the new links by the currently in use seasons from the existing (and/or new) OV/DT links.
            var currentSessions = crossReferences
                .Select(NormalEpisodeSeasonNumberSelector)
                .Except([-1])
                .ToHashSet();
            if (currentSessions.Count > 0)
            {
                if (!isOVA)
                    currentSessions.Add(0);
                _logger.LogTrace("Filtering available episodes by currently in use seasons. (Current Sessions: {CurrentSessions}, Pass: 2/4)", string.Join(", ", currentSessions));
                tmdbEpisodes = (isOVA ? tmdbEpisodes : tmdbNormalEpisodes.Concat(tmdbSpecialEpisodes))
                    .Where(episode => currentSessions.Contains(episode.SeasonNumber))
                    .ToList();
                tmdbNormalEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
                    .Where(episode => episode.SeasonNumber != 0)
                    .OrderBy(episode => episode.SeasonNumber)
                    .ThenBy(episode => episode.EpisodeNumber)
                    .ToList();
                tmdbSpecialEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
                    .Where(episode => episode.SeasonNumber == 0)
                    .OrderBy(episode => episode.EpisodeNumber)
                    .ToList();
            }

            current = 0;
            foreach (var episode in secondPass)
            {
                // Try find a match.
                current++;
                _logger.LogTrace("Linking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {EpisodeID}, Progress: {Current}/{Total}, Pass: 2/4)", episode.EpisodeTypeEnum, episode.EpisodeNumber, episode.EpisodeID, current, secondPass.Count);
                var isSpecial = episode.AbstractEpisodeType is EpisodeType.Special || anime.AbstractAnimeType is not AnimeType.TVSeries and not AnimeType.Web;
                var episodeList = isSpecial ? tmdbSpecialEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(anime, episode, episodeList, isSpecial && !isOVA);
                if (crossRef.MatchRating is MatchRating.TitleMatches)
                {
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);

                    crossReferences.Add(crossRef);
                    toAdd.Add(crossRef);
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 2/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                }
                else
                {
                    _logger.LogTrace("Skipping episode in the second pass. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 2/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                    thirdPass.Add(episode);
                }
            }
        }

        // Run a third pass on the episodes that weren't OV, DT or T links in the first pass.
        if (thirdPass.Count > 0)
        {
            // Filter the new links by the currently in use seasons from the existing (and/or new) OV/DT links.
            var currentSessions = crossReferences
                .Select(NormalEpisodeSeasonNumberSelector)
                .Except([-1])
                .ToHashSet();
            if (currentSessions.Count > 0)
            {
                if (!isOVA)
                    currentSessions.Add(0);
                _logger.LogTrace("Filtering available episodes by currently in use seasons. (Current Sessions: {CurrentSessions}, Pass: 3/4)", string.Join(", ", currentSessions));
                tmdbEpisodes = (isOVA ? tmdbEpisodes : tmdbNormalEpisodes.Concat(tmdbSpecialEpisodes))
                    .Where(episode => currentSessions.Contains(episode.SeasonNumber))
                    .ToList();
                tmdbNormalEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
                    .Where(episode => episode.SeasonNumber != 0)
                    .OrderBy(episode => episode.SeasonNumber)
                    .ThenBy(episode => episode.EpisodeNumber)
                    .ToList();
                tmdbSpecialEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
                    .Where(episode => episode.SeasonNumber == 0)
                    .OrderBy(episode => episode.EpisodeNumber)
                    .ToList();
            }

            current = 0;
            foreach (var episode in thirdPass)
            {
                // Try find a match.
                current++;
                _logger.LogTrace("Linking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {EpisodeID}, Progress: {Current}/{Total}, Pass: 3/4)", episode.EpisodeTypeEnum, episode.EpisodeNumber, episode.EpisodeID, current, thirdPass.Count);
                var isSpecial = episode.AbstractEpisodeType is EpisodeType.Special || anime.AbstractAnimeType is not AnimeType.TVSeries and not AnimeType.Web;
                var episodeList = isSpecial ? tmdbSpecialEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(anime, episode, episodeList, isSpecial && !isOVA);
                if (crossRef.MatchRating is not MatchRating.FirstAvailable and not MatchRating.SarahJessicaParker)
                {
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);

                    crossReferences.Add(crossRef);
                    toAdd.Add(crossRef);
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 3/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                }
                else
                {
                    _logger.LogTrace("Skipping episode in the third pass. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 3/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                    fourthPass.Add(episode);
                }
            }
        }

        // Run a fourth pass on the episodes on the remaining episodes.
        if (fourthPass.Count > 0)
        {
            // Filter the new links by the currently in use seasons from the existing (and/or new) OV/DT links.
            var currentSessions = crossReferences
                .Select(NormalEpisodeSeasonNumberSelector)
                .Except([-1])
                .ToHashSet();
            if (currentSessions.Count > 0)
            {
                if (!isOVA)
                    currentSessions.Add(0);
                _logger.LogTrace("Filtering available episodes by currently in use seasons. (Current Sessions: {CurrentSessions}, Pass: 4/4)", string.Join(", ", currentSessions));
                tmdbEpisodes = (isOVA ? tmdbEpisodes : tmdbNormalEpisodes.Concat(tmdbSpecialEpisodes))
                    .Where(episode => currentSessions.Contains(episode.SeasonNumber))
                    .ToList();
                tmdbNormalEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
                    .Where(episode => episode.SeasonNumber != 0)
                    .OrderBy(episode => episode.SeasonNumber)
                    .ThenBy(episode => episode.EpisodeNumber)
                    .ToList();
                tmdbSpecialEpisodes = isOVA ? tmdbEpisodes : tmdbEpisodes
                    .Where(episode => episode.SeasonNumber == 0)
                    .OrderBy(episode => episode.EpisodeNumber)
                    .ToList();
            }

            current = 0;
            foreach (var episode in fourthPass)
            {
                // Try find a match.
                current++;
                _logger.LogTrace("Linking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {EpisodeID}, Progress: {Current}/{Total}, Pass: 4/4)", episode.EpisodeTypeEnum, episode.EpisodeNumber, episode.EpisodeID, current, fourthPass.Count);
                var isSpecial = episode.AbstractEpisodeType is EpisodeType.Special || anime.AbstractAnimeType is not AnimeType.TVSeries and not AnimeType.Web;
                var episodeList = isSpecial ? tmdbSpecialEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(anime, episode, episodeList, isSpecial && !isOVA);
                if (crossRef.TmdbEpisodeID != 0)
                {
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: 4/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating);
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);
                }
                else
                {
                    _logger.LogTrace("No match found for episode. (AniDB ID: {AnidbEpisodeID}, Pass: 4/4)", episode.EpisodeID);
                }

                crossReferences.Add(crossRef);
                toAdd.Add(crossRef);
            }
        }

        if (!saveToDatabase)
        {
            _logger.LogDebug(
                "Found {a} anidb/tmdb episode links for show {ShowTitle} in {Delta}. (Anime={AnimeId},Show={ShowId})",
                crossReferences.Count,
                anime.PreferredTitle,
                DateTime.Now - startedAt,
                anidbAnimeId,
                tmdbShowId
            );
            return crossReferences;
        }

        // Remove the current anidb episodes that does not overlap with the show.
        var toRemove = existing.Values
            .SelectMany(list => list)
            .Where(xref => (anidbEpisodes.ContainsKey(xref.AnidbEpisodeID) && !toSkip.Contains(xref.CrossRef_AniDB_TMDB_EpisodeID)) || (xref.TmdbShowID == tmdbShowId && !tmdbEpisodeDict.ContainsKey(xref.TmdbEpisodeID)))
            .ToList();

        _logger.LogDebug(
            "Added/removed/skipped {a}/{r}/{s} anidb/tmdb episode cross-references for show {ShowTitle} in {Delta} (Anime={AnimeId},Show={ShowId})",
            toAdd.Count,
            toRemove.Count,
            existing.Count - toRemove.Count,
            anime.PreferredTitle,
            DateTime.Now - startedAt,
            anidbAnimeId,
            tmdbShowId);
        _xrefAnidbTmdbEpisodes.Save(toAdd);
        _xrefAnidbTmdbEpisodes.Delete(toRemove);

        return crossReferences;

        int NormalEpisodeSeasonNumberSelector(CrossRef_AniDB_TMDB_Episode xref) => xref.TmdbEpisodeID is not 0 && (isOVA || anidbEpisodes[xref.AnidbEpisodeID].AbstractEpisodeType is EpisodeType.Episode) && tmdbEpisodeDict.TryGetValue(xref.TmdbEpisodeID, out var tmdbEpisode) ? tmdbEpisode.SeasonNumber : -1;
    }

    private CrossRef_AniDB_TMDB_Episode TryFindAnidbAndTmdbMatch(SVR_AniDB_Anime anime, SVR_AniDB_Episode anidbEpisode, IReadOnlyList<TMDB_Episode> tmdbEpisodes, bool isSpecial)
    {
        // Skip matching if we try to match a music video or complete movie.
        var anidbTitle = _anidbEpisodeTitles.GetByEpisodeIDAndLanguage(anidbEpisode.EpisodeID, TitleLanguage.English)
            .Where(title => !title.Title.Trim().Equals($"Episode {anidbEpisode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))
            .FirstOrDefault()?.Title;
        var titlesToNotSearch = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "Complete Movie", "Music Video" };
        if (!string.IsNullOrEmpty(anidbTitle) && titlesToNotSearch.Any(title => anidbTitle.Contains(title, StringComparison.InvariantCultureIgnoreCase)))
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.SarahJessicaParker);

        // Fix up the title for the first/single episode of a few anime types.
        var titlesToSearch = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "OAD", "OVA", "Short Movie", "Special", "TV Special", "Web" };
        if (!string.IsNullOrEmpty(anidbTitle) && titlesToSearch.Contains(anidbTitle))
        {
            var englishAnimeTitle = anime.Titles.FirstOrDefault(title => title.TitleType == TitleType.Official && title.Language == TitleLanguage.English)?.Title;
            if (englishAnimeTitle is not null)
            {
                var i = englishAnimeTitle.IndexOf(':');
                anidbTitle = i > 0 && i < englishAnimeTitle.Length - 1 ? englishAnimeTitle[(i + 1)..].TrimStart() : englishAnimeTitle;
            }
        }

        var anidbDate = anidbEpisode.GetAirDateAsDateOnly();
        if (anidbDate is not null && anidbDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
        {
            _logger.LogTrace("Skipping future episode {EpisodeID}", anidbEpisode.EpisodeID);
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.SarahJessicaParker, 0);
        }

        var airdateProbability = tmdbEpisodes
            .Select(episode => new { episode, probability = CalculateAirDateProbability(anidbDate, episode.AiredAt) })
            .Where(result => result.probability != 0)
            .OrderByDescending(result => result.probability)
            .ThenBy(result => result.episode.SeasonNumber == 0)
            .ThenBy(result => result.episode.SeasonNumber)
            .ThenBy(result => result.episode.EpisodeNumber)
            .ToList();
        var titleSearchResults = !string.IsNullOrEmpty(anidbTitle) ? tmdbEpisodes
            .Search(anidbTitle, episode => [ReplaceTitle(episode.EnglishTitle)], true)
            .OrderBy(result => result)
            .ToList() : [];

        // Exact match first.
        if (titleSearchResults.Count > 0 && titleSearchResults[0] is { } exactTitleMatch && exactTitleMatch.ExactMatch && exactTitleMatch.LengthDifference < 3)
        {
            var tmdbEpisode = exactTitleMatch.Result;
            var dateMatches = airdateProbability.Any(result => result.episode == tmdbEpisode);
            var rating = dateMatches ? MatchRating.DateAndTitleMatches : MatchRating.TitleMatches;
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
        }

        // Almost exact match second.
        if (titleSearchResults.Count > 0 && titleSearchResults[0] is { } kindaTitleMatch && kindaTitleMatch.Distance < 0.2D && kindaTitleMatch.LengthDifference < 6)
        {
            var tmdbEpisode = kindaTitleMatch.Result;
            var dateMatches = airdateProbability.Any(result => result.episode == tmdbEpisode);
            var rating = dateMatches ? MatchRating.DateAndTitleKindaMatches : MatchRating.TitleKindaMatches;
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
        }

        // Followed by checking the air date.
        if (airdateProbability.Count > 0)
        {
            var tmdbEpisode = airdateProbability.FirstOrDefault(r => titleSearchResults.Any(result => result.Result == r.episode))?.episode;
            var rating = tmdbEpisode is null ? MatchRating.DateMatches : MatchRating.DateAndTitleKindaMatches;
            tmdbEpisode ??= airdateProbability[0].episode;
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
        }

        // Followed by _any_ title match.
        if (titleSearchResults.Count > 0)
        {
            var tmdbEpisode = titleSearchResults[0]!.Result;
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, MatchRating.TitleKindaMatches);
        }

        // And finally, just pick the first available episode if it's not a special.
        if (!isSpecial && tmdbEpisodes.Count > 0)
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisodes[0].TmdbEpisodeID, tmdbEpisodes[0].TmdbShowID, MatchRating.FirstAvailable);

        // And if all above failed, then return an empty link.
        return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.SarahJessicaParker);
    }

    private static double CalculateAirDateProbability(DateOnly? firstDate, DateOnly? secondDate, int maxDifferenceInDays = 2)
    {
        if (!firstDate.HasValue || !secondDate.HasValue)
            return 0;

        var difference = Math.Abs(secondDate.Value.DayNumber - firstDate.Value.DayNumber);
        if (difference == 0)
            return 1;

        if (difference <= maxDifferenceInDays)
            return (maxDifferenceInDays - difference) / (double)maxDifferenceInDays;

        return 0;
    }

    private static string ReplaceTitle(string title) =>
        _characterReplacementDict.Aggregate(title, (current, kv) => current.Replace(kv.Key, kv.Value));

    #endregion
}
