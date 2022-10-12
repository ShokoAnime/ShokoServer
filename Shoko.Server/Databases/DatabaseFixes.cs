using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

namespace Shoko.Server.Databases;

public class DatabaseFixes
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public static void MigrateAniDBToNet()
    {
        var anidb = ServerSettings.Instance.AniDb.ServerAddress;
        if (!anidb.EndsWith(".info", StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        ServerSettings.Instance.AniDb.ServerAddress = anidb.Substring(0, anidb.Length - 5) + ".net";
        ServerSettings.Instance.SaveSettings();
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


    public static void FixContinueWatchingGroupFilter_20160406()
    {
        // group filters

        // check if it already exists
        var lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();

        if (lockedGFs != null)
        {
            foreach (var gf in lockedGFs)
            {
                if (gf.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    gf.FilterType = (int)GroupFilterType.ContinueWatching;
                    RepoFactory.GroupFilter.Save(gf);
                }
            }
        }
    }

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
                }).DistinctBy(a => new[] { a.AniDBID, a.TvDBID }).ToList();
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
            Name = a.SeiyuuName?.Replace("`", "'"),
            AniDBID = a.SeiyuuID,
            ImagePath = a.GetPosterPath()?.Replace(creatorBasePath, "")
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
        var commandFactory = ShokoServer.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
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
            .Select(anime => new AniDB_AnimeUpdate { AnimeID = anime.AnimeID, UpdatedAt = anime.DateTimeUpdated })
            .ToList();

        RepoFactory.AniDB_AnimeUpdate.Save(tosave);
    }

    public static void FixDuplicateTagFiltersAndUpdateSeasons()
    {
        var filters = RepoFactory.GroupFilter.GetAll();
        var seasons = filters.Where(a => a.FilterType == (int)GroupFilterType.Season).ToList();
        var tags = filters.Where(a => a.FilterType == (int)GroupFilterType.Tag).ToList();

        var tagsGrouping = tags.GroupBy(a => a.GroupFilterName).SelectMany(a => a.Skip(1)).ToList();

        tagsGrouping.ForEach(RepoFactory.GroupFilter.Delete);

        tags = filters.Where(a => a.FilterType == (int)GroupFilterType.Tag).ToList();

        foreach (var filter in tags.Where(a => a.GroupConditions.Contains("`")))
        {
            filter.GroupConditions = filter.GroupConditions.Replace("`", "'");
            RepoFactory.GroupFilter.Save(filter);
        }

        foreach (var seasonFilter in seasons)
        {
            seasonFilter.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(seasonFilter);
        }
    }

    public static void RecalculateYears()
    {
        try
        {
            var filters = RepoFactory.GroupFilter.GetAll();
            if (filters.Count == 0)
            {
                return;
            }

            foreach (var gf in filters)
            {
                if (gf.FilterType != (int)GroupFilterType.Year)
                {
                    continue;
                }

                gf.CalculateGroupsAndSeries();
                RepoFactory.GroupFilter.Save(gf);
            }

            RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

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

    public static void FixTagsWithInclude()
    {
        try
        {
            foreach (var gf in RepoFactory.GroupFilter.GetAll())
            {
                if (gf.FilterType != (int)GroupFilterType.Tag)
                {
                    continue;
                }

                foreach (var gfc in gf.Conditions)
                {
                    if (gfc.ConditionType != (int)GroupFilterConditionType.Tag)
                    {
                        continue;
                    }

                    if (gfc.ConditionOperator == (int)GroupFilterOperator.Include)
                    {
                        gfc.ConditionOperator = (int)GroupFilterOperator.In;
                        RepoFactory.GroupFilterCondition.Save(gfc);
                        continue;
                    }

                    if (gfc.ConditionOperator == (int)GroupFilterOperator.Exclude)
                    {
                        gfc.ConditionOperator = (int)GroupFilterOperator.NotIn;
                        RepoFactory.GroupFilterCondition.Save(gfc);
                    }
                }

                gf.CalculateGroupsAndSeries();
                RepoFactory.GroupFilter.Save(gf);
            }
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

    public static void MakeTagsApplyToSeries()
    {
        try
        {
            var filters = RepoFactory.GroupFilter.GetAll();
            if (filters.Count == 0)
            {
                return;
            }

            foreach (var gf in filters)
            {
                if (gf.FilterType != (int)GroupFilterType.Tag)
                {
                    continue;
                }

                gf.ApplyToSeries = 1;
                gf.CalculateGroupsAndSeries();
                RepoFactory.GroupFilter.Save(gf);
            }

            RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

    public static void MakeYearsApplyToSeries()
    {
        try
        {
            var filters = RepoFactory.GroupFilter.GetAll();
            if (filters.Count == 0)
            {
                return;
            }

            foreach (var gf in filters)
            {
                if (gf.FilterType != (int)GroupFilterType.Year)
                {
                    continue;
                }

                gf.ApplyToSeries = 1;
                gf.CalculateGroupsAndSeries();
                RepoFactory.GroupFilter.Save(gf);
            }

            RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

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
                var group = groupCreator.GetOrCreateSingleGroupForSeries(session.Wrap(), series);
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
                records.Select(record => record.JMMUserID).Distinct()));
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
            series.UpdateStats(true, true, true);
        }
    }
}
