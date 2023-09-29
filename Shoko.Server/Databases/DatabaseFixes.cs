using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NHibernate;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

namespace Shoko.Server.Databases;

public class DatabaseFixes
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public static void MigrateGroupFilterToFilterPreset()
    {
        var legacyConverter = Utils.ServiceContainer.GetRequiredService<LegacyFilterConverter>();
        using var session = DatabaseFactory.SessionFactory.OpenSession();
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
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        session.CreateSQLQuery("DROP TABLE GroupFilter; DROP TABLE GroupFilterCondition").ExecuteUpdate();
    }
    
    public static void MigrateAniDBToNet()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var anidb = settings.AniDb.ServerAddress;
        if (!anidb.EndsWith(".info", StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        settings.AniDb.ServerAddress = anidb.Substring(0, anidb.Length - 5) + ".net";
        Utils.SettingsProvider.SaveSettings();
    }

    public static void DeleteSerieUsersWithoutSeries()
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
                    logger.Info("Fixed hashes on file: {0}", vid.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    public static void FixEmptyVideoInfos()
    {
        // List<SVR_VideoLocal> locals = RepoFactory.VideoLocal.GetAll()
        //     .Where(a => string.IsNullOrEmpty(a.FileName))
        //     .ToList();
        // foreach (SVR_VideoLocal v in locals)
        // {
        //     SVR_VideoLocal_Place p = v.Places.OrderBy(a => a.ImportFolderType).FirstOrDefault();
        //     if (!string.IsNullOrEmpty(p?.FilePath) && v.Media != null)
        //     {
        //         v.FileName = p.FilePath;
        //         int a = p.FilePath.LastIndexOf($"{Path.DirectorySeparatorChar}", StringComparison.InvariantCulture);
        //         if (a > 0)
        //             v.FileName = p.FilePath.Substring(a + 1);
        //         SVR_VideoLocal_Place.FillVideoInfoFromMedia(v, v.Media);
        //         RepoFactory.VideoLocal.Save(v, false);
        //     }
        // }
    }

    public static void RemoveOldMovieDBImageRecords()
    {
        try
        {
            RepoFactory.MovieDB_Fanart.Delete(RepoFactory.MovieDB_Fanart.GetAll());
            RepoFactory.MovieDB_Poster.Delete(RepoFactory.MovieDB_Poster.GetAll());
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Could not RemoveOldMovieDBImageRecords: " + ex);
        }
    }


    public static void FixContinueWatchingGroupFilter_20160406() { }

    public static void MigrateTraktLinks_V1_to_V2()
    {
        // Empty to preserve version info
    }

    public static void MigrateTvDBLinks_V1_to_V2()
    {
        // Empty to preserve version info
    }

    public static void MigrateTvDBLinks_v2_to_V3()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            // Clean up possibly failed migration
            RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinks();

            // This method doesn't need mappings, and it's simple enough to work on all DB types
            // Migrate Special's overrides
            var specials = session
                .CreateSQLQuery(
                    @"SELECT DISTINCT AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber FROM CrossRef_AniDB_TvDBV2 WHERE TvDBSeasonNumber = 0")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("AniDBStartEpisodeType", NHibernateUtil.Int32)
                .AddScalar("AniDBStartEpisodeNumber", NHibernateUtil.Int32)
                .AddScalar("TvDBID", NHibernateUtil.Int32)
                .AddScalar("TvDBSeasonNumber", NHibernateUtil.Int32)
                .AddScalar("TvDBStartEpisodeNumber", NHibernateUtil.Int32)
                .List<object[]>().Select(a => new CrossRef_AniDB_TvDBV2
                {
                    AnimeID = (int)a[0],
                    AniDBStartEpisodeType = (int)a[1],
                    AniDBStartEpisodeNumber = (int)a[2],
                    TvDBID = (int)a[3],
                    TvDBSeasonNumber = (int)a[4],
                    TvDBStartEpisodeNumber = (int)a[5]
                }).ToLookup(a => a.AnimeID);

            // Split them by series so that we can escape on error more easily
            foreach (var special in specials)
            {
                var overrides = TvDBLinkingHelper.GetSpecialsOverridesFromLegacy(special.ToList());
                foreach (var episodeOverride in overrides)
                {
                    var exists =
                        RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(
                            episodeOverride.AniDBEpisodeID, episodeOverride.TvDBEpisodeID);
                    if (exists != null)
                    {
                        continue;
                    }

                    RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Save(episodeOverride);
                }
            }

            // override OVAs if they don't have default links
            var ovas = session
                .CreateSQLQuery(
                    @"SELECT DISTINCT AniDB_Anime.AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber FROM CrossRef_AniDB_TvDBV2 INNER JOIN AniDB_Anime on AniDB_Anime.AnimeID = CrossRef_AniDB_TvDBV2.AnimeID WHERE AnimeType = 1 OR AnimeType = 3")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("AniDBStartEpisodeType", NHibernateUtil.Int32)
                .AddScalar("AniDBStartEpisodeNumber", NHibernateUtil.Int32)
                .AddScalar("TvDBID", NHibernateUtil.Int32)
                .AddScalar("TvDBSeasonNumber", NHibernateUtil.Int32)
                .AddScalar("TvDBStartEpisodeNumber", NHibernateUtil.Int32)
                .List<object[]>().Select(a => new CrossRef_AniDB_TvDBV2
                {
                    AnimeID = (int)a[0],
                    AniDBStartEpisodeType = (int)a[1],
                    AniDBStartEpisodeNumber = (int)a[2],
                    TvDBID = (int)a[3],
                    TvDBSeasonNumber = (int)a[4],
                    TvDBStartEpisodeNumber = (int)a[5]
                }).ToLookup(a => a.AnimeID);

            // Split them by series so that we can escape on error more easily
            foreach (var special in ovas)
            {
                var overrides = TvDBLinkingHelper.GetSpecialsOverridesFromLegacy(special.ToList());
                foreach (var episodeOverride in overrides)
                {
                    var exists =
                        RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(
                            episodeOverride.AniDBEpisodeID, episodeOverride.TvDBEpisodeID);
                    if (exists != null)
                    {
                        continue;
                    }

                    RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Save(episodeOverride);
                }
            }

            // Series Links
            var links = session
                .CreateSQLQuery(
                    @"SELECT AnimeID, TvDBID, CrossRefSource FROM CrossRef_AniDB_TvDBV2")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("TvDBID", NHibernateUtil.Int32)
                .AddScalar("CrossRefSource", NHibernateUtil.Int32)
                .List<object[]>().Select(a => new CrossRef_AniDB_TvDB
                {
                    AniDBID = (int)a[0], TvDBID = (int)a[1], CrossRefSource = (CrossRefSource)a[2]
                }).DistinctBy(a => new[]
                {
                    a.AniDBID, a.TvDBID
                }).ToList();
            foreach (var link in links)
            {
                var exists =
                    RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(
                        link.AniDBID, link.TvDBID);
                if (exists != null)
                {
                    continue;
                }

                RepoFactory.CrossRef_AniDB_TvDB.Save(link);
            }

            // Scan Series Without links for prequel/sequel links
            var list = RepoFactory.CrossRef_AniDB_TvDB.GetSeriesWithoutLinks();

            // AniDB_Anime_Relation is a direct repository, so GetFullLinearRelationTree will be slow
            // Using a visited node set to skip processed nodes should be faster
            var visitedNodes = new HashSet<int>();
            var seriesWithoutLinksLookup = list.ToDictionary(a => a.AniDB_ID);

            foreach (var animeseries in list)
            {
                if (visitedNodes.Contains(animeseries.AniDB_ID))
                {
                    continue;
                }

                var relations = RepoFactory.AniDB_Anime_Relation.GetFullLinearRelationTree(animeseries.AniDB_ID);
                var tvDBID = relations.SelectMany(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a))
                    .FirstOrDefault(a => a != null)?.TvDBID;
                // No link was found in the entire relation tree
                if (tvDBID == null)
                {
                    relations.ForEach(a => visitedNodes.Add(a));
                    continue;
                }

                var seriesToUpdate = relations.Where(a => seriesWithoutLinksLookup.ContainsKey(a))
                    .Select(a => seriesWithoutLinksLookup[a]).ToList();
                foreach (var series in seriesToUpdate)
                {
                    var link = new CrossRef_AniDB_TvDB
                    {
                        AniDBID = series.AniDB_ID, TvDBID = tvDBID.Value, CrossRefSource = CrossRefSource.Automatic
                    };
                    // No need to check for existence
                    RepoFactory.CrossRef_AniDB_TvDB.Save(link);
                    visitedNodes.Add(series.AniDB_ID);
                }
            }

            list = RepoFactory.AnimeSeries.GetAll().ToList();
            var count = 0;

            list.AsParallel().ForAll(animeseries =>
            {
                Interlocked.Increment(ref count);
                if (count % 50 == 0)
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, "Generating TvDB Episode Matchings",
                        $" {count}/{list.Count}");
                }

                TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true);
            });

            var dropV2 = "DROP TABLE CrossRef_AniDB_TvDBV2";
            session.CreateSQLQuery(dropV2).ExecuteUpdate();
        }
    }

    public static void RegenTvDBMatches()
    {
        RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinks();

        var list = RepoFactory.AnimeSeries.GetAll().ToList();
        var count = 0;

        list.AsParallel().ForAll(animeseries =>
        {
            Interlocked.Increment(ref count);
            if (count % 50 == 0)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, "Generating TvDB Episode Matchings",
                    $" {count}/{list.Count}");
            }

            TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true);
        });
    }

    public static void FixAniDB_EpisodesWithMissingTitles()
    {
        // Deprecated. It's been a while since this was relevant
    }

    public static void FixDuplicateTraktLinks()
    {
        // Empty to preserve version info
    }

    public static void FixDuplicateTvDBLinks()
    {
        // Empty to preserve version info
    }

    public static void PopulateCharactersAndStaff()
    {
        var allcharacters = RepoFactory.AniDB_Character.GetAll();
        var allstaff = RepoFactory.AniDB_Seiyuu.GetAll();
        var allanimecharacters = RepoFactory.AniDB_Anime_Character.GetAll().ToLookup(a => a.CharID, b => b);
        var allcharacterstaff = RepoFactory.AniDB_Character_Seiyuu.GetAll();
        var charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar;
        var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;

        var charstosave = allcharacters.Select(character => new AnimeCharacter
        {
            Name = character.CharName?.Replace("`", "'"),
            AniDBID = character.CharID,
            Description = character.CharDescription?.Replace("`", "'"),
            ImagePath = character.GetPosterPath()?.Replace(charBasePath, "")
        }).ToList();
        RepoFactory.AnimeCharacter.Save(charstosave);

        var stafftosave = allstaff.Select(a => new AnimeStaff
        {
            Name = a.SeiyuuName?.Replace("`", "'"), AniDBID = a.SeiyuuID, ImagePath = a.GetPosterPath()?.Replace(creatorBasePath, "")
        }).ToList();
        RepoFactory.AnimeStaff.Save(stafftosave);

        // This is not accurate. There was a mistake in DB design
        var xrefstosave = (from xref in allcharacterstaff
            let animes = allanimecharacters[xref.CharID].ToList()
            from anime in animes
            select new CrossRef_Anime_Staff
            {
                AniDB_AnimeID = anime.AnimeID,
                Language = "Japanese",
                RoleType = (int)StaffRoleType.Seiyuu,
                Role = anime.CharType,
                RoleID = RepoFactory.AnimeCharacter.GetByAniDBID(xref.CharID).CharacterID,
                StaffID = RepoFactory.AnimeStaff.GetByAniDBID(xref.SeiyuuID).StaffID
            }).ToList();
        RepoFactory.CrossRef_Anime_Staff.Save(xrefstosave);
    }

    public static void FixCharactersWithGrave()
    {
        var list = RepoFactory.AnimeCharacter.GetAll()
            .Where(character => character.Description != null && character.Description.Contains("`")).ToList();
        foreach (var character in list)
        {
            character.Description = character.Description.Replace("`", "'");
            RepoFactory.AnimeCharacter.Save(character);
        }
    }

    public static void RemoveBasePathsFromStaffAndCharacters()
    {
        var charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath();
        var creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath();
        var charactersList = RepoFactory.AnimeCharacter.GetAll()
            .Where(a => a.ImagePath.StartsWith(charBasePath)).ToList();
        foreach (var character in charactersList)
        {
            character.ImagePath = character.ImagePath.Replace(charBasePath, "");
            while (character.ImagePath.StartsWith("" + Path.DirectorySeparatorChar))
            {
                character.ImagePath = character.ImagePath.Substring(1);
            }

            while (character.ImagePath.StartsWith("" + Path.AltDirectorySeparatorChar))
            {
                character.ImagePath = character.ImagePath.Substring(1);
            }

            RepoFactory.AnimeCharacter.Save(character);
        }

        var creatorsList = RepoFactory.AnimeStaff.GetAll()
            .Where(a => a.ImagePath.StartsWith(creatorBasePath)).ToList();
        foreach (var creator in creatorsList)
        {
            creator.ImagePath = creator.ImagePath.Replace(creatorBasePath, "");
            creator.ImagePath = creator.ImagePath.Replace(charBasePath, "");
            while (creator.ImagePath.StartsWith("" + Path.DirectorySeparatorChar))
            {
                creator.ImagePath = creator.ImagePath.Substring(1);
            }

            while (creator.ImagePath.StartsWith("" + Path.AltDirectorySeparatorChar))
            {
                creator.ImagePath = creator.ImagePath.Substring(1);
            }

            RepoFactory.AnimeStaff.Save(creator);
        }
    }

    public static void PopulateMyListIDs()
    {
        // nah
    }

    public static void RefreshAniDBInfoFromXML()
    {
        var i = 0;
        var list = RepoFactory.AniDB_Episode.GetAll().Where(a => string.IsNullOrEmpty(a.Description))
            .Select(a => a.AnimeID).Distinct().ToList();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        foreach (var animeID in list)
        {
            if (i % 10 == 0)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, "Populating AniDB Info from Cache",
                    $" {i}/{list.Count}");
            }

            i++;
            try
            {
                var command = commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
                {
                    c.CacheOnly = true;
                    c.DownloadRelations = false;
                    c.AnimeID = animeID;
                    c.CreateSeriesEntry = false;
                    c.BubbleExceptions = true;
                });
                command.ProcessCommand();
            }
            catch (Exception e)
            {
                logger.Error(
                    $"There was an error Populating AniDB Info for AniDB_Anime {animeID}, Update the Series' AniDB Info for a full stack: {e.Message}");
            }
        }
    }

    public static void MigrateAniDB_AnimeUpdates()
    {
        var tosave = RepoFactory.AniDB_Anime.GetAll()
            .Select(anime => new AniDB_AnimeUpdate
            {
                AnimeID = anime.AnimeID, UpdatedAt = anime.DateTimeUpdated
            })
            .ToList();

        RepoFactory.AniDB_AnimeUpdate.Save(tosave);
    }

    public static void MigrateAniDB_FileUpdates()
    {
        var tosave = RepoFactory.AniDB_File.GetAll()
            .Select(file => new AniDB_FileUpdate
            {
                FileSize = file.FileSize, Hash = file.Hash, HasResponse = true, UpdatedAt = file.DateTimeUpdated
            })
            .ToList();

        tosave.AddRange(RepoFactory.CrossRef_File_Episode.GetAll().Where(a => RepoFactory.AniDB_File.GetByHash(a.Hash) == null)
            .Select(a => (xref: a, vl: RepoFactory.VideoLocal.GetByHash(a.Hash))).Where(a => a.vl != null).Select(a => new AniDB_FileUpdate
            {
                FileSize = a.xref.FileSize, Hash = a.xref.Hash, HasResponse = false, UpdatedAt = a.vl.DateTimeCreated
            }));

        RepoFactory.AniDB_FileUpdate.Save(tosave);
    }

    public static void FixDuplicateTagFiltersAndUpdateSeasons() { }

    public static void RecalculateYears() { }

    public static void PopulateResourceLinks()
    {
        // deprecated
    }

    public static void PopulateTagWeight()
    {
        try
        {
            foreach (var atag in RepoFactory.AniDB_Anime_Tag.GetAll())
            {
                atag.Weight = 0;
                RepoFactory.AniDB_Anime_Tag.Save(atag);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Could not PopulateTagWeight: " + ex);
        }
    }

    public static void FixTagsWithInclude() { }

    public static void MakeTagsApplyToSeries() { }

    public static void MakeYearsApplyToSeries() { }

    public static void UpdateAllTvDBSeries()
    {
        Importer.RunImport_UpdateTvDB(true);
    }

    public static void DummyMigrationOfObsoletion()
    {
    }

    public static void EnsureNoOrphanedGroupsOrSeries()
    {
        var emptyGroups = RepoFactory.AnimeGroup.GetAll().Where(a => a.GetAllSeries().Count == 0).ToArray();
        RepoFactory.AnimeGroup.Delete(emptyGroups);
        var orphanedSeries = RepoFactory.AnimeSeries.GetAll().Where(a => a.AnimeGroup == null).ToArray();
        var groupCreator = new AnimeGroupCreator();
        using var session = DatabaseFactory.SessionFactory.OpenSession();
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
                    name = series.GetSeriesName();
                }
                catch
                {
                    // ignore
                }

                logger.Error(e,
                    $"Unable to update group for orphaned series: AniDB ID: {series.AniDB_ID} SeriesID: {series.AnimeSeriesID} Series Name: {name}");
            }
        }
    }

    public static void FixWatchDates()
    {
        // Reset incorrectly parsed watch dates for anidb file.
        logger.Debug($"Looking for faulty anidb file entries...");
        logger.Debug($"Looking for faulty episode user records...");
        // Fetch every episode user record stored to both remove orphaned records and to make sure the watch date is correct.
        var userDict = RepoFactory.JMMUser.GetAll().ToDictionary(user => user.JMMUserID);
        var fileListDict = RepoFactory.AnimeEpisode.GetAll()
            .ToDictionary(episode => episode.AnimeEpisodeID, episode => episode.GetVideoLocals());
        var episodesURsToSave = new List<SVR_AnimeEpisode_User>();
        var episodeURsToRemove = new List<SVR_AnimeEpisode_User>();
        foreach (var episodeUserRecord in RepoFactory.AnimeEpisode_User.GetAll())
        {
            // Remove any unkown episode user records.
            if (!fileListDict.ContainsKey(episodeUserRecord.AnimeEpisodeID) ||
                !userDict.ContainsKey(episodeUserRecord.JMMUserID))
            {
                episodeURsToRemove.Add(episodeUserRecord);
                continue;
            }

            // Fetch the file user record for when a file for the episode was last watched.
            var fileUserRecord = fileListDict[episodeUserRecord.AnimeEpisodeID]
                .Select(file => file.GetUserRecord(episodeUserRecord.JMMUserID))
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

        logger.Debug($"Found {episodesURsToSave.Count} episode user records to fix.");
        RepoFactory.AnimeEpisode_User.Delete(episodeURsToRemove);
        RepoFactory.AnimeEpisode_User.Save(episodesURsToSave);
        logger.Debug($"Updating series user records and series stats.");
        // Update all the series and groups to use the new watch dates.
        var seriesList = episodesURsToSave
            .GroupBy(record => record.AnimeSeriesID)
            .Select(records => (RepoFactory.AnimeSeries.GetByID(records.Key),
                records.Select(record => record.JMMUserID).Distinct())).ToList();
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
                var seriesUserRecord = series.GetOrCreateUserRecord(userID);
                seriesUserRecord.LastEpisodeUpdate = DateTime.Now;
                logger.Debug(
                    $"Updating series user contract for user \"{userDict[seriesUserRecord.JMMUserID].Username}\". (UserID={seriesUserRecord.JMMUserID},SeriesID={seriesUserRecord.AnimeSeriesID})");
                RepoFactory.AnimeSeries_User.Save(seriesUserRecord);
            }

            // Update the rest of the stats for the series.
            series.UpdateStats(true, true);
        }

        var groups = seriesList.Select(a => a.Item1.AnimeGroup).Where(a => a != null).DistinctBy(a => a.AnimeGroupID);
        foreach (var group in groups)
        {
            group.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
        }
    }

    public static void FixTagParentIDsAndNameOverrides()
    {
        var xmlUtils = Utils.ServiceContainer.GetRequiredService<HttpXmlUtils>();
        var animeParser = Utils.ServiceContainer.GetRequiredService<HttpAnimeParser>();
        var animeCreator = Utils.ServiceContainer.GetRequiredService<AnimeCreator>();
        var animeList = RepoFactory.AniDB_Anime.GetAll();
        logger.Info($"Updating anidb tags for {animeList.Count} local anidb anime entries...");

        var count = 0;
        foreach (var anime in animeList)
        {
            if (++count % 10 == 0)
                logger.Info($"Updating tags for local anidb anime entries... ({count}/{animeList.Count})");

            var xml = xmlUtils.LoadAnimeHTTPFromFile(anime.AnimeID);
            if (string.IsNullOrEmpty(xml))
            {
                logger.Warn($"Unable to load cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
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
                logger.Error(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
                continue;
            }

            animeCreator.CreateTags(response.Tags, anime);
            RepoFactory.AniDB_Anime.Save(anime, false);
        }

        // One last time, clean up any unreferenced tags after we've processed
        // all the tags and their cross-references.
        var tagsToDelete = RepoFactory.AniDB_Tag.GetAll()
            .Where(a => !RepoFactory.AniDB_Anime_Tag.GetByTagID(a.TagID).Any())
            .ToList();
        RepoFactory.AniDB_Tag.Delete(tagsToDelete);

        logger.Info($"Done updating anidb tags for {animeList.Count} anidb anime entries.");
    }

    public static void FixAnimeSourceLinks()
    {
        var animesToSave = new HashSet<SVR_AniDB_Anime>();
        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            if (!string.IsNullOrEmpty(anime.Site_JP))
            {
                animesToSave.Add(anime);
                anime.Site_JP = string.Join("|", anime.Site_JP.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());
            }
            if (!string.IsNullOrEmpty(anime.Site_EN))
            {
                animesToSave.Add(anime);
                anime.Site_EN = string.Join("|", anime.Site_EN.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());
            }
        }

        logger.Trace($"Found {animesToSave.Count} animes with faulty source links. Updating…");

        RepoFactory.AniDB_Anime.Save(animesToSave);

        logger.Trace($"Updated {animesToSave.Count} animes with faulty source links.");
    }

    public static void FixEpisodeDateTimeUpdated()
    {
        var xmlUtils = Utils.ServiceContainer.GetRequiredService<HttpXmlUtils>();
        var animeParser = Utils.ServiceContainer.GetRequiredService<HttpAnimeParser>();
        var animeCreator = Utils.ServiceContainer.GetRequiredService<AnimeCreator>();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
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

        logger.Info($"Updating last updated episode timestamps for {anidbAnimeIDs.Count} local anidb anime entries...");

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
                logger.Info($"Updating last updated episode timestamps for local anidb anime entries... ({progressCount}/{anidbAnimeIDs.Count})");

            var xml = xmlUtils.LoadAnimeHTTPFromFile(anime.AnimeID);
            if (string.IsNullOrEmpty(xml))
            {
                logger.Warn($"Unable to load cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
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
                logger.Error(e, $"Unable to parse cached Anime_HTTP xml dump for anime: {anime.AnimeID}/{anime.MainTitle}");
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
            commandFactory.CreateAndSave<CommandRequest_GetAnimeHTTP_Force>(c =>
            {
                c.AnimeID = animeID;
                c.DownloadRelations = false;
            });
        }

        logger.Info($"Done updating last updated episode timestamps for {anidbAnimeIDs.Count} local anidb anime entries. Updated {updatedCount} episodes, reset {resetCount} episodes and queued anime {animeToUpdateSet.Count} updates for {faultyCount} faulty episodes.");
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

        foreach (var series in seriesList)
            series.UpdateStats(false, true);
    }

    public static void FixOrphanedShokoEpisodes()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
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
        logger.Trace($"Checking {allAniDBEpisodes.Values.Count} anidb episodes for broken or incorrect links…");
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
        logger.Trace($"Checked {allAniDBEpisodes.Values.Count} anidb episodes for broken or incorrect links. Found {shokoEpisodesToSave.Count} shoko episodes to fix and {shokoEpisodesToRemove.Count} to remove.");
        RepoFactory.AnimeEpisode.Save(shokoEpisodesToSave);

        // Remove any existing links to the episodes that will be removed.
        logger.Trace($"Checking {shokoEpisodesToRemove.Count} orphaned shoko episodes before deletion.");
        var anidbFilesToRemove = new List<SVR_AniDB_File>();
        var xrefsToRemove = new List<CrossRef_File_Episode>();
        var videosToRefetch = new List<SVR_VideoLocal>();
        var tvdbXRefsToRemove = new List<CrossRef_AniDB_TvDB_Episode>();
        var tvdbXRefOverridesToRemove = new List<CrossRef_AniDB_TvDB_Episode_Override>();
        foreach (var shokoEpisode in shokoEpisodesToRemove)
        {
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(shokoEpisode.AniDB_EpisodeID);
            var videos = xrefs
                .Select(xref => RepoFactory.VideoLocal.GetByHashAndSize(xref.Hash, xref.FileSize))
                .Where(video => video != null)
                .ToList();
            var anidbFiles = xrefs
                .Where(xref => xref.CrossRefSource == (int)CrossRefSource.AniDB)
                .Select(xref => RepoFactory.AniDB_File.GetByHashAndFileSize(xref.Hash, xref.FileSize))
                .Where(anidbFile => anidbFile != null)
                .ToList();
            var tvdbXRefs = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(shokoEpisode.AniDB_EpisodeID);
            var tvdbXRefOverrides = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(shokoEpisode.AniDB_EpisodeID);
            xrefsToRemove.AddRange(xrefs);
            videosToRefetch.AddRange(videos);
            anidbFilesToRemove.AddRange(anidbFiles);
            tvdbXRefsToRemove.AddRange(tvdbXRefs);
            tvdbXRefOverridesToRemove.AddRange(tvdbXRefOverrides);
        }

        // Schedule a refetch of any video files affected by the removal of the
        // episodes. They were likely moved to another episode entry so let's
        // try and fetch that.
        logger.Trace($"Scheduling {videosToRefetch.Count} videos for a re-fetch.");
        foreach (var video in videosToRefetch)
        {
            commandFactory.CreateAndSave<CommandRequest_ProcessFile>(c =>
            {
                c.VideoLocalID = video.VideoLocalID;
                c.SkipMyList = true;
                c.ForceAniDB = true;
            });
        }

        logger.Trace($"Deleting {shokoEpisodesToRemove.Count} orphaned shoko episodes.");
        RepoFactory.AnimeEpisode.Delete(shokoEpisodesToRemove);

        logger.Trace($"Deleting {anidbFilesToRemove.Count} orphaned anidb files.");
        RepoFactory.AniDB_File.Delete(anidbFilesToRemove);

        logger.Trace($"Deleting {tvdbXRefsToRemove.Count} orphaned anidb/tvdb episode cross-references.");
        RepoFactory.CrossRef_AniDB_TvDB_Episode.Delete(tvdbXRefsToRemove);

        logger.Trace($"Deleting {tvdbXRefOverridesToRemove.Count} orphaned anidb/tvdb episode cross-reference overrides.");
        RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Delete(tvdbXRefOverridesToRemove);

        logger.Trace($"Deleting {xrefsToRemove.Count} orphaned file/episode cross-references.");
        RepoFactory.CrossRef_File_Episode.Delete(xrefsToRemove);
    }
}
