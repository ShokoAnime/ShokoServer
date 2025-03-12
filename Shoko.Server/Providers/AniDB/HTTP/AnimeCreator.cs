using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AnimeCreator
{
    private readonly ILogger<AnimeCreator> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly ConcurrentDictionary<int, object> _updatingIDs = [];

    public AnimeCreator(ILogger<AnimeCreator> logger, ISettingsProvider settings, ISchedulerFactory schedulerFactory, IVideoReleaseService videoReleaseService)
    {
        _logger = logger;
        _settingsProvider = settings;
        _schedulerFactory = schedulerFactory;
        _videoReleaseService = videoReleaseService;
    }


#pragma warning disable CS0618
    public async Task<(bool animeUpdated, bool titlesUpdated, bool descriptionUpdated, bool shouldUpdateFiles, Dictionary<SVR_AniDB_Episode, UpdateReason> episodeChanges)> CreateAnime(ResponseGetAnime response, SVR_AniDB_Anime anime, int relDepth)
    {
        _logger.LogTrace("Updating anime {AnimeID}", response?.Anime?.AnimeID);
        if ((response?.Anime?.AnimeID ?? 0) == 0) return (false, false, false, false, []);
        var lockObj = _updatingIDs.GetOrAdd(response.Anime.AnimeID, new object());
        Monitor.Enter(lockObj);
        try
        {
            // check if we updated in a lock
            var existingAnime = RepoFactory.AniDB_Anime.GetByAnimeID(response.Anime.AnimeID);
            if (existingAnime != null && DateTime.Now - existingAnime.DateTimeUpdated < TimeSpan.FromSeconds(2)) return (false, false, false, false, []);

            var settings = _settingsProvider.GetSettings();
            _logger.LogTrace("------------------------------------------------");
            _logger.LogTrace(
                "PopulateAndSaveFromHTTP: for {AnimeID} - {MainTitle} @ Depth: {RelationDepth}/{MaxRelationDepth}",
                response.Anime.AnimeID, response.Anime.MainTitle, relDepth, settings.AniDb.MaxRelationDepth
            );
            _logger.LogTrace("------------------------------------------------");

            // We need various values to be populated to be considered valid
            if (string.IsNullOrEmpty(response.Anime.MainTitle) || response.Anime.AnimeID <= 0)
            {
                _logger.LogError("AniDB_Anime was unable to populate as it received invalid info. " +
                                 "This is not an error on our end. It is AniDB's issue, " +
                                 "as they did not return either an ID or a title for the anime");
                return (false, false, false, false, []);
            }

            var taskTimer = Stopwatch.StartNew();
            var totalTimer = Stopwatch.StartNew();
            var (updated, descriptionUpdated, shouldUpdateFiles) = PopulateAnime(response.Anime, anime);
            RepoFactory.AniDB_Anime.Save(anime);

            taskTimer.Stop();
            _logger.LogTrace("PopulateAnime in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            // alternatively these could be written as an if...then statement spanning two lines.
            // we need ConfigureAwait(true) because of the lock
            var (episodesAddedOrRemoved, updatedEpisodes) = await CreateEpisodes(response.Episodes, anime).ConfigureAwait(true);
            if (episodesAddedOrRemoved && !updated) updated = true;

            taskTimer.Stop();
            _logger.LogTrace("CreateEpisodes in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            var titlesUpdated = CreateTitles(response.Titles, anime);
            updated = updated || titlesUpdated;
            shouldUpdateFiles = shouldUpdateFiles || titlesUpdated;
            taskTimer.Stop();
            _logger.LogTrace("CreateTitles in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            updated = CreateTags(response.Tags, anime) || updated;
            taskTimer.Stop();
            _logger.LogTrace("CreateTags in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateCharacters(response.Characters, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateCharacters in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateStaff(response.Staff, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateStaff in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateResources(response.Resources, anime);
            taskTimer.Stop();
            _logger.LogTrace("CreateResources in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateRelations(response.Relations, anime.AnimeID);
            taskTimer.Stop();
            _logger.LogTrace("CreateRelations in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            CreateSimilarAnime(response.Similar, anime.AnimeID);
            taskTimer.Stop();
            _logger.LogTrace("CreateSimilarAnime in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            // Track when we last tried to update the metadata.
            anime.DateTimeUpdated = DateTime.Now;

            // Track when we last updated the metadata.
            if (updated)
                anime.DateTimeDescUpdated = anime.DateTimeUpdated;

            RepoFactory.AniDB_Anime.Save(anime);

            totalTimer.Stop();
            _logger.LogTrace("TOTAL TIME in : {Time}", totalTimer.Elapsed);
            _logger.LogTrace("------------------------------------------------");

            return (updated, titlesUpdated, descriptionUpdated, shouldUpdateFiles, updatedEpisodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating anime {AnimeID}", response.Anime.AnimeID);
            throw;
        }
        finally
        {
            Monitor.Exit(lockObj);
            _updatingIDs.TryRemove(response.Anime.AnimeID, out _);
        }
    }
#pragma warning restore CS0618

    private static (bool animeUpdated, bool descriptionUpdated, bool shouldUpdateFiles) PopulateAnime(ResponseAnime animeInfo, SVR_AniDB_Anime anime)
    {
        var isUpdated = false;
        var descriptionUpdated = false;
        var shouldUpdateFiles = false;
        var isNew = anime.AnimeID == 0 || anime.AniDB_AnimeID == 0;
        var description = animeInfo.Description ?? string.Empty;
        var episodeCountSpecial = animeInfo.EpisodeCount - animeInfo.EpisodeCountNormal;
        if (anime.AirDate != animeInfo.AirDate)
        {
            anime.AirDate = animeInfo.AirDate;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.AllCinemaID != animeInfo.AllCinemaID)
        {
            anime.AllCinemaID = animeInfo.AllCinemaID;
            isUpdated = true;
        }

        if (anime.AnimeID != animeInfo.AnimeID)
        {
            anime.AnimeID = animeInfo.AnimeID;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.AnimeType != (int)animeInfo.AnimeType)
        {
            anime.AnimeType = (int)animeInfo.AnimeType;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.ANNID != animeInfo.ANNID)
        {
            anime.ANNID = animeInfo.ANNID;
            isUpdated = true;
        }

        if (anime.AvgReviewRating != animeInfo.AvgReviewRating)
        {
            anime.AvgReviewRating = animeInfo.AvgReviewRating;
            isUpdated = true;
        }

        if (anime.BeginYear != animeInfo.BeginYear)
        {
            anime.BeginYear = animeInfo.BeginYear;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.Description != description)
        {
            anime.Description = description;
            isUpdated = true;
            descriptionUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.EndDate != animeInfo.EndDate)
        {
            anime.EndDate = animeInfo.EndDate;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.EndYear != animeInfo.EndYear)
        {
            anime.EndYear = animeInfo.EndYear;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.MainTitle != animeInfo.MainTitle)
        {
            anime.MainTitle = animeInfo.MainTitle;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.EpisodeCount != animeInfo.EpisodeCount)
        {
            anime.EpisodeCount = animeInfo.EpisodeCount;
            isUpdated = true;
        }

        if (anime.EpisodeCountNormal != animeInfo.EpisodeCountNormal)
        {
            anime.EpisodeCountNormal = animeInfo.EpisodeCountNormal;
            isUpdated = true;
        }

        if (anime.EpisodeCountSpecial != episodeCountSpecial)
        {
            anime.EpisodeCountSpecial = episodeCountSpecial;
            isUpdated = true;
        }

        if (anime.Picname != animeInfo.Picname)
        {
            anime.Picname = animeInfo.Picname;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.Rating != animeInfo.Rating)
        {
            anime.Rating = animeInfo.Rating;
            isUpdated = true;
        }

        if (anime.IsRestricted != animeInfo.IsRestricted)
        {
            anime.IsRestricted = animeInfo.IsRestricted;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.ReviewCount != animeInfo.ReviewCount)
        {
            anime.ReviewCount = animeInfo.ReviewCount;
            isUpdated = true;
        }

        if (anime.TempRating != animeInfo.TempRating)
        {
            anime.TempRating = animeInfo.TempRating;
            isUpdated = true;
        }

        if (anime.TempVoteCount != animeInfo.TempVoteCount)
        {
            anime.TempVoteCount = animeInfo.TempVoteCount;
            isUpdated = true;
        }

        if (anime.URL != animeInfo.URL)
        {
            anime.URL = animeInfo.URL;
            isUpdated = true;
        }

        if (anime.VoteCount != animeInfo.VoteCount)
        {
            anime.VoteCount = animeInfo.VoteCount;
            isUpdated = true;
        }

        if (isNew)
        {
            anime.AllTags = string.Empty;
            anime.AllTitles = string.Empty;
            anime.ImageEnabled = 1;
        }

        if (isNew || isUpdated)
        {
#pragma warning disable CS0618
            anime.DateTimeUpdated = anime.DateTimeDescUpdated = DateTime.Now;
#pragma warning restore CS0618
        }

        return (isUpdated, descriptionUpdated, shouldUpdateFiles);
    }

    private async Task<(bool, Dictionary<SVR_AniDB_Episode, UpdateReason>)> CreateEpisodes(List<ResponseEpisode> rawEpisodeList, SVR_AniDB_Anime anime)
    {
        if (rawEpisodeList == null)
            return (false, []);

        var episodeCountSpecial = 0;
        var episodeCountNormal = 0;
        var epIDs = rawEpisodeList
            .Select(e => e.EpisodeID)
            .ToHashSet();
        var epsBelongingToThisAnime = RepoFactory.AniDB_Episode.GetByAnimeID(anime.AnimeID)
            .ToDictionary(e => e.EpisodeID);
        var epsBelongingToOtherAnime = epIDs
            .Where(id => !epsBelongingToThisAnime.ContainsKey(id))
            .Select(id => RepoFactory.AniDB_Episode.GetByEpisodeID(id))
            .Where(episode => episode != null)
            .ToList();
        var currentAniDBEpisodes = epsBelongingToThisAnime.Values
            .Concat(epsBelongingToOtherAnime)
            .ToDictionary(a => a.EpisodeID);
        var currentAniDBEpisodeTitles = currentAniDBEpisodes.Keys
            .ToDictionary(id => id, id => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(id).ToHashSet());
        var epsToRemove = currentAniDBEpisodes.Values
            .Where(a => !epIDs.Contains(a.EpisodeID))
            .ToList();
        var epsToSave = new List<SVR_AniDB_Episode>();
        var titlesToRemove = new List<SVR_AniDB_Episode_Title>();
        var titlesToSave = new List<SVR_AniDB_Episode_Title>();
        var episodeEventsToEmit = new Dictionary<SVR_AniDB_Episode, UpdateReason>();

        foreach (var rawEpisode in rawEpisodeList)
        {
            // Load the titles for the episode now, since we might need to check
            // them even if we don't update the episode itself.
            if (!currentAniDBEpisodeTitles.TryGetValue(rawEpisode.EpisodeID, out var currentTitles))
                currentTitles = new();

            // Check if the existing record, if any, needs to be updated.
            var isNew = false;
            var isUpdated = false;
            if (currentAniDBEpisodes.TryGetValue(rawEpisode.EpisodeID, out var episode))
            {
                // The data we have stored is either in sync (or newer) than
                // the raw episode data, so skip updating the episode, so if the
                // episode does not belong to the anime being processed then
                // skip it...
                if (episode.DateTimeUpdated >= rawEpisode.LastUpdated && episode.AnimeID != rawEpisode.AnimeID)
                    continue;

                var airDate = AniDBExtensions.GetAniDBDateAsSeconds(rawEpisode.AirDate);
                var rating = rawEpisode.Rating.ToString(CultureInfo.InvariantCulture);
                var votes = rawEpisode.Votes.ToString(CultureInfo.InvariantCulture);
                var description = rawEpisode.Description ?? string.Empty;
                if (episode.AirDate != airDate)
                {
                    episode.AirDate = airDate;
                    isUpdated = true;
                }

                if (episode.AnimeID != anime.AnimeID)
                {
                    episode.AnimeID = anime.AnimeID;
                    isUpdated = true;
                }

                if (episode.EpisodeNumber != rawEpisode.EpisodeNumber)
                {
                    episode.EpisodeNumber = rawEpisode.EpisodeNumber;
                    isUpdated = true;
                }

                if (episode.EpisodeType != (int)rawEpisode.EpisodeType)
                {
                    episode.EpisodeType = (int)rawEpisode.EpisodeType;
                    isUpdated = true;
                }

                if (episode.LengthSeconds != rawEpisode.LengthSeconds)
                {
                    episode.LengthSeconds = rawEpisode.LengthSeconds;
                    isUpdated = true;
                }

                if (episode.Rating != rating)
                {
                    episode.Rating = rating;
                    isUpdated = true;
                }

                if (episode.Votes != votes)
                {
                    episode.Votes = votes;
                    isUpdated = true;
                }

                if (episode.Description != description)
                {
                    episode.Description = description;
                    isUpdated = true;
                }
            }
            // Create a new record.
            else
            {
                isNew = true;
                episode = new()
                {
                    AirDate = AniDBExtensions.GetAniDBDateAsSeconds(rawEpisode.AirDate),
                    AnimeID = rawEpisode.AnimeID,
                    DateTimeUpdated = rawEpisode.LastUpdated,
                    EpisodeID = rawEpisode.EpisodeID,
                    EpisodeNumber = rawEpisode.EpisodeNumber,
                    EpisodeType = (int)rawEpisode.EpisodeType,
                    LengthSeconds = rawEpisode.LengthSeconds,
                    Rating = rawEpisode.Rating.ToString(CultureInfo.InvariantCulture),
                    Votes = rawEpisode.Votes.ToString(CultureInfo.InvariantCulture),
                    Description = rawEpisode.Description ?? string.Empty
                };
            }

            // Convert the raw titles to their equivalent database model.
            var newTitles = rawEpisode.Titles
                .Select(rawtitle => new SVR_AniDB_Episode_Title
                {
                    AniDB_EpisodeID = rawEpisode.EpisodeID,
                    Language = rawtitle.Language,
                    Title = rawtitle.Title,
                })
                .ToList();

            var deltaTitles = newTitles.Where(a => !currentTitles.Contains(a)).ToList();
            // Mark the new titles to-be saved.
            titlesToSave.AddRange(deltaTitles);
            if (deltaTitles.Count > 0 && !episodeEventsToEmit.ContainsKey(episode))
                episodeEventsToEmit[episode] = UpdateReason.Updated;

            // Remove outdated titles.
            if (currentTitles.Count > 0)
                titlesToRemove.AddRange(currentTitles.Where(a => !newTitles.Any(b => b.Equals(a))));

            // Since the HTTP API doesn't return a count of the number of normal
            // episodes and/or specials, then we will calculate it now.
            switch (rawEpisode.EpisodeType)
            {
                case EpisodeType.Episode:
                    episodeCountNormal++;
                    break;

                case EpisodeType.Special:
                    episodeCountSpecial++;
                    break;
            }

            // Emit the event.
            if (isNew || isUpdated)
                episodeEventsToEmit[episode] = isNew ? UpdateReason.Added : UpdateReason.Updated;

            // We need to save the "date time updated" regardless of if there were other changes,
            // since it will be used to determine if the episode should belong to this anime or
            // another anime.
            if (isNew || isUpdated || episode.DateTimeUpdated != rawEpisode.LastUpdated)
            {
                episode.DateTimeUpdated = rawEpisode.LastUpdated;
                epsToSave.Add(episode);
            }
        }

        if (epsToRemove.Count > 0)
        {
            _logger.LogTrace("Deleting the following episodes (no longer in AniDB)");
            foreach (var ep in epsToRemove)
            {
                _logger.LogTrace("AniDB Ep: {EpisodeID} Type: {EpisodeType} Number: {EpisodeNumber}", ep.EpisodeID,
                    ep.EpisodeType, ep.EpisodeNumber);
            }
        }

        // Validate existing shoko episodes.
        var correctSeries = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
        var shokoEpisodesToRemove = new List<SVR_AnimeEpisode>();
        var shokoEpisodesToSave = new List<SVR_AnimeEpisode>();
        var shokoSeriesDict = new Dictionary<int, SVR_AnimeSeries>();
        var storedReleasesToRemove = new List<StoredReleaseInfo>();
        var xrefsToRemove = new List<SVR_CrossRef_File_Episode>();
        var videosToRefetch = new List<VideoLocal>();
        var tmdbXRefsToRemove = new List<CrossRef_AniDB_TMDB_Episode>();
        if (correctSeries != null)
            shokoSeriesDict.Add(correctSeries.AnimeSeriesID, correctSeries);
        foreach (var episode in epsToSave)
        {
            // No shoko episode, continue.
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
            if (shokoEpisode == null)
                continue;

            // The series exists and the episode mapping is correct, continue.
            if ((
                    shokoSeriesDict.TryGetValue(shokoEpisode.AnimeSeriesID, out var actualSeries) ||
                    shokoSeriesDict.TryAdd(shokoEpisode.AnimeSeriesID, actualSeries = RepoFactory.AnimeSeries.GetByID(shokoEpisode.AnimeSeriesID))
                ) && actualSeries != null && actualSeries.AniDB_ID == episode.AnimeID)
                continue;

            // The series was incorrectly linked to the wrong series. Correct it
            // if it's possible, or delete the episode.
            if (correctSeries != null)
            {
                shokoEpisode.AnimeSeriesID = correctSeries.AnimeSeriesID;
                shokoEpisodesToSave.Add(shokoEpisode);
                continue;
            }

            // Delete the episode and clean up any remaining traces of the shoko
            // episode.
            shokoEpisodesToRemove.Add(shokoEpisode);
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episode.EpisodeID);
            var videos = xrefs
                .Select(xref => RepoFactory.VideoLocal.GetByEd2kAndSize(xref.Hash, xref.FileSize))
                .Where(video => video != null)
                .ToList();
            var storedReleases = RepoFactory.StoredReleaseInfo.GetByAnidbEpisodeID(episode.EpisodeID);
            var tmdbXRefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(episode.EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            storedReleasesToRemove.AddRange(storedReleases);
            tmdbXRefsToRemove.AddRange(tmdbXRefs);
        }
        shokoSeriesDict.Clear();

        // Remove any existing links to the episodes that will be removed.
        foreach (var episode in epsToRemove)
        {
            if (currentAniDBEpisodeTitles.TryGetValue(episode.EpisodeID, out var currentTitles))
                titlesToRemove.AddRange(currentTitles);
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
            if (shokoEpisode != null)
                shokoEpisodesToRemove.Add(shokoEpisode);
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episode.EpisodeID);
            var videos = xrefs
                .Select(xref => RepoFactory.VideoLocal.GetByEd2kAndSize(xref.Hash, xref.FileSize))
                .WhereNotNull()
                .ToList();
            var databaseReleases = RepoFactory.StoredReleaseInfo.GetByAnidbEpisodeID(episode.EpisodeID);
            var tmdbXRefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(episode.EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            storedReleasesToRemove.AddRange(databaseReleases);
            tmdbXRefsToRemove.AddRange(tmdbXRefs);
        }

        RepoFactory.StoredReleaseInfo.Delete(storedReleasesToRemove.DistinctBy(a => a.StoredReleaseInfoID).ToList());
        RepoFactory.AniDB_Episode.Save(epsToSave);
        RepoFactory.AniDB_Episode.Delete(epsToRemove);
        RepoFactory.AniDB_Episode_Title.Save(titlesToSave);
        RepoFactory.AniDB_Episode_Title.Delete(titlesToRemove);
        RepoFactory.AnimeEpisode.Save(shokoEpisodesToSave);
        RepoFactory.AnimeEpisode.Delete(shokoEpisodesToRemove);
        RepoFactory.CrossRef_File_Episode.Delete(xrefsToRemove);
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(tmdbXRefsToRemove);

        // Schedule a refetch of any video files affected by the removal of the
        // episodes. They were likely moved to another episode entry so let's
        // try and fetch that.
        foreach (var video in videosToRefetch)
        {
            // If auto-match is not available then clear the release so the video is
            // not referencing no longer existing episodes.
            await _videoReleaseService.ClearReleaseForVideo(video);
            await _videoReleaseService.ScheduleFindReleaseForVideo(video, prioritize: true);
        }

        var episodeCount = episodeCountSpecial + episodeCountNormal;
        anime.EpisodeCountNormal = episodeCountNormal;
        anime.EpisodeCountSpecial = episodeCountSpecial;
        anime.EpisodeCount = episodeCount;

        // Add removed episodes to the dictionary.
        foreach (var episode in epsToRemove)
            episodeEventsToEmit.Add(episode, UpdateReason.Removed);

        return (
            episodeEventsToEmit.ContainsValue(UpdateReason.Added) || epsToRemove.Count > 0,
            episodeEventsToEmit
        );
    }

    private static bool CreateTitles(List<ResponseTitle> titles, SVR_AniDB_Anime anime)
    {
        // after this runs once, it should clean up the dupes from before
        if (titles == null)
            return false;

        var allTitles = string.Empty;
        var existingTitles = RepoFactory.AniDB_Anime_Title.GetByAnimeID(anime.AnimeID);
        var keySelector = new Func<SVR_AniDB_Anime_Title, string>(t => $"{t.TitleType},{t.Language},{t.Title}");
        var existingTitleDict = existingTitles.DistinctBy(keySelector).ToDictionary(keySelector);
        var titlesToKeep = new HashSet<int>();
        var titlesToSave = new Dictionary<string, SVR_AniDB_Anime_Title>();

        foreach (var rawtitle in titles)
        {
            if (string.IsNullOrEmpty(rawtitle?.Title)) continue;

            var key = $"{rawtitle.TitleType},{rawtitle.Language},{rawtitle.Title}";
            if (existingTitleDict.TryGetValue(key, out var title))
            {
                titlesToKeep.Add(title.AniDB_Anime_TitleID);
                if (allTitles.Length > 0)
                    allTitles += "|";
                allTitles += rawtitle.Title;
                continue;
            }

            if (titlesToSave.ContainsKey(key)) continue;

            titlesToSave[key] = new()
            {
                AnimeID = anime.AnimeID,
                Language = rawtitle.Language,
                Title = rawtitle.Title,
                TitleType = rawtitle.TitleType
            };

            if (allTitles.Length > 0)
                allTitles += "|";
            allTitles += rawtitle.Title;
        }

        var titlesToDelete = existingTitles.ExceptBy(titlesToKeep, t => t.AniDB_Anime_TitleID).ToList();

        anime.AllTitles = allTitles;
        RepoFactory.AniDB_Anime_Title.Delete(titlesToDelete);
        RepoFactory.AniDB_Anime_Title.Save(titlesToSave.Values);

        return titlesToSave.Count > 0 || titlesToDelete.Count > 0;
    }

    /// <summary>
    /// A dictionary containing the name overrides for tags whose name either
    /// doesn't makes much sense or is otherwise confusing.
    /// </summary>
    /// <remarks>
    /// We use the tag name since the id _can_ change sometimes.
    /// </remarks>
    private static readonly Dictionary<string, string> TagNameOverrideDict = new()
    {
        {"new", "original work"},
        {"original work", "source material"},
    };

    private static AniDB_Tag FindOrCreateTag(ResponseTag rawTag)
    {
        var tag = RepoFactory.AniDB_Tag.GetByTagID(rawTag.TagID);

        // We're trying to add older details to an existing tag,
        // so skip updating the tag but still create the cross-reference.
        if (tag != null && tag.LastUpdated != DateTime.UnixEpoch && tag.LastUpdated >= rawTag.LastUpdated)
            return tag;

        if (tag == null)
        {
            // There are situations in which an ID may have changed, this is
            // usually due to it being moved, but may be for other reasons.
            var existingTags = RepoFactory.AniDB_Tag.GetBySourceName(rawTag.TagName);
            var lastUpdatedTag = existingTags.MaxBy(existingTag => existingTag.LastUpdated);

            // One (or more, but idc) of the existing tags are more recently
            // updated than the tag we're trying to create, so skip creating
            // the tag and instead use more recent tag.
            if (lastUpdatedTag != null && lastUpdatedTag.LastUpdated >= rawTag.LastUpdated)
                return lastUpdatedTag;

            var xrefsToRemap = existingTags
                .SelectMany(t => RepoFactory.AniDB_Anime_Tag.GetByTagID(t.TagID))
                .ToList();
            foreach (var xref in xrefsToRemap)
            {
                xref.TagID = rawTag.TagID;
                RepoFactory.AniDB_Anime_Tag.Save(xref);
            }

            // Delete the obsolete tag(s).
            RepoFactory.AniDB_Tag.Delete(existingTags);

            // While we're at it, clean up other unreferenced tags.
            RepoFactory.AniDB_Tag.Delete(RepoFactory.AniDB_Tag.GetAll()
                .Where(a => !RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID).Any()).ToList());

            // Also clean up dead cross-references. They shouldn't exist,
            // but they sometime does for whatever reason. ¯\_(ツ)_/¯
            var orphanedXRefs = RepoFactory.AniDB_Anime_Tag.GetAll().Where(a =>
                RepoFactory.AniDB_Tag.GetByTagID(a.TagID) == null ||
                RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null).ToList();

            RepoFactory.AniDB_Anime_Tag.Delete(orphanedXRefs);

            tag = new AniDB_Tag();
        }

        TagNameOverrideDict.TryGetValue(rawTag.TagName, out var nameOverride);
        tag.TagID = rawTag.TagID;
        tag.ParentTagID = rawTag.ParentTagID;
        tag.TagNameSource = rawTag.TagName;
        tag.TagNameOverride = nameOverride;
        tag.TagDescription = rawTag.TagDescription ?? string.Empty;
        tag.GlobalSpoiler = rawTag.GlobalSpoiler;
        tag.Verified = rawTag.Verified;
        tag.LastUpdated = rawTag.LastUpdated;

        return tag;
    }

    public static bool CreateTags(List<ResponseTag> tags, SVR_AniDB_Anime anime)
    {
        if (tags == null)
            return false;

        // find all the current links, and then later remove the ones that are no longer relevant
        var allTags = string.Empty;
        var tagsToSave = new List<AniDB_Tag>();
        var xrefsToSave = new List<AniDB_Anime_Tag>();
        var currentTags = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(anime.AnimeID);
        var newTagIDs = new HashSet<int>();
        foreach (var rawtag in tags)
        {
            if (rawtag.TagID <= 0 || string.IsNullOrEmpty(rawtag.TagName))
                continue;

            var tag = FindOrCreateTag(rawtag);
            if (!newTagIDs.Add(tag.TagID))
                continue;

            tagsToSave.Add(tag);

            var xref = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDAndTagID(rawtag.AnimeID, tag.TagID) ?? new();
            xref.AnimeID = rawtag.AnimeID;
            xref.TagID = tag.TagID;
            xref.LocalSpoiler = rawtag.LocalSpoiler;
            xref.Weight = rawtag.Weight;
            xrefsToSave.Add(xref);

            // Only add it to the cached array if the tag is verified. This
            // ensures the v1 and v2 api is only displaying verified tags.
            if (tag.Verified)
            {
                if (allTags.Length > 0)
                    allTags += "|";
                allTags += tag.TagName;
            }
        }

        anime.AllTags = allTags;

        var xrefsToDelete = currentTags.Where(curTag => !newTagIDs.Contains(curTag.TagID)).ToList();
        RepoFactory.AniDB_Tag.Save(tagsToSave);
        RepoFactory.AniDB_Anime_Tag.Save(xrefsToSave);
        RepoFactory.AniDB_Anime_Tag.Delete(xrefsToDelete);

        return xrefsToSave.Count > 0 || xrefsToDelete.Count > 0;
    }

    public void CreateCharacters(List<ResponseCharacter> chars, SVR_AniDB_Anime anime, bool skipCreatorScheduling = false)
    {
        if (chars == null) return;

        var charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar;
        var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;
        var settings = _settingsProvider.GetSettings();

        var existingCreators = new Dictionary<int, AniDB_Creator>();
        var existingXrefs = RepoFactory.AniDB_Anime_Character.GetByAnimeID(anime.AnimeID)
            .ToLookup(a => a.CharacterID);
        var existingCreatorXrefs = RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(anime.AnimeID)
            .ToLookup(a => (a.CharacterID, a.CreatorID));

        var charactersToKeep = new HashSet<int>();
        var charactersToSave = new List<AniDB_Character>();
        var characterXrefsToKeep = new HashSet<int>();
        var characterXrefsToSave = new List<AniDB_Anime_Character>();

        var creatorsToSchedule = new HashSet<int>();
        var creatorsToSave = new List<AniDB_Creator>();
        var creatorXrefsToKeep = new HashSet<int>();
        var creatorXrefsToSave = new List<AniDB_Anime_Character_Creator>();

        try
        {
            var charLookup = chars.ToLookup(a => a.CharacterID);
            foreach (var groupings in charLookup.Where(a => a.Count() > 1))
                _logger.LogWarning("Anime had a duplicate character listing for CharacterID: {CharID}", groupings.Key);

            var characterOrdering = 0;
            foreach (var (rawCharacter, _) in charLookup)
            {
                var characterIndex = characterOrdering++;
                if (rawCharacter.AnimeID != anime.AnimeID || rawCharacter.CharacterID <= 0 || string.IsNullOrEmpty(rawCharacter.CharacterAppearanceType))
                    continue;

                var gender = rawCharacter.Gender switch
                {
                    null => PersonGender.Unknown,
                    _ => Enum.TryParse<PersonGender>(rawCharacter.Gender, true, out var result) ? result : PersonGender.Unknown
                };
                var characterType = rawCharacter.CharacterType switch
                {
                    null => CharacterType.Unknown,
                    _ => Enum.TryParse<CharacterType>(rawCharacter.CharacterType, true, out var result) ? result : CharacterType.Unknown
                };
                var character = RepoFactory.AniDB_Character.GetByCharacterID(rawCharacter.CharacterID) ?? new()
                {
                    CharacterID = rawCharacter.CharacterID,
                };
                if (character.AniDB_CharacterID is 0)
                {
                    if (rawCharacter == null) continue;
                    if (rawCharacter.CharacterID <= 0 || string.IsNullOrEmpty(rawCharacter.CharacterName)) continue;

                    character.Description = rawCharacter.CharacterDescription ?? string.Empty;
                    character.OriginalName = rawCharacter.CharacterKanjiName ?? string.Empty;
                    character.Name = rawCharacter.CharacterName;
                    character.ImagePath = rawCharacter.PicName ?? string.Empty;
                    character.Gender = gender;
                    character.Type = characterType;
                    charactersToSave.Add(character);
                }
                else if (rawCharacter.LastUpdated >= character.LastUpdated)
                {
                    if (string.IsNullOrEmpty(rawCharacter?.CharacterName)) continue;

                    var updated = false;
                    if (character.Description != (rawCharacter.CharacterDescription ?? string.Empty))
                    {
                        character.Description = rawCharacter.CharacterDescription ?? string.Empty;
                        updated = true;
                    }
                    if (character.Name != rawCharacter.CharacterName)
                    {
                        character.Name = rawCharacter.CharacterName;
                        updated = true;
                    }
                    if (character.OriginalName != (rawCharacter.CharacterKanjiName ?? string.Empty))
                    {
                        character.OriginalName = rawCharacter.CharacterKanjiName ?? string.Empty;
                        updated = true;
                    }
                    if (character.ImagePath != (rawCharacter.PicName ?? string.Empty))
                    {
                        character.ImagePath = rawCharacter.PicName ?? string.Empty;
                        updated = true;
                    }
                    if (character.Gender != gender)
                    {
                        character.Gender = gender;
                        updated = true;
                    }
                    if (character.Type != characterType)
                    {
                        character.Type = characterType;
                        updated = true;
                    }
                    if (character.LastUpdated != rawCharacter.LastUpdated)
                    {
                        character.LastUpdated = rawCharacter.LastUpdated;
                        updated = true;
                    }
                    if (updated)
                        charactersToSave.Add(character);
                    charactersToKeep.Add(character.AniDB_CharacterID);
                }
                else
                {
                    charactersToKeep.Add(character.AniDB_CharacterID);
                }

                var appearance = rawCharacter.CharacterAppearanceType;
                var appearanceType = appearance switch
                {
                    "main character in" => CharacterAppearanceType.Main_Character,
                    "secondary cast in" => CharacterAppearanceType.Minor_Character,
                    "appears in" => CharacterAppearanceType.Background_Character,
                    "cameo appearance in" => CharacterAppearanceType.Cameo,
                    _ => CharacterAppearanceType.Unknown,
                };
                var xref = existingXrefs.Contains(rawCharacter.CharacterID)
                    ? existingXrefs[rawCharacter.CharacterID].First()
                    : new AniDB_Anime_Character()
                    {
                        AnimeID = anime.AnimeID,
                        CharacterID = rawCharacter.CharacterID,
                    };
                if (xref.AniDB_Anime_CharacterID == 0)
                {
                    xref.Ordering = characterIndex;
                    xref.Appearance = appearance;
                    xref.AppearanceType = appearanceType;
                    characterXrefsToSave.Add(xref);
                }
                else
                {
                    var updated = false;
                    if (xref.Ordering != characterIndex)
                    {
                        xref.Ordering = characterIndex;
                        updated = true;
                    }
                    if (xref.Appearance != appearance)
                    {
                        xref.Appearance = appearance;
                        updated = true;
                    }
                    if (xref.AppearanceType != appearanceType)
                    {
                        xref.AppearanceType = appearanceType;
                        updated = true;
                    }
                    if (updated)
                        characterXrefsToSave.Add(xref);
                    characterXrefsToKeep.Add(xref.AniDB_Anime_CharacterID);
                }

                var creatorLookup = rawCharacter.Seiyuus.ToLookup(a => a.SeiyuuID);
                foreach (var groupings in creatorLookup.Where(a => a.Count() > 1))
                    _logger.LogWarning("Anime had a duplicate voice actor listing for SeiyuuID: {SeiyuuID} and CharacterID: {CharID}", groupings.Key, rawCharacter.CharacterID);

                var actorOrdering = 0;
                foreach (var (rawSeiyuu, _) in creatorLookup)
                {
                    var actorIndex = actorOrdering++;
                    if (!existingCreators.TryGetValue(rawSeiyuu.SeiyuuID, out var creator))
                    {
                        creator = RepoFactory.AniDB_Creator.GetByCreatorID(rawSeiyuu.SeiyuuID) ?? new()
                        {
                            CreatorID = rawSeiyuu.SeiyuuID,
                            Type = CreatorType.Unknown,
                        };
                        if (creator.AniDB_CreatorID == 0)
                        {
                            creator.Name = rawSeiyuu.SeiyuuName;
                            creator.ImagePath = rawSeiyuu.PicName;
                            creatorsToSave.Add(creator);
                        }
                        else
                        {
                            var updated = false;
                            if (string.IsNullOrEmpty(creator.Name) && !string.IsNullOrEmpty(rawSeiyuu.SeiyuuName))
                            {
                                creator.Name = rawSeiyuu.SeiyuuName;
                                updated = true;
                            }
                            if (string.IsNullOrEmpty(creator.ImagePath) && !string.IsNullOrEmpty(rawSeiyuu.PicName))
                            {
                                creator.ImagePath = rawSeiyuu.PicName;
                                updated = true;
                            }
                            if (updated)
                                creatorsToSave.Add(creator);
                        }

                        if (settings.AniDb.DownloadCreators && creator.Type is CreatorType.Unknown)
                            creatorsToSchedule.Add(creator.CreatorID);
                        existingCreators[rawSeiyuu.SeiyuuID] = creator;
                    }

                    var creatorXref = existingCreatorXrefs.Contains((rawCharacter.CharacterID, rawSeiyuu.SeiyuuID))
                        ? existingCreatorXrefs[(rawCharacter.CharacterID, rawSeiyuu.SeiyuuID)].First()
                        : new AniDB_Anime_Character_Creator()
                        {
                            AnimeID = anime.AnimeID,
                            CharacterID = rawCharacter.CharacterID,
                            CreatorID = rawSeiyuu.SeiyuuID,
                        };
                    if (creatorXref.AniDB_Anime_Character_CreatorID == 0)
                    {
                        creatorXref.Ordering = actorIndex;
                        creatorXrefsToSave.Add(creatorXref);
                    }
                    else
                    {
                        var updated = false;
                        if (creatorXref.Ordering != actorIndex)
                        {
                            creatorXref.Ordering = actorIndex;
                            updated = true;
                        }
                        if (updated)
                            creatorXrefsToSave.Add(creatorXref);
                        creatorXrefsToKeep.Add(creatorXref.AniDB_Anime_Character_CreatorID);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Populate and Save Characters for {MainTitle} (Anime={AnimeID})", anime.MainTitle, anime.AnimeID);
            // If we continue we may be doing potential damage to otherwise existing entries, so abort for now.
            return;
        }

        var xrefsToDelete = existingXrefs
            .SelectMany(x => x)
            .ExceptBy(characterXrefsToKeep, x => x.AniDB_Anime_CharacterID)
            .ToList();
        var xrefsCreatorToDelete = existingCreatorXrefs
            .SelectMany(x => x)
            .ExceptBy(creatorXrefsToKeep, x => x.AniDB_Anime_Character_CreatorID)
            .ToList();
        var charactersToRemove = xrefsToDelete
            .Select(x => x.Character)
            .WhereNotNull()
            .Where(x => !x.GetRoles().Concat(characterXrefsToSave.Where(y => y.CharacterID == x.CharacterID)).ExceptBy(xrefsToDelete.Select(y => y.AniDB_Anime_CharacterID), y => y.AniDB_Anime_CharacterID).Any())
            .ToList();
        var creatorsToRemove = xrefsCreatorToDelete
            .Select(x => x.Creator)
            .WhereNotNull()
            .Where(x => x.Staff.Count == 0 && !x.Characters.Concat(creatorXrefsToSave.Where(y => y.CreatorID == x.CreatorID)).ExceptBy(xrefsCreatorToDelete.Select(y => y.AniDB_Anime_Character_CreatorID), y => y.AniDB_Anime_Character_CreatorID).Any())
            .ToList();

        try
        {
            RepoFactory.AniDB_Creator.Save(creatorsToSave);
            RepoFactory.AniDB_Creator.Delete(creatorsToRemove);

            RepoFactory.AniDB_Character.Save(charactersToSave);
            RepoFactory.AniDB_Character.Delete(charactersToRemove);

            RepoFactory.AniDB_Anime_Character.Save(characterXrefsToSave);
            RepoFactory.AniDB_Anime_Character.Delete(xrefsToDelete);

            RepoFactory.AniDB_Anime_Character_Creator.Save(creatorXrefsToSave);
            RepoFactory.AniDB_Anime_Character_Creator.Delete(xrefsCreatorToDelete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to save characters and creators for {MainTitle}", anime.MainTitle);
        }

        if (!skipCreatorScheduling)
            ScheduleCreators(creatorsToSchedule, anime.MainTitle);
    }

    public void CreateStaff(List<ResponseStaff> staffList, SVR_AniDB_Anime anime, bool skipCreatorScheduling = false)
    {
        if (staffList == null) return;

        var settings = _settingsProvider.GetSettings();
        var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;

        var existingCreators = new Dictionary<int, AniDB_Creator>();
        var existingXrefs = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(anime.AnimeID)
            .ToLookup(x => (x.AnimeID, x.CreatorID, x.Role));

        var creatorsToSchedule = new HashSet<int>();
        var creatorsToSave = new List<AniDB_Creator>();
        var creatorXrefsToKeep = new HashSet<int>();
        var creatorXrefsToSave = new List<AniDB_Anime_Staff>();
        try
        {
            var staffLookup = staffList.ToLookup(a => (a.AnimeID, a.CreatorID, a.CreatorType));
            foreach (var groupings in staffLookup.Where(a => a.Count() > 1))
                _logger.LogWarning("Anime had a duplicate staff listing for CreatorID: {CreatorID} and CreatorType: {CreatorType}", groupings.Key.CreatorID, groupings.Key.CreatorType);

            var staffOrdering = 0;
            foreach (var (rawStaff, _) in staffLookup)
            {
                var staffIndex = staffOrdering++;
                if (!existingCreators.TryGetValue(rawStaff.CreatorID, out var creator))
                {
                    creator = RepoFactory.AniDB_Creator.GetByCreatorID(rawStaff.CreatorID) ?? new()
                    {
                        CreatorID = rawStaff.CreatorID,
                        Type = CreatorType.Unknown,
                    };
                    if (creator.AniDB_CreatorID == 0)
                    {
                        creator.Name = rawStaff.CreatorName;
                        creatorsToSave.Add(creator);
                    }
                    else
                    {
                        var updated = false;
                        if (string.IsNullOrEmpty(creator.Name) && !string.IsNullOrEmpty(rawStaff.CreatorName))
                        {
                            creator.Name = rawStaff.CreatorName;
                            updated = true;
                        }
                        if (updated)
                            creatorsToSave.Add(creator);
                    }

                    if (settings.AniDb.DownloadCreators && creator.Type is CreatorType.Unknown)
                        creatorsToSchedule.Add(creator.CreatorID);
                    existingCreators[rawStaff.CreatorID] = creator;
                }

                var role = rawStaff.CreatorType;
                var roleType = role switch
                {
                    "Animation Work" when creator.Type is CreatorType.Company => CreatorRoleType.Studio,
                    "Work" when creator.Type is CreatorType.Company => CreatorRoleType.Studio,
                    "Original Work" => CreatorRoleType.SourceWork,
                    "Music" => CreatorRoleType.Music,
                    "Character Design" => CreatorRoleType.CharacterDesign,
                    "Direction" => CreatorRoleType.Director,
                    "Series Composition" => CreatorRoleType.SeriesComposer,
                    "Chief Animation Direction" => CreatorRoleType.Producer,
                    _ => CreatorRoleType.Staff
                };
                var staff = existingXrefs.Contains((anime.AnimeID, rawStaff.CreatorID, role))
                    ? existingXrefs[(anime.AnimeID, rawStaff.CreatorID, role)].First()
                    : new AniDB_Anime_Staff()
                    {
                        AnimeID = anime.AnimeID,
                        CreatorID = rawStaff.CreatorID,
                        Role = role,
                    };
                if (staff.AniDB_Anime_StaffID == 0)
                {
                    staff.Ordering = staffIndex;
                    staff.RoleType = roleType;
                    creatorXrefsToSave.Add(staff);
                }
                else
                {
                    var updated = false;
                    if (staff.Ordering != staffIndex)
                    {
                        staff.Ordering = staffIndex;
                        updated = true;
                    }
                    if (staff.RoleType != roleType)
                    {
                        staff.RoleType = roleType;
                        updated = true;
                    }
                    if (updated)
                        creatorXrefsToSave.Add(staff);
                    creatorXrefsToKeep.Add(staff.AniDB_Anime_StaffID);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Populate and Save Staff for {MainTitle}", anime.MainTitle);
            // If we continue we may be doing potential damage to otherwise existing entries, so abort for now.
            return;
        }
        var xrefsCreatorToDelete = existingXrefs
            .SelectMany(x => x)
            .ExceptBy(creatorXrefsToKeep, x => x.AniDB_Anime_StaffID)
            .ToList();
        var creatorsToRemove = xrefsCreatorToDelete
            .Select(x => x.Creator)
            .WhereNotNull()
            .Where(x => x.Characters.Count == 0 && !x.Staff.Concat(creatorXrefsToSave.Where(y => y.CreatorID == x.CreatorID)).ExceptBy(xrefsCreatorToDelete.Select(y => y.AniDB_Anime_StaffID), y => y.AniDB_Anime_StaffID).Any())
            .ToList();

        try
        {
            RepoFactory.AniDB_Creator.Save(creatorsToSave);
            RepoFactory.AniDB_Creator.Delete(creatorsToRemove);

            RepoFactory.AniDB_Anime_Staff.Save(creatorXrefsToSave);
            RepoFactory.AniDB_Anime_Staff.Delete(xrefsCreatorToDelete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Save Staff for {MainTitle}", anime.MainTitle);
        }

        if (!skipCreatorScheduling)
            ScheduleCreators(creatorsToSchedule, anime.MainTitle);
    }

    private async void ScheduleCreators(IEnumerable<int> creatorIDs, string mainTitle)
    {
        try
        {
            var creatorList = creatorIDs.ToList();
            if (creatorList.Count == 0) return;
            var scheduler = await _schedulerFactory.GetScheduler();
            _logger.LogInformation("Scheduling {Count} creators to be updated for {MainTitle}", creatorList.Count, mainTitle);
            foreach (var creatorId in creatorList)
                await scheduler.StartJob<GetAniDBCreatorJob>(c => c.CreatorID = creatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Schedule Creators for {MainTitle}", mainTitle);
        }
    }

    private static void CreateResources(List<ResponseResource> resources, SVR_AniDB_Anime anime)
    {
        if (resources == null)
        {
            return;
        }

        var malLinks = new List<CrossRef_AniDB_MAL>();
        foreach (var resource in resources)
        {
            int id;
            switch (resource.ResourceType)
            {
                case AniDB_ResourceLinkType.ANN:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.ANNID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.ALLCinema:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.AllCinemaID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.VNDB:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.VNDBID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Bangumi:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.BangumiID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.DotLain:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.LainID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Site_JP:
                    {
                        if (string.IsNullOrEmpty(anime.Site_JP))
                            anime.Site_JP = resource.ResourceID;
                        else
                            anime.Site_JP = string.Join("|", anime.Site_JP.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Append(resource.ResourceID).Distinct());
                        break;
                    }
                case AniDB_ResourceLinkType.Site_EN:
                    {
                        if (string.IsNullOrEmpty(anime.Site_EN))
                            anime.Site_EN = resource.ResourceID;
                        else
                            anime.Site_EN = string.Join("|", anime.Site_EN.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Append(resource.ResourceID).Distinct());
                        break;
                    }
                case AniDB_ResourceLinkType.Wiki_EN:
                    {
                        anime.Wikipedia_ID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Wiki_JP:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.WikipediaJP_ID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Syoboi:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.SyoboiID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Anison:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        anime.AnisonID = id;
                        break;
                    }
                case AniDB_ResourceLinkType.Crunchyroll:
                    {
                        anime.CrunchyrollID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.Funimation:
                    {
                        anime.FunimationID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.HiDive:
                    {
                        anime.HiDiveID = resource.ResourceID;
                        break;
                    }
                case AniDB_ResourceLinkType.MAL:
                    {
                        if (!int.TryParse(resource.ResourceID, out id))
                        {
                            break;
                        }

                        if (id == 0)
                        {
                            break;
                        }

                        if (RepoFactory.CrossRef_AniDB_MAL.GetByMALID(id).Any(a => a.AnimeID == anime.AnimeID))
                        {
                            continue;
                        }

                        var xref = new CrossRef_AniDB_MAL
                        {
                            AnimeID = anime.AnimeID,
                            CrossRefSource = (int)CrossRefSource.AniDB,
                            MALID = id,
                            StartEpisodeNumber = 1,
                            StartEpisodeType = 1
                        };

                        malLinks.Add(xref);
                        break;
                    }
            }
        }

        RepoFactory.CrossRef_AniDB_MAL.Save(malLinks);
    }

    private static void CreateRelations(List<ResponseRelation> relations, int animeID)
    {
        var existingRelations = RepoFactory.AniDB_Anime_Relation.GetByAnimeID(animeID)
            .ToLookup(a => a.RelatedAnimeID);
        var toSkip = new HashSet<int>();
        var toSave = new List<SVR_AniDB_Anime_Relation>();
        foreach (var raw in relations ?? [])
        {
            if (raw.AnimeID != animeID || raw.RelatedAnimeID <= 0)
                continue;

            var relation = existingRelations.Contains(raw.RelatedAnimeID) ? existingRelations[raw.RelatedAnimeID].FirstOrDefault() : new();
            if (relation.AniDB_Anime_RelationID is not 0)
                toSkip.Add(relation.AniDB_Anime_RelationID);

            relation.AnimeID = raw.AnimeID;
            relation.RelatedAnimeID = raw.RelatedAnimeID;
            relation.RelationType = raw.RelationType switch
            {
                RelationType.Prequel => "prequel",
                RelationType.Sequel => "sequel",
                RelationType.MainStory => "parent story",
                RelationType.SideStory => "side story",
                RelationType.FullStory => "full story",
                RelationType.Summary => "summary",
                RelationType.Other => "other",
                RelationType.AlternativeSetting => "alternative setting",
                RelationType.AlternativeVersion => "alternative version",
                RelationType.SameSetting => "same setting",
                RelationType.SharedCharacters => "character",
                _ => "other"
            };
            toSave.Add(relation);
        }

        var toRemove = existingRelations
            .SelectMany(l => l)
            .ExceptBy(toSkip, r => r.AniDB_Anime_RelationID)
            .ToList();
        RepoFactory.AniDB_Anime_Relation.Delete(toRemove);
        RepoFactory.AniDB_Anime_Relation.Save(toSave);
    }

    private static void CreateSimilarAnime(List<ResponseSimilar> similarList, int animeID)
    {
        if (similarList == null) return;

        var existingSimilar = RepoFactory.AniDB_Anime_Similar.GetByAnimeID(animeID)
            .ToLookup(a => a.SimilarAnimeID);
        var toKeep = new HashSet<int>();
        var toSave = new List<AniDB_Anime_Similar>();
        foreach (var raw in similarList)
        {
            if (raw.AnimeID != animeID || raw.Approval < 0 || raw.SimilarAnimeID <= 0 || raw.Total < 0)
                continue;

            var similar = existingSimilar.Contains(raw.SimilarAnimeID) ? existingSimilar[raw.SimilarAnimeID].FirstOrDefault() : new();
            if (similar.AniDB_Anime_SimilarID is not 0)
                toKeep.Add(similar.AniDB_Anime_SimilarID);

            similar.AnimeID = raw.AnimeID;
            similar.Approval = raw.Approval;
            similar.Total = raw.Total;
            similar.SimilarAnimeID = raw.SimilarAnimeID;
            toSave.Add(similar);
        }

        var toRemove = existingSimilar
            .SelectMany(l => l)
            .ExceptBy(toKeep, s => s.AniDB_Anime_SimilarID)
            .ToList();
        RepoFactory.AniDB_Anime_Similar.Delete(toRemove);
        RepoFactory.AniDB_Anime_Similar.Save(toSave);
    }
}
