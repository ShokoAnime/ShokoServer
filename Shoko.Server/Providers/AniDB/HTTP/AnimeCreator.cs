using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;

using AbstractAnimeType = Shoko.Abstractions.Metadata.Enums.AnimeType;
using AbstractEpisodeType = Shoko.Abstractions.Metadata.Enums.EpisodeType;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class AnimeCreator
{
    private readonly ILogger<AnimeCreator> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IQueueScheduler _scheduler;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly MetadataTextStore _textStore;
    private readonly MetadataCrossReferenceStore _crossReferences;
    private readonly ConcurrentDictionary<int, object> _updatingIDs = [];

    public AnimeCreator(
        ILogger<AnimeCreator> logger,
        ISettingsProvider settings,
        IQueueScheduler scheduler,
        IVideoReleaseService videoReleaseService,
        MetadataTextStore textStore,
        MetadataCrossReferenceStore crossReferences
    )
    {
        _logger = logger;
        _settingsProvider = settings;
        _scheduler = scheduler;
        _videoReleaseService = videoReleaseService;
        _textStore = textStore;
        _crossReferences = crossReferences;
    }


#pragma warning disable CS0618
    public async Task<(bool animeUpdated, bool titlesUpdated, bool descriptionUpdated, bool shouldUpdateFiles, Dictionary<AniDB_Episode, UpdateReason> episodeChanges)> CreateAnime(ResponseGetAnime response, AniDB_Anime anime, int relDepth)
    {
        _logger.LogTrace("Updating anime {AnimeID}", response?.Anime?.AnimeID);
        if (response?.Anime is null || response.Anime.AnimeID == 0) return (false, false, false, false, []);
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
            var texts = new AnimeTexts();
            var (episodesAddedOrRemoved, updatedEpisodes) = await CreateEpisodes(response.Episodes, anime, texts).ConfigureAwait(true);
            if (episodesAddedOrRemoved && !updated) updated = true;

            taskTimer.Stop();
            _logger.LogTrace("CreateEpisodes in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            var titlesUpdated = CreateTitles(response.Titles, anime, texts);
            updated = updated || titlesUpdated;
            shouldUpdateFiles = shouldUpdateFiles || titlesUpdated;
            taskTimer.Stop();
            _logger.LogTrace("CreateTitles in: {Time}", taskTimer.Elapsed);
            taskTimer.Restart();

            updated = CreateTags(response.Tags, anime, _textStore) || updated;
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

            CreateResources(response, anime.AnimeID);
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
            // The dates, type and episodes may all have changed.
            anime.ResetReleaseStatus();
            anime.ResetRegularAirDates();
            Monitor.Exit(lockObj);
            _updatingIDs.TryRemove(response.Anime.AnimeID, out _);
        }
    }
#pragma warning restore CS0618

    /// <summary>
    ///   Copies the anime's fields from AniDB's response onto the stored row,
    ///   stamping a new row's creation date.
    /// </summary>
    /// <param name="animeInfo">The anime as AniDB gave it.</param>
    /// <param name="anime">The stored row, new or existing.</param>
    /// <returns>Whether the anime, its description or its files' names changed.</returns>
    internal static (bool animeUpdated, bool descriptionUpdated, bool shouldUpdateFiles) PopulateAnime(ResponseAnime animeInfo, AniDB_Anime anime)
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

        if (anime.AnimeID != animeInfo.AnimeID)
        {
            anime.AnimeID = animeInfo.AnimeID;
            isUpdated = true;
            shouldUpdateFiles = true;
        }

        if (anime.AnimeType != (AbstractAnimeType)animeInfo.AnimeType)
        {
            anime.AnimeType = (AbstractAnimeType)animeInfo.AnimeType;
            isUpdated = true;
            shouldUpdateFiles = true;
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
            anime.ImageEnabled = 1;
        }

#pragma warning disable CS0618
        // Make sure these fields are set for new entries.
        if (isNew)
            anime.CreatedAt = anime.DateTimeUpdated = anime.DateTimeDescUpdated = DateTime.Now;
#pragma warning restore CS0618

        return (isUpdated, descriptionUpdated, shouldUpdateFiles);
    }

    /// <summary>
    ///   Makes the row of an episode not stored before.
    /// </summary>
    /// <param name="rawEpisode">The episode as AniDB gave it.</param>
    /// <param name="createdAt">When the row is first stored.</param>
    /// <returns>The new row.</returns>
    internal static AniDB_Episode NewEpisode(ResponseEpisode rawEpisode, DateTime createdAt) => new()
    {
        AirDate = AniDBExtensions.GetAniDBDateAsSeconds(rawEpisode.AirDate),
        AnimeID = rawEpisode.AnimeID,
        CreatedAt = createdAt,
        DateTimeUpdated = rawEpisode.LastUpdated,
        EpisodeID = rawEpisode.EpisodeID,
        EpisodeNumber = rawEpisode.EpisodeNumber,
        EpisodeType = (AbstractEpisodeType)rawEpisode.EpisodeType,
        LengthSeconds = rawEpisode.LengthSeconds,
        Rating = rawEpisode.Rating.ToString(CultureInfo.InvariantCulture),
        Votes = rawEpisode.Votes.ToString(CultureInfo.InvariantCulture),
        Description = rawEpisode.Description ?? string.Empty,
    };

    private async Task<(bool, Dictionary<AniDB_Episode, UpdateReason>)> CreateEpisodes(List<ResponseEpisode> rawEpisodeList, AniDB_Anime anime, AnimeTexts texts)
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
            .WhereNotNull()
            .ToList();
        var currentAniDBEpisodes = epsBelongingToThisAnime.Values
            .Concat(epsBelongingToOtherAnime)
            .ToDictionary(a => a.EpisodeID);
        var epsToRemove = currentAniDBEpisodes.Values
            .Where(a => !epIDs.Contains(a.EpisodeID))
            .ToList();
        var epsToSave = new List<AniDB_Episode>();
        var episodeEventsToEmit = new Dictionary<AniDB_Episode, UpdateReason>();

        foreach (var rawEpisode in rawEpisodeList)
        {
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

                if (episode.EpisodeType != (AbstractEpisodeType)rawEpisode.EpisodeType)
                {
                    episode.EpisodeType = (AbstractEpisodeType)rawEpisode.EpisodeType;
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
                episode = NewEpisode(rawEpisode, DateTime.Now);
            }

            // Work out the titles to store, leaving out the generic one with
            // the episode's own number, which is made up when read.
            var episodeID = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, rawEpisode.EpisodeID.ToString(CultureInfo.InvariantCulture));
            var currentTitles = _textStore.GetTitles(episodeID, MetadataSource.AniDB);
            var newTitles = AnidbTextListing.PlanEpisodeTitles(
                currentTitles,
                rawEpisode.Titles.Select(rawtitle => new AnidbTextListing.ListedTitle(rawtitle.Language, TitleType.None, rawtitle.Title)),
                (AbstractEpisodeType)rawEpisode.EpisodeType,
                rawEpisode.EpisodeNumber
            );
            texts.Episodes.Add((episodeID, newTitles));
            if (newTitles.Any(title => !currentTitles.Any(current => current.Language == title.Language && current.Value == title.Value)) && !episodeEventsToEmit.ContainsKey(episode))
                episodeEventsToEmit[episode] = UpdateReason.Updated;

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
        var shokoEpisodesToRemove = new List<AnimeEpisode>();
        var shokoEpisodesToSave = new List<AnimeEpisode>();
        var shokoSeriesDict = new Dictionary<int, AnimeSeries>();
        var storedReleasesToRemove = new List<StoredReleaseInfo>();
        var xrefsToRemove = new List<CrossRef_File_Episode>();
        var videosToRefetch = new List<VideoLocal>();
        var episodeLinksToRemove = new List<CrossRef_AniDB_Metadata_Episode>();
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
                    shokoSeriesDict.TryAdd(shokoEpisode.AnimeSeriesID, actualSeries = RepoFactory.AnimeSeries.GetByID(shokoEpisode.AnimeSeriesID)!)
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
                .WhereNotNull()
                .ToList();
            var storedReleases = RepoFactory.StoredReleaseInfo.GetByAnidbEpisodeID(episode.EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            storedReleasesToRemove.AddRange(storedReleases);
            episodeLinksToRemove.AddRange(RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByAnidbEpisodeID(episode.EpisodeID));
        }
        shokoSeriesDict.Clear();

        // Remove any existing links to the episodes that will be removed.
        foreach (var episode in epsToRemove)
        {
            texts.RemovedEpisodes.Add(((IMetadata)episode).ID);
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
            if (shokoEpisode != null)
                shokoEpisodesToRemove.Add(shokoEpisode);
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episode.EpisodeID);
            var videos = xrefs
                .Select(xref => RepoFactory.VideoLocal.GetByEd2kAndSize(xref.Hash, xref.FileSize))
                .WhereNotNull()
                .ToList();
            var databaseReleases = RepoFactory.StoredReleaseInfo.GetByAnidbEpisodeID(episode.EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            storedReleasesToRemove.AddRange(databaseReleases);
            episodeLinksToRemove.AddRange(RepoFactory.CrossRef_AniDB_Metadata_Episode.GetByAnidbEpisodeID(episode.EpisodeID));
        }

        RepoFactory.StoredReleaseInfo.Delete(storedReleasesToRemove.DistinctBy(a => a.StoredReleaseInfoID).ToList());
        RepoFactory.AniDB_Episode.Save(epsToSave);
        RepoFactory.AniDB_Episode.Delete(epsToRemove);
        RepoFactory.AnimeEpisode.Save(shokoEpisodesToSave);
        RepoFactory.AnimeEpisode.Delete(shokoEpisodesToRemove);
        RepoFactory.CrossRef_File_Episode.Delete(xrefsToRemove);
        RemoveEpisodeLinks(episodeLinksToRemove);

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

    /// <summary>
    ///   Removes the links the removed or moved episodes had on every source,
    ///   closing the gaps they leave among the anime's links.
    /// </summary>
    /// <param name="links">The links.</param>
    private void RemoveEpisodeLinks(IReadOnlyList<CrossRef_AniDB_Metadata_Episode> links)
    {
        if (links.Count is 0)
            return;

        using var changes = _crossReferences.BeginChanges();
        foreach (var link in links.DistinctBy(link => link.CrossRef_AniDB_Metadata_EpisodeID))
            _crossReferences.RemoveEpisodeLink(link.Source, link.AnidbAnimeID, link.AnidbEpisodeID, ((IMetadataCrossReference)link).ProviderID);
    }

    /// <summary>
    ///   Stores the titles AniDB lists for an anime, together with the titles
    ///   of its episodes worked out before, in one transaction.
    /// </summary>
    /// <param name="titles">The anime's titles, as AniDB lists them, or <c>null</c> to leave them alone.</param>
    /// <param name="anime">The anime.</param>
    /// <param name="texts">The episodes' titles, and the episodes that went.</param>
    /// <returns>Whether the anime's titles changed.</returns>
    private bool CreateTitles(List<ResponseTitle>? titles, AniDB_Anime anime, AnimeTexts texts)
        => StoreTitles(_textStore, titles, anime, texts.Episodes, texts.RemovedEpisodes);

    /// <summary>
    ///   Stores the titles AniDB lists for an anime and for its episodes, and
    ///   removes the texts of the episodes that went, in one transaction.
    /// </summary>
    /// <param name="textStore">The text store.</param>
    /// <param name="titles">The anime's titles, as AniDB lists them, or <c>null</c> to leave them alone.</param>
    /// <param name="anime">The anime.</param>
    /// <param name="episodes">The episodes AniDB lists, and the titles to store for each.</param>
    /// <param name="removedEpisodes">The episodes that went.</param>
    /// <returns>Whether the anime's titles changed.</returns>
    internal static bool StoreTitles(
        MetadataTextStore textStore,
        List<ResponseTitle>? titles,
        AniDB_Anime anime,
        IReadOnlyList<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles)> episodes,
        IReadOnlyCollection<MetadataGuid> removedEpisodes
    )
    {
        var animeID = ((IMetadata)anime).ID;
        var entries = new List<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles, IReadOnlyList<IText> Overviews)>();
        if (titles is not null)
        {
            var planned = AnidbTextListing.PlanAnimeTitles(
                textStore.GetTitles(animeID, MetadataSource.AniDB),
                titles.Where(title => title is not null).Select(title => new AnidbTextListing.ListedTitle(title.Language, title.TitleType, title.Title))
            );
            entries.Add((animeID, planned, []));
        }

        entries.AddRange(episodes.DistinctBy(episode => episode.Entry).Select(episode => (episode.Entry, episode.Titles, (IReadOnlyList<IText>)[])));
        if (entries.Count is 0 && removedEpisodes.Count is 0)
            return false;

        var changed = textStore.WriteWithTexts(entries, removedEpisodes, _ => []);
        return titles is not null && changed.Contains(animeID);
    }

    /// <summary>
    ///   The titles worked out for an anime's episodes, stored together with
    ///   the anime's own.
    /// </summary>
    private sealed class AnimeTexts
    {
        /// <summary>
        ///   The episodes AniDB lists, and the titles to store for each.
        /// </summary>
        public List<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles)> Episodes { get; } = [];

        /// <summary>
        ///   The episodes that went, whose texts go with them.
        /// </summary>
        public List<MetadataGuid> RemovedEpisodes { get; } = [];
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

    /// <summary>
    ///   Finds the tag AniDB lists, or creates it, and fills it in unless the
    ///   stored one is newer.
    /// </summary>
    /// <param name="rawTag">The tag as AniDB lists it.</param>
    /// <param name="textStore">Removes the core's names of the tags that go.</param>
    /// <param name="renames">Collects the core's name for each tag filled in, or <c>null</c> when it keeps its own.</param>
    /// <returns>The tag, not yet saved.</returns>
    private static AniDB_Tag FindOrCreateTag(ResponseTag rawTag, MetadataTextStore textStore, Dictionary<int, string?> renames)
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

            // Delete the obsolete tag(s), and while we're at it, clean up
            // other unreferenced tags. The core's names for them go too.
            var tagsToDelete = existingTags
                .Concat(RepoFactory.AniDB_Tag.GetAll().Where(a => !RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID).Any()))
                .DistinctBy(a => a.AniDB_TagID)
                .ToList();
            RepoFactory.AniDB_Tag.Delete(tagsToDelete);
            foreach (var deletedTag in tagsToDelete.Where(a => a.TagID != rawTag.TagID))
                textStore.RemoveEntry(((IMetadata)deletedTag).ID, MetadataSource.Shoko);

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
        renames[rawTag.TagID] = nameOverride;
        tag.TagDescription = rawTag.TagDescription ?? string.Empty;
        tag.GlobalSpoiler = rawTag.GlobalSpoiler;
        tag.Verified = rawTag.Verified;
        tag.LastUpdated = rawTag.LastUpdated;

        return tag;
    }

    /// <summary>
    ///   Stores the tags AniDB lists for an anime, with the core's names for
    ///   the tags it renames, and links them to the anime.
    /// </summary>
    /// <param name="tags">The tags, as AniDB lists them, or <c>null</c> to leave them alone.</param>
    /// <param name="anime">The anime.</param>
    /// <param name="textStore">Stores the core's names for the tags it renames.</param>
    /// <returns>Whether the anime's links to its tags changed.</returns>
    public static bool CreateTags(List<ResponseTag>? tags, AniDB_Anime anime, MetadataTextStore textStore)
    {
        if (tags == null)
            return false;

        // find all the current links, and then later remove the ones that are no longer relevant
        var allTags = string.Empty;
        var tagsToSave = new List<AniDB_Tag>();
        var xrefsToSave = new List<AniDB_Anime_Tag>();
        var currentTags = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(anime.AnimeID);
        var newTagIDs = new HashSet<int>();
        var renames = new Dictionary<int, string?>();
        foreach (var rawtag in tags)
        {
            if (rawtag.TagID <= 0 || string.IsNullOrEmpty(rawtag.TagName))
                continue;

            // The name the core gives a tag is stored before the tag is used,
            // as the tag's name is read from it.
            var tag = FindOrCreateTag(rawtag, textStore, renames);
            if (renames.Remove(tag.TagID, out var rename))
                SetTagRename(tag, rename, textStore);

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
        anime.ResetSourceMaterial();

        return xrefsToSave.Count > 0 || xrefsToDelete.Count > 0;
    }

    /// <summary>
    ///   Stores the name the core gives a tag, as the core's overall
    ///   preferred title of the tag, or removes it.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="rename">The name, or <c>null</c> when the tag keeps its own.</param>
    /// <param name="textStore">The text store.</param>
    private static void SetTagRename(AniDB_Tag tag, string? rename, MetadataTextStore textStore)
        => textStore.SetOverallTitle(
            ((IMetadata)tag).ID,
            MetadataSource.Shoko,
            rename is null
                ? null
                : new TitleStub
                {
                    Source = MetadataSource.Shoko,
                    Language = TitleLanguage.English,
                    LanguageCode = "en",
                    Value = rename,
                    Type = TitleType.Main,
                }
        );

    public void CreateCharacters(List<ResponseCharacter> chars, AniDB_Anime anime, bool skipCreatorScheduling = false)
    {
        if (chars == null) return;

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
            .Where(x => !RepoFactory.AniDB_Anime_Character.GetByCharacterID(x.CharacterID).Concat(characterXrefsToSave.Where(y => y.CharacterID == x.CharacterID)).ExceptBy(xrefsToDelete.Select(y => y.AniDB_Anime_CharacterID), y => y.AniDB_Anime_CharacterID).Any())
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

    public void CreateStaff(List<ResponseStaff> staffList, AniDB_Anime anime, bool skipCreatorScheduling = false)
    {
        if (staffList == null) return;

        var settings = _settingsProvider.GetSettings();

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
            _logger.LogInformation("Scheduling {Count} creators to be updated for {MainTitle}", creatorList.Count, mainTitle);
            foreach (var creatorId in creatorList)
                await _scheduler.StartJob<GetAniDBCreatorJob>(c => c.CreatorID = creatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to Schedule Creators for {MainTitle}", mainTitle);
        }
    }

    /// <summary>
    ///   Stores every resource of an anime and of its episodes, replacing the
    ///   rows kept from the last update, and links the anime to its
    ///   MyAnimeList entries.
    /// </summary>
    /// <param name="response">The parsed anime.</param>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>How many rows were saved and how many were deleted.</returns>
    internal static (int Saved, int Deleted) CreateResources(ResponseGetAnime response, int animeID)
    {
        var wanted = (response.Resources ?? [])
            .Concat((response.Episodes ?? []).SelectMany(episode => episode.Resources))
            .ToList();
        var (toSave, toDelete) = DiffResources(RepoFactory.AniDB_Resource.GetAllByAnimeID(animeID), wanted, animeID);
        if (toDelete.Count > 0)
            RepoFactory.AniDB_Resource.Delete(toDelete);
        if (toSave.Count > 0)
            RepoFactory.AniDB_Resource.Save(toSave);

        var malLinks = new List<CrossRef_AniDB_MAL>();
        foreach (var resource in response.Resources ?? [])
        {
            if (resource.ResourceType is not ResourceLinkType.MAL || resource.Identifiers.Count is 0)
                continue;
            if (!int.TryParse(resource.Identifiers[0], out var malID) || malID <= 0)
                continue;
            if (RepoFactory.CrossRef_AniDB_MAL.GetByMALID(malID).Any(a => a.AnimeID == animeID) || malLinks.Any(a => a.MALID == malID))
                continue;

            malLinks.Add(new() { AnimeID = animeID, MALID = malID });
        }

        if (malLinks.Count > 0)
            RepoFactory.CrossRef_AniDB_MAL.Save(malLinks);

        return (toSave.Count, toDelete.Count);
    }

    /// <summary>
    ///   Works out which resource rows to save and which to delete so the
    ///   stored rows match the parsed ones, keeping the rows that did not
    ///   change.
    /// </summary>
    /// <param name="existing">The rows stored for the anime and its episodes.</param>
    /// <param name="wanted">The parsed resources of the anime and its episodes.</param>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The new or changed rows, and the rows no longer wanted.</returns>
    internal static (List<AniDB_Resource> ToSave, List<AniDB_Resource> ToDelete) DiffResources(IReadOnlyList<AniDB_Resource> existing, IReadOnlyList<ResponseResource> wanted, int animeID)
    {
        var existingByPlace = existing
            .GroupBy(row => (row.EpisodeID, row.Ordering))
            .ToDictionary(group => group.Key, group => group.ToList());
        var toSave = new List<AniDB_Resource>();
        var toDelete = new List<AniDB_Resource>();
        foreach (var resource in wanted)
        {
            var row = new AniDB_Resource
            {
                AnimeID = animeID,
                EpisodeID = resource.EpisodeID,
                ResourceType = resource.ResourceType,
                Ordering = resource.Ordering,
                Identifiers = [.. resource.Identifiers],
                Urls = [.. resource.Urls],
            };
            if (existingByPlace.Remove((row.EpisodeID, row.Ordering), out var rows))
            {
                var kept = rows[0];
                toDelete.AddRange(rows.Skip(1));
                if (kept.IsSameAs(row))
                    continue;

                kept.AnimeID = row.AnimeID;
                kept.ResourceType = row.ResourceType;
                kept.Identifiers = row.Identifiers;
                kept.Urls = row.Urls;
                toSave.Add(kept);
                continue;
            }

            toSave.Add(row);
        }

        toDelete.AddRange(existingByPlace.Values.SelectMany(rows => rows));
        return (toSave, toDelete);
    }

    private static void CreateRelations(List<ResponseRelation> relations, int animeID)
    {
        var existingRelations = RepoFactory.AniDB_Anime_Relation.GetByAnimeID(animeID)
            .ToLookup(a => a.RelatedAnimeID);
        var toSkip = new HashSet<int>();
        var toSave = new List<AniDB_Anime_Relation>();
        foreach (var raw in relations ?? [])
        {
            if (raw.AnimeID != animeID || raw.RelatedAnimeID <= 0)
                continue;

            var relation = existingRelations.Contains(raw.RelatedAnimeID) ? existingRelations[raw.RelatedAnimeID].FirstOrDefault()! : new();
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
            relation.Verified = raw.RelationType switch
            {
                RelationType.AlternativeSetting or RelationType.AlternativeVersion => raw.Verified ?? false,
                _ => true
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

    internal static void CreateSimilarAnime(List<ResponseSimilar> similarList, int animeID)
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

            var similar = existingSimilar.Contains(raw.SimilarAnimeID) ? existingSimilar[raw.SimilarAnimeID].FirstOrDefault()! : new();
            if (similar.AniDB_Anime_SimilarID is not 0)
                toKeep.Add(similar.AniDB_Anime_SimilarID);

            similar.AnimeID = raw.AnimeID;
            similar.Approval = raw.Approval;
            similar.Total = raw.Total;
            similar.SimilarAnimeID = raw.SimilarAnimeID;
            similar.Ordering = raw.Ordering;
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
