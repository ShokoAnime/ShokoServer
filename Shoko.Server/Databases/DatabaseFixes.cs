using System;
using System.Collections.Generic;
using System.Linq;
using AniDBAPI;
using AniDBAPI.Commands;
using NLog;
using Pri.LongPath;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

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

            var charstosave = allcharacters.Select(character => new AnimeCharacter
                {
                    Name = character.CharName?.Replace("`", "'"),
                    AniDBID = character.CharID,
                    Description = character.CharDescription?.Replace("`", "'"),
                    ImagePath = character.GetPosterPath()
                }).ToList();
            RepoFactory.AnimeCharacter.Save(charstosave);

            var stafftosave = allstaff.Select(a => new AnimeStaff
                {
                    Name = a.SeiyuuName?.Replace("`", "'"),
                    AniDBID = a.SeiyuuID,
                    ImagePath = a.GetPosterPath()
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

        public static void PopulateAniDBEpisodeDescriptions()
        {
            int i = 0;
            var list = RepoFactory.AniDB_Episode.GetAll().Where(a => string.IsNullOrEmpty(a.Description))
                .Select(a => a.AnimeID).Distinct().ToList();
            foreach (var animeID in list)
            {
                if (i % 10 == 0)
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, "Populating Episode Descriptions from Cache",
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
                        ShokoService.AnidbProcessor.SaveResultsForAnimeXML(session, animeID, false, getAnimeCmd);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(
                        $"There was an error Populating AniDB_Episode Descriptions for AniDB_Anime {animeID}, Update the Series' AniDB Info for a full stack: {e.Message}");
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
    }
}