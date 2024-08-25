
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
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
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

    private readonly ISettingsProvider _settingsProvider;
    
    private readonly ISchedulerFactory _schedulerFactory;

    private readonly TmdbImageService _imageService;

    public TmdbLinkingService(
        ILogger<TmdbLinkingService> logger,
        ISettingsProvider settingsProvider,
        ISchedulerFactory schedulerFactory,
        TmdbImageService imageService
    )
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _imageService = imageService;
    }

    #region Movie Links

    public async Task AddMovieLink(int animeId, int episodeId, int movieId, bool additiveLink = false, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            await RemoveAllMovieLinksForAnime(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Movie Link: AniDB (ID:{AnidbID}) → TMDB Movie (ID:{TmdbID})", animeId, movieId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId) ??
            new(animeId, movieId);
        xref.AnidbEpisodeID = episodeId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(xref);
    }

    public async Task RemoveMovieLink(int animeId, int movieId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        await RemoveMovieLink(xref, removeImageFiles, purge ? true : null);
    }

    public async Task RemoveAllMovieLinksForAnime(int animeId, bool purge = false, bool removeImageFiles = true)
    {
        _logger.LogInformation("Removing All TMDB Movie Links for: {AnimeID}", animeId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeId);
        if (xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, removeImageFiles, purge ? true : null);
    }

    public async Task RemoveAllMovieLinksForMovie(int movieId)
    {
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(movieId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveMovieLink(xref, false, false);
    }

    private async Task RemoveMovieLink(CrossRef_AniDB_TMDB_Movie xref, bool removeImageFiles = true, bool? purge = null)
    {
        _imageService.ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Movie, xref.TmdbMovieID);

        _logger.LogInformation("Removing TMDB Movie Link: AniDB ({AnidbID}) → TMDB Movie (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbMovieID);
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Delete(xref);

        if (purge ?? RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(xref.TmdbMovieID).Count == 0)
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
        _logger.LogInformation("Adding TMDB Show Link: AniDB (ID:{AnidbID}) → TMDB Show (ID:{TmdbID})", animeId, showId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId) ??
            new(animeId, showId);
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Show.Save(xref);
    }

    public async Task RemoveShowLink(int animeId, int showId, bool purge = false, bool removeImageFiles = true)
    {
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        await RemoveShowLink(xref, removeImageFiles, purge ? true : null);
    }

    public async Task RemoveAllShowLinksForAnime(int animeId, bool purge = false, bool removeImageFiles = true)
    {
        _logger.LogInformation("Removing All TMDB Show Links for: {AnimeID}", animeId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeId);
        if (xrefs == null || xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        foreach (var xref in xrefs)
            await RemoveShowLink(xref, removeImageFiles, purge ? true : null);
    }

    public async Task RemoveAllShowLinksForShow(int showId)
    {
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(showId);
        if (xrefs.Count == 0)
            return;

        foreach (var xref in xrefs)
            await RemoveShowLink(xref, false, false);
    }

    private async Task RemoveShowLink(CrossRef_AniDB_TMDB_Show xref, bool removeImageFiles = true, bool? purge = null)
    {
        _imageService.ResetPreferredImage(xref.AnidbAnimeID, ForeignEntityType.Show, xref.TmdbShowID);

        _logger.LogInformation("Removing TMDB Show Link: AniDB ({AnidbID}) → TMDB Show (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbShowID);
        RepoFactory.CrossRef_AniDB_TMDB_Show.Delete(xref);

        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetOnlyByAnidbAnimeAndTmdbShowIDs(xref.AnidbAnimeID, xref.TmdbShowID);
        _logger.LogInformation("Removing {XRefsCount} Show Episodes for AniDB Anime ({AnidbID})", xrefs.Count, xref.AnidbAnimeID);
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(xrefs);

        var scheduler = await _schedulerFactory.GetScheduler();
        if (purge ?? RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(xref.TmdbShowID).Count == 0)
            await (await _schedulerFactory.GetScheduler().ConfigureAwait(false)).StartJob<PurgeTmdbShowJob>(c =>
            {
                c.TmdbShowID = xref.TmdbShowID;
                c.RemoveImageFiles = removeImageFiles;
            });
    }

    public void ResetAllEpisodeLinks(int anidbAnimeId)
    {
        var showId = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(anidbAnimeId)
            .FirstOrDefault()?.TmdbShowID;
        if (showId.HasValue)
        {
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(anidbAnimeId);
            var toSave = new List<CrossRef_AniDB_TMDB_Episode>();
            var toDelete = new List<CrossRef_AniDB_TMDB_Episode>();

            // Reset existing xrefs.
            var existingIDs = new HashSet<int>();
            foreach (var xref in xrefs)
            {
                if (existingIDs.Add(xref.AnidbEpisodeID))
                {
                    xref.TmdbEpisodeID = 0;
                    toSave.Add(xref);
                }
                else
                {
                    toDelete.Add(xref);
                }
            }

            // Add missing xrefs.
            var anidbEpisodesWithoutXrefs = RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeId)
                .Where(episode => !existingIDs.Contains(episode.AniDB_EpisodeID) && episode.EpisodeType is (int)EpisodeType.Episode or (int)EpisodeType.Special);
            foreach (var anidbEpisode in anidbEpisodesWithoutXrefs)
                toSave.Add(new(anidbEpisode.AniDB_EpisodeID, anidbAnimeId, 0, showId.Value, MatchRating.UserVerified));

            // Save the changes.
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toDelete);
        }
        else
        {
            // Remove all episode cross-references if no show is linked.
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(anidbAnimeId);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(xrefs);
        }
    }

    public bool SetEpisodeLink(int anidbEpisodeId, int tmdbEpisodeId, bool additiveLink = true, int? index = null)
    {
        var anidbEpisode = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeId);
        if (anidbEpisode == null)
            return false;

        // Set an empty link.
        if (tmdbEpisodeId == 0)
        {
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisodeId, anidbEpisode.AnimeID, 0, 0);
            toSave.TmdbShowID = 0;
            toSave.TmdbEpisodeID = 0;
            toSave.Ordering = 0;
            var toDelete = xrefs.Skip(1).ToList();
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toDelete);

            return true;
        }

        var tmdbEpisode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(tmdbEpisodeId);
        if (tmdbEpisode == null)
            return false;

        // Add another link
        if (additiveLink)
        {
            var toSave = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeAndTmdbEpisodeIDs(anidbEpisodeId, tmdbEpisodeId)
                ?? new(anidbEpisodeId, anidbEpisode.AnimeID, tmdbEpisodeId, tmdbEpisode.TmdbShowID);
            var existingAnidbLinks = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(anidbEpisodeId).Count;
            var existingTmdbLinks = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(tmdbEpisodeId).Count;
            if (toSave.CrossRef_AniDB_TMDB_EpisodeID == 0 && !index.HasValue)
                index = existingAnidbLinks > 0 ? existingAnidbLinks - 1 : existingTmdbLinks > 0 ? existingTmdbLinks - 1 : 0;
            if (index.HasValue)
                toSave.Ordering = index.Value;
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
        }
        else
        {
            var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(anidbEpisodeId);
            var toSave = xrefs.Count > 0 ? xrefs[0] : new(anidbEpisodeId, anidbEpisode.AnimeID, tmdbEpisodeId, tmdbEpisode.TmdbShowID);
            toSave.TmdbShowID = tmdbEpisode.TmdbShowID;
            toSave.TmdbEpisodeID = tmdbEpisode.TmdbEpisodeID;
            toSave.Ordering = 0;
            var toDelete = xrefs.Skip(1).ToList();
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toSave);
            RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toDelete);
        }

        return true;
    }

    #endregion

    #region Episode Links

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> MatchAnidbToTmdbEpisodes(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId, bool useExisting = false, bool saveToDatabase = false)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(anidbAnimeId);
        if (anime == null)
            return [];

        // Mapping logic
        var toSkip = new HashSet<int>();
        var toAdd = new List<CrossRef_AniDB_TMDB_Episode>();
        var crossReferences = new List<CrossRef_AniDB_TMDB_Episode>();
        var existing = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAllByAnidbAnimeAndTmdbShowIDs(anidbAnimeId, tmdbShowId)
            .GroupBy(xref => xref.AnidbEpisodeID)
            .ToDictionary(grouped => grouped.Key, grouped => grouped.ToList());
        var anidbEpisodes = RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeId)
            .Where(episode => episode.EpisodeType is (int)EpisodeType.Episode or (int)EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeTypeEnum)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToDictionary(episode => episode.EpisodeID);
        var tmdbEpisodes = RepoFactory.TMDB_Episode.GetByTmdbShowID(tmdbShowId)
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
        foreach (var episode in anidbEpisodes.Values)
        {
            if (useExisting && existing.TryGetValue(episode.EpisodeID, out var existingLinks))
            {
                // If hidden then return an empty link for the hidden episode.
                if (episode.AnimeEpisode?.IsHidden ?? false)
                {
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
                    crossReferences.Add(new(episode.EpisodeID, anidbAnimeId, 0, 0, MatchRating.SarahJessicaParker, 0));
                    continue;
                }

                // Else try find a match.
                var isSpecial = episode.EpisodeTypeEnum is EpisodeType.Special;
                var episodeList = isSpecial ? tmdbSpecialEpisodes : tmdbNormalEpisodes;
                var crossRef = TryFindAnidbAndTmdbMatch(episode, episodeList, isSpecial);
                if (crossRef.TmdbEpisodeID != 0)
                {
                    var index = episodeList.FindIndex(episode => episode.TmdbEpisodeID == crossRef.TmdbEpisodeID);
                    if (index != -1)
                        episodeList.RemoveAt(index);
                }
                crossReferences.Add(crossRef);
                toAdd.Add(crossRef);
            }
        }

        if (!saveToDatabase)
            return crossReferences;

        // Remove the current anidb episodes that does not overlap with the show.
        var toRemove = existing.Values
            .SelectMany(list => list)
            .Where(xref => anidbEpisodes.ContainsKey(xref.AnidbEpisodeID) && !toSkip.Contains(xref.CrossRef_AniDB_TMDB_EpisodeID))
            .ToList();

        _logger.LogDebug(
            "Added/removed/skipped {a}/{r}/{s} anidb/tmdb episode cross-references for show {ShowTitle} (Anime={AnimeId},Show={ShowId})",
            toAdd.Count,
            toRemove.Count,
            existing.Count - toRemove.Count,
            anime.PreferredTitle,
            anidbAnimeId,
            tmdbShowId);
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Save(toAdd);
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(toRemove);

        return crossReferences;
    }

    private static CrossRef_AniDB_TMDB_Episode TryFindAnidbAndTmdbMatch(SVR_AniDB_Episode anidbEpisode, IReadOnlyList<TMDB_Episode> tmdbEpisodes, bool isSpecial)
    {
        var anidbDate = anidbEpisode.GetAirDateAsDateOnly();
        var anidbTitles = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(anidbEpisode.EpisodeID, TitleLanguage.English)
            .Where(title => !title.Title.Trim().Equals($"Episode {anidbEpisode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var airdateProbability = tmdbEpisodes
            .Select(episode => (episode, probability: CalculateAirDateProbability(anidbDate, episode.AiredAt)))
            .Where(result => result.probability != 0)
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
