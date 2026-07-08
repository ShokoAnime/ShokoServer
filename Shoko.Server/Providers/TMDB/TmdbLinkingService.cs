using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

public class TmdbLinkingService : ITmdbLinkingService
{
    private static readonly Dictionary<char, char> _characterReplacementDict = new()
    {
        { '’', '\'' },
        { '”', '"' },
        { '‘', '\'' },
        { '“', '"' },
    };

    private static readonly HashSet<string> _titlesToSearch = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "OAD",
        "OVA",
        "Short Movie",
        "Special",
        "TV Special",
        "Web"
    };

    private readonly ILogger<TmdbLinkingService> _logger;

    private readonly IQueueScheduler _scheduler;

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
        IQueueScheduler schedulerFactory,
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
        _scheduler = schedulerFactory;
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
            seriesItem.IsTmdbAutoMatchingDisabled = disabled;
            _animeSeries.Save(seriesItem, false, false);

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

    public async Task AddMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool additiveLink = false, MatchRating matchRating = MatchRating.UserVerified)
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
        _logger.LogInformation("Adding TMDB Movie Link: AniDB episode (EpisodeID={EpisodeID}, AnimeID={AnimeID}) → TMDB movie (MovieID={TmdbID})", anidbEpisodeId, episode.AnimeID, tmdbMovieId);
        var xref = _xrefAnidbTmdbMovies.GetByAnidbEpisodeAndTmdbMovieIDs(anidbEpisodeId, tmdbMovieId) ?? new(anidbEpisodeId, episode.AnimeID, tmdbMovieId);
        xref.AnidbAnimeID = episode.AnimeID;
        xref.MatchRating = matchRating;
        _xrefAnidbTmdbMovies.Save(xref);
    }

    public async Task RemoveMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool purge = false)
    {
        var xref = _xrefAnidbTmdbMovies.GetByAnidbEpisodeAndTmdbMovieIDs(anidbEpisodeId, tmdbMovieId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        if (_anidbEpisodes.GetByEpisodeID(anidbEpisodeId) is { } anidbEpisode && _animeSeries.GetByAnimeID(anidbEpisode.AnimeID) is { } series && !series.IsTmdbAutoMatchingDisabled)
        {
            series.IsTmdbAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, false);
        }

        await RemoveMovieLink(xref, purge);
    }

    public async Task RemoveAllMovieLinksForAnime(int anidbAnimeId, bool purge = false)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByAnidbAnimeID(anidbAnimeId);
        _logger.LogInformation("Removing {Count} TMDB movie links for AniDB anime. (AnimeID={AnimeID})", xrefs.Count, anidbAnimeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        if (_animeSeries.GetByAnimeID(anidbAnimeId) is { } series && !series.IsTmdbAutoMatchingDisabled)
        {
            series.IsTmdbAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, false);
        }

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, purge);
    }

    public async Task RemoveAllMovieLinksForEpisode(int anidbEpisodeId, bool purge = false)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByAnidbEpisodeID(anidbEpisodeId);
        _logger.LogInformation("Removing {Count} TMDB movie links for AniDB episode. (EpisodeID={EpisodeID})", xrefs.Count, anidbEpisodeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        if (_anidbEpisodes.GetByEpisodeID(anidbEpisodeId) is { } anidbEpisode && _animeSeries.GetByAnimeID(anidbEpisode.AnimeID) is { } series && !series.IsTmdbAutoMatchingDisabled)
        {
            series.IsTmdbAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, false);
        }

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, purge);
    }

    public async Task RemoveAllMovieLinksForMovie(int tmdbMovieId)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByTmdbMovieID(tmdbMovieId);
        _logger.LogInformation("Removing {Count} TMDB movie links for TMDB movie. (MovieID={MovieID})", xrefs.Count, tmdbMovieId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, false);
    }

    private async Task RemoveMovieLink(CrossRef_AniDB_TMDB_Movie xref, bool purge = false)
    {
        _logger.LogInformation("Removing TMDB movie link: AniDB episode (EpisodeID={EpisodeID}, AnimeID={AnimeID}) → TMDB movie (ID:{TmdbID})", xref.AnidbEpisodeID, xref.AnidbAnimeID, xref.TmdbMovieID);
        _xrefAnidbTmdbMovies.Delete(xref);

        if (purge)
            await _scheduler.StartJob<PurgeTmdbMovieJob>(c =>
            {
                c.TmdbMovieID = xref.TmdbMovieID;
            });
    }

    #endregion

    #region Show Links

    public async Task AddShowLink(int anidbAnimeId, int tmdbShowId, bool additiveLink = true, MatchRating matchRating = MatchRating.UserVerified)
    {
        // Remove all existing links.
        if (!additiveLink)
            await RemoveAllShowLinksForAnime(anidbAnimeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB show link: AniDB (AnimeID={AnidbID}) → TMDB Show (ID={TmdbID})", anidbAnimeId, tmdbShowId);
        var xref = _xrefAnidbTmdbShows.GetByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId) ??
            new(anidbAnimeId, tmdbShowId);
        xref.MatchRating = matchRating;
        _xrefAnidbTmdbShows.Save(xref);
        await Task.Run(() => MatchAnidbToTmdbEpisodes(anidbAnimeId, tmdbShowId, null, true, true));
    }

    public async Task RemoveShowLink(int anidbAnimeId, int tmdbShowId, bool purge = false)
    {
        var xref = _xrefAnidbTmdbShows.GetByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = _animeSeries.GetByAnimeID(anidbAnimeId);
        if (series != null && !series.IsTmdbAutoMatchingDisabled)
        {
            series.IsTmdbAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, false);
        }

        await RemoveShowLink(xref, purge);
    }

    public async Task RemoveAllShowLinksForAnime(int animeId, bool purge = false)
    {
        var xrefs = _xrefAnidbTmdbShows.GetByAnidbAnimeID(animeId);
        _logger.LogInformation("Removing {Count} TMDB show links for AniDB anime. (AnimeID={AnimeID})", xrefs.Count, animeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = _animeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTmdbAutoMatchingDisabled)
        {
            series.IsTmdbAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, false);
        }

        foreach (var xref in xrefs)
            await RemoveShowLink(xref, purge);
    }

    public async Task RemoveAllShowLinksForShow(int showId)
    {
        var xrefs = _xrefAnidbTmdbShows.GetByTmdbShowID(showId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveShowLink(xref, false);
    }

    private async Task RemoveShowLink(CrossRef_AniDB_TMDB_Show xref, bool purge = false)
    {
        _logger.LogInformation("Removing TMDB show link: AniDB anime (AnimeID={AnidbID}) → TMDB show (ID={TmdbID})", xref.AnidbAnimeID, xref.TmdbShowID);
        _xrefAnidbTmdbShows.Delete(xref);

        var xrefs = _xrefAnidbTmdbEpisodes.GetOnlyByAnidbAnimeAndTmdbShowIDs(xref.AnidbAnimeID, xref.TmdbShowID).ToList();
        // When removing the last show link, also remove floating episode xrefs (TmdbShowID=0)
        // created by ResetAllEpisodeLinks. Only do this when no links remain so we don't
        // accidentally delete xrefs that still belong to surviving show links.
        if (_xrefAnidbTmdbShows.GetByAnidbAnimeID(xref.AnidbAnimeID).Count == 0)
            xrefs.AddRange(_xrefAnidbTmdbEpisodes.GetOnlyByAnidbAnimeAndTmdbShowIDs(xref.AnidbAnimeID, 0));
        _logger.LogInformation("Removing {XRefsCount} episodes cross-references for AniDB anime (AnimeID={AnidbID}) and TMDB show (ID={TmdbID})", xrefs.Count, xref.AnidbAnimeID, xref.TmdbShowID);
        _xrefAnidbTmdbEpisodes.Delete(xrefs);
        if (purge)
            await _scheduler.StartJob<PurgeTmdbShowJob>(c =>
            {
                c.TmdbShowID = xref.TmdbShowID;
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
                    xref.MatchRating = allowAuto ? MatchRating.None : MatchRating.UserVerified;
                    toSave.Add(xref);
                }
                else
                {
                    toDelete.Add(xref);
                }
            }

            // Add missing xrefs.
            var anidbEpisodesWithoutXrefs = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
                .Where(episode => !existingIDs.Contains(episode.AniDB_EpisodeID) && episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special)
                .ToList();
            foreach (var anidbEpisode in anidbEpisodesWithoutXrefs)
                toSave.Add(new(anidbEpisode.AniDB_EpisodeID, anidbAnimeId, 0, 0, allowAuto ? MatchRating.None : MatchRating.UserVerified));

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
                _anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber - 1).FirstOrDefault() is { } previousEpisode)
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

    public IReadOnlyList<ITmdbEpisodeCrossReference> MatchAnidbToTmdbEpisodes(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId, bool useExisting = false, bool saveToDatabase = false, bool? useExistingOtherShows = null)
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
        var isOVA = anime.AnimeType is AnimeType.OVA;
        var toSkip = new HashSet<int>();
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode>();
        var crossReferences = new List<CrossRef_AniDB_TMDB_Episode>();
        var secondPass = new List<AniDB_Episode>();
        var fourthPass = new List<AniDB_Episode>();
        var thirdPass = new List<AniDB_Episode>();
        var existing = _xrefAnidbTmdbEpisodes.GetAllByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId)
            .GroupBy(xref => xref.AnidbEpisodeID)
            .ToDictionary(grouped => grouped.Key, grouped => grouped.ToList());
        var anidbEpisodes = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
            .Where(episode => episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeType)
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
                _logger.LogTrace("Skipping existing episode link: AniDB episode (EpisodeID={EpisodeID}, AnimeID={AnimeID}) → TMDB episode (EpisodeID={TmdbID})", link.AnidbEpisodeID, link.AnidbAnimeID, link.TmdbEpisodeID);

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
        bool IsSpecialEpisode(AniDB_Episode ep) => ep.EpisodeType is EpisodeType.Special || anime.AnimeType is not AnimeType.TV and not AnimeType.Web;

        List<TMDB_Episode> GetEpisodeList(AniDB_Episode ep) => IsSpecialEpisode(ep) ? tmdbSpecialEpisodes : tmdbNormalEpisodes;

        // Ranks episodes by their best candidate match without consuming it, so passes can process the
        // strongest matches first and let weaker duplicate claims fall back to their next-best candidate
        // instead of losing a shared TMDB episode purely by AniDB episode order. The match found here is
        // cached and handed back to the caller so the loop that actually consumes candidates doesn't have
        // to run the same title-search/air-date computation a second time for every episode.
        List<(AniDB_Episode Episode, CrossRef_AniDB_TMDB_Episode CrossRef)> RankByConfidence(IEnumerable<AniDB_Episode> episodes) =>
            episodes
                .Select(ep =>
                {
                    var crossRef = TryFindAnidbAndTmdbMatch(anime, ep, GetEpisodeList(ep), IsSpecialEpisode(ep) && !isOVA, show.OriginalLanguageCode, out var confidence);
                    return (Episode: ep, CrossRef: crossRef, Confidence: confidence);
                })
                .OrderByDescending(ranked => ranked.Confidence)
                .Select(ranked => (ranked.Episode, ranked.CrossRef))
                .ToList();

        // Re-finds a match for an episode, reusing the cached match from RankByConfidence when its chosen
        // TMDB candidate hasn't since been claimed by an earlier (stronger) episode in the same pass —
        // in that case re-running the deterministic match against the unchanged remaining pool is
        // guaranteed to produce the same result, so the recompute is skipped.
        CrossRef_AniDB_TMDB_Episode ResolveRankedMatch(AniDB_Episode episode, CrossRef_AniDB_TMDB_Episode cached, List<TMDB_Episode> episodeList) =>
            cached.TmdbEpisodeID != 0 && episodeList.Any(candidate => candidate.TmdbEpisodeID == cached.TmdbEpisodeID)
                ? cached
                : TryFindAnidbAndTmdbMatch(anime, episode, episodeList, IsSpecialEpisode(episode) && !isOVA, show.OriginalLanguageCode);

        // Narrows the TMDB candidate pool to seasons already established by an existing/new OV/DT
        // link, so later passes can't stray into an unrelated season once one has been established.
        void FilterToCurrentSeasons(int passNumber)
        {
            var currentSessions = crossReferences
                .Select(NormalEpisodeSeasonNumberSelector)
                .Except([-1])
                .ToHashSet();
            if (currentSessions.Count == 0)
                return;

            if (!isOVA)
                currentSessions.Add(0);
            _logger.LogTrace("Filtering available episodes by currently in use seasons. (Current Sessions: {CurrentSessions}, Pass: {PassNumber}/4)", string.Join(", ", currentSessions), passNumber);
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

        // Runs a deferred pass: episodes whose match satisfies `accepts` are linked and removed from
        // the shared candidate pool; the rest are queued into `overflow` for the next pass.
        void RunDeferredPass(int passNumber, string passLabel, List<AniDB_Episode> episodes, Func<MatchRating, bool> accepts, List<AniDB_Episode> overflow)
        {
            FilterToCurrentSeasons(passNumber);

            var passCurrent = 0;
            foreach (var (episode, cachedCrossRef) in RankByConfidence(episodes))
            {
                passCurrent++;
                _logger.LogTrace("Linking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {EpisodeID}, Progress: {Current}/{Total}, Pass: {PassNumber}/4)", episode.EpisodeType, episode.EpisodeNumber, episode.EpisodeID, passCurrent, episodes.Count, passNumber);
                var episodeList = GetEpisodeList(episode);
                var crossRef = ResolveRankedMatch(episode, cachedCrossRef, episodeList);
                if (accepts(crossRef.MatchRating))
                {
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);

                    crossReferences.Add(crossRef);
                    toAdd.Add(crossRef);
                    _logger.LogTrace("Adding new link for episode. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: {PassNumber}/4)", episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating, passNumber);
                }
                else
                {
                    _logger.LogTrace("Skipping episode in the {PassLabel} pass. (AniDB ID: {AnidbEpisodeID}, TMDB ID: {TMDbEpisodeID}, Rating: {MatchRating}, Pass: {PassNumber}/4)", passLabel, episode.EpisodeID, crossRef.TmdbEpisodeID, crossRef.MatchRating, passNumber);
                    overflow.Add(episode);
                }
            }
        }

        var current = 0;
        foreach (var (episode, cachedCrossRef) in RankByConfidence(anidbEpisodes.Values))
        {
            current++;
            _logger.LogTrace("Checking episode {EpisodeType} {EpisodeNumber}. (AniDB ID: {AnidbEpisodeID}, Progress: {Current}/{Total}, Pass: 1/4)", episode.EpisodeType, episode.EpisodeNumber, episode.EpisodeID, current, anidbEpisodes.Count);
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
                        crossReferences.Add(new(episode.EpisodeID, anidbAnimeId, 0, 0, MatchRating.None, 0));
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
                    crossReferences.Add(new(episode.EpisodeID, anidbAnimeId, 0, 0, MatchRating.None, 0));
                    continue;
                }

                // Else try find a match.
                _logger.LogTrace("Linking episode. (AniDB ID: {AnidbEpisodeID}, Pass: 1/4)", episode.EpisodeID);
                var episodeList = GetEpisodeList(episode);
                var crossRef = ResolveRankedMatch(episode, cachedCrossRef, episodeList);
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
            RunDeferredPass(2, "second", secondPass, rating => rating is MatchRating.TitleMatches, thirdPass);

        // Run a third pass on the episodes that weren't OV, DT or T links in the first pass.
        if (thirdPass.Count > 0)
            RunDeferredPass(3, "third", thirdPass, rating => rating is not MatchRating.FirstAvailable and not MatchRating.None, fourthPass);

        // Run a fourth pass on the remaining episodes. Every match is accepted here (including a
        // MatchRating.None miss), so nothing ever overflows past this pass.
        if (fourthPass.Count > 0)
            RunDeferredPass(4, "fourth", fourthPass, _ => true, []);

        ReconcileEpisodeOrderInversions(anidbEpisodes, tmdbEpisodeDict, toAdd);

        if (!saveToDatabase)
        {
            _logger.LogDebug(
                "Found {a} anidb/tmdb episode links for show {ShowTitle} in {Delta}. (Anime={AnimeId}, Show={ShowId})",
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
            "Added/removed/skipped {a}/{r}/{s} anidb/tmdb episode cross-references for show {ShowTitle} in {Delta} (Anime={AnimeId}, Show={ShowId})",
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

        int NormalEpisodeSeasonNumberSelector(CrossRef_AniDB_TMDB_Episode xref) => xref.TmdbEpisodeID is not 0 && (isOVA || anidbEpisodes[xref.AnidbEpisodeID].EpisodeType is EpisodeType.Episode) && tmdbEpisodeDict.TryGetValue(xref.TmdbEpisodeID, out var tmdbEpisode) ? tmdbEpisode.SeasonNumber : -1;
    }

    private CrossRef_AniDB_TMDB_Episode TryFindAnidbAndTmdbMatch(AniDB_Anime anime, AniDB_Episode anidbEpisode, IReadOnlyList<TMDB_Episode> tmdbEpisodes, bool isSpecial, string originalLanguageCode) =>
        TryFindAnidbAndTmdbMatch(anime, anidbEpisode, tmdbEpisodes, isSpecial, originalLanguageCode, out _);

    // The out confidence score lets callers rank multiple AniDB episodes contending for the same
    // TMDB episode within a pass, so the strongest match claims it instead of whichever AniDB
    // episode happened to be processed first.
    private CrossRef_AniDB_TMDB_Episode TryFindAnidbAndTmdbMatch(AniDB_Anime anime, AniDB_Episode anidbEpisode, IReadOnlyList<TMDB_Episode> tmdbEpisodes, bool isSpecial, string originalLanguageCode, out double confidence)
    {
        confidence = 0;

        var anidbTitle = ResolveAnidbSearchTitle(anime, anidbEpisode, out var isExcludedTitle);
        if (isExcludedTitle)
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.None);

        var anidbDate = anidbEpisode.GetAirDateAsDate()?.ToDateOnly();
        if (anidbDate is not null && anidbDate > DateTime.UtcNow.AddDays(1).ToDateOnly())
        {
            _logger.LogTrace("Skipping future episode {EpisodeID}", anidbEpisode.EpisodeID);
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.None, 0);
        }

        var airdateProbability = tmdbEpisodes
            .Select(episode => (episode, probability: CalculateAirDateProbability(anidbDate, episode.AiredAt)))
            .Where(result => result.probability != 0)
            .OrderByDescending(result => result.probability)
            .ThenBy(result => result.episode.SeasonNumber == 0)
            .ThenBy(result => result.episode.SeasonNumber)
            .ThenBy(result => result.episode.EpisodeNumber)
            .ToList();
        var titleSearchResults = !string.IsNullOrEmpty(anidbTitle) ? tmdbEpisodes
            .Search(anidbTitle, episode => GetEpisodeTitleCandidates(episode, originalLanguageCode), true)
            .OrderBy(result => result)
            .ToList() : [];

        var crossRef = SelectBestEpisodeMatch(anidbEpisode, tmdbEpisodes, isSpecial, titleSearchResults, airdateProbability, out confidence);
        return crossRef;
    }

    // Resolves the AniDB title to search TMDB with, falling back to the anime's main-title language
    // for non-English content, and fixing it up for the first/single episode of a few anime types.
    // isExcludedTitle is set when the episode is a bare "Complete Movie"/"Music Video" placeholder
    // that should never be searched for.
    private string? ResolveAnidbSearchTitle(AniDB_Anime anime, AniDB_Episode anidbEpisode, out bool isExcludedTitle)
    {
        isExcludedTitle = false;

        var mainTitle = anime.Titles.FirstOrDefault(t => t.TitleType == TitleType.Main);
        var fallbackLanguage = mainTitle?.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle?.Language ?? TitleLanguage.English,
        };
        var anidbTitle = _anidbEpisodeTitles.GetByEpisodeIDAndLanguage(anidbEpisode.EpisodeID, TitleLanguage.English)
            .FirstOrDefault(title => !IsGenericEpisodeTitle(title.Title, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber))?.Title
            ?? (fallbackLanguage != TitleLanguage.English
                ? _anidbEpisodeTitles.GetByEpisodeIDAndLanguage(anidbEpisode.EpisodeID, fallbackLanguage)
                    .FirstOrDefault(title => !IsGenericEpisodeTitle(title.Title, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber))?.Title
                : null);
        if (string.IsNullOrEmpty(anidbTitle))
            return anidbTitle;

        var titlesToNotSearch = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { "Complete Movie", "Music Video" };
        if (titlesToNotSearch.Any(title => anidbTitle.Contains(title, StringComparison.InvariantCultureIgnoreCase)))
        {
            isExcludedTitle = true;
            return null;
        }

        // Fix up the title for the first/single episode of a few anime types.
        if (_titlesToSearch.Contains(anidbTitle) && anime.Titles.FirstOrDefault(title => title.TitleType == TitleType.Official && title.Language == TitleLanguage.English)?.Title is { } englishAnimeTitle)
        {
            var i = englishAnimeTitle.IndexOf(':');
            anidbTitle = i > 0 && i < englishAnimeTitle.Length - 1 ? englishAnimeTitle[(i + 1)..].TrimStart() : englishAnimeTitle;
        }

        return anidbTitle;
    }

    // Picks the strongest candidate out of the title/air-date evidence gathered by the caller, tiered
    // from exact title+date corroboration down to a positional fallback.
    private static CrossRef_AniDB_TMDB_Episode SelectBestEpisodeMatch(
        AniDB_Episode anidbEpisode,
        IReadOnlyList<TMDB_Episode> tmdbEpisodes,
        bool isSpecial,
        List<SeriesSearch.SearchResult<TMDB_Episode>> titleSearchResults,
        List<(TMDB_Episode episode, double probability)> airdateProbability,
        out double confidence)
    {
        if (TryExactTitleMatch(anidbEpisode, titleSearchResults, airdateProbability, out var crossRef, out confidence))
            return crossRef;

        if (TryKindaTitleMatch(anidbEpisode, titleSearchResults, airdateProbability, out crossRef, out confidence))
            return crossRef;

        if (TryAirDateMatch(anidbEpisode, titleSearchResults, airdateProbability, out crossRef, out confidence))
            return crossRef;

        if (TryAnyTitleMatch(anidbEpisode, titleSearchResults, out crossRef, out confidence))
            return crossRef;

        confidence = 0;

        // And finally, just pick the first available episode if it's not a special.
        if (!isSpecial && tmdbEpisodes.Count > 0)
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisodes[0].TmdbEpisodeID, tmdbEpisodes[0].TmdbShowID, MatchRating.FirstAvailable);

        // And if all above failed, then return an empty link.
        return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, 0, 0, MatchRating.None);
    }

    private static bool TryExactTitleMatch(
        AniDB_Episode anidbEpisode,
        List<SeriesSearch.SearchResult<TMDB_Episode>> titleSearchResults,
        List<(TMDB_Episode episode, double probability)> airdateProbability,
        out CrossRef_AniDB_TMDB_Episode crossRef,
        out double confidence) =>
        TryTitleMatch(anidbEpisode, titleSearchResults, airdateProbability,
            isCandidate: result => result.ExactMatch && result.LengthDifference < 3,
            (MatchRating.TitleMatches, MatchRating.DateAndTitleMatches),
            out crossRef, out confidence);

    private static bool TryKindaTitleMatch(
        AniDB_Episode anidbEpisode,
        List<SeriesSearch.SearchResult<TMDB_Episode>> titleSearchResults,
        List<(TMDB_Episode episode, double probability)> airdateProbability,
        out CrossRef_AniDB_TMDB_Episode crossRef,
        out double confidence) =>
        TryTitleMatch(anidbEpisode, titleSearchResults, airdateProbability,
            isCandidate: result => result.Distance < 0.2D && result.LengthDifference < 6,
            (MatchRating.TitleKindaMatches, MatchRating.DateAndTitleKindaMatches),
            out crossRef, out confidence);

    // Shared shape for TryExactTitleMatch/TryKindaTitleMatch: both take the best title-search result if it
    // passes a threshold, upgrade the rating when the air date also corroborates it, and score confidence
    // the same way — they only differ in the threshold and which (title-only, date-and-title) rating pair applies.
    private static bool TryTitleMatch(
        AniDB_Episode anidbEpisode,
        List<SeriesSearch.SearchResult<TMDB_Episode>> titleSearchResults,
        List<(TMDB_Episode episode, double probability)> airdateProbability,
        Func<SeriesSearch.SearchResult<TMDB_Episode>, bool> isCandidate,
        (MatchRating TitleOnly, MatchRating DateAndTitle) ratings,
        out CrossRef_AniDB_TMDB_Episode crossRef,
        out double confidence)
    {
        crossRef = null!;
        confidence = 0;
        if (titleSearchResults.Count == 0 || titleSearchResults[0] is not { } titleMatch || !isCandidate(titleMatch))
            return false;

        var tmdbEpisode = titleMatch.Result;
        var dateMatches = airdateProbability.Any(result => result.episode == tmdbEpisode);
        var rating = dateMatches ? ratings.DateAndTitle : ratings.TitleOnly;
        confidence = (dateMatches ? 1 : 0) + (1 - titleMatch.Distance);
        crossRef = new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
        return true;
    }

    private static bool TryAirDateMatch(
        AniDB_Episode anidbEpisode,
        List<SeriesSearch.SearchResult<TMDB_Episode>> titleSearchResults,
        List<(TMDB_Episode episode, double probability)> airdateProbability,
        out CrossRef_AniDB_TMDB_Episode crossRef,
        out double confidence)
    {
        crossRef = null!;
        confidence = 0;
        if (airdateProbability.Count == 0)
            return false;

        var matched = airdateProbability.FirstOrDefault(r => titleSearchResults.Any(result => result.Result == r.episode));
        var tmdbEpisode = matched.episode ?? airdateProbability[0].episode;
        var rating = matched.episode is null ? MatchRating.DateMatches : MatchRating.DateAndTitleKindaMatches;
        confidence = matched.episode is null ? airdateProbability[0].probability : matched.probability;
        crossRef = new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
        return true;
    }

    private static bool TryAnyTitleMatch(
        AniDB_Episode anidbEpisode,
        List<SeriesSearch.SearchResult<TMDB_Episode>> titleSearchResults,
        out CrossRef_AniDB_TMDB_Episode crossRef,
        out double confidence)
    {
        crossRef = null!;
        confidence = 0;
        if (titleSearchResults.Count == 0)
            return false;

        var tmdbEpisode = titleSearchResults[0]!.Result;
        confidence = 1 - titleSearchResults[0]!.Distance;
        crossRef = new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, MatchRating.TitleKindaMatches);
        return true;
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

    private static IReadOnlyList<string> GetEpisodeTitleCandidates(TMDB_Episode episode, string originalLanguageCode) =>
        episode.GetAllTitles()
            .Where(t => (t.LanguageCode == "en" && t.CountryCode == "US") || t.LanguageCode == originalLanguageCode)
            .Where(t => !t.Value.Trim().Equals($"Episode {episode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))
            .Select(t => ReplaceTitle(t.Value))
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();

    private static string ReplaceTitle(string title) =>
        _characterReplacementDict.Aggregate(title, (current, kv) => current.Replace(kv.Key, kv.Value));

    private static bool IsGenericEpisodeTitle(string title, EpisodeType episodeType, int episodeNumber) =>
        title.Trim().Equals($"Episode {episodeType.Prefix}{episodeNumber}", StringComparison.InvariantCultureIgnoreCase);

    // Ratings assigned with zero title evidence — the only ones a coincidental air-date mismatch can put out of order.
    private static readonly HashSet<MatchRating> _weakOrderRatings = [MatchRating.DateMatches, MatchRating.FirstAvailable];

    // Swaps adjacent weak TMDB matches into AniDB order; strong matches stay in the ordering (true adjacency) but are never swapped.
    internal static void ReconcileEpisodeOrderInversions(
        IReadOnlyDictionary<int, AniDB_Episode> anidbEpisodes,
        IReadOnlyDictionary<int, TMDB_Episode> tmdbEpisodeDict,
        IReadOnlyList<CrossRef_AniDB_TMDB_Episode> toAdd)
    {
        var groups = toAdd
            .Where(xref => xref.TmdbEpisodeID != 0 && anidbEpisodes.ContainsKey(xref.AnidbEpisodeID))
            .GroupBy(xref => anidbEpisodes[xref.AnidbEpisodeID].EpisodeType);

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(xref => anidbEpisodes[xref.AnidbEpisodeID].EpisodeNumber).ToList();

            // Repeat until a full pass makes no swaps, to fully untangle runs of 3+ reversed weak matches.
            while (BubbleSwapPass(ordered, tmdbEpisodeDict))
            {
                // Intentionally empty: BubbleSwapPass has already applied this pass's swaps in place.
            }
        }
    }

    // A single left-to-right adjacent-swap pass over the ordered group; returns true if anything moved.
    private static bool BubbleSwapPass(List<CrossRef_AniDB_TMDB_Episode> ordered, IReadOnlyDictionary<int, TMDB_Episode> tmdbEpisodeDict)
    {
        var swapped = false;
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var a = ordered[i];
            var b = ordered[i + 1];
            if (!ShouldSwap(a, b, tmdbEpisodeDict))
                continue;

            (a.TmdbEpisodeID, b.TmdbEpisodeID) = (b.TmdbEpisodeID, a.TmdbEpisodeID);
            (a.TmdbShowID, b.TmdbShowID) = (b.TmdbShowID, a.TmdbShowID);
            (a.MatchRating, b.MatchRating) = (b.MatchRating, a.MatchRating);
            swapped = true;
        }

        return swapped;
    }

    private static bool ShouldSwap(CrossRef_AniDB_TMDB_Episode a, CrossRef_AniDB_TMDB_Episode b, IReadOnlyDictionary<int, TMDB_Episode> tmdbEpisodeDict)
    {
        if (!_weakOrderRatings.Contains(a.MatchRating) || !_weakOrderRatings.Contains(b.MatchRating))
            return false;
        if (!tmdbEpisodeDict.TryGetValue(a.TmdbEpisodeID, out var tmdbA) || !tmdbEpisodeDict.TryGetValue(b.TmdbEpisodeID, out var tmdbB))
            return false;

        return (tmdbA.SeasonNumber, tmdbA.EpisodeNumber).CompareTo((tmdbB.SeasonNumber, tmdbB.EpisodeNumber)) > 0;
    }

    #endregion
}
