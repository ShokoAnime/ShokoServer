using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ImageMagick;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NHibernate;
using NHibernate.Exceptions;
using NLog;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
#pragma warning disable CA2012
namespace Shoko.Server.Databases;

public class DatabaseFixes
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static Tuple<bool, string> NoOperation(object connection) { return new Tuple<bool, string>(true, null); }

    public static void UpdateAllStats()
    {
        var scheduler = ISystemService.StaticServices.GetRequiredService<IQueueScheduler>();
        Task.WhenAll(RepoFactory.AnimeSeries.GetAll().Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID))).GetAwaiter()
            .GetResult();
    }

    public static void MigrateGroupFilterToFilterPreset()
    {
        var legacyConverter = ISystemService.StaticServices.GetRequiredService<LegacyFilterConverter>();
        using var session = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        var groupFilters = session.CreateSQLQuery(
                "SELECT GroupFilterID, " +
                "ParentGroupFilterID, " +
                "GroupFilterName, " +
                "ApplyToSeries, " +
                "BaseCondition, " +
                "Locked, " +
                "FilterType, " +
                "InvisibleInClients, " +
                "GroupConditions, " +
                "SortingCriteria " +
                "FROM GroupFilter")
            .AddScalar("GroupFilterID", NHibernateUtil.Int32)
            .AddScalar("ParentGroupFilterID", NHibernateUtil.Int32)
            .AddScalar("GroupFilterName", NHibernateUtil.String)
            .AddScalar("ApplyToSeries", NHibernateUtil.Int32)
            .AddScalar("BaseCondition", NHibernateUtil.Int32)
            .AddScalar("Locked", NHibernateUtil.Int32)
            .AddScalar("FilterType", NHibernateUtil.Int32)
            .AddScalar("InvisibleInClients", NHibernateUtil.Int32)
            .AddScalar("GroupConditions", NHibernateUtil.StringClob)
            .AddScalar("SortingCriteria", NHibernateUtil.String)
            .List();

        var filters = new Dictionary<CL_GroupFilter, List<CL_GroupFilterCondition>>();
        foreach (var item in groupFilters)
        {
            var fields = (object[])item;
            var filter = new CL_GroupFilter
            {
                GroupFilterID = (int)fields[0],
                ParentGroupFilterID = (int?)fields[1],
                GroupFilterName = (string)fields[2],
                ApplyToSeries = (int)fields[3],
                BaseCondition = (int)fields[4],
                Locked = (int?)fields[5],
                FilterType = (int)fields[6],
                InvisibleInClients = (int)fields[7]
            };
            var conditions = JsonConvert.DeserializeObject<List<CL_GroupFilterCondition>>((string)fields[8]);
            filters[filter] = conditions;
        }

        var idMappings = new Dictionary<int, int>();
        // first, do the ones with no parent
        foreach (var key in filters.Keys.Where(a => a.ParentGroupFilterID is null).OrderBy(a => a.GroupFilterID))
        {
            var filter = legacyConverter.FromLegacy(key, filters[key]);
            RepoFactory.FilterPreset.Save(filter);
            idMappings[key.GroupFilterID] = filter.FilterPresetID;
        }

        var filtersToProcess = filters.Keys.Where(a => !idMappings.ContainsKey(a.GroupFilterID) && idMappings.ContainsKey(a.ParentGroupFilterID.Value))
            .ToList();
        while (filtersToProcess.Count > 0)
        {
            foreach (var key in filtersToProcess)
            {
                var filter = legacyConverter.FromLegacy(key, filters[key]);
                filter.ParentFilterPresetID = idMappings[key.ParentGroupFilterID.Value];
                RepoFactory.FilterPreset.Save(filter);
                idMappings[key.GroupFilterID] = filter.FilterPresetID;
            }

            filtersToProcess = filters.Keys.Where(a => !idMappings.ContainsKey(a.GroupFilterID) && idMappings.ContainsKey(a.ParentGroupFilterID.Value))
                .ToList();
        }
    }

    public static void DropGroupFilter()
    {
        using var session = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        session.CreateSQLQuery("DROP TABLE GroupFilter; DROP TABLE GroupFilterCondition").ExecuteUpdate();
    }

    public static void DeleteSeriesUsersWithoutSeries()
    {
        //DB Fix Series not deleting series_user
        var list = new HashSet<int>(RepoFactory.AnimeSeries.Cache.GetAllKeys());
        RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.Cache.GetAll()
            .Where(a => !list.Contains(a.AnimeSeriesID))
            .ToList());
    }

    public static void RefreshAniDBInfoFromXML()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var i = 0;
        var list = RepoFactory.AniDB_Episode.GetAll().Where(a => string.IsNullOrEmpty(a.Description))
            .Select(a => a.AnimeID).Distinct().ToList();

        var anidbService = ISystemService.StaticServices.GetRequiredService<IAnidbService>();
        foreach (var animeID in list)
        {
            if (i % 10 == 0)
            {
                systemService.StartupMessage = $"Database - Validating - Populating AniDB Info from Cache {i}/{list.Count}...";
            }

            i++;
            try
            {
                anidbService.RefreshAnimeByID(animeID, AnidbRefreshMethod.Cache | AnidbRefreshMethod.SkipSupplementaryUpdate).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _logger.Error(
                    $"There was an error Populating AniDB Info for AniDB_Anime {animeID}, Update the Series' AniDB Info for a full stack: {e.Message}");
            }
        }
    }

    public static void RefreshAnimeSeriesUserStats()
    {
        var userDataService = (UserDataService)ISystemService.StaticServices.GetRequiredService<IUserDataService>();
        foreach (var series in RepoFactory.AnimeSeries.GetAll())
            userDataService.UpdateWatchedStats(series, series.AllAnimeEpisodes);
    }

    public static void MigrateAniDB_AnimeUpdates()
    {
        var updates = RepoFactory.AniDB_Anime.GetAll()
            .Select(anime => new AniDB_AnimeUpdate
            {
                AnimeID = anime.AnimeID,
                UpdatedAt = anime.DateTimeUpdated,
            })
            .ToList();

        RepoFactory.AniDB_AnimeUpdate.Save(updates);
    }

    public static void PopulateTagWeight()
    {
        try
        {
            foreach (var tag in RepoFactory.AniDB_Anime_Tag.GetAll())
            {
                tag.Weight = 0;
                RepoFactory.AniDB_Anime_Tag.Save(tag);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Could not PopulateTagWeight: " + ex);
        }
    }

    public static void EnsureNoOrphanedGroupsOrSeries()
    {
        var emptyGroups = RepoFactory.AnimeGroup.GetAll().Where(a => a.AllSeries.Count == 0).ToArray();
        RepoFactory.AnimeGroup.Delete(emptyGroups);
        var orphanedSeries = RepoFactory.AnimeSeries.GetAll().Where(a => a.AnimeGroupID == 0 || a.AnimeGroup is null).ToArray();
        var groupCreator = ISystemService.StaticServices.GetRequiredService<AnimeGroupCreator>();
        using var session = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        foreach (var series in orphanedSeries)
        {
            try
            {
                var group = groupCreator.GetOrCreateSingleGroupForSeries(series);
                series.AnimeGroupID = group.AnimeGroupID;
                RepoFactory.AnimeSeries.Save(series, false);
            }
            catch (Exception e)
            {
                var name = "";
                try
                {
                    name = series.Title;
                }
                catch
                {
                    // ignore
                }

                _logger.Error(e,
                    $"Unable to update group for orphaned series: AniDB ID: {series.AniDB_ID} SeriesID: {series.AnimeSeriesID} Series Name: {name}");
            }
        }
    }

    public static void FixWatchDates()
    {
        // Reset incorrectly parsed watch dates for anidb file.
        _logger.Debug($"Looking for faulty anidb file entries...");
        _logger.Debug($"Looking for faulty episode user records...");
        // Fetch every episode user record stored to both remove orphaned records and to make sure the watch date is correct.
        var userDict = RepoFactory.JMMUser.GetAll().ToDictionary(user => user.JMMUserID);
        var episodeDict = RepoFactory.AnimeEpisode.GetAll()
            .ToDictionary(episode => episode.AnimeEpisodeID, episode => episode.VideoLocals);
        var episodesURsToSave = new List<AnimeEpisode_User>();
        var episodeURsToRemove = new List<AnimeEpisode_User>();
        foreach (var episodeUserRecord in RepoFactory.AnimeEpisode_User.GetAll())
        {
            // Remove any unknown episode user records.
            if (!episodeDict.ContainsKey(episodeUserRecord.AnimeEpisodeID) ||
                !userDict.ContainsKey(episodeUserRecord.JMMUserID))
            {
                episodeURsToRemove.Add(episodeUserRecord);
                continue;
            }

            // Fetch the file user record for when a file for the episode was last watched.
            var fileUserRecord = episodeDict[episodeUserRecord.AnimeEpisodeID]
                .Select(file => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(episodeUserRecord.JMMUserID, file.VideoLocalID))
                .WhereNotNull()
                .OrderByDescending(record => record.LastUpdated)
                .FirstOrDefault(record => record.WatchedDate.HasValue);
            if (fileUserRecord != null)
            {
                // Check if the episode user record contains the same date and only update it if it does not.
                if (!episodeUserRecord.WatchedDate.HasValue ||
                    !episodeUserRecord.WatchedDate.Value.Equals(fileUserRecord.WatchedDate.Value))
                {
                    episodeUserRecord.WatchedDate = fileUserRecord.WatchedDate;
                    if (episodeUserRecord.WatchedCount == 0)
                    {
                        episodeUserRecord.WatchedCount++;
                    }

                    episodesURsToSave.Add(episodeUserRecord);
                }
            }
            // We couldn't find a watched date for any of the files, so make sure the episode user record is also marked as unwatched.
            else if (episodeUserRecord.WatchedDate.HasValue)
            {
                episodeUserRecord.WatchedDate = null;
                episodesURsToSave.Add(episodeUserRecord);
            }
        }

        _logger.Debug($"Found {episodesURsToSave.Count} episode user records to fix and {episodeURsToRemove.Count} orphaned records.");
        RepoFactory.AnimeEpisode_User.Delete(episodeURsToRemove);
        RepoFactory.AnimeEpisode_User.Save(episodesURsToSave);
        _logger.Debug($"Updating series user records and series stats.");
        // Update all the series and groups to use the new watch dates.
        var seriesList = episodesURsToSave
            .GroupBy(record => record.AnimeSeriesID)
            .Select(records => (RepoFactory.AnimeSeries.GetByID(records.Key),
                records.Select(record => record.JMMUserID).Distinct())).ToList();
        var seriesService = ISystemService.StaticServices.GetRequiredService<AnimeSeriesService>();
        foreach (var (series, userIDs) in seriesList)
        {
            // No idea why we would have episode entries for a deleted series, but just in case.
            if (series is null)
            {
                continue;
            }

            // Update the timestamp for when an episode for the series was last partially or fully watched.
            foreach (var userID in userIDs)
            {
                var seriesUserRecord = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID)
                    ?? new() { JMMUserID = userID, AnimeSeriesID = series.AnimeSeriesID };
                seriesUserRecord.LastEpisodeUpdate = seriesUserRecord.LastUpdated = DateTime.Now;
                _logger.Debug(
                    $"Updating series user contract for user \"{userDict[seriesUserRecord.JMMUserID].Username}\". (UserID={seriesUserRecord.JMMUserID}, SeriesID={seriesUserRecord.AnimeSeriesID})");
                RepoFactory.AnimeSeries_User.Save(seriesUserRecord);
            }

            // Update the rest of the stats for the series.
            seriesService.UpdateStats(series, true, true);
        }

        var groupService = ISystemService.StaticServices.GetRequiredService<AnimeGroupService>();
        var groups = seriesList.Select(a => a.Item1.AnimeGroup).WhereNotNull().DistinctBy(a => a.AnimeGroupID);
        foreach (var group in groups)
        {
            groupService.UpdateStatsFromTopLevel(group, true, true);
        }
    }

    public static void FixTagParentIDsAndNameOverrides()
    {
        var xmlUtils = ISystemService.StaticServices.GetRequiredService<HttpXmlUtils>();
        var animeParser = ISystemService.StaticServices.GetRequiredService<HttpAnimeParser>();
        var animeList = RepoFactory.AniDB_Anime.GetAll();
        _logger.Info($"Updating anidb tags for {animeList.Count} local anidb anime entries...");

        var count = 0;
        foreach (var anime in animeList)
        {
            if (++count % 10 == 0)
                _logger.Info($"Updating tags for local anidb anime entries... ({count}/{animeList.Count})");

            var xml = xmlUtils.LoadAnimeHTTPFromFile(anime.AnimeID).Result;
            if (string.IsNullOrEmpty(xml))
            {
                _logger.Warn($"Unable to load cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                continue;
            }

            ResponseGetAnime response;
            try
            {
                response = animeParser.Parse(anime.AnimeID, xml);
                if (response is null) throw new NullReferenceException(nameof(response));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                continue;
            }

            AnimeCreator.CreateTags(response.Tags, anime);
            RepoFactory.AniDB_Anime.Save(anime);
        }

        // One last time, clean up any unreferenced tags after we've processed
        // all the tags and their cross-references.
        var tagsToDelete = RepoFactory.AniDB_Tag.GetAll()
            .Where(a => RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID).Count is 0)
            .ToList();
        RepoFactory.AniDB_Tag.Delete(tagsToDelete);

        _logger.Info($"Done updating anidb tags for {animeList.Count} anidb anime entries.");
    }

    public static void FixAnimeSourceLinks()
    {
        var animeToSave = new HashSet<AniDB_Anime>();
        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            if (!string.IsNullOrEmpty(anime.Site_JP))
            {
                animeToSave.Add(anime);
                anime.Site_JP = string.Join("|", anime.Site_JP.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());
            }
            if (!string.IsNullOrEmpty(anime.Site_EN))
            {
                animeToSave.Add(anime);
                anime.Site_EN = string.Join("|", anime.Site_EN.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());
            }
        }

        _logger.Trace($"Found {animeToSave.Count} anime with faulty source links. Updating…");

        RepoFactory.AniDB_Anime.Save(animeToSave);

        _logger.Trace($"Updated {animeToSave.Count} anime with faulty source links.");
    }

    public static void FixEpisodeDateTimeUpdated()
    {
        var xmlUtils = ISystemService.StaticServices.GetRequiredService<HttpXmlUtils>();
        var animeParser = ISystemService.StaticServices.GetRequiredService<HttpAnimeParser>();
        var anidbService = ISystemService.StaticServices.GetRequiredService<IAnidbService>();
        var anidbAnimeDict = RepoFactory.AniDB_Anime.GetAll()
            .ToDictionary(an => an.AnimeID);
        var anidbEpisodeDict = RepoFactory.AniDB_Episode.GetAll()
            .ToDictionary(ep => ep.EpisodeID);
        var anidbAnimeIDs = anidbEpisodeDict.Values
            .GroupBy(ep => ep.AnimeID)
            .Where(list => anidbAnimeDict.ContainsKey(list.Key))
            .ToDictionary(list => anidbAnimeDict[list.Key], list => list.ToList());
        // This list will _hopefully_ initially be an empty…
        var episodesToSave = anidbEpisodeDict.Values
            .Where(ep => !anidbAnimeDict.ContainsKey(ep.AnimeID))
            .ToList();
        var animeToUpdateSet = anidbEpisodeDict.Values
            .Where(ep => !anidbAnimeDict.ContainsKey(ep.AnimeID))
            .Select(ep => ep.AnimeID)
            .Distinct()
            .ToHashSet();

        _logger.Info($"Updating last updated episode timestamps for {anidbAnimeIDs.Count} local anidb anime entries...");

        // …but if we do have any, then reset their timestamp now.
        foreach (var faultyEpisode in episodesToSave)
            faultyEpisode.DateTimeUpdated = DateTime.UnixEpoch;

        var faultyCount = episodesToSave.Count;
        var resetCount = 0;
        var updatedCount = 0;
        var progressCount = 0;
        foreach (var (anime, episodeList) in anidbAnimeIDs)
        {
            if (++progressCount % 10 == 0)
                _logger.Info($"Updating last updated episode timestamps for local anidb anime entries... ({progressCount}/{anidbAnimeIDs.Count})");

            var xml = xmlUtils.LoadAnimeHTTPFromFile(anime.AnimeID).Result;
            if (string.IsNullOrEmpty(xml))
            {
                _logger.Warn($"Unable to load cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                // We're unable to find the xml file, so the safest thing to do for future-proofing is to reset the dates.
                foreach (var episode in episodeList)
                {
                    resetCount++;
                    episode.DateTimeUpdated = DateTime.UnixEpoch;
                    episodesToSave.Add(episode);
                }
                continue;
            }

            ResponseGetAnime response;
            try
            {
                response = animeParser.Parse(anime.AnimeID, xml);
                if (response is null) throw new NullReferenceException(nameof(response));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                // We're unable to parse the xml file, so the safest thing to do for future-proofing is to reset the dates.
                foreach (var episode in episodeList)
                {
                    resetCount++;
                    episode.DateTimeUpdated = DateTime.UnixEpoch;
                    episodesToSave.Add(episode);
                }
                continue;
            }

            var responseEpisodeDict = response.Episodes.ToDictionary(ep => ep.EpisodeID);
            foreach (var episode in episodeList)
            {
                // The episode was found in the XML file, so we can safely update the timestamp.
                if (responseEpisodeDict.TryGetValue(episode.EpisodeID, out var responseEpisode))
                {
                    updatedCount++;
                    episode.DateTimeUpdated = responseEpisode.LastUpdated;
                    episodesToSave.Add(episode);
                }
                // The episode was deleted from the anime at some point, or the cache is outdated.
                else
                {
                    episode.DateTimeUpdated = DateTime.UnixEpoch;
                    faultyCount++;
                    episodesToSave.Add(episode);
                    animeToUpdateSet.Add(episode.AnimeID);
                }
            }
        }

        // Save the changes, if any.
        RepoFactory.AniDB_Episode.Save(episodesToSave);

        // Queue an update for the anime entries that needs it, hopefully fixing
        // the faulty episodes after the update.
        foreach (var animeID in animeToUpdateSet)
            anidbService.ScheduleRefreshOfAnimeByID(animeID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful)
                .GetAwaiter()
                .GetResult();

        _logger.Info($"Done updating last updated episode timestamps for {anidbAnimeIDs.Count} local anidb anime entries. Updated {updatedCount} episodes, reset {resetCount} episodes and queued anime {animeToUpdateSet.Count} updates for {faultyCount} faulty episodes.");
    }

    public static void UpdateSeriesWithHiddenEpisodes()
    {
        var seriesList = RepoFactory.AnimeEpisode.GetAll()
            .Where(episode => episode.IsHidden)
            .Select(episode => episode.AnimeSeriesID)
            .Distinct()
            .Select(seriesID => RepoFactory.AnimeSeries.GetByID(seriesID))
            .WhereNotNull()
            .ToList();

        var seriesService = ISystemService.StaticServices.GetRequiredService<AnimeSeriesService>();
        foreach (var series in seriesList)
            seriesService.UpdateStats(series, false, true);
    }

    public static void FixOrphanedShokoEpisodes()
    {
        var videoReleaseService = ISystemService.StaticServices.GetRequiredService<IVideoReleaseService>();
        var allSeries = RepoFactory.AnimeSeries.GetAll()
            .ToDictionary(series => series.AnimeSeriesID);
        var allSeriesAnidbId = allSeries.Values
            .ToDictionary(series => series.AniDB_ID);
        var allAniDBEpisodes = RepoFactory.AniDB_Episode.GetAll()
            .ToDictionary(ep => ep.EpisodeID);
        var shokoEpisodesToRemove = RepoFactory.AnimeEpisode.GetAll()
            .Where(episode =>
            {
                // Series doesn't exist anymore.
                if (!allSeries.TryGetValue(episode.AnimeSeriesID, out var series))
                    return true;

                // AniDB Episode doesn't exist anymore.
                if (!allAniDBEpisodes.TryGetValue(episode.AniDB_EpisodeID, out var anidbEpisode))
                    return true;

                return false;
            })
            .ToHashSet();

        // Validate existing shoko episodes.
        _logger.Trace($"Checking {allAniDBEpisodes.Values.Count} anidb episodes for broken or incorrect links…");
        var shokoEpisodesToSave = new List<AnimeEpisode>();
        foreach (var episode in allAniDBEpisodes.Values)
        {
            // No shoko episode, continue.
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
            if (shokoEpisode is null)
                continue;

            // The series exists and the episode mapping is correct, continue.
            if (allSeries.TryGetValue(shokoEpisode.AnimeSeriesID, out var actualSeries) && actualSeries.AniDB_ID == episode.AnimeID)
                continue;

            // The series was incorrectly linked to the wrong series. Correct it
            // if it's possible, or delete the episode.
            if (allSeriesAnidbId.TryGetValue(episode.AnimeID, out var correctSeries))
            {
                shokoEpisode.AnimeSeriesID = correctSeries.AnimeSeriesID;
                shokoEpisodesToSave.Add(shokoEpisode);
                continue;
            }

            // Delete the episode and clean up any remaining traces of the shoko
            // episode.
            shokoEpisodesToRemove.Add(shokoEpisode);
        }
        _logger.Trace($"Checked {allAniDBEpisodes.Values.Count} anidb episodes for broken or incorrect links. Found {shokoEpisodesToSave.Count} shoko episodes to fix and {shokoEpisodesToRemove.Count} to remove.");
        RepoFactory.AnimeEpisode.Save(shokoEpisodesToSave);

        // Remove any existing links to the episodes that will be removed.
        _logger.Trace($"Checking {shokoEpisodesToRemove.Count} orphaned shoko episodes before deletion.");
        var databaseReleasesToRemove = new List<StoredReleaseInfo>();
        var xrefsToRemove = new List<CrossRef_File_Episode>();
        var videosToRefetch = new List<VideoLocal>();
        var tmdbXrefsToRemove = new List<CrossRef_AniDB_TMDB_Episode>();
        foreach (var shokoEpisode in shokoEpisodesToRemove)
        {
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(shokoEpisode.AniDB_EpisodeID);
            var videos = xrefs
                .Select(xref => RepoFactory.VideoLocal.GetByEd2kAndSize(xref.Hash, xref.FileSize))
                .WhereNotNull()
                .ToList();
            var databaseReleases = RepoFactory.StoredReleaseInfo.GetByAnidbEpisodeID(shokoEpisode.AniDB_EpisodeID);
            var tmdbXrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(shokoEpisode.AniDB_EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            databaseReleasesToRemove.AddRange(databaseReleases);
            tmdbXrefsToRemove.AddRange(tmdbXrefs);
        }

        // Schedule a refetch of any video files affected by the removal of the
        // episodes. They were likely moved to another episode entry so let's
        // try and fetch that.
        _logger.Trace($"Scheduling {videosToRefetch.Count} videos for a re-fetch.");
        // If auto-match is not available then clear the release so the video is
        // not referencing no longer existing episodes.
        var autoMatch = videoReleaseService.AutoMatchEnabled;
        foreach (var video in videosToRefetch)
        {
            videoReleaseService.ClearReleaseForVideo(video).GetAwaiter().GetResult();
            videoReleaseService.ScheduleFindReleaseForVideo(video).GetAwaiter().GetResult();
        }

        _logger.Trace($"Deleting {shokoEpisodesToRemove.Count} orphaned shoko episodes.");
        RepoFactory.AnimeEpisode.Delete(shokoEpisodesToRemove);

        _logger.Trace($"Deleting {databaseReleasesToRemove.Count} orphaned releases.");
        RepoFactory.StoredReleaseInfo.Delete(databaseReleasesToRemove);

        _logger.Trace($"Deleting {tmdbXrefsToRemove.Count} orphaned tmdb xrefs.");
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(tmdbXrefsToRemove);

        _logger.Trace($"Deleting {xrefsToRemove.Count} orphaned file/episode cross-references.");
        RepoFactory.CrossRef_File_Episode.Delete(xrefsToRemove);
    }

    public static void CleanupAfterAddingTMDB()
    {
        var service = ISystemService.StaticServices.GetRequiredService<TmdbMetadataService>();

        // Remove the "MovieDB" directory in the image directory, since it's no longer used,
        var dir = new DirectoryInfo(Path.Join(ApplicationPaths.Instance.ImagesPath, "MovieDB"));
        if (dir.Exists)
            dir.Delete(true);

        // Schedule commands to get the new movie info for existing cross-reference
        service.UpdateAllMovies(true, true).ConfigureAwait(false).GetAwaiter().GetResult();

        // Schedule tmdb searches if we have auto linking enabled.
        service.ScanForMatches().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void CleanupAfterRemovingTvDB()
    {
        var dir = new DirectoryInfo(Path.Join(ApplicationPaths.Instance.ImagesPath, "TvDB"));
        if (dir.Exists)
            dir.Delete(true);
    }

    public static void ClearQuartzQueue()
    {
        var queueHandler = ISystemService.StaticServices.GetRequiredService<QueueHandler>();
        queueHandler.Clear().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void RepairMissingTMDBPersons()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var service = ISystemService.StaticServices.GetRequiredService<TmdbMetadataService>();
        var missingIds = new HashSet<int>();
        var updateCount = 0;
        var skippedCount = 0;
        var peopleIds = RepoFactory.TMDB_Person.GetAll().Select(person => person.TmdbPersonID).ToHashSet();
        var str = systemService.StartupMessage ?? "";
        foreach (var person in RepoFactory.TMDB_Episode_Cast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in RepoFactory.TMDB_Episode_Crew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        foreach (var person in RepoFactory.TMDB_Movie_Cast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in RepoFactory.TMDB_Movie_Crew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        systemService.StartupMessage = $"{str} - 0 / {missingIds.Count}";
        _logger.Debug("Found {Count} unique missing TMDB People for Episode & Movie staff", missingIds.Count);
        foreach (var personId in missingIds)
        {
            var (_, updated) = service.UpdatePerson(personId, forceRefresh: true).ConfigureAwait(false).GetAwaiter().GetResult();
            if (updated)
                updateCount++;
            else
                skippedCount++;
            systemService.StartupMessage = $"{str} - {updateCount + skippedCount} / {missingIds.Count}";
        }

        _logger.Info("Updated missing TMDB People: Found/Updated/Skipped {Found}/{Updated}/{Skipped}",
            missingIds.Count, updateCount, skippedCount);
    }

    public static void RecreateAnimeCharactersAndCreators()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var xmlUtils = ISystemService.StaticServices.GetRequiredService<HttpXmlUtils>();
        var animeParser = ISystemService.StaticServices.GetRequiredService<HttpAnimeParser>();
        var animeCreator = ISystemService.StaticServices.GetRequiredService<AnimeCreator>();
        var anidbService = ISystemService.StaticServices.GetRequiredService<IAnidbService>();
        var animeList = RepoFactory.AniDB_Anime.GetAll();
        var str = systemService.StartupMessage ?? "";
        systemService.StartupMessage = $"{str} - 0 / {animeList.Count}";
        _logger.Info($"Recreating characters and creator relations for {animeList.Count} anidb anime entries...");

        var count = 0;
        foreach (var anime in animeList)
        {
            if (++count % 10 == 0)
            {
                _logger.Info($"Recreating characters and creator relations for anidb anime entries... ({count}/{animeList.Count})");
                systemService.StartupMessage = $"{str} - {count} / {animeList.Count}";
            }

            var xml = xmlUtils.LoadAnimeHTTPFromFile(anime.AnimeID).Result;
            if (string.IsNullOrEmpty(xml))
            {
                _logger.Warn($"Unable to load cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                anidbService.ScheduleRefreshOfAnime(anime, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipSupplementaryUpdate)
                    .GetAwaiter()
                    .GetResult();
                continue;
            }

            ResponseGetAnime response;
            try
            {
                response = animeParser.Parse(anime.AnimeID, xml);
                if (response is null) throw new NullReferenceException(nameof(response));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                anidbService.ScheduleRefreshOfAnime(anime, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipSupplementaryUpdate)
                    .GetAwaiter()
                    .GetResult();
                continue;
            }

            animeCreator.CreateCharacters(response.Characters, anime, skipCreatorScheduling: true);

            animeCreator.CreateStaff(response.Staff, anime, skipCreatorScheduling: true);

            RepoFactory.AniDB_Anime.Save(anime);
        }


        _logger.Info($"Done recreating characters and creator relations for {animeList.Count} anidb anime entries.");
    }

    public static void ScheduleTmdbImageUpdates()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var tmdbMetadataService = ISystemService.StaticServices.GetRequiredService<TmdbMetadataService>();
        var tmdbMovies = RepoFactory.TMDB_Movie.GetAll();
        var tmdbShows = RepoFactory.TMDB_Show.GetAll();
        var movies = tmdbMovies.Count;
        var shows = tmdbShows.Count;
        var str = systemService.StartupMessage ?? string.Empty;
        systemService.StartupMessage = $"{str} - 0 / {movies} movies - 0 / {shows} shows";
        _logger.Info($"Scheduling tmdb image updates for {movies} tmdb movies and {shows} tmdb shows...");

        var count = 0;
        foreach (var tmdbMovie in tmdbMovies)
        {
            if (++count % 10 == 0 || count == movies)
            {
                _logger.Info($"Scheduling tmdb image updates for tmdb movies... ({count}/{movies})");
                systemService.StartupMessage = $"{str} - {count} / {movies} movies - 0 / {shows} shows";
            }

            tmdbMetadataService.ScheduleDownloadAllMovieImages(tmdbMovie.Id)
                .GetAwaiter()
                .GetResult();
        }

        count = 0;
        foreach (var tmdbShow in tmdbShows)
        {
            if (++count % 10 == 0 || count == shows)
            {
                _logger.Info($"Scheduling tmdb image updates for tmdb shows... ({count}/{shows})");
                systemService.StartupMessage = $"{str} - {movies} / {movies} movies - {count} / {shows} shows";
            }

            tmdbMetadataService.ScheduleDownloadAllShowImages(tmdbShow.Id)
                .GetAwaiter()
                .GetResult();
        }

        _logger.Info($"Done scheduling tmdb image updates for {movies} tmdb movies and {shows} tmdb shows.");
    }

    public static void MoveTmdbImagesOnDisc()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var imageDir = Path.Join(ApplicationPaths.Instance.ImagesPath, "TMDB");
        if (!Directory.Exists(imageDir))
            return;

        var total = 0;
        var skipped = 0;
        var str = systemService.StartupMessage ?? string.Empty;
        var folders = Directory.GetDirectories(imageDir)
            .Where(a => a[(imageDir.Length + 1)..].Length == 2)
            .ToArray();
        if (folders.Length > 0 && folders.Any(a => Directory.GetFiles(a).Any(b => Path.GetFileNameWithoutExtension(b).Length != 32)))
        {
            var folderCount = 0;
            foreach (var folder in folders)
            {
                folderCount++;
                var files = Directory.GetFiles(folder)
                    .Where(a => Path.GetFileNameWithoutExtension(a).Length != 32)
                    .ToArray();
                if (files.Length == 0)
                    continue;

                var count = 0;

                total += files.Length;
                _logger.Info($"Moving TMDb images on disc for folder {folderCount} out of {folders.Length}: {count}/{files.Length} ({total} total, {skipped} skipped)");
                systemService.StartupMessage = $"{str} - {folderCount} / {folders.Length} folders - 0 / {files.Length} images - {total} total, {skipped} skipped";
                foreach (var file in files)
                {
                    if (++count % 10 == 0 || count == files.Length)
                    {
                        _logger.Info($"Moving TMDb images on disc for folder {folderCount} out of {folders.Length}: {count}/{files.Length} ({total} total, {skipped} skipped)");
                        systemService.StartupMessage = $"{str} - {folderCount} / {folders.Length} folders - {count} / {files.Length} images - {total} total, {skipped} skipped";
                    }

                    var fileExt = Path.GetExtension(file);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var hashedFileName = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(fileName))).ToLower();
                    var folderName = hashedFileName[..2];
                    var newFile = Path.Combine(imageDir, folderName, hashedFileName + fileExt);
                    if (File.Exists(newFile))
                    {
                        skipped++;
                        File.Delete(file);
                        continue;
                    }

                    Directory.CreateDirectory(Path.Combine(imageDir, folderName));

                    File.Move(file, newFile);
                }
            }

            foreach (var folder in folders)
            {
                if (!Directory.EnumerateFiles(folder).Any())
                    Directory.Delete(folder, false);
            }
        }

        var imageTypes = new string[] { "Poster", "Banner", "Backdrop", "Logo" };
        foreach (var imageType in imageTypes)
        {
            var imageTypeDir = Path.Join(imageDir, imageType.ToString());
            if (!Directory.Exists(imageTypeDir))
                continue;

            var files = Directory.GetFiles(imageTypeDir);
            if (files.Length == 0)
                continue;

            var count = 0;

            total += files.Length;
            _logger.Info($"Moving TMDb {imageType} images on disc: 0/{files.Length}");
            systemService.StartupMessage = $"{str} - 0 / {files.Length} {imageType} images";
            foreach (var file in files)
            {
                if (++count % 10 == 0 || count == files.Length)
                {
                    _logger.Info($"Moving TMDb {imageType} images on disc: {count}/{files.Length}");
                    systemService.StartupMessage = $"{str} - {count} / {files.Length} {imageType} images";
                }

                var fileExt = Path.GetExtension(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var hashedFileName = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(fileName))).ToLower();
                var folderName = hashedFileName[..2];
                var newFile = Path.Combine(imageDir, folderName, hashedFileName + fileExt);
                if (File.Exists(newFile))
                {
                    skipped++;
                    File.Delete(file);
                    continue;
                }

                Directory.CreateDirectory(Path.Combine(imageDir, folderName));

                File.Move(file, newFile);
            }

            Directory.Delete(imageTypeDir);
        }

        _logger.Info($"Moved {total} TMDb images on disc. Skipped {skipped} images.");
    }

    public static void MoveAnidbFileDataToReleaseInfoFormat()
    {
        using var session = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();

        // get anidb files, xrefs, anidb release groups
        var videos = new List<DBF_VideoLocal>();
        var anidbFileUpdates = new List<DBF_AniDB_FileUpdate>();
        var anidbFileDict = new Dictionary<string, DBF_AniDB_File>();
        var anidbReleaseGroupDict = new Dictionary<int, DBF_AniDB_ReleaseGroup>();
        var crossRefTypes = new Dictionary<int, int>();
        var anidbFileAudioLanguageDict = new Dictionary<int, List<TitleLanguage>>();
        var anidbFileSubtitleLanguageDict = new Dictionary<int, List<TitleLanguage>>();
        var rawVideoLocal = session.CreateSQLQuery("SELECT VideoLocalID, Hash, MD5, SHA1, CRC32 FROM VideoLocal")
            .AddScalar("VideoLocalID", NHibernateUtil.Int32)
            .AddScalar("Hash", NHibernateUtil.String)
            .AddScalar("MD5", NHibernateUtil.String)
            .AddScalar("SHA1", NHibernateUtil.String)
            .AddScalar("CRC32", NHibernateUtil.String)
            .List();
        var rawAnidbFileList = session.CreateSQLQuery("SELECT FileID, Hash, GroupID, File_Source, File_Description, File_ReleaseDate, DateTimeUpdated, FileName, FileSize, FileVersion, InternalVersion, IsDeprecated, IsCensored, IsChaptered FROM AniDB_File")
            .AddScalar("FileID", NHibernateUtil.Int32)
            .AddScalar("Hash", NHibernateUtil.String)
            .AddScalar("GroupID", NHibernateUtil.Int32)
            .AddScalar("File_Source", NHibernateUtil.String)
            .AddScalar("File_Description", NHibernateUtil.String)
            .AddScalar("File_ReleaseDate", NHibernateUtil.Int32)
            .AddScalar("DateTimeUpdated", NHibernateUtil.DateTime)
            .AddScalar("FileName", NHibernateUtil.String)
            .AddScalar("FileSize", NHibernateUtil.Int64)
            .AddScalar("FileVersion", NHibernateUtil.Int32)
            .AddScalar("InternalVersion", NHibernateUtil.Int32)
            .AddScalar("IsDeprecated", NHibernateUtil.Boolean)
            .AddScalar("IsCensored", NHibernateUtil.Boolean)
            .AddScalar("IsChaptered", NHibernateUtil.Boolean)
            .List();
        var rawAnidbFileUpdateList = session.CreateSQLQuery("SELECT Hash, FileSize, HasResponse, UpdatedAt FROM AniDB_FileUpdate")
            .AddScalar("Hash", NHibernateUtil.String)
            .AddScalar("FileSize", NHibernateUtil.Int64)
            .AddScalar("HasResponse", NHibernateUtil.Boolean)
            .AddScalar("UpdatedAt", NHibernateUtil.DateTime)
            .List();
        var rawAnidbReleaseGroupList = session.CreateSQLQuery("SELECT GroupID, GroupName, GroupNameShort FROM AniDB_ReleaseGroup")
            .AddScalar("GroupID", NHibernateUtil.Int32)
            .AddScalar("GroupName", NHibernateUtil.String)
            .AddScalar("GroupNameShort", NHibernateUtil.String)
            .List();
        var rawCrossRefSource = session.CreateSQLQuery("SELECT CrossRef_File_EpisodeID, CrossRefSource FROM CrossRef_File_Episode")
            .AddScalar("CrossRef_File_EpisodeID", NHibernateUtil.Int32)
            .AddScalar("CrossRefSource", NHibernateUtil.Int32)
            .List();
        var rawAnidbFileLanguages = session.CreateSQLQuery("SELECT DISTINCT FileID, LanguageName FROM CrossRef_Languages_AniDB_File")
            .AddScalar("FileID", NHibernateUtil.Int32)
            .AddScalar("LanguageName", NHibernateUtil.String)
            .List();
        var rawAnidbFileSubtitles = session.CreateSQLQuery("SELECT DISTINCT FileID, LanguageName FROM CrossRef_Subtitles_AniDB_File")
            .AddScalar("FileID", NHibernateUtil.Int32)
            .AddScalar("LanguageName", NHibernateUtil.String)
            .List();
        foreach (object[] fields in rawVideoLocal)
        {
            var video = new DBF_VideoLocal()
            {
                VideoLocalID = (int)fields[0],
                ED2K = (string)fields[1],
                MD5 = (string)fields[2],
                SHA1 = (string)fields[3],
                CRC32 = (string)fields[4],
            };
            if (video.VideoLocalID == 0)
                continue;

            if (video.ED2K is not { Length: 32 } or "00000000000000000000000000000000")
                continue;

            if (video.MD5 is not { Length: 32 } or "00000000000000000000000000000000")
                video.MD5 = null;

            if (video.SHA1 is not { Length: 40 } or "0000000000000000000000000000000000000000")
                video.SHA1 = null;

            if (video.CRC32 is not { Length: 8 } or "00000000")
                video.CRC32 = null;

            videos.Add(video);
        }
        foreach (object[] fields in rawAnidbFileList)
        {
            var anidbFile = new DBF_AniDB_File
            {
                FileID = (int)fields[0],
                ED2k = (string)fields[1],
                GroupID = (int)fields[2],
                File_Source = (string)fields[3],
                File_Description = (string)fields[4],
                File_ReleaseDate = (int)fields[5] > 0 ? DateTime.UnixEpoch.AddSeconds((int)fields[5]).ToLocalTime() : null,
                DateTimeUpdated = ((DateTime)fields[6]).ToLocalTime(),
                FileName = (string)fields[7],
                FileSize = (long)fields[8],
                FileVersion = (int)fields[9],
                InternalVersion = (int)fields[10],
                IsDeprecated = (bool)fields[11],
                IsCensored = (bool?)fields[12],
                IsChaptered = (bool)fields[13],
            };
            if (anidbFile.FileID == 0 || anidbFile.InternalVersion < 2)
                continue;

            anidbFileDict.Add(anidbFile.ED2k, anidbFile);
        }
        foreach (object[] fields in rawAnidbFileUpdateList)
        {
            var anidbFileUpdate = new DBF_AniDB_FileUpdate
            {
                ED2K = (string)fields[0],
                FileSize = (long)fields[1],
                HasResponse = (bool)fields[2],
                UpdatedAt = ((DateTime)fields[3]).ToLocalTime(),
            };
            anidbFileUpdates.Add(anidbFileUpdate);
        }
        foreach (object[] fields in rawAnidbReleaseGroupList)
        {
            var anidbReleaseGroup = new DBF_AniDB_ReleaseGroup
            {
                GroupID = (int)fields[0],
                GroupName = (string)fields[1],
                GroupNameShort = (string)fields[2],
            };
            if (anidbReleaseGroup.GroupID == 0)
                continue;

            anidbReleaseGroupDict.Add(anidbReleaseGroup.GroupID, anidbReleaseGroup);
        }
        foreach (object[] fields in rawCrossRefSource)
        {
            var id = (int)fields[0];
            var source = (int)fields[1];
            crossRefTypes.Add(id, source);
        }
        foreach (object[] fields in rawAnidbFileLanguages)
        {
            var fileID = (int)fields[0];
            var language = (string)fields[1];
            if (!anidbFileAudioLanguageDict.ContainsKey(fileID))
                anidbFileAudioLanguageDict[fileID] = [];
            anidbFileAudioLanguageDict[fileID].Add(language.GetTitleLanguage());
        }
        foreach (object[] fields in rawAnidbFileSubtitles)
        {
            var fileID = (int)fields[0];
            var language = (string)fields[1];
            if (!anidbFileSubtitleLanguageDict.ContainsKey(fileID))
                anidbFileSubtitleLanguageDict[fileID] = [];
            anidbFileSubtitleLanguageDict[fileID].Add(language.GetTitleLanguage());
        }

        var videoLocalHashDigests = new List<VideoLocal_HashDigest>();
        foreach (var video in videos)
        {
            videoLocalHashDigests.Add(new VideoLocal_HashDigest
            {
                VideoLocalID = video.VideoLocalID,
                Type = "ED2K",
                Value = video.ED2K,
            });
            if (!string.IsNullOrEmpty(video.MD5))
                videoLocalHashDigests.Add(new VideoLocal_HashDigest
                {
                    VideoLocalID = video.VideoLocalID,
                    Type = "MD5",
                    Value = video.MD5,
                });
            if (!string.IsNullOrEmpty(video.SHA1))
                videoLocalHashDigests.Add(new VideoLocal_HashDigest
                {
                    VideoLocalID = video.VideoLocalID,
                    Type = "SHA1",
                    Value = video.SHA1,
                });
            if (!string.IsNullOrEmpty(video.CRC32))
                videoLocalHashDigests.Add(new VideoLocal_HashDigest
                {
                    VideoLocalID = video.VideoLocalID,
                    Type = "CRC32",
                    Value = video.CRC32,
                });
        }
        RepoFactory.VideoLocalHashDigest.Save(videoLocalHashDigests);

        // create the releases using the above info
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var anidbProvider = ISystemService.StaticServices.GetRequiredService<IVideoReleaseService>().GetProviderInfo<AnidbReleaseProvider>();
        var potentialReleases = RepoFactory.CrossRef_File_Episode.GetAll()
            .GroupBy(x => (x.Hash, x.FileSize, crossRefTypes[x.CrossRef_File_EpisodeID]))
            .ToList();
        var anidbFileUpdateLookup = anidbFileUpdates.ToLookup(x => x.ED2K);
        var crossRefsToRemove = new List<CrossRef_File_Episode>();
        var storedReleaseInfos = new List<StoredReleaseInfo>();
        var storedReleaseInfoAttempts = new List<StoredReleaseInfo_MatchAttempt>();
        var count = 0;
        var str = systemService.StartupMessage ?? string.Empty;
        var creditlessRegex = AnidbReleaseProvider.CreditlessRegex;
        foreach (var groupBy in potentialReleases)
        {
            if (++count % 10000 == 0 || count == 1 || count == potentialReleases.Count)
            {
                _logger.Info($"Converting releases: {count}/{potentialReleases.Count}");
                systemService.StartupMessage = $"{str} - {count} / {potentialReleases.Count}";
            }

            var (ed2k, fileSize, source) = groupBy.Key;
            var video = RepoFactory.VideoLocal.GetByEd2k(ed2k);
            var anidbFileUpdateList = anidbFileUpdateLookup.Contains(ed2k)
                ? anidbFileUpdateLookup[ed2k].OrderByDescending(x => x.UpdatedAt).ToList()
                : [];
            var anidbFile = source is 1 /* CrossRefType.AniDB */ && anidbFileDict.ContainsKey(ed2k)
                ? anidbFileDict[ed2k]
                : null;
            var importedAt = video?.DateTimeImported ?? anidbFileUpdateList.FirstOrDefault(a => a.HasResponse)?.UpdatedAt ?? anidbFileUpdateList.FirstOrDefault()?.UpdatedAt ?? DateTime.Now;
            var storedReleaseInfo = new StoredReleaseInfo
            {
                ED2K = ed2k,
                FileSize = fileSize,
                ProviderName = "User",
                CrossReferences = groupBy
                    .OrderBy(x => x.CrossRef_File_EpisodeID)
                    .Select(xref =>
                    {
                        var embedded = new EmbeddedCrossReference
                        {
                            PercentageStart = xref.PercentageRange.Start,
                            PercentageEnd = xref.PercentageRange.End,
                        };
                        embedded.ProviderIDs[CrossReferenceIDs.AniDB_Episode] = xref.EpisodeID.ToString();
                        embedded.ProviderIDs[CrossReferenceIDs.AniDB_Anime] = xref.AnimeID.ToString();
                        return embedded;
                    })
                    .ToList(),
                CreatedAt = importedAt,
                LastUpdatedAt = importedAt,
            };

            if (anidbFile is not null)
            {
                var lastCheckedAt = anidbFileUpdateList.FirstOrDefault(a => a.HasResponse)?.UpdatedAt;
                if (lastCheckedAt is not null && storedReleaseInfo.LastUpdatedAt < lastCheckedAt.Value)
                    storedReleaseInfo.LastUpdatedAt = lastCheckedAt.Value;

                var audioLanguages = anidbFileAudioLanguageDict.ContainsKey(anidbFile.FileID)
                    ? anidbFileAudioLanguageDict[anidbFile.FileID]
                    : [];
                var subtitleLanguages = anidbFileSubtitleLanguageDict.ContainsKey(anidbFile.FileID)
                    ? anidbFileSubtitleLanguageDict[anidbFile.FileID]
                    : [];
                var anidbReleaseGroup = anidbFile is not null && anidbReleaseGroupDict.ContainsKey(anidbFile.GroupID)
                    ? anidbReleaseGroupDict[anidbFile.GroupID]
                    : null;

                storedReleaseInfo.ID = $"{AnidbReleaseProvider.IdPrefix}{ed2k}+{fileSize}";
                storedReleaseInfo.ProviderName = anidbProvider.Name;
                storedReleaseInfo.ReleaseURI = $"{AnidbReleaseProvider.ReleasePrefix}{anidbFile.FileID}";
                storedReleaseInfo.Version = anidbFile.FileVersion;
                storedReleaseInfo.Comment = string.IsNullOrEmpty(anidbFile.File_Description) ? null : anidbFile.File_Description;
                storedReleaseInfo.OriginalFilename = anidbFile.FileName;
                storedReleaseInfo.IsCensored = anidbFile.IsCensored;
                storedReleaseInfo.IsChaptered = anidbFile.IsChaptered;
                storedReleaseInfo.IsCreditless =
                    (!string.IsNullOrEmpty(anidbFile.FileName) && creditlessRegex.IsMatch(anidbFile.FileName)) ||
                    (video?.Places is { Count: > 0 } places && places.Any(x => creditlessRegex.IsMatch(x.FileName)));
                storedReleaseInfo.IsCorrupted = anidbFile.IsDeprecated;
                storedReleaseInfo.Source = Enum.Parse<GetFile_Source>(anidbFile.File_Source, ignoreCase: true) switch
                {
                    GetFile_Source.TV => ReleaseSource.TV,
                    GetFile_Source.DTV => ReleaseSource.TV,
                    GetFile_Source.HDTV => ReleaseSource.TV,
                    GetFile_Source.DVD => ReleaseSource.DVD,
                    GetFile_Source.HKDVD => ReleaseSource.DVD,
                    GetFile_Source.HDDVD => ReleaseSource.DVD,
                    GetFile_Source.VHS => ReleaseSource.VHS,
                    GetFile_Source.Camcorder => ReleaseSource.Camera,
                    GetFile_Source.VCD => ReleaseSource.VCD,
                    GetFile_Source.SVCD => ReleaseSource.VCD,
                    GetFile_Source.LaserDisc => ReleaseSource.LaserDisc,
                    GetFile_Source.BluRay => ReleaseSource.BluRay,
                    GetFile_Source.Web => ReleaseSource.Web,
                    GetFile_Source.Film8mm => ReleaseSource.Film,
                    GetFile_Source.Film16mm => ReleaseSource.Film,
                    GetFile_Source.Film35mm => ReleaseSource.Film,
                    _ => ReleaseSource.Unknown,
                };
                storedReleaseInfo.ProvidedFileSize = fileSize;
                storedReleaseInfo.Hashes = [
                    new() { Type = "ED2K", Value = ed2k },
                    ..video?.Hashes.Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata }) ?? [],
                ];
                storedReleaseInfo.ReleasedAt = anidbFile.File_ReleaseDate?.ToDateOnly();
                storedReleaseInfo.AudioLanguages = audioLanguages;
                storedReleaseInfo.SubtitleLanguages = subtitleLanguages;

                if (anidbReleaseGroup is not null)
                {
                    storedReleaseInfo.GroupID = anidbReleaseGroup.GroupID.ToString();
                    storedReleaseInfo.GroupSource = "AniDB";
                    storedReleaseInfo.GroupName = anidbReleaseGroup.GroupName;
                    storedReleaseInfo.GroupShortName = anidbReleaseGroup.GroupNameShort;
                }
            }

            storedReleaseInfos.Add(storedReleaseInfo);

            foreach (var anidbFileUpdate in anidbFileUpdateList)
            {
                storedReleaseInfoAttempts.Add(new StoredReleaseInfo_MatchAttempt
                {
                    ED2K = ed2k,
                    FileSize = fileSize,
                    AttemptedProviderNames = [anidbProvider.Name],
                    ProviderName = anidbFileUpdate.HasResponse ? anidbProvider.Name : null,
                    ProviderID = anidbFileUpdate.HasResponse ? anidbProvider.ID : null,
                    AttemptStartedAt = anidbFileUpdate.UpdatedAt,
                    AttemptEndedAt = anidbFileUpdate.UpdatedAt,
                });
            }
        }

        RepoFactory.StoredReleaseInfo.Save(storedReleaseInfos);
        RepoFactory.StoredReleaseInfo_MatchAttempt.Save(storedReleaseInfoAttempts);

        // drop the tables once the new info is saved.
        var tablesToDrop = new[]
        {
            "AniDB_File",
            "AniDB_FileUpdate",
            "AniDB_ReleaseGroup",
            "CrossRef_Languages_AniDB_File",
            "CrossRef_Subtitles_AniDB_File",
        };
        foreach (var table in tablesToDrop)
            session.CreateSQLQuery($"DROP TABLE {table};").ExecuteUpdate();
        session.CreateSQLQuery("ALTER TABLE CrossRef_File_Episode DROP COLUMN CrossRefSource;").ExecuteUpdate();
        session.CreateSQLQuery("ALTER TABLE VideoLocal DROP COLUMN MD5;").ExecuteUpdate();
        session.CreateSQLQuery("ALTER TABLE VideoLocal DROP COLUMN SHA1;").ExecuteUpdate();
        session.CreateSQLQuery("ALTER TABLE VideoLocal DROP COLUMN CRC32;").ExecuteUpdate();
    }

    private static string _defaultScriptName = null;

    public static Tuple<bool, string> MigrateRenamers(object _)
    {
        var factory = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().Instance;
        var configurationService = ISystemService.StaticServices.GetRequiredService<IConfigurationService>();
        var renamerService = ISystemService.StaticServices.GetRequiredService<IVideoRelocationService>();
        var sessionFactory = factory.CreateSessionFactory();
        using var session = sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        try
        {
            const string SelectCommand1 = "SELECT ScriptName, RenamerType, IsEnabledOnImport, Script FROM RenameScript;";
            const string SelectCommand2 = "SELECT Name, Type, Settings FROM RenamerInstance;";
            const string InsertCommand = "INSERT INTO StoredRelocationPipe (ProviderID, Name, Configuration) VALUES (:ProviderID, :Name, :Configuration);";
            const string DropCommand = "DROP TABLE IF EXISTS RenameScript; DROP TABLE IF EXISTS RenamerInstance;";
            string defaultName = null;
            var rawPresets = new List<StoredRelocationPreset>();
            var webAomRenamer = renamerService.GetProviderInfo<WebAOMRenamer>();
            var renamersByKey = renamerService.GetAvailableProviders()
                .Where(a => a.Provider.GetType().FullName is { Length: > 0 })
                .ToDictionary(a => a.Provider.GetType().FullName);
            var defaultRenamerConfigName = SettingsMigrations.MigratedDefaultRenamer;
            try
            {
                var rawRenamerScripts = session.CreateSQLQuery(SelectCommand1)
                    .AddScalar("ScriptName", NHibernateUtil.String)
                    .AddScalar("RenamerType", NHibernateUtil.String)
                    .AddScalar("IsEnabledOnImport", NHibernateUtil.Int32)
                    .AddScalar("Script", NHibernateUtil.String)
                    .List<object[]>();
                foreach (var fields in rawRenamerScripts)
                {
                    if (fields.Length is not 4)
                    {
                        _logger.Warn("A RenameScript could not be converted to StoredRelocationPreset, but there wasn't enough data to log");
                        continue;
                    }

                    var renamerScript = new DBF_RenamerScript
                    {
                        ScriptName = (string)fields[0],
                        RenamerType = (string)fields[1],
                        IsEnabledOnImport = (int)fields[2] == 1,
                        Script = (string)fields[3],
                    };
                    if (renamerScript.ScriptName is "AAA_WORKINGFILE_TEMP_AAA")
                        continue;

                    try
                    {
                        byte[] configuration = null;
                        var providerInfo = renamerScript.RenamerType.Equals("Legacy")
                            ? webAomRenamer
                            : renamersByKey.ContainsKey(renamerScript.RenamerType)
                                ? renamersByKey[renamerScript.RenamerType]
                                : null;
                        if (providerInfo is null)
                        {
                            if (renamerScript.RenamerType == "GroupAwareRenamer")
                            {
                                configuration = webAomRenamer.ConfigurationInfo is null ? null : Encoding.UTF8.GetBytes(
                                    configurationService.Serialize(
                                        new WebAOMSettings
                                        {
                                            Script = renamerScript.Script,
                                            GroupAwareSorting = true
                                        }
                                    )
                                );
                                rawPresets.Add(new() { Name = renamerScript.ScriptName, ProviderID = providerInfo.ID, Configuration = configuration, IsDefault = renamerScript.ScriptName == defaultRenamerConfigName });
                                continue;
                            }

                            _logger.Warn("A RenameScript could not be converted to StoredRelocationPreset. Renamer name: " + renamerScript.ScriptName + " Renamer type: " + renamerScript.RenamerType + Environment.NewLine + "Script: " + Environment.NewLine + renamerScript.Script);
                            SaveFailedRenamerItem("RenamerScript", renamerScript.RenamerType, renamerScript.ScriptName, Encoding.UTF8.GetBytes(renamerScript.Script), ".txt");
                            continue;
                        }

                        if (providerInfo.ConfigurationInfo is not null)
                        {
                            var config = configurationService.New(providerInfo.ConfigurationInfo);
                            // Migrate the script if we find a 'Script' property.
                            providerInfo.ConfigurationInfo.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(b => b.Name == "Script")
                                ?.SetValue(config, renamerScript.Script);
                            configuration = Encoding.UTF8.GetBytes(configurationService.Serialize(config));
                        }

                        rawPresets.Add(new() { Name = renamerScript.ScriptName, ProviderID = providerInfo.ID, Configuration = configuration, IsDefault = renamerScript.ScriptName == defaultRenamerConfigName });
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "A RenameScript could not be converted to StoredRelocationPreset. Renamer name: " + renamerScript.ScriptName + " Renamer type: " + renamerScript.RenamerType + Environment.NewLine + "Script: " + Environment.NewLine + renamerScript.Script);
                        SaveFailedRenamerItem("RenamerScript", renamerScript.RenamerType, renamerScript.ScriptName, Encoding.UTF8.GetBytes(renamerScript.Script), ".txt");
                        continue;
                    }
                }
            }
            catch (GenericADOException) { }
            try
            {
                var rawRenamerConfigs = session.CreateSQLQuery(SelectCommand2)
                        .AddScalar("Name", NHibernateUtil.String)
                        .AddScalar("Type", NHibernateUtil.String)
                        .AddScalar("Settings", NHibernateUtil.BinaryBlob)
                        .List<object[]>();
                foreach (var fields in rawRenamerConfigs)
                {
                    if (fields.Length is not 3)
                    {
                        _logger.Warn("A RenamerInstance could not be converted to StoredRelocationPreset, but there wasn't enough data to log");
                        continue;
                    }
                    var renamerConfig = new DBF_RenamerConfig
                    {
                        Name = (string)fields[0] ?? "_",
                        Type = (string)fields[1],
                        Settings = (byte[])fields[2],
                    };
                    var settingsString = Environment.NewLine + "Settings (base64): " + Convert.ToBase64String(renamerConfig.Settings ?? []);
                    var scriptString = string.Empty;
                    try
                    {
                        byte[] configuration = null;
                        var providerInfo = renamersByKey.ContainsKey(renamerConfig.Type)
                            ? renamersByKey[renamerConfig.Type]
                            : null;
                        try
                        {
                            var settingsJson = MessagePackSerializer.ConvertToJson(renamerConfig.Settings, MessagePackSerializer.DefaultOptions.WithCompression(MessagePackCompression.Lz4BlockArray));
                            settingsString = Environment.NewLine + "Settings (JSON): " + settingsJson;
                            scriptString = Environment.NewLine + "Script: " + Environment.NewLine + JsonNode.Parse(settingsJson)?["Script"] ?? string.Empty;
                        }
                        catch (MessagePackSerializationException)
                        {
                            _logger.Warn("A RenamerInstance Settings object could not be converted to JSON. Renamer name: " + renamerConfig.Name + " Renamer type: " + renamerConfig.Type + settingsString + scriptString);
                        }
                        if (providerInfo is null)
                        {
                            _logger.Warn("A RenamerInstance could not be converted to StoredRelocationPreset. Renamer name: " + renamerConfig.Name + " Renamer type: " + renamerConfig.Type + settingsString + scriptString);
                            SaveFailedRenamerItem("RenamerConfig", renamerConfig.Type, renamerConfig.Name, renamerConfig.Settings, ".messagepack");
                            continue;
                        }

                        if (providerInfo.ConfigurationInfo is not null)
                        {
                            var config = MessagePackSerializer.Typeless.Deserialize(renamerConfig.Settings);
                            if (config.GetType() != providerInfo.ConfigurationInfo.Type)
                            {
                                _logger.Warn("A RenamerInstance could not be converted to StoredRelocationPreset. Mismatched config type. Renamer name: " + renamerConfig.Name + " Renamer type: " + renamerConfig.Type + settingsString + scriptString);
                                SaveFailedRenamerItem("RenamerConfig", renamerConfig.Type, renamerConfig.Name, renamerConfig.Settings, ".messagepack");
                                continue;
                            }
                            configuration = Encoding.UTF8.GetBytes(configurationService.Serialize(config as IConfiguration));
                        }

                        rawPresets.Add(new() { Name = renamerConfig.Name, ProviderID = providerInfo.ID, Configuration = configuration, IsDefault = renamerConfig.Name == defaultRenamerConfigName });
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "A RenamerInstance could not be converted to StoredRelocationPreset. Renamer name: " + renamerConfig.Name + " Renamer type: " + renamerConfig.Type + settingsString + scriptString);
                        SaveFailedRenamerItem("RenamerConfig", renamerConfig.Type, renamerConfig.Name, renamerConfig.Settings, ".messagepack");
                        continue;
                    }
                }
            }
            catch (GenericADOException) { }
            if (rawPresets.Count == 0)
            {
                defaultName = "Default";
                rawPresets.Add(new() { Name = "Default", ProviderID = webAomRenamer.ID, Configuration = Encoding.UTF8.GetBytes(configurationService.Serialize(configurationService.New<WebAOMSettings>())), IsDefault = true });
            }
            var presets = new List<StoredRelocationPreset>();
            foreach (var presetGroup in rawPresets.GroupBy(t => t.Name.Trim()))
            {
                var index = 0;
                foreach (var preset in presetGroup)
                {
                    if (index > 0)
                        preset.Name += index is 1 ? " (copy)" : $" (copy #{index})";
                    if (preset.IsDefault)
                        defaultName = preset.Name.Trim();
                    index++;
                    presets.Add(preset);
                }
            }

            if (string.IsNullOrEmpty(defaultName))
                defaultName = presets[0].Name;

            foreach (var presetGroup in rawPresets.GroupBy(t => t.Name.Trim()))
            {
                var index = 0;
                foreach (var preset in presetGroup)
                {
                    if (index > 0)
                        preset.IsDefault = false;
                    else
                        preset.IsDefault = preset.Name.Trim() == defaultName;
                    index++;
                }
            }

            _defaultScriptName = defaultName;

            foreach (var renamer in presets)
            {
                var command = session.CreateSQLQuery(InsertCommand);
                command.SetParameter("ProviderID", renamer.ProviderID);
                command.SetParameter("Name", renamer.Name);
                command.SetParameter("Configuration", renamer.Configuration);
                command.ExecuteUpdate();
            }

            session.CreateSQLQuery(DropCommand).ExecuteUpdate();
            transaction.Commit();
        }
        catch (Exception e)
        {
            transaction.Rollback();
            return new Tuple<bool, string>(false, e.ToString());
        }

        return new Tuple<bool, string>(true, null);
    }

    private static string GetFailedMigrationsBasePath()
    {
        var settings = ISettingsProvider.Instance.GetSettings();
        var dirPath = settings.Database.DatabaseBackupDirectory;
        return Path.Combine(
            string.IsNullOrWhiteSpace(dirPath) ? ApplicationPaths.StaticDataPath
                : Path.Combine(ApplicationPaths.StaticDataPath, dirPath),
            "failed_migrations"
        );
    }

    private static string SanitizeFileName(string name)
        => Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, '_'));

    private static void SaveFailedRenamerItem(string category, string typeName, string name, byte[] data, string extension)
    {
        var dir = Path.Combine(GetFailedMigrationsBasePath(), category, SanitizeFileName(typeName));
        Directory.CreateDirectory(dir);
        var basePath = Path.Combine(dir, SanitizeFileName(name));
        var filePath = basePath + extension;
        if (File.Exists(filePath))
        {
            var copyIndex = 1;
            do
            {
                filePath = $"{basePath} (copy #{++copyIndex}){extension}";
            } while (File.Exists(filePath));
        }
        File.WriteAllBytes(filePath, data);
    }

    public static void SetDefaultRenamer()
    {
        var presets = RepoFactory.StoredRelocationPreset.GetAll();
        if (presets.Count == 0)
            return;

        switch (presets.Count(a => a.IsDefault))
        {
            case 0:
                var defaultName = _defaultScriptName ?? SettingsMigrations.MigratedDefaultRenamer;
                var defaultPreset = presets.FirstOrDefault(a => a.Name.Trim() == defaultName)
                    ?? presets.FirstOrDefault();
                if (defaultPreset is not null)
                {
                    defaultPreset.IsDefault = true;
                    RepoFactory.StoredRelocationPreset.Save(defaultPreset);
                }
                break;

            case > 1:
                foreach (var preset in presets.Where(a => a.IsDefault).Skip(1))
                {
                    preset.IsDefault = false;
                    RepoFactory.StoredRelocationPreset.Save(preset);
                }
                break;
        }
    }

    public static void MigrateAnidbVotes()
    {
        using var session = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        const string SelectCommand = "SELECT EntityID, VoteValue, VoteType FROM AniDB_Vote;";
        const string DropCommand = "DROP TABLE IF EXISTS AniDB_Vote;";
        IList<object[]> rawVotes;
        try
        {
            rawVotes = session.CreateSQLQuery(SelectCommand)
                    .AddScalar("EntityID", NHibernateUtil.Int32)
                    .AddScalar("VoteValue", NHibernateUtil.Int32)
                    .AddScalar("VoteType", NHibernateUtil.Int32)
                    .List<object[]>();
        }
        catch (GenericADOException)
        {
            return;
        }

        // If we have no user, then this is a new install, so skip the migration.
        var allUsers = RepoFactory.JMMUser.GetAll();
        if (allUsers.Count == 0)
        {
            session.CreateSQLQuery(DropCommand).ExecuteUpdate();
            return;
        }

        // Find the most qualified user to add the AniDB_Vote data to.
        var user = allUsers.FirstOrDefault(u => u.IsAdmin == 1 && u.IsAniDBUser == 1)
            ?? allUsers.FirstOrDefault(u => u.IsAniDBUser == 1)
            ?? allUsers.FirstOrDefault(u => u.IsAdmin == 1)
            ?? allUsers[0];

        var toSaveSeries = new List<AnimeSeries_User>();
        var toSaveEpisode = new List<AnimeEpisode_User>();
        foreach (var fields in rawVotes)
        {
            if (fields.Length != 3)
            {
                _logger.Warn("An AniDB_Vote could not be converted to the newer user data type, but there wasn't enough data to log");
                continue;
            }
            var vote = new DNF_AniDB_Vote()
            {
                EntityID = (int)fields[0],
                VoteValue = (int)fields[1],
                VoteType = (VoteType)fields[2],
            };

            if (vote.VoteValue != -1 && vote.VoteValue is < 100 or > 1000)
            {
                _logger.Warn("Invalid value found for entity {EntityID}: {VoteValue}. Dropping vote", vote.EntityID, vote.VoteValue);
                continue;
            }

            switch (vote.VoteType)
            {
                case VoteType.AnimePermanent:
                case VoteType.AnimeTemporary:
                {
                    if (RepoFactory.AnimeSeries.GetByAnimeID(vote.EntityID) is not { } series)
                    {
                        _logger.Warn("Unable to find an AnimeSeries entry for AniDB Anime with id {0}. Dropping vote.", vote.EntityID);
                        continue;
                    }

                    var userData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(user.JMMUserID, series.AnimeSeriesID)
                        ?? new() { JMMUserID = user.JMMUserID, AnimeSeriesID = series.AnimeSeriesID };
                    userData.AbsoluteUserRating = vote.VoteValue;
                    userData.UserRatingVoteType = vote.VoteType is VoteType.AnimePermanent
                        ? SeriesVoteType.Permanent
                        : SeriesVoteType.Temporary;
                    toSaveSeries.Add(userData);
                    break;
                }

                case VoteType.Episode:
                {
                    if (RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(vote.EntityID) is not { } series)
                    {
                        _logger.Warn("Unable to find an AnimeEpisode entry for AniDB Anime with id {0}. Dropping vote.", vote.EntityID);
                        continue;
                    }

                    var userData = RepoFactory.AnimeEpisode_User.GetByUserAndEpisodeID(user.JMMUserID, series.AnimeEpisodeID)
                        ?? new() { JMMUserID = user.JMMUserID, AnimeEpisodeID = series.AnimeEpisodeID, LastUpdated = DateTime.Now };
                    userData.AbsoluteUserRating = vote.VoteValue;
                    toSaveEpisode.Add(userData);
                    break;
                }
            }
        }

        _logger.Info(
            "Found {0} series votes and {1} episodes votes across {2} vote rows to preserve.",
            toSaveSeries.Count,
            toSaveEpisode.Count,
            rawVotes.Count
        );

        RepoFactory.AnimeSeries_User.Save(toSaveSeries);
        RepoFactory.AnimeEpisode_User.Save(toSaveEpisode);

        session.CreateSQLQuery(DropCommand).ExecuteUpdate();
    }

    public static void MigrateToUnifiedImages()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var imageManager = (ImageManager)ISystemService.StaticServices.GetRequiredService<IImageManager>();
        var imagesPath = ApplicationPaths.Instance.ImagesPath;
        using var session = ISystemService.StaticServices.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        var str = systemService.StartupMessage ?? string.Empty;
        var settings = ISettingsProvider.Instance.GetSettings();

        foreach (var (oldName, newName) in new (string, string)[]
        {
            ("AniDB", "AniDB_old"),
            ("AniDB_Char", "AniDB_Char_old"),
            ("AniDB_Creator", "AniDB_Creator_old"),
            ("TMDB", "TMDB_old"),
        })
        {
            var oldDir = Path.Join(imagesPath, oldName);
            var backupDir = Path.Join(imagesPath, newName);
            if (Directory.Exists(oldDir) && !Directory.Exists(backupDir) && Directory.EnumerateFiles(oldDir, "*.*", SearchOption.AllDirectories).Any())
                Directory.Move(oldDir, backupDir);
        }

        var oldTmdbImages = new List<DNF_TMDB_Image>();
        try
        {
            var rawTmdbImages = session.CreateSQLQuery(
                    "SELECT TMDB_ImageID, IsEnabled, Width, Height, Language, RemoteFileName, UserRating, UserVotes FROM TMDB_Image")
                .AddScalar("TMDB_ImageID", NHibernateUtil.Int32)
                .AddScalar("IsEnabled", NHibernateUtil.Int32)
                .AddScalar("Width", NHibernateUtil.Int32)
                .AddScalar("Height", NHibernateUtil.Int32)
                .AddScalar("Language", NHibernateUtil.String)
                .AddScalar("RemoteFileName", NHibernateUtil.String)
                .AddScalar("UserRating", NHibernateUtil.Double)
                .AddScalar("UserVotes", NHibernateUtil.Int32)
                .List<object[]>();

            foreach (var fields in rawTmdbImages)
            {
                var remoteFileName = (string)fields[5];
                if (string.IsNullOrEmpty(remoteFileName))
                    continue;

                oldTmdbImages.Add(new DNF_TMDB_Image
                {
                    TMDB_ImageID = (int)fields[0],
                    IsEnabled = (int)fields[1] == 1,
                    Width = (int)fields[2],
                    Height = (int)fields[3],
                    Language = (string)fields[4],
                    RemoteFileName = remoteFileName,
                    UserRating = (double)fields[6],
                    UserVotes = (int)fields[7],
                });
            }
        }
        catch (GenericADOException)
        {
            _logger.Info("TMDB_Image table does not exist, skipping.");
        }

        systemService.StartupMessage = $"{str} - Loaded {oldTmdbImages.Count} TMDB_Image records to migrate.";
        _logger.Info("Loaded {Count} TMDB_Image records to migrate.", oldTmdbImages.Count);

        var oldTmdbImageEntities = new List<DNF_TMDB_Image_Entity>();
        try
        {
            var rawTmdbImageEntities = session.CreateSQLQuery(
                    "SELECT TMDB_Image_EntityID, RemoteFileName, ImageType, TmdbEntityType, TmdbEntityID, Ordering, ReleasedAt FROM TMDB_Image_Entity")
                .AddScalar("TMDB_Image_EntityID", NHibernateUtil.Int32)
                .AddScalar("RemoteFileName", NHibernateUtil.String)
                .AddScalar("ImageType", NHibernateUtil.Int32)
                .AddScalar("TmdbEntityType", NHibernateUtil.Int32)
                .AddScalar("TmdbEntityID", NHibernateUtil.Int32)
                .AddScalar("Ordering", NHibernateUtil.Int32)
                .AddScalar("ReleasedAt", NHibernateUtil.Date)
                .List<object[]>();

            foreach (var fields in rawTmdbImageEntities)
            {
                var releasedAt = (DateTime?)fields[6];
                oldTmdbImageEntities.Add(new DNF_TMDB_Image_Entity
                {
                    TMDB_Image_EntityID = (int)fields[0],
                    RemoteFileName = (string)fields[1],
                    ImageType = LegacyImageEntityTypeConverter((int)fields[2]),
                    TmdbEntityType = (int)fields[3],
                    TmdbEntityID = (int)fields[4],
                    Ordering = (int)fields[5],
                    ReleasedAt = releasedAt.HasValue ? DateOnly.FromDateTime(releasedAt.Value) : null,
                });
            }
        }
        catch (GenericADOException)
        {
            _logger.Info("TMDB_Image_Entity table does not exist, skipping.");
        }

        systemService.StartupMessage = $"{str} - Loaded {oldTmdbImages.Count} TMDB_Image_Entity records to migrate.";
        _logger.Info("Loaded {Count} TMDB_Image_Entity records to migrate.", oldTmdbImageEntities.Count);

        var oldPreferredImages = new List<DNF_AniDB_PreferredImage>();
        try
        {
            var rawAnimePreferredImages = session.CreateSQLQuery(
                    "SELECT AniDB_Anime_PreferredImageID, AnidbAnimeID, ImageID, ImageSource, ImageType FROM AniDB_Anime_PreferredImage")
                .AddScalar("AniDB_Anime_PreferredImageID", NHibernateUtil.Int32)
                .AddScalar("AnidbAnimeID", NHibernateUtil.Int32)
                .AddScalar("ImageID", NHibernateUtil.Int32)
                .AddScalar("ImageSource", NHibernateUtil.Int32)
                .AddScalar("ImageType", NHibernateUtil.Int32)
                .List<object[]>();

            foreach (var fields in rawAnimePreferredImages)
            {
                oldPreferredImages.Add(new DNF_AniDB_PreferredImage
                {
                    PreferredImageID = (int)fields[0],
                    AnidbAnimeID = (int)fields[1],
                    AnidbEpisodeID = 0,
                    ImageID = (int)fields[2],
                    ImageSource = (int)fields[3],
                    ImageType = LegacyImageEntityTypeConverter((int)fields[4]),
                });
            }
        }
        catch (GenericADOException)
        {
            _logger.Info("AniDB_Anime_PreferredImage table does not exist, skipping.");
        }

        try
        {
            var rawEpisodePreferredImages = session.CreateSQLQuery(
                    "SELECT AniDB_Episode_PreferredImageID, AnidbAnimeID, AnidbEpisodeID, ImageID, ImageSource, ImageType FROM AniDB_Episode_PreferredImage")
                .AddScalar("AniDB_Episode_PreferredImageID", NHibernateUtil.Int32)
                .AddScalar("AnidbAnimeID", NHibernateUtil.Int32)
                .AddScalar("AnidbEpisodeID", NHibernateUtil.Int32)
                .AddScalar("ImageID", NHibernateUtil.Int32)
                .AddScalar("ImageSource", NHibernateUtil.Int32)
                .AddScalar("ImageType", NHibernateUtil.Int32)
                .List<object[]>();

            foreach (var fields in rawEpisodePreferredImages)
            {
                oldPreferredImages.Add(new DNF_AniDB_PreferredImage
                {
                    PreferredImageID = (int)fields[0],
                    AnidbAnimeID = (int)fields[1],
                    AnidbEpisodeID = (int)fields[2],
                    ImageID = (int)fields[3],
                    ImageSource = (int)fields[4],
                    ImageType = LegacyImageEntityTypeConverter((int)fields[5]),
                });
            }
        }
        catch (GenericADOException)
        {
            _logger.Info("AniDB_Episode_PreferredImage table does not exist, skipping.");
        }

        systemService.StartupMessage = $"{str} - Loaded {oldPreferredImages.Count} PreferredImage records to migrate.";
        _logger.Info("Loaded {Count} PreferredImage records to migrate.", oldPreferredImages.Count);

        var oldUserAvatars = new List<DNF_UserAvatar>();
        try
        {
            var rawAvatars = session.CreateSQLQuery(
                    "SELECT JMMUserID, AvatarImageBlob, AvatarImageMetadata FROM JMMUser WHERE AvatarImageBlob IS NOT NULL AND AvatarImageBlob <> ''")
                .AddScalar("JMMUserID", NHibernateUtil.Int32)
                .AddScalar("AvatarImageBlob", NHibernateUtil.BinaryBlob)
                .AddScalar("AvatarImageMetadata", NHibernateUtil.String)
                .List<object[]>();

            foreach (var fields in rawAvatars)
            {
                oldUserAvatars.Add(new DNF_UserAvatar
                {
                    JMMUserID = (int)fields[0],
                    AvatarImageBlob = (byte[])fields[1],
                    AvatarImageMetadata = (string)fields[2],
                });
            }
        }
        catch (GenericADOException)
        {
            _logger.Info("AvatarImageBlob column does not exist on JMMUser, skipping.");
        }

        systemService.StartupMessage = $"{str} - Loaded {oldUserAvatars.Count} user avatar records to migrate.";
        _logger.Info("Loaded {Count} user avatar records to migrate.", oldUserAvatars.Count);

        var migratedCount = 0;
        var tmdbResourceIDToEnabled = new Dictionary<string, bool>(oldTmdbImages.Count);
        var oldTMDBImageIDToNewGuid = new Dictionary<int, Guid>(oldTmdbImages.Count);
        foreach (var old in oldTmdbImages)
        {
            var resourceID = TmdbImageService.SafeTransformResourceID(old.RemoteFileName);
            var guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.TMDB, resourceID);
            if (RepoFactory.ShokoImage.GetByID(guid) != null)
            {
                oldTMDBImageIDToNewGuid[old.TMDB_ImageID] = guid;
                continue;
            }

            // Try eager detection from ResourceID as preferred source
            var contentType = ContentTypeHelper.UnknownMimeType;
            try
            {
                if (imageManager.GetContentTypeFromResourceID(DataSource.TMDB, resourceID) is { Length: > 0 } eager)
                    contentType = eager;
            }
            catch (UnsupportedImageTypeException ex)
            {
                _logger.Warn(ex, "Unsupported image type for {ResourceID}, falling back.", resourceID);
            }

            // Fallback to ContentTypeHelper if eager detection didn't yield a result
            if (contentType is ContentTypeHelper.UnknownMimeType && ContentTypeHelper.TryGetContentType(resourceID, out var mapped))
                contentType = mapped;

            var guidStr = guid.ToString("N");
            var ext = ShokoImage.GetExtensionForMimeType(contentType);
            var oldFileName = Path.GetFileNameWithoutExtension(resourceID);
            var oldFileExt = resourceID.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? ".png"
                : Path.GetExtension(resourceID);
            var oldHashedName = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(oldFileName))).ToLower();
            var oldPath = Path.Join(imagesPath, "TMDB_old", oldHashedName[..2], oldHashedName + oldFileExt);
            var newPath = Path.Join(imagesPath, "TMDB", guidStr[..2], guidStr + ext);
            var oldPathExists = File.Exists(oldPath);
            var newPathExists = File.Exists(newPath);

            var languageCode = old.Language is { Length: 2 } ? old.Language : null;

            var width = old.Width > 0 ? old.Width : (int?)null;
            var height = old.Height > 0 ? old.Height : (int?)null;
            if (oldPathExists)
            {
                try
                {
                    var metadata = new MagickImageInfo(oldPath);
                    width = (int)metadata.Width;
                    height = (int)metadata.Height;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to get image metadata for {Path}", oldPath);
                    oldPathExists = false;
                }
            }
            else if (newPathExists)
            {
                try
                {
                    var metadata = new MagickImageInfo(newPath);
                    width = (int)metadata.Width;
                    height = (int)metadata.Height;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to get image metadata for {Path}", newPath);
                    File.Delete(newPath);
                }
            }

            var image = new ShokoImage
            {
                ID = guid,
                PrimaryID = guid,
                Source = DataSource.TMDB,
                ResourceID = resourceID,
                LanguageCode = languageCode,
                Width = width,
                Height = height,
                ContentType = contentType,
                DownloadAttempts = (byte)(oldPathExists ? 1 : 0),
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            };
            RepoFactory.ShokoImage.Save(image);

            tmdbResourceIDToEnabled[resourceID] = old.IsEnabled;
            oldTMDBImageIDToNewGuid[old.TMDB_ImageID] = guid;

            if (oldPathExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                File.Move(oldPath, newPath, overwrite: true);
            }

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Migrating TMDB images... {migratedCount}/{oldTmdbImages.Count}";
            }
        }

        systemService.StartupMessage = $"{str} - Migrated {migratedCount} TMDB images to ShokoImage.";
        _logger.Info("Migrated {Count} TMDB_Image records to ShokoImage.", migratedCount);

        migratedCount = 0;
        foreach (var old in oldTmdbImageEntities)
        {
            var resourceID = TmdbImageService.SafeTransformResourceID(old.RemoteFileName);
            var guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.TMDB, resourceID);
            if (RepoFactory.ShokoImage.GetByID(guid) is null)
            {
                var guidStr = guid.ToString("N");
                var oldFileName = Path.GetFileNameWithoutExtension(resourceID);
                var oldFileExt = resourceID.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    ? ".png"
                    : Path.GetExtension(resourceID);
                var oldHashedName = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(oldFileName))).ToLower();
                var oldPath = Path.Join(imagesPath, "TMDB_old", oldHashedName[..2], oldHashedName + oldFileExt);
                var newPath = Path.Join(imagesPath, "TMDB", guidStr[..2], guidStr);
                MigrateImage(resourceID, DataSource.TMDB, oldPath, newPath, imageManager);
            }

            var mappedEntityType = old.TmdbEntityType switch
            {
                1 => DataEntityType.Collection,
                2 => DataEntityType.Movie,
                4 => DataEntityType.Show,
                8 => DataEntityType.Season,
                16 => DataEntityType.Episode,
                32 => DataEntityType.Company,
                128 => DataEntityType.Network,
                256 => DataEntityType.Person,
                _ => DataEntityType.Unknown,
            };
            if (mappedEntityType == DataEntityType.Unknown)
                continue;

            var entityID = old.TmdbEntityID.ToString();
            var hasXref = RepoFactory.ShokoImage_Entity.GetByImageID(guid)
                .Any(x => x.EntitySource is DataSource.TMDB && x.EntityType == mappedEntityType && x.EntityID == entityID && x.ImageType == old.ImageType);
            if (!hasXref)
            {
                var xref = new ShokoImage_Entity
                {
                    ImageID = guid,
                    PrimaryImageID = guid,
                    ImageType = old.ImageType,
                    ImageSource = DataSource.TMDB,
                    EntitySource = DataSource.TMDB,
                    EntityType = mappedEntityType,
                    EntityID = entityID,
                    Ordering = Math.Max(0, old.Ordering),
                    EntityReleasedAt = old.ReleasedAt,
                    IsEnabled = tmdbResourceIDToEnabled.TryGetValue(resourceID, out var isEnabled) && isEnabled,
                    IsDesired = true,
                    Source = DataSource.TMDB,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                };
                RepoFactory.ShokoImage_Entity.Save(xref);
            }

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Migrating TMDB image xrefs... {migratedCount}/{oldTmdbImageEntities.Count}";
            }
        }

        _logger.Info("Migrated {Count} TMDB_Image_Entity records to ShokoImage_Entity.", migratedCount);
        systemService.StartupMessage = $"{str} - Migrated {migratedCount} TMDB image xrefs to ShokoImage_Entity.";

        // Migrate AniDB PreferredImage records
        migratedCount = 0;
        foreach (var old in oldPreferredImages)
        {
            Guid guid;
            if (old is { ImageSource: 0 /* abstractions and old internal anidb */, ImageType: ImageEntityType.Primary })
            {
                // AniDB Poster — find Picname from the anime record
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(old.AnidbAnimeID);
                if (anime is null || string.IsNullOrEmpty(anime.Picname))
                    continue;

                guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, anime.Picname);
            }
            else if (old is { ImageSource: 1 /* abstractions tmdb */ or 4 /* old internal tmdb */})
            {
                // TMDB — look up from old TMDB ImageID
                if (!oldTMDBImageIDToNewGuid.TryGetValue(old.ImageID, out guid))
                    continue;
            }
            else
            {
                continue;
            }

            DataEntityType entityType;
            DataSource entitySource;
            int entityID;
            if (old.AnidbEpisodeID == 0 || old.AnidbEpisodeID is null)
            {
                if (RepoFactory.AnimeSeries.GetByAnimeID(old.AnidbAnimeID) is not { } shokoSeries)
                    continue;

                entityType = DataEntityType.Series;
                entityID = shokoSeries.AnimeSeriesID;
                entitySource = DataSource.Shoko;
            }
            else
            {
                if (RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(old.AnidbEpisodeID.Value) is not { } shokoEpisode)
                    continue;

                entityType = DataEntityType.Episode;
                entityID = shokoEpisode.AnimeEpisodeID;
                entitySource = DataSource.Shoko;
            }

            var xref = new ShokoImage_Entity
            {
                ImageID = guid,
                PrimaryImageID = guid,
                ImageType = old.ImageType,
                ImageSource = (DataSource)old.ImageSource,
                EntitySource = entitySource,
                EntityType = entityType,
                EntityID = entityID.ToString(),
                IsPreferred = true,
                IsEnabled = true,
                IsDesired = true,
                Source = DataSource.User,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            };
            RepoFactory.ShokoImage_Entity.Save(xref);

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Migrating PreferredImage records... {migratedCount}/{oldPreferredImages.Count}";
            }
        }

        _logger.Info("Migrated {Count} PreferredImage records.", migratedCount);
        systemService.StartupMessage = $"{str} - Migrated {migratedCount} PreferredImage records.";

        // User avatar migration
        migratedCount = 0;
        foreach (var old in oldUserAvatars)
        {
            // Parse AvatarImageMetadata JSON to get ContentType, Width, Height
            DNF_UserAvatarMetadata metadata;
            try
            {
                metadata = JsonConvert.DeserializeObject<DNF_UserAvatarMetadata>(old.AvatarImageMetadata);
                if (metadata is null)
                {
                    _logger.Warn("Failed to parse avatar metadata for user {UserID}, skipping.", old.JMMUserID);
                    continue;
                }
            }
            catch
            {
                _logger.Warn("Failed to parse avatar metadata for user {UserID}, skipping.", old.JMMUserID);
                continue;
            }

            var md5Hex = Convert.ToHexString(MD5.HashData(old.AvatarImageBlob)).ToLower();
            var guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.User, md5Hex);
            if (RepoFactory.ShokoImage.GetByID(guid) is null)
            {
                var image = new ShokoImage
                {
                    ID = guid,
                    PrimaryID = guid,
                    Source = DataSource.User,
                    ResourceID = md5Hex,
                    ContentType = metadata.ContentType ?? "image/png",
                    Width = metadata.Width > 0 ? metadata.Width : null,
                    Height = metadata.Height > 0 ? metadata.Height : null,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                };
                RepoFactory.ShokoImage.Save(image);
            }

            // Write blob to disk
            var guidStr = guid.ToString("N");
            var contentType = metadata.ContentType ?? "image/png";
            var ext = ShokoImage.GetExtensionForMimeType(contentType);
            var newPath = Path.Join(imagesPath, "User", guidStr[..2], guidStr + ext);
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.WriteAllBytes(newPath, old.AvatarImageBlob);

            // Create ShokoImage_Entity xref
            var xref = new ShokoImage_Entity
            {
                ImageID = guid,
                PrimaryImageID = guid,
                ImageType = ImageEntityType.Primary,
                ImageSource = DataSource.User,
                EntitySource = DataSource.Shoko,
                EntityType = DataEntityType.User,
                EntityID = old.JMMUserID.ToString(),
                IsPreferred = true,
                IsEnabled = true,
                IsDesired = true,
                Source = DataSource.Shoko,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            };
            RepoFactory.ShokoImage_Entity.Save(xref);

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Migrating user avatars... {migratedCount}/{oldUserAvatars.Count}";
            }
        }

        _logger.Info("Migrated {Count} user avatar records.", migratedCount);
        systemService.StartupMessage = $"{str} - Migrated {migratedCount} user avatar records.";

        migratedCount = 0;
        var anidbAnimeList = RepoFactory.AniDB_Anime.GetAll();
        foreach (var anime in anidbAnimeList)
        {
            if (string.IsNullOrEmpty(anime.Picname))
                continue;

            var resourceID = anime.Picname;
            var guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, resourceID);
            if (RepoFactory.ShokoImage.GetByID(guid) is null)
            {
                var guidStr = guid.ToString("N");
                var oldPath = Path.Join(
                    imagesPath,
                    "AniDB_old",
                    anime.AnimeID.ToString() is { Length: > 1 } sid
                        ? sid[..2] : anime.AnimeID.ToString(),
                    resourceID
                );
                var newPath = Path.Join(imagesPath, "AniDB", guidStr[..2], guidStr);
                MigrateImage(resourceID, DataSource.AniDB, oldPath, newPath, imageManager);
            }

            var entityID = anime.AnimeID.ToString();
            var hasXref = RepoFactory.ShokoImage_Entity.GetByImageID(guid)
                .Any(x => x is { EntitySource: DataSource.AniDB, EntityType: DataEntityType.Anime } && x.EntityID == entityID);
            if (!hasXref)
            {
                var xref = new ShokoImage_Entity
                {
                    ImageID = guid,
                    PrimaryImageID = guid,
                    ImageType = ImageEntityType.Primary,
                    ImageSource = DataSource.AniDB,
                    EntitySource = DataSource.AniDB,
                    EntityType = DataEntityType.Anime,
                    EntityID = anime.AnimeID.ToString(),
                    IsEnabled = true,
                    IsDesired = true,
                    Source = DataSource.AniDB,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                };
                RepoFactory.ShokoImage_Entity.Save(xref);
            }

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Backfilling AniDB anime images... {migratedCount}/{anidbAnimeList.Count}";
            }
        }

        _logger.Info("Backfilled {Count} AniDB anime image records.", migratedCount);
        systemService.StartupMessage = $"{str} - Backfilled {migratedCount} AniDB anime image records.";

        migratedCount = 0;
        var anidbCreatorList = RepoFactory.AniDB_Creator.GetAll();
        foreach (var creator in anidbCreatorList)
        {
            if (string.IsNullOrEmpty(creator.ImagePath))
                continue;

            var resourceID = creator.ImagePath;
            var guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, resourceID);
            if (RepoFactory.ShokoImage.GetByID(guid) is null)
            {
                var guidStr = guid.ToString("N");
                var oldPath = Path.Join(
                    imagesPath,
                    "AniDB_Creator_old",
                    creator.CreatorID.ToString() is { Length: > 1 } sid
                        ? sid[..2] : creator.CreatorID.ToString(),
                    resourceID
                );
                var newPath = Path.Join(imagesPath, "AniDB", guidStr[..2], guidStr);
                MigrateImage(resourceID, DataSource.AniDB, oldPath, newPath, imageManager);
            }

            var entityID = creator.CreatorID.ToString();
            var hasXref = RepoFactory.ShokoImage_Entity.GetByImageID(guid)
                .Any(x => x is { EntitySource: DataSource.AniDB, EntityType: DataEntityType.Creator } && x.EntityID == entityID);
            if (!hasXref)
            {
                var xref = new ShokoImage_Entity
                {
                    ImageID = guid,
                    PrimaryImageID = guid,
                    ImageType = ImageEntityType.Primary,
                    ImageSource = DataSource.AniDB,
                    EntitySource = DataSource.AniDB,
                    EntityType = DataEntityType.Creator,
                    EntityID = creator.CreatorID.ToString(),
                    IsEnabled = true,
                    IsDesired = settings.AniDb.DownloadCreators,
                    Source = DataSource.AniDB,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                };
                RepoFactory.ShokoImage_Entity.Save(xref);
            }

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Backfilling AniDB creator images... {migratedCount}/{anidbCreatorList.Count}";
            }
        }

        _logger.Info("Backfilled {Count} AniDB creator image records.", migratedCount);
        systemService.StartupMessage = $"{str} - Backfilled {migratedCount} AniDB creator image records.";

        migratedCount = 0;
        var anidbCharacterList = RepoFactory.AniDB_Character.GetAll();
        foreach (var character in anidbCharacterList)
        {
            if (string.IsNullOrEmpty(character.ImagePath))
                continue;

            var resourceID = character.ImagePath;
            var guid = IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, resourceID);
            if (RepoFactory.ShokoImage.GetByID(guid) is null)
            {
                var guidStr = guid.ToString("N");
                var oldPath = Path.Join(
                    imagesPath,
                    "AniDB_Char_old",
                    character.CharacterID.ToString() is { Length: > 1 } sid
                        ? sid[..2] : character.CharacterID.ToString(),
                    resourceID);
                var newPath = Path.Join(imagesPath, "AniDB", guidStr[..2], guidStr);
                MigrateImage(resourceID, DataSource.AniDB, oldPath, newPath, imageManager);
            }

            var entityID = character.CharacterID.ToString();
            var hasXref = RepoFactory.ShokoImage_Entity.GetByImageID(guid)
                .Any(x => x is { EntitySource: DataSource.AniDB, EntityType: DataEntityType.Character } && x.EntityID == entityID);
            if (!hasXref)
            {
                var xref = new ShokoImage_Entity
                {
                    ImageID = guid,
                    PrimaryImageID = guid,
                    ImageType = ImageEntityType.Primary,
                    ImageSource = DataSource.AniDB,
                    EntitySource = DataSource.AniDB,
                    EntityType = DataEntityType.Character,
                    EntityID = character.CharacterID.ToString(),
                    IsEnabled = true,
                    IsDesired = settings.AniDb.DownloadCharacters,
                    Source = DataSource.AniDB,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                };
                RepoFactory.ShokoImage_Entity.Save(xref);
            }

            migratedCount++;
            if (migratedCount % 1000 == 0)
            {
                systemService.StartupMessage = $"{str} - Backfilling AniDB character images... {migratedCount}/{anidbCharacterList.Count}";
            }
        }

        _logger.Info("Backfilled {Count} AniDB character image records.", migratedCount);
        systemService.StartupMessage = $"{str} - Backfilled {migratedCount} AniDB character image records.";

        // 3k. Cleanup
        var tablesToDrop = new[] { "TMDB_Image", "TMDB_Image_Entity", "AniDB_Anime_PreferredImage", "AniDB_Episode_PreferredImage" };
        foreach (var table in tablesToDrop)
        {
            try
            {
                session.CreateSQLQuery($"DROP TABLE {table};").ExecuteUpdate();
            }
            catch (GenericADOException) { }
        }
        try
        {
            session.CreateSQLQuery("ALTER TABLE JMMUser DROP COLUMN AvatarImageBlob;").ExecuteUpdate();
        }
        catch (GenericADOException) { }
        try
        {
            session.CreateSQLQuery("ALTER TABLE JMMUser DROP COLUMN AvatarImageMetadata;").ExecuteUpdate();
        }
        catch (GenericADOException) { }

        // Remove old image directories
        foreach (var oldName in new[] { "AniDB_old", "AniDB_Char_old", "AniDB_Creator_old", "TMDB_old" })
        {
            var dir = Path.Join(imagesPath, oldName);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        systemService.StartupMessage = str;
        _logger.Info("Completed migration to unified images.");
    }

    private static void MigrateImage(string resourceID, DataSource source, string oldPath, string newPath, ImageManager imageManager)
    {
        var guid = IImageManager.GetIDForImageSourceAndResourceID(source, resourceID);
        var oldPathExists = File.Exists(oldPath);
        var newPathExists = File.Exists(newPath);

        // Try eager detection from ResourceID as preferred source
        var contentType = ContentTypeHelper.UnknownMimeType;
        try
        {
            if (imageManager.GetContentTypeFromResourceID(DataSource.TMDB, resourceID) is { Length: > 0 } eager)
                contentType = eager;
        }
        catch (UnsupportedImageTypeException ex)
        {
            _logger.Warn(ex, "Unsupported image type for {ResourceID}, falling back.", resourceID);
            return;
        }

        // Fallback to ContentTypeHelper if eager detection didn't yield a result
        if (contentType is ContentTypeHelper.UnknownMimeType && ContentTypeHelper.TryGetContentType(resourceID, out var mapped))
            contentType = mapped;

        var ext = ShokoImage.GetExtensionForMimeType(contentType);
        newPath += ext;

        var width = (int?)null;
        var height = (int?)null;
        if (oldPathExists)
        {
            try
            {
                var metadata = new MagickImageInfo(oldPath);
                width = (int)metadata.Width;
                height = (int)metadata.Height;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not get metadata for {Path}", oldPath);
                oldPathExists = false;
            }
        }
        else if (newPathExists)
        {
            try
            {
                var metadata = new MagickImageInfo(newPath);
                width = (int)metadata.Width;
                height = (int)metadata.Height;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to get image metadata for {Path}", newPath);
                File.Delete(newPath);
            }
        }

        var image = new ShokoImage
        {
            ID = guid,
            PrimaryID = guid,
            Source = source,
            ResourceID = resourceID,
            ContentType = contentType,
            Width = width,
            Height = height,
            DownloadAttempts = (byte)(oldPathExists ? 1 : 0),
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };
        RepoFactory.ShokoImage.Save(image);

        if (oldPathExists)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Move(oldPath, newPath, overwrite: true);
        }
    }

    private static ImageEntityType LegacyImageEntityTypeConverter(int legacyType) => legacyType switch
    {
        1 => ImageEntityType.Backdrop,
        2 => ImageEntityType.Banner,
        3 => ImageEntityType.Logo,
        4 => ImageEntityType.Primary,
        5 => ImageEntityType.Disc,
        6 => ImageEntityType.Primary,
        7 => ImageEntityType.Backdrop,
        8 => ImageEntityType.Primary,
        9 => ImageEntityType.Primary,
        _ => ImageEntityType.None,
    };

    public static void MoveImagesToExtensionPaths()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var imageManager = (ImageManager)ISystemService.StaticServices.GetRequiredService<IImageManager>();
        var imagesPath = ApplicationPaths.Instance.ImagesPath;
        var images = RepoFactory.ShokoImage.GetAll();
        var str = systemService.StartupMessage ?? string.Empty;

        // Correct default ContentType from ResourceIDs
        var correctedCount = 0;
        foreach (var image in images)
        {
            if (image.ContentType is not ContentTypeHelper.UnknownMimeType)
                continue;

            try
            {
                var contentType = imageManager.GetContentTypeFromResourceID(image.Source, image.ResourceID);
                if (contentType is not null)
                {
                    image.ContentType = contentType;
                    RepoFactory.ShokoImage.Save(image);
                    correctedCount++;
                }
            }
            catch (UnsupportedImageTypeException ex)
            {
                _logger.Warn(ex, "Unsupported image type for {ResourceID}, keeping default.", image.ResourceID);
            }

            if (correctedCount % 1000 == 0 && correctedCount > 0)
                systemService.StartupMessage = $"{str} - Correcting image content types... {correctedCount}";
        }
        _logger.Info("Corrected {Count} image content types from resource IDs.", correctedCount);
        systemService.StartupMessage = $"{str} - Corrected {correctedCount} image content types.";

        // Move files to extension paths
        var migratedCount = 0;
        foreach (var image in images)
        {
            var id = image.ID.ToString("N");
            var ext = ShokoImage.GetExtensionForMimeType(image.ContentType);
            var source = image.Source.ToString();
            var oldPath = Path.Join(imagesPath, source, id[..2], id);
            var newPath = Path.Join(imagesPath, source, id[..2], id + ext);
            if (File.Exists(oldPath))
            {
                if (!File.Exists(newPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                    File.Move(oldPath, newPath, overwrite: false);
                }
                else
                {
                    File.Delete(oldPath);
                }
            }

            migratedCount++;
            if (migratedCount % 1000 == 0)
                systemService.StartupMessage = $"{str} - Moving images to extension paths... {migratedCount}/{images.Count}";
        }

        _logger.Info("Completed moving {Count} images to extension paths.", migratedCount);
        systemService.StartupMessage = $"{str} - Completed moving {migratedCount} images to extension paths.";
    }

    public static void PopulateImageAvailability()
    {
        var systemService = ISystemService.StaticServices.GetRequiredService<SystemService>();
        var str = systemService.StartupMessage ?? string.Empty;
        var images = RepoFactory.ShokoImage.GetAll();
        var scannedCount = 0;
        var updatedCount = 0;
        foreach (var batch in images.Chunk(20))
        {
            // The disk check is I/O bound, so recompute the batch in parallel.
            var changed = new ConcurrentBag<ShokoImage>();
            Parallel.ForEach(batch, image =>
            {
                var wasAvailable = image.IsAvailable;
                if (image.RefreshAvailability() != wasAvailable)
                    changed.Add(image);
            });
            if (!changed.IsEmpty)
            {
                RepoFactory.ShokoImage.Save(changed.ToArray());
                updatedCount += changed.Count;
            }

            scannedCount += batch.Length;
            if (scannedCount % 1000 == 0)
            {
                _logger.Info("Populating image availability flags... {Scanned}/{Total} scanned, {Updated} updated.", scannedCount, images.Count, updatedCount);
                systemService.StartupMessage = $"{str} - Populating image availability flags... {scannedCount}/{images.Count}";
            }
        }
        _logger.Info("Populated availability flag for {Updated} of {Total} images.", updatedCount, images.Count);
        systemService.StartupMessage = $"{str} - Populated availability flag for {updatedCount} images.";
    }

    private class DNF_UserAvatarMetadata
    {
        public string ContentType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DBF_VideoLocal
    {
        public int VideoLocalID { get; set; }

        public string ED2K { get; set; }

        public string MD5 { get; set; }

        public string SHA1 { get; set; }

        public string CRC32 { get; set; }
    }

    public class DBF_AniDB_File
    {
        public int FileID { get; set; }
        public string ED2k { get; set; }
        public int GroupID { get; set; }
        public string File_Source { get; set; }
        public string File_Description { get; set; }
        public DateTime? File_ReleaseDate { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int FileVersion { get; set; }
        public bool? IsCensored { get; set; }
        public bool IsDeprecated { get; set; }
        public int InternalVersion { get; set; }
        public bool IsChaptered { get; set; }
    }

    private class DBF_AniDB_ReleaseGroup
    {
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public string GroupNameShort { get; set; }
    }

    private class DBF_AniDB_FileUpdate
    {
        public string ED2K { get; set; }
        public long FileSize { get; set; }
        public bool HasResponse { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private class DBF_RenamerConfig
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public byte[] Settings { get; set; }
    }

    private class DBF_RenamerScript
    {
        public string ScriptName { get; set; }
        public string RenamerType { get; set; }
        public bool IsEnabledOnImport { get; set; }
        public string Script { get; set; }
    }

    private class DNF_AniDB_Vote
    {
        public int EntityID { get; set; }

        public int VoteValue { get; set; }

        public VoteType VoteType { get; set; }
    }

    private class DNF_TMDB_Image
    {
        public int TMDB_ImageID { get; set; }
        public bool IsEnabled { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Language { get; set; }
        public string RemoteFileName { get; set; }
        public double UserRating { get; set; }
        public int UserVotes { get; set; }
    }

    private class DNF_TMDB_Image_Entity
    {
        public int TMDB_Image_EntityID { get; set; }
        public string RemoteFileName { get; set; }
        public ImageEntityType ImageType { get; set; }
        public int TmdbEntityType { get; set; }
        public int TmdbEntityID { get; set; }
        public int Ordering { get; set; }
        public DateOnly? ReleasedAt { get; set; }
    }

    private class DNF_AniDB_PreferredImage
    {
        public int PreferredImageID { get; set; }
        public int AnidbAnimeID { get; set; }
        public int? AnidbEpisodeID { get; set; }
        public int ImageID { get; set; }
        public int ImageSource { get; set; }
        public ImageEntityType ImageType { get; set; }
    }

    private class DNF_UserAvatar
    {
        public int JMMUserID { get; set; }
        public byte[] AvatarImageBlob { get; set; }
        public string AvatarImageMetadata { get; set; }
    }

}
