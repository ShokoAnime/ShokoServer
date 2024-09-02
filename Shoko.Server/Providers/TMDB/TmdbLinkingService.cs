
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

using EpisodeType = Shoko.Models.Enums.EpisodeType;

// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public class TmdbLinkingService
{
    private readonly ILogger<TmdbLinkingService> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

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
            _animeSeries.Save(series, false, true, true);
        }

        await RemoveMovieLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllMovieLinksForAnime(int anidbAnimeId, bool purge = false, bool removeImageFiles = true)
    {
        var xrefs = _xrefAnidbTmdbMovies.GetByAnidbAnimeID(anidbAnimeId);
        _logger.LogInformation("Removing {Count} TMDB movie links for AniDB anime. (AnimeID={AnimeID})", xrefs.Count, anidbAnimeId);

        // Disable auto-matching when we remove an existing match for the series.
        if (_animeSeries.GetByAnimeID(anidbAnimeId) is { } series && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, true);
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
            _animeSeries.Save(series, false, true, true);
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

    public async Task AddShowLink(int animeId, int showId, bool additiveLink = true, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            await RemoveAllShowLinksForAnime(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB show link: AniDB (AnimeID={AnidbID}) → TMDB Show (ID={TmdbID})", animeId, showId);
        var xref = _xrefAnidbTmdbShows.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId) ??
            new(animeId, showId);
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        _xrefAnidbTmdbShows.Save(xref);
    }

    public async Task RemoveShowLink(int animeId, int showId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = _xrefAnidbTmdbShows.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = _animeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, true);
        }

        await RemoveShowLink(xref, removeImageFiles, purge);
    }

    public async Task RemoveAllShowLinksForAnime(int animeId, bool purge = false, bool removeImageFiles = true)
    {
        _logger.LogInformation("Removing All TMDB show links for AniDB anime. (AnimeID={AnimeID})", animeId);
        var xrefs = _xrefAnidbTmdbShows.GetByAnidbAnimeID(animeId);
        if (xrefs == null || xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = _animeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            _animeSeries.Save(series, false, true, true);
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

        var xrefs = _xrefAnidbTmdbEpisodes.GetOnlyByAnidbAnimeAndTmdbShowIDs(xref.AnidbAnimeID, xref.TmdbShowID);
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

    public void ResetAllEpisodeLinks(int anidbAnimeId)
    {
        var showId = _xrefAnidbTmdbShows.GetByAnidbAnimeID(anidbAnimeId)
            .FirstOrDefault()?.TmdbShowID;
        if (showId.HasValue)
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
                    xref.TmdbShowID = showId.Value;
                    xref.TmdbEpisodeID = 0;
                    xref.Ordering = 0;
                    xref.MatchRating = MatchRating.SarahJessicaParker;
                    toSave.Add(xref);
                }
                else
                {
                    toDelete.Add(xref);
                }
            }

            // Add missing xrefs.
            var anidbEpisodesWithoutXrefs = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
                .Where(episode => !existingIDs.Contains(episode.AniDB_EpisodeID) && episode.EpisodeType is (int)EpisodeType.Episode or (int)EpisodeType.Special);
            foreach (var anidbEpisode in anidbEpisodesWithoutXrefs)
                toSave.Add(new(anidbEpisode.AniDB_EpisodeID, anidbAnimeId, 0, showId.Value, MatchRating.UserVerified));

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
            toSave.MatchRating = MatchRating.SarahJessicaParker;
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
            var existingAnidbLinks = _xrefAnidbTmdbEpisodes.GetByAnidbEpisodeID(anidbEpisodeId).Count;
            var existingTmdbLinks = _xrefAnidbTmdbEpisodes.GetByTmdbEpisodeID(tmdbEpisodeId).Count;
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
            toSave.Ordering = 0;
            toSave.MatchRating = MatchRating.UserVerified;
            var toDelete = xrefs.Skip(1).ToList();
            _xrefAnidbTmdbEpisodes.Save(toSave);
            _xrefAnidbTmdbEpisodes.Delete(toDelete);
        }

        return true;
    }

    #endregion

    #region Episode Links

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> MatchAnidbToTmdbEpisodes(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId, bool useExisting = false, bool saveToDatabase = false)
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
        var toSkip = new HashSet<int>();
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode>();
        var crossReferences = new List<CrossRef_AniDB_TMDB_Episode>();
        var existing = _xrefAnidbTmdbEpisodes.GetAllByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId)
            .GroupBy(xref => xref.AnidbEpisodeID)
            .ToDictionary(grouped => grouped.Key, grouped => grouped.ToList());
        var anidbEpisodes = _anidbEpisodes.GetByAnimeID(anidbAnimeId)
            .Where(episode => episode.EpisodeType is (int)EpisodeType.Episode or (int)EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeTypeEnum)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToDictionary(episode => episode.EpisodeID);
        var tmdbEpisodes = _tmdbEpisodes.GetByTmdbShowID(tmdbShowId)
            .Where(episode => episode.SeasonNumber == 0 || !tmdbSeasonId.HasValue || episode.TmdbSeasonID == tmdbSeasonId.Value)
            .ToList();
        var tmdbNormalEpisodes = tmdbEpisodes
            .Where(episode => episode.SeasonNumber != 0)
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();
        var tmdbSpecialEpisodes = tmdbEpisodes
            .Where(episode => episode.SeasonNumber == 0)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToList();
        var current = 0;
        foreach (var episode in anidbEpisodes.Values)
        {
            current++;
            _logger.LogTrace("Checking episode {EpisodeType} {EpisodeNumber} (ID: {EpisodeID}, Progress: {Current}/{Total})", episode.EpisodeTypeEnum, episode.EpisodeNumber, episode.EpisodeID, current, anidbEpisodes.Count);
            if (useExisting && existing.TryGetValue(episode.EpisodeID, out var existingLinks))
            {
                // If hidden then return an empty link for the hidden episode.
                if (episode.AnimeEpisode?.IsHidden ?? false)
                {
                    _logger.LogTrace("Skipping hidden episode {EpisodeID}", episode.EpisodeID);
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
                _logger.LogTrace("Skipping existing links for episode {EpisodeID}", episode.EpisodeID);
                foreach (var link in existingLinks.DistinctBy((link => (link.TmdbShowID, link.TmdbEpisodeID))))
                {
                    crossReferences.Add(link);
                    toSkip.Add(link.CrossRef_AniDB_TMDB_EpisodeID);
                }
            }
            else
            {
                // If hidden then skip linking episode.
                if (episode.AnimeEpisode?.IsHidden ?? false)
                {
                    _logger.LogTrace("Skipping hidden episode {EpisodeID}", episode.EpisodeID);
                    crossReferences.Add(new(episode.EpisodeID, anidbAnimeId, 0, 0, MatchRating.SarahJessicaParker, 0));
                    continue;
                }

                // Else try find a match.
                _logger.LogTrace("Linking episode {EpisodeID}", episode.EpisodeID);
                var isSpecial = episode.EpisodeTypeEnum is EpisodeType.Special;
                var episodeList = isSpecial ? tmdbSpecialEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(episode, episodeList, isSpecial);
                if (crossRef.TmdbEpisodeID != 0)
                {
                    _logger.LogTrace("Found match for episode {EpisodeID} (TMDB ID: {TMDbEpisodeID})", episode.EpisodeID, crossRef.TmdbEpisodeID);
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);
                }
                else
                {
                    _logger.LogTrace("No match found for episode {EpisodeID}", episode.EpisodeID);
                }
                crossReferences.Add(crossRef);
                toAdd.Add(crossRef);
            }
        }

        if (!saveToDatabase)
        {
            _logger.LogDebug(
                "Found {a} anidb/tmdb episode cross-references for show {ShowTitle} in {Delta}ms (Anime={AnimeId},Show={ShowId})",
                crossReferences.Count,
                anime.PreferredTitle,
                (DateTime.Now - startedAt).TotalMilliseconds,
                anidbAnimeId,
                tmdbShowId
            );
            return crossReferences;
        }

        // Remove the current anidb episodes that does not overlap with the show.
        var toRemove = existing.Values
            .SelectMany(list => list)
            .Where(xref => anidbEpisodes.ContainsKey(xref.AnidbEpisodeID) && !toSkip.Contains(xref.CrossRef_AniDB_TMDB_EpisodeID))
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
    }

    private CrossRef_AniDB_TMDB_Episode TryFindAnidbAndTmdbMatch(SVR_AniDB_Episode anidbEpisode, IReadOnlyList<TMDB_Episode> tmdbEpisodes, bool isSpecial)
    {
        var anidbDate = anidbEpisode.GetAirDateAsDateOnly();
        var anidbTitles = _anidbEpisodeTitles.GetByEpisodeIDAndLanguage(anidbEpisode.EpisodeID, TitleLanguage.English)
            .Where(title => !title.Title.Trim().Equals($"Episode {anidbEpisode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var airdateProbability = tmdbEpisodes
            .Select(episode => (episode, probability: CalculateAirDateProbability(anidbDate, episode.AiredAt)))
            .Where(result => result.probability != 0)
            .Reverse()
            .OrderByDescending(result => result.probability)
            .ToList();
        var titleSearchResults = anidbTitles.Count > 0 ? tmdbEpisodes
            .Select(episode => anidbTitles.Search(episode.EnglishTitle, title => new string[] { title.Title }, true, 1).FirstOrDefault()?.Map(episode))
            .WhereNotNull()
            .OrderBy(result => result)
            .ToList() : [];

        // title first, then date
        if (isSpecial)
        {
            if (titleSearchResults.Count > 0)
            {
                var tmdbEpisode = titleSearchResults[0]!.Result;
                var dateAndTitleMatches = airdateProbability.Any(result => result.episode == tmdbEpisode);
                var rating = dateAndTitleMatches ? MatchRating.DateAndTitleMatches : MatchRating.TitleMatches;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
            }

            if (airdateProbability.Count > 0)
            {
                var tmdbEpisode = airdateProbability[0]!.episode;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, MatchRating.DateMatches);
            }
        }
        // date first, then title
        else
        {
            // Exact match first.
            if (titleSearchResults.Count > 0 && titleSearchResults[0].ExactMatch && titleSearchResults[0].LengthDifference == 0)
            {
                var tmdbEpisode = titleSearchResults[0]!.Result;
                var dateAndTitleMatches = airdateProbability.Any(result => result.episode == tmdbEpisode);
                var rating = dateAndTitleMatches ? MatchRating.DateAndTitleMatches : MatchRating.TitleMatches;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
            }

            if (airdateProbability.Count > 0)
            {
                var tmdbEpisode = airdateProbability[0]!.episode;
                var dateAndTitleMatches = titleSearchResults.Any(result => result.Result == tmdbEpisode);
                var rating = dateAndTitleMatches ? MatchRating.DateAndTitleMatches : MatchRating.DateMatches;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, rating);
            }

            if (titleSearchResults.Count > 0)
            {
                var tmdbEpisode = titleSearchResults[0]!.Result;
                return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisode.TmdbEpisodeID, tmdbEpisode.TmdbShowID, MatchRating.TitleMatches);
            }
        }

        if (tmdbEpisodes.Count > 0)
            return new(anidbEpisode.EpisodeID, anidbEpisode.AnimeID, tmdbEpisodes[0].TmdbEpisodeID, tmdbEpisodes[0].TmdbShowID, MatchRating.FirstAvailable);

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

    #endregion
}
