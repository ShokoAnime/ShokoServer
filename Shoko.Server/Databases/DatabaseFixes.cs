using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NHibernate;
using NLog;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

#pragma warning disable CA2012
#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public class DatabaseFixes
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static void NoOperation() { }

    public static void UpdateAllStats()
    {
        var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().ConfigureAwait(false).GetAwaiter().GetResult();
        Task.WhenAll(RepoFactory.AnimeSeries.GetAll().Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID))).GetAwaiter()
            .GetResult();
    }

    public static void MigrateGroupFilterToFilterPreset()
    {
        var legacyConverter = Utils.ServiceContainer.GetRequiredService<LegacyFilterConverter>();
        using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
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

        var filters = new Dictionary<GroupFilter, List<GroupFilterCondition>>();
        foreach (var item in groupFilters)
        {
            var fields = (object[])item;
            var filter = new GroupFilter
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
            var conditions = JsonConvert.DeserializeObject<List<GroupFilterCondition>>((string)fields[8]);
            filters[filter] = conditions;
        }

        var idMappings = new Dictionary<int, int>();
        // first, do the ones with no parent
        foreach (var key in filters.Keys.Where(a => a.ParentGroupFilterID == null).OrderBy(a => a.GroupFilterID))
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
        using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        session.CreateSQLQuery("DROP TABLE GroupFilter; DROP TABLE GroupFilterCondition").ExecuteUpdate();
    }

    public static void DeleteSeriesUsersWithoutSeries()
    {
        //DB Fix Series not deleting series_user
        var list = new HashSet<int>(RepoFactory.AnimeSeries.Cache.Keys);
        RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.Cache.Values
            .Where(a => !list.Contains(a.AnimeSeriesID))
            .ToList());
    }

    public static void FixHashes()
    {
        try
        {
            foreach (var vid in RepoFactory.VideoLocal.GetAll())
            {
                var fixedHash = false;
                if (vid.CRC32.Equals("00000000"))
                {
                    vid.CRC32 = null;
                    fixedHash = true;
                }

                if (vid.MD5.Equals("00000000000000000000000000000000"))
                {
                    vid.MD5 = null;
                    fixedHash = true;
                }

                if (vid.SHA1.Equals("0000000000000000000000000000000000000000"))
                {
                    vid.SHA1 = null;
                    fixedHash = true;
                }

                if (fixedHash)
                {
                    RepoFactory.VideoLocal.Save(vid, false);
                    _logger.Info("Fixed hashes on file: {0}", vid.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }
    }

    public static void RefreshAniDBInfoFromXML()
    {
        var i = 0;
        var list = RepoFactory.AniDB_Episode.GetAll().Where(a => string.IsNullOrEmpty(a.Description))
            .Select(a => a.AnimeID).Distinct().ToList();

        var jobFactory = Utils.ServiceContainer.GetRequiredService<JobFactory>();
        foreach (var animeID in list)
        {
            if (i % 10 == 0)
            {
                ServerState.Instance.ServerStartingStatus = $"Database - Validating - Populating AniDB Info from Cache {i}/{list.Count}...";
            }

            i++;
            try
            {
                var command = jobFactory.CreateJob<GetAniDBAnimeJob>(c =>
                {
                    c.CacheOnly = true;
                    c.DownloadRelations = false;
                    c.AnimeID = animeID;
                    c.CreateSeriesEntry = false;
                    c.SkipTmdbUpdate = true;
                });
                command.Process().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _logger.Error(
                    $"There was an error Populating AniDB Info for AniDB_Anime {animeID}, Update the Series' AniDB Info for a full stack: {e.Message}");
            }
        }
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

    public static void MigrateAniDB_FileUpdates()
    {
        var updates = RepoFactory.AniDB_File.GetAll()
            .Select(file => new AniDB_FileUpdate
            {
                FileSize = file.FileSize,
                Hash = file.Hash,
                HasResponse = true,
                UpdatedAt = file.DateTimeUpdated,
            })
            .ToList();

        updates.AddRange(RepoFactory.CrossRef_File_Episode.GetAll().Where(a => RepoFactory.AniDB_File.GetByHash(a.Hash) == null)
            .Select(a => (xref: a, vl: RepoFactory.VideoLocal.GetByEd2k(a.Hash))).Where(a => a.vl != null).Select(a => new AniDB_FileUpdate
            {
                FileSize = a.xref.FileSize,
                Hash = a.xref.Hash,
                HasResponse = false,
                UpdatedAt = a.vl.DateTimeCreated,
            }));

        RepoFactory.AniDB_FileUpdate.Save(updates);
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
        var orphanedSeries = RepoFactory.AnimeSeries.GetAll().Where(a => a.AnimeGroup == null).ToArray();
        var groupCreator = Utils.ServiceContainer.GetRequiredService<AnimeGroupCreator>();
        using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
        foreach (var series in orphanedSeries)
        {
            try
            {
                var group = groupCreator.GetOrCreateSingleGroupForSeries(series);
                series.AnimeGroupID = group.AnimeGroupID;
                RepoFactory.AnimeSeries.Save(series, false, false);
            }
            catch (Exception e)
            {
                var name = "";
                try
                {
                    name = series.PreferredTitle;
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
        var fileListDict = RepoFactory.AnimeEpisode.GetAll()
            .ToDictionary(episode => episode.AnimeEpisodeID, episode => episode.VideoLocals);
        var episodesURsToSave = new List<SVR_AnimeEpisode_User>();
        var episodeURsToRemove = new List<SVR_AnimeEpisode_User>();
        foreach (var episodeUserRecord in RepoFactory.AnimeEpisode_User.GetAll())
        {
            // Remove any unknown episode user records.
            if (!fileListDict.ContainsKey(episodeUserRecord.AnimeEpisodeID) ||
                !userDict.ContainsKey(episodeUserRecord.JMMUserID))
            {
                episodeURsToRemove.Add(episodeUserRecord);
                continue;
            }

            // Fetch the file user record for when a file for the episode was last watched.
            var fileUserRecord = fileListDict[episodeUserRecord.AnimeEpisodeID]
                .Select(file => RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(episodeUserRecord.JMMUserID, file.VideoLocalID))
                .Where(record => record != null)
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

        _logger.Debug($"Found {episodesURsToSave.Count} episode user records to fix.");
        RepoFactory.AnimeEpisode_User.Delete(episodeURsToRemove);
        RepoFactory.AnimeEpisode_User.Save(episodesURsToSave);
        _logger.Debug($"Updating series user records and series stats.");
        // Update all the series and groups to use the new watch dates.
        var seriesList = episodesURsToSave
            .GroupBy(record => record.AnimeSeriesID)
            .Select(records => (RepoFactory.AnimeSeries.GetByID(records.Key),
                records.Select(record => record.JMMUserID).Distinct())).ToList();
        var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
        foreach (var (series, userIDs) in seriesList)
        {
            // No idea why we would have episode entries for a deleted series, but just in case.
            if (series == null)
            {
                continue;
            }

            // Update the timestamp for when an episode for the series was last partially or fully watched.
            foreach (var userID in userIDs)
            {
                var seriesUserRecord = seriesService.GetOrCreateUserRecord(series.AnimeSeriesID, userID);
                seriesUserRecord.LastEpisodeUpdate = DateTime.Now;
                _logger.Debug(
                    $"Updating series user contract for user \"{userDict[seriesUserRecord.JMMUserID].Username}\". (UserID={seriesUserRecord.JMMUserID},SeriesID={seriesUserRecord.AnimeSeriesID})");
                RepoFactory.AnimeSeries_User.Save(seriesUserRecord);
            }

            // Update the rest of the stats for the series.
            seriesService.UpdateStats(series, true, true);
        }

        var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
        var groups = seriesList.Select(a => a.Item1.AnimeGroup).Where(a => a != null).DistinctBy(a => a.AnimeGroupID);
        foreach (var group in groups)
        {
            groupService.UpdateStatsFromTopLevel(group, true, true);
        }
    }

    public static void FixTagParentIDsAndNameOverrides()
    {
        var xmlUtils = Utils.ServiceContainer.GetRequiredService<HttpXmlUtils>();
        var animeParser = Utils.ServiceContainer.GetRequiredService<HttpAnimeParser>();
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
                if (response == null) throw new NullReferenceException(nameof(response));
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
        var animeToSave = new HashSet<SVR_AniDB_Anime>();
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
        var xmlUtils = Utils.ServiceContainer.GetRequiredService<HttpXmlUtils>();
        var animeParser = Utils.ServiceContainer.GetRequiredService<HttpAnimeParser>();
        var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
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
                if (response == null) throw new NullReferenceException(nameof(response));
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
        {
            scheduler.StartJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = animeID;
                c.DownloadRelations = false;
                c.ForceRefresh = true;
            }).GetAwaiter().GetResult();
        }

        _logger.Info($"Done updating last updated episode timestamps for {anidbAnimeIDs.Count} local anidb anime entries. Updated {updatedCount} episodes, reset {resetCount} episodes and queued anime {animeToUpdateSet.Count} updates for {faultyCount} faulty episodes.");
    }

    public static void UpdateSeriesWithHiddenEpisodes()
    {
        var seriesList = RepoFactory.AnimeEpisode.GetAll()
            .Where(episode => episode.IsHidden)
            .Select(episode => episode.AnimeSeriesID)
            .Distinct()
            .Select(seriesID => RepoFactory.AnimeSeries.GetByID(seriesID))
            .Where(series => series != null)
            .ToList();

        var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
        foreach (var series in seriesList)
            seriesService.UpdateStats(series, false, true);
    }

    public static void FixOrphanedShokoEpisodes()
    {
        var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
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
        var shokoEpisodesToSave = new List<SVR_AnimeEpisode>();
        foreach (var episode in allAniDBEpisodes.Values)
        {
            // No shoko episode, continue.
            var shokoEpisode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID);
            if (shokoEpisode == null)
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
        var anidbFilesToRemove = new List<SVR_AniDB_File>();
        var xrefsToRemove = new List<SVR_CrossRef_File_Episode>();
        var videosToRefetch = new List<SVR_VideoLocal>();
        var tmdbXrefsToRemove = new List<CrossRef_AniDB_TMDB_Episode>();
        foreach (var shokoEpisode in shokoEpisodesToRemove)
        {
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(shokoEpisode.AniDB_EpisodeID);
            var videos = xrefs
                .Select(xref => RepoFactory.VideoLocal.GetByEd2kAndSize(xref.Hash, xref.FileSize))
                .Where(video => video != null)
                .ToList();
            var anidbFiles = xrefs
                .Where(xref => xref.CrossRefSource == (int)CrossRefSource.AniDB)
                .Select(xref => RepoFactory.AniDB_File.GetByEd2kAndFileSize(xref.Hash, xref.FileSize))
                .Where(anidbFile => anidbFile != null)
                .ToList();
            var tmdbXrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(shokoEpisode.AniDB_EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            anidbFilesToRemove.AddRange(anidbFiles);
            tmdbXrefsToRemove.AddRange(tmdbXrefs);
        }

        // Schedule a refetch of any video files affected by the removal of the
        // episodes. They were likely moved to another episode entry so let's
        // try and fetch that.
        _logger.Trace($"Scheduling {videosToRefetch.Count} videos for a re-fetch.");
        foreach (var video in videosToRefetch)
        {
            scheduler.StartJob<ProcessFileJob>(c =>
            {
                c.VideoLocalID = video.VideoLocalID;
                c.SkipMyList = true;
                c.ForceRecheck = true;
            }).GetAwaiter().GetResult();
        }

        _logger.Trace($"Deleting {shokoEpisodesToRemove.Count} orphaned shoko episodes.");
        RepoFactory.AnimeEpisode.Delete(shokoEpisodesToRemove);

        _logger.Trace($"Deleting {anidbFilesToRemove.Count} orphaned anidb files.");
        RepoFactory.AniDB_File.Delete(anidbFilesToRemove);

        _logger.Trace($"Deleting {tmdbXrefsToRemove.Count} orphaned tmdb xrefs.");
        RepoFactory.CrossRef_AniDB_TMDB_Episode.Delete(tmdbXrefsToRemove);

        _logger.Trace($"Deleting {xrefsToRemove.Count} orphaned file/episode cross-references.");
        RepoFactory.CrossRef_File_Episode.Delete(xrefsToRemove);
    }

    public static void CleanupAfterAddingTMDB()
    {
        var service = Utils.ServiceContainer.GetRequiredService<TmdbMetadataService>();

        // Remove the "MovieDB" directory in the image directory, since it's no longer used,
        var dir = new DirectoryInfo(Path.Join(ImageUtils.GetBaseImagesPath(), "MovieDB"));
        if (dir.Exists)
            dir.Delete(true);

        // Schedule commands to get the new movie info for existing cross-reference
        service.UpdateAllMovies(true, true).ConfigureAwait(false).GetAwaiter().GetResult();

        // Schedule tmdb searches if we have auto linking enabled.
        service.ScanForMatches().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void CreateDefaultRenamerConfig()
    {
        var existingRenamer = RepoFactory.RenamerConfig.GetByName("Default");
        if (existingRenamer != null)
            return;

        var renamerService = Utils.ServiceContainer.GetRequiredService<RenameFileService>();
        renamerService.RenamersByKey.TryGetValue("WebAOM", out var renamer);

        if (renamer == null)
            return;

        var defaultSettings = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(a => a.Name == "DefaultSettings")?.GetMethod?.Invoke(renamer, null);

        var config = new RenamerConfig
        {
            Name = "Default",
            Type = typeof(WebAOMRenamer),
            Settings = defaultSettings,
        };

        RepoFactory.RenamerConfig.Save(config);
    }

    public static void CleanupAfterRemovingTvDB()
    {
        var dir = new DirectoryInfo(Path.Join(ImageUtils.GetBaseImagesPath(), "TvDB"));
        if (dir.Exists)
            dir.Delete(true);
    }

    public static void ClearQuartzQueue()
    {
        var queueHandler = Utils.ServiceContainer.GetRequiredService<QueueHandler>();
        queueHandler.Clear().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static void RepairMissingTMDBPersons()
    {
        var service = Utils.ServiceContainer.GetRequiredService<TmdbMetadataService>();
        var missingIds = new HashSet<int>();
        var updateCount = 0;
        var skippedCount = 0;
        var peopleIds = RepoFactory.TMDB_Person.GetAll().Select(person => person.TmdbPersonID).ToHashSet();
        var str = ServerState.Instance.ServerStartingStatus;
        foreach (var person in RepoFactory.TMDB_Episode_Cast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in RepoFactory.TMDB_Episode_Crew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        foreach (var person in RepoFactory.TMDB_Movie_Cast.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);
        foreach (var person in RepoFactory.TMDB_Movie_Crew.GetAll())
            if (!peopleIds.Contains(person.TmdbPersonID)) missingIds.Add(person.TmdbPersonID);

        ServerState.Instance.ServerStartingStatus = $"{str} - 0 / {missingIds.Count}";
        _logger.Debug("Found {Count} unique missing TMDB People for Episode & Movie staff", missingIds.Count);
        foreach (var personId in missingIds)
        {
            var (_, updated) = service.UpdatePerson(personId, forceRefresh: true).ConfigureAwait(false).GetAwaiter().GetResult();
            if (updated)
                updateCount++;
            else
                skippedCount++;
            ServerState.Instance.ServerStartingStatus = $"{str} - {updateCount + skippedCount} / {missingIds.Count}";
        }

        _logger.Info("Updated missing TMDB People: Found/Updated/Skipped {Found}/{Updated}/{Skipped}",
            missingIds.Count, updateCount, skippedCount);
    }

    public static void RecreateAnimeCharactersAndCreators()
    {
        var xmlUtils = Utils.ServiceContainer.GetRequiredService<HttpXmlUtils>();
        var animeParser = Utils.ServiceContainer.GetRequiredService<HttpAnimeParser>();
        var animeCreator = Utils.ServiceContainer.GetRequiredService<AnimeCreator>();
        var schedulerFactory = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().ConfigureAwait(false).GetAwaiter().GetResult();
        var animeList = RepoFactory.AniDB_Anime.GetAll();
        var str = ServerState.Instance.ServerStartingStatus;
        ServerState.Instance.ServerStartingStatus = $"{str} - 0 / {animeList.Count}";
        _logger.Info($"Recreating characters and creator relations for {animeList.Count} anidb anime entries...");

        var count = 0;
        foreach (var anime in animeList)
        {
            if (++count % 10 == 0)
            {
                _logger.Info($"Recreating characters and creator relations for anidb anime entries... ({count}/{animeList.Count})");
                ServerState.Instance.ServerStartingStatus = $"{str} - {count} / {animeList.Count}";
            }

            var xml = xmlUtils.LoadAnimeHTTPFromFile(anime.AnimeID).Result;
            if (string.IsNullOrEmpty(xml))
            {
                _logger.Warn($"Unable to load cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = anime.AnimeID;
                    c.CacheOnly = false;
                    c.ForceRefresh = true;
                    c.DownloadRelations = false;
                    c.CreateSeriesEntry = false;
                    c.RelDepth = 0;
                    c.SkipTmdbUpdate = true;
                }).ConfigureAwait(false).GetAwaiter().GetResult();
                continue;
            }

            ResponseGetAnime response;
            try
            {
                response = animeParser.Parse(anime.AnimeID, xml);
                if (response == null) throw new NullReferenceException(nameof(response));
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = anime.AnimeID;
                    c.CacheOnly = false;
                    c.ForceRefresh = true;
                    c.DownloadRelations = false;
                    c.CreateSeriesEntry = false;
                    c.RelDepth = 0;
                    c.SkipTmdbUpdate = true;
                }).ConfigureAwait(false).GetAwaiter().GetResult();
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
        var tmdbMetadataService = Utils.ServiceContainer.GetRequiredService<TmdbMetadataService>();
        var tmdbMovies = RepoFactory.TMDB_Movie.GetAll();
        var tmdbShows = RepoFactory.TMDB_Show.GetAll();
        var movies = tmdbMovies.Count;
        var shows = tmdbShows.Count;
        var str = ServerState.Instance.ServerStartingStatus;
        ServerState.Instance.ServerStartingStatus = $"{str} - 0 / {movies} movies - 0 / {shows} shows";
        _logger.Info($"Scheduling tmdb image updates for {movies} tmdb movies and {shows} tmdb shows...");

        var count = 0;
        foreach (var tmdbMovie in tmdbMovies)
        {
            if (++count % 10 == 0 || count == movies)
            {
                _logger.Info($"Scheduling tmdb image updates for tmdb movies... ({count}/{movies})");
                ServerState.Instance.ServerStartingStatus = $"{str} - {count} / {movies} movies - 0 / {shows} shows";
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
                ServerState.Instance.ServerStartingStatus = $"{str} - {movies} / {movies} movies - {count} / {shows} shows";
            }

            tmdbMetadataService.ScheduleDownloadAllShowImages(tmdbShow.Id)
                .GetAwaiter()
                .GetResult();
        }

        _logger.Info($"Done scheduling tmdb image updates for {movies} tmdb movies and {shows} tmdb shows.");
    }

    public static void MoveTmdbImagesOnDisc()
    {
        var imageDir = Path.Join(ImageUtils.GetBaseImagesPath(), "TMDB");
        if (!Directory.Exists(imageDir))
            return;

        var total = 0;
        var skipped = 0;
        var str = ServerState.Instance.ServerStartingStatus;
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
                ServerState.Instance.ServerStartingStatus = $"{str} - {folderCount} / {folders.Length} folders - 0 / {files.Length} images - {total} total, {skipped} skipped";
                foreach (var file in files)
                {
                    if (++count % 10 == 0 || count == files.Length)
                    {
                        _logger.Info($"Moving TMDb images on disc for folder {folderCount} out of {folders.Length}: {count}/{files.Length} ({total} total, {skipped} skipped)");
                        ServerState.Instance.ServerStartingStatus = $"{str} - {folderCount} / {folders.Length} folders - {count} / {files.Length} images - {total} total, {skipped} skipped";
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

        var imageTypes = Enum.GetValues<ImageEntityType>();
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
            ServerState.Instance.ServerStartingStatus = $"{str} - 0 / {files.Length} {imageType} images";
            foreach (var file in files)
            {
                if (++count % 10 == 0 || count == files.Length)
                {
                    _logger.Info($"Moving TMDb {imageType} images on disc: {count}/{files.Length}");
                    ServerState.Instance.ServerStartingStatus = $"{str} - {count} / {files.Length} {imageType} images";
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
}
