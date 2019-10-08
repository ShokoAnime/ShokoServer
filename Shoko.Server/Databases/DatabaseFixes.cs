using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using AniDBAPI;
using AniDBAPI.Commands;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Settings;

namespace Shoko.Server.Databases
{
    public class DatabaseFixes
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static void DeleteSerieUsersWithoutSeries()
        {
            //DB Fix Series not deleting series_user
            HashSet<int> list = new HashSet<int>(RepoFactory.AnimeSeries.Cache.Keys);
            RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.Cache.Values
                .Where(a => !list.Contains(a.AnimeSeriesID))
                .ToList());
        }

        public static void FixHashes()
        {
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetAll())
                {
                    bool fixedHash = false;
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
            List<SVR_VideoLocal> locals = RepoFactory.VideoLocal.GetAll()
                .Where(a => string.IsNullOrEmpty(a.FileName))
                .ToList();
            foreach (SVR_VideoLocal v in locals)
            {
                SVR_VideoLocal_Place p = v.Places.OrderBy(a => a.ImportFolderType).FirstOrDefault();
                if (!string.IsNullOrEmpty(p?.FilePath) && v.Media != null)
                {
                    v.FileName = p.FilePath;
                    int a = p.FilePath.LastIndexOf($"{Path.DirectorySeparatorChar}", StringComparison.InvariantCulture);
                    if (a > 0)
                        v.FileName = p.FilePath.Substring(a + 1);
                    SVR_VideoLocal_Place.FillVideoInfoFromMedia(v, v.Media);
                    RepoFactory.VideoLocal.Save(v, false);
                }
            }
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
            List<SVR_GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();

            if (lockedGFs != null)
                foreach (SVR_GroupFilter gf in lockedGFs)
                    if (gf.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        gf.FilterType = (int) GroupFilterType.ContinueWatching;
                        RepoFactory.GroupFilter.Save(gf);
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
                        AnimeID = (int) a[0],
                        AniDBStartEpisodeType = (int) a[1],
                        AniDBStartEpisodeNumber = (int) a[2],
                        TvDBID = (int) a[3],
                        TvDBSeasonNumber = (int) a[4],
                        TvDBStartEpisodeNumber = (int) a[5]
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
                        if (exists != null) continue;
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
                        AnimeID = (int) a[0],
                        AniDBStartEpisodeType = (int) a[1],
                        AniDBStartEpisodeNumber = (int) a[2],
                        TvDBID = (int) a[3],
                        TvDBSeasonNumber = (int) a[4],
                        TvDBStartEpisodeNumber = (int) a[5]
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
                        if (exists != null) continue;
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
                        AniDBID = (int) a[0],
                        TvDBID = (int) a[1],
                        CrossRefSource = (CrossRefSource) a[2]
                    }).DistinctBy(a => new[] {a.AniDBID, a.TvDBID}).ToList();
                foreach (var link in links)
                {
                    var exists =
                        RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(
                            link.AniDBID, link.TvDBID);
                    if (exists != null) continue;
                    RepoFactory.CrossRef_AniDB_TvDB.Save(link);
                }

                // Scan Series Without links for prequel/sequel links
                var list = RepoFactory.CrossRef_AniDB_TvDB.GetSeriesWithoutLinks();

                // AniDB_Anime_Relation is a direct repository, so GetFullLinearRelationTree will be slow
                // Using a visited node set to skip processed nodes should be faster
                HashSet<int> visitedNodes = new HashSet<int>();
                var seriesWithoutLinksLookup = list.ToDictionary(a => a.AniDB_ID);

                foreach (var animeseries in list)
                {
                    if (visitedNodes.Contains(animeseries.AniDB_ID)) continue;

                    var relations = RepoFactory.AniDB_Anime_Relation.GetFullLinearRelationTree(animeseries.AniDB_ID);
                    int? tvDBID = relations.SelectMany(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a))
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
                        CrossRef_AniDB_TvDB link = new CrossRef_AniDB_TvDB
                        {
                            AniDBID = series.AniDB_ID,
                            TvDBID = tvDBID.Value,
                            CrossRefSource = CrossRefSource.Automatic
                        };
                        // No need to check for existence
                        RepoFactory.CrossRef_AniDB_TvDB.Save(link);
                        visitedNodes.Add(series.AniDB_ID);
                    }
                }

                list = RepoFactory.AnimeSeries.GetAll().ToList();
                int count = 0;

                list.AsParallel().ForAll(animeseries =>
                {
                    Interlocked.Increment(ref count);
                    if (count % 50 == 0)
                    {
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Validating, "Generating TvDB Episode Matchings",
                            $" {count}/{list.Count}");
                    }

                    TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true);
                });

                string dropV2 = "DROP TABLE CrossRef_AniDB_TvDBV2";
                session.CreateSQLQuery(dropV2).ExecuteUpdate();
            }
        }

        public static void RegenTvDBMatches()
        {
            RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinks();

            var list = RepoFactory.AnimeSeries.GetAll().ToList();
            int count = 0;

            list.AsParallel().ForAll(animeseries =>
            {
                Interlocked.Increment(ref count);
                if (count % 50 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, "Generating TvDB Episode Matchings",
                        $" {count}/{list.Count}");
                }

                TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true);
            });
        }

        public static void FixAniDB_EpisodesWithMissingTitles()
        {
            logger.Info("Checking for Episodes with Missing Titles");
            var episodes = RepoFactory.AniDB_Episode.GetAll()
                .Where(a => !RepoFactory.AniDB_Episode_Title.GetByEpisodeID(a.EpisodeID).Any() &&
                            RepoFactory.AnimeSeries.GetByAnimeID(a.AnimeID) != null).ToList();
            var animeIDs = episodes.Select(a => a.AnimeID).Distinct().OrderBy(a => a).ToList();
            int count = 0;
            logger.Info($"There are {episodes.Count} episodes in {animeIDs.Count} anime with missing titles. Attempting to fill them from HTTP cache");
            foreach (int animeID in animeIDs)
            {
                count++;
                try
                {
                    var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime == null)
                    {
                        logger.Info($"Anime {animeID} is missing it's AniDB_Anime record. That's a problem. Try importing a file for the anime.");
                        continue;
                    }

                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating,
                        $"Generating Episode Info for {anime.MainTitle}",
                        $" {count}/{animeIDs.Count}");
                    XmlDocument docAnime = APIUtils.LoadAnimeHTTPFromFile(animeID);
                    if (docAnime == null) continue;
                    logger.Info($"{anime.MainTitle} has a proper HTTP cache. Attempting to regenerate info from it.");

                    var rawEpisodes = AniDBHTTPHelper.ProcessEpisodes(docAnime, animeID);
                    anime.CreateEpisodes(rawEpisodes);
                    logger.Info($"Recreating Episodes for {anime.MainTitle}");
                    SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                    if (series == null) continue;
                    series.CreateAnimeEpisodes();
                }
                catch (Exception e)
                {
                    logger.Error($"Error Populating Episode Titles for Anime ({animeID}): {e}");
                }
            }
            logger.Info("Finished Filling Episode Titles from Cache.");
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
            string charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar;
            string creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar;

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
                    RoleType = (int) StaffRoleType.Seiyuu,
                    Role = anime.CharType,
                    RoleID = RepoFactory.AnimeCharacter.GetByAniDBID(xref.CharID).CharacterID,
                    StaffID = RepoFactory.AnimeStaff.GetByAniDBID(xref.SeiyuuID).StaffID,
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
            string charBasePath = ImageUtils.GetBaseAniDBCharacterImagesPath();
            string creatorBasePath = ImageUtils.GetBaseAniDBCreatorImagesPath();
            var charactersList = RepoFactory.AnimeCharacter.GetAll()
                .Where(a => a.ImagePath.StartsWith(charBasePath)).ToList();
            foreach (var character in charactersList)
            {
                character.ImagePath = character.ImagePath.Replace(charBasePath, "");
                while (character.ImagePath.StartsWith("" + Path.DirectorySeparatorChar))
                    character.ImagePath = character.ImagePath.Substring(1);
                while (character.ImagePath.StartsWith("" + Path.AltDirectorySeparatorChar))
                    character.ImagePath = character.ImagePath.Substring(1);
                RepoFactory.AnimeCharacter.Save(character);
            }

            var creatorsList = RepoFactory.AnimeStaff.GetAll()
                .Where(a => a.ImagePath.StartsWith(creatorBasePath)).ToList();
            foreach (var creator in creatorsList)
            {
                creator.ImagePath = creator.ImagePath.Replace(creatorBasePath, "");
                creator.ImagePath = creator.ImagePath.Replace(charBasePath, "");
                while (creator.ImagePath.StartsWith("" + Path.DirectorySeparatorChar))
                    creator.ImagePath = creator.ImagePath.Substring(1);
                while (creator.ImagePath.StartsWith("" + Path.AltDirectorySeparatorChar))
                    creator.ImagePath = creator.ImagePath.Substring(1);
                RepoFactory.AnimeStaff.Save(creator);
            }
        }

        public static void PopulateMyListIDs()
        {
            // Don't bother with no AniDB creds, we assume first run
            if (!ShokoService.AnidbProcessor.ValidAniDBCredentials()) return;

            // Don't even bother on new DBs
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                long vlCount = session.CreateSQLQuery("SELECT COUNT(VideoLocalID) FROM VideoLocal").UniqueResult<long>();
                if (vlCount == 0) return;
            }

            // Get the list from AniDB
            AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
            cmd.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password);
            enHelperActivityType ev = cmd.Process();
            if (ev != enHelperActivityType.GotMyListHTTP)
            {
                logger.Warn("AniDB did not return a successful code: " + ev);
                return;
            }
            // Add missing files on AniDB
            var onlineFiles = cmd.MyListItems.ToLookup(a => a.FileID);
            var dictAniFiles = RepoFactory.AniDB_File.GetAll().ToLookup(a => a.Hash);

            var list = RepoFactory.VideoLocal.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)).ToList();
            int count = 0;
            foreach (SVR_VideoLocal vid in list)
            {
                count++;
                if (count % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, "Populating MyList IDs (this will help solve MyList issues)",
                        $" {count}/{list.Count}");
                }

                // Does it have a linked AniFile
                if (!dictAniFiles.Contains(vid.Hash)) continue;

                int fileID = dictAniFiles[vid.Hash].FirstOrDefault()?.FileID ?? 0;
                if (fileID == 0) continue;
                // Is it in MyList
                if (!onlineFiles.Contains(fileID)) continue;

                Raw_AniDB_MyListFile file = onlineFiles[fileID].FirstOrDefault(a => a != null && a.ListID != 0);
                if (file == null || vid.MyListID != 0) continue;

                vid.MyListID = file.ListID;
                RepoFactory.VideoLocal.Save(vid);
            }
        }

        public static void PopulateAniDBEpisodeDescriptions()
        {
            int i = 0;
            var list = RepoFactory.AniDB_Episode.GetAll().Where(a => string.IsNullOrEmpty(a.Description))
                .Select(a => a.AnimeID).Distinct().ToList();
            foreach (var animeID in list)
            {
                if (i % 10 == 0)
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, "Populating AniDB Info from Cache",
                        $" {i}/{list.Count}");
                i++;
                try
                {
                    var getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
                    getAnimeCmd.Init(animeID, false, false, true);
                    var result = getAnimeCmd.Process();
                    if (result == enHelperActivityType.Banned_555 || result == enHelperActivityType.NoSuchAnime)
                        continue;
                    if (getAnimeCmd.Anime == null) continue;
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        ShokoService.AnidbProcessor.SaveResultsForAnimeXML(session, animeID, false, false, getAnimeCmd, 0);
                    }
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
            List<AniDB_AnimeUpdate> tosave = RepoFactory.AniDB_Anime.GetAll()
                .Select(anime => new AniDB_AnimeUpdate
                {
                    AnimeID = anime.AnimeID,
                    UpdatedAt = anime.DateTimeUpdated
                })
                .ToList();

            RepoFactory.AniDB_AnimeUpdate.Save(tosave);
        }

        public static void FixDuplicateTagFiltersAndUpdateSeasons()
        {
            var filters = RepoFactory.GroupFilter.GetAll();
            var seasons = filters.Where(a => a.FilterType == (int) GroupFilterType.Season).ToList();
            var tags = filters.Where(a => a.FilterType == (int) GroupFilterType.Tag).ToList();

            var tagsGrouping = tags.GroupBy(a => a.GroupFilterName).SelectMany(a => a.Skip(1)).ToList();

            tagsGrouping.ForEach(RepoFactory.GroupFilter.Delete);

            tags = filters.Where(a => a.FilterType == (int) GroupFilterType.Tag).ToList();

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
                if (filters.Count == 0) return;
                foreach (SVR_GroupFilter gf in filters)
                {
                    if (gf.FilterType != (int) GroupFilterType.Year) continue;
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
            int i = 0;
            var animes = RepoFactory.AniDB_Anime.GetAll().ToList();
            foreach (var anime in animes)
            {
                if (i % 10 == 0)
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, "Populating Resource Links from Cache",
                        $" {i}/{animes.Count}");
                i++;
                try
                {
                    var xmlDocument = APIUtils.LoadAnimeHTTPFromFile(anime.AnimeID);
                    if (xmlDocument == null) continue;
                    var resourceLinks = AniDBHTTPHelper.ProcessResources(xmlDocument, anime.AnimeID);
                    anime.CreateResources(resourceLinks);
                }
                catch (Exception e)
                {
                    logger.Error(
                        $"There was an error Populating Resource Links for AniDB_Anime {anime.AnimeID}, Update the Series' AniDB Info for a full stack: {e.Message}");
                }
            }


            using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                i = 0;
                var batches = animes.Batch(50).ToList();
                foreach (var animeBatch in batches)
                {
                    i++;
                    ServerState.Instance.CurrentSetupStatus = string.Format(Commons.Properties.Resources.Database_Validating,
                        "Saving AniDB_Anime batch ", $"{i}/{batches.Count}");
                    try
                    {
                        using (var transaction = session.BeginTransaction())
                        {
                            foreach (var anime in animeBatch)
                                RepoFactory.AniDB_Anime.SaveWithOpenTransaction(session.Wrap(), anime);
                            transaction.Commit();
                        }

                    }
                    catch (Exception e)
                    {
                        logger.Error($"There was an error saving anime while Populating Resource Links: {e}");
                    }
                }
            }
        }

        public static void PopulateTagWeight()
        {
            try
            {
                foreach (AniDB_Anime_Tag atag in RepoFactory.AniDB_Anime_Tag.GetAll())
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
                foreach (SVR_GroupFilter gf in RepoFactory.GroupFilter.GetAll())
                {
                    if (gf.FilterType != (int) GroupFilterType.Tag) continue;
                    foreach (GroupFilterCondition gfc in gf.Conditions)
                    {
                        if (gfc.ConditionType != (int) GroupFilterConditionType.Tag) continue;
                        if (gfc.ConditionOperator == (int) GroupFilterOperator.Include)
                        {
                            gfc.ConditionOperator = (int) GroupFilterOperator.In;
                            RepoFactory.GroupFilterCondition.Save(gfc);
                            continue;
                        }
                        if (gfc.ConditionOperator == (int) GroupFilterOperator.Exclude)
                        {
                            gfc.ConditionOperator = (int) GroupFilterOperator.NotIn;
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
                if (filters.Count == 0) return;
                foreach (SVR_GroupFilter gf in filters)
                {
                    if (gf.FilterType != (int) GroupFilterType.Tag) continue;
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
                if (filters.Count == 0) return;
                foreach (SVR_GroupFilter gf in filters)
                {
                    if (gf.FilterType != (int) GroupFilterType.Year) continue;
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
    }
}
