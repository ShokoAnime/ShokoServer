using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AniDBAPI;
using AniDBAPI.Commands;

using Shoko.Models;
using NLog;
using NutzCode.CloudFileSystem;
using Directory = System.IO.Directory;
using Shoko.Server.Collections;
using Shoko.Models.Azure;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using Shoko.Server.API.core;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Models.TvDB;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Tasks;



namespace Shoko.Server
{
    public class ShokoServiceImplementation : IShokoServer
    {

        //TODO Split this file into subfiles with partial class, Move #region funcionality from the interface to those subfiles. Also move this to API folder

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public List<CL_AnimeGroup_User> GetAllGroups(int userID)
        {
            List<CL_AnimeGroup_User> grps = new List<CL_AnimeGroup_User>();
            try
            {
                return RepoFactory.AnimeGroup.GetAll().Select(a => a.GetUserContract(userID)).OrderBy(a => a.GroupName).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return grps;
        }

        public List<CL_AnimeGroup_User> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
        {
            List<CL_AnimeGroup_User> grps = new List<CL_AnimeGroup_User>();
            try
            {
                int? grpid = animeGroupID;
                while (grpid.HasValue)
                {
                    grpid = null;
                    SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                    if (grp != null)
                    {
                        grps.Add(grp.GetUserContract(userID));
                        grpid = grp.AnimeGroupParentID;
                    }
                }
                return grps;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return grps;
        }

        public List<CL_AnimeGroup_User> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
        {
            List<CL_AnimeGroup_User> grps = new List<CL_AnimeGroup_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {           
                    SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (series == null)
                        return grps;

                    foreach (SVR_AnimeGroup grp in series.AllGroupsAbove)
                    {
                        grps.Add(grp.GetUserContract(userID));
                    }

                    return grps;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return grps;
        }

        public CL_AnimeGroup_User GetGroup(int animeGroupID, int userID)
        {
            try
            {
                return RepoFactory.AnimeGroup.GetByID(animeGroupID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public string DeleteAnimeGroup(int animeGroupID, bool deleteFiles)
        {
            try
            {

                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return "Group does not exist";

                int? parentGroupID = grp.AnimeGroupParentID;

                foreach (SVR_AnimeSeries ser in grp.GetAllSeries())
                {
                    DeleteAnimeSeries(ser.AnimeSeriesID, deleteFiles, false);
                }

                // delete all sub groups
                foreach (SVR_AnimeGroup subGroup in grp.GetAllChildGroups())
                {
                    DeleteAnimeGroup(subGroup.AnimeGroupID, deleteFiles);
                }
                List<SVR_GroupFilter> gfs =
                    RepoFactory.GroupFilter.GetWithConditionsTypes(new HashSet<GroupFilterConditionType>()
                    {
                        GroupFilterConditionType.AnimeGroup
                    });
                foreach (SVR_GroupFilter gf in gfs)
                {
                    bool change = false;
                    List<GroupFilterCondition> c =
                        gf.Conditions.Where(a => a.ConditionType == (int) GroupFilterConditionType.AnimeGroup).ToList();
                    foreach (GroupFilterCondition gfc in c)
                    {
                        int thisGrpID = 0;
                        int.TryParse(gfc.ConditionParameter, out thisGrpID);
                        if (thisGrpID == animeGroupID)
                        {
                            change = true;
                            gf.Conditions.Remove(gfc);
                        }
                    }
                    if (change)
                    {
                        if (gf.Conditions.Count == 0)
                            RepoFactory.GroupFilter.Delete(gf.GroupFilterID);
                        else
                        {
                            gf.EvaluateAnimeGroups();
                            RepoFactory.GroupFilter.Save(gf);
                        }
                    }
                }


                RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);

                // finally update stats

                if (parentGroupID.HasValue)
                {
                    SVR_AnimeGroup grpParent = RepoFactory.AnimeGroup.GetByID(parentGroupID.Value);

                    if (grpParent != null)
                    {
                        grpParent.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                        //StatsCache.Instance.UpdateUsingGroup(grpParent.TopLevelAnimeGroup.AnimeGroupID);
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public List<CL_AnimeGroup_User> GetAnimeGroupsForFilter(int groupFilterID, int userID,
            bool getSingleSeriesGroups)
        {
            List<CL_AnimeGroup_User> retGroups = new List<CL_AnimeGroup_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return retGroups;
                    SVR_GroupFilter gf;
                    gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                    if ((gf != null) && gf.GroupsIds.ContainsKey(userID))
                        retGroups =
                            gf.GroupsIds[userID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                                .Where(a => a != null)
                                .Select(a => a.GetUserContract(userID))
                                .ToList();
                    if (getSingleSeriesGroups)
                    {
                        List<CL_AnimeGroup_User> nGroups = new List<CL_AnimeGroup_User>();
                        foreach (CL_AnimeGroup_User cag in retGroups)
                        {
                            CL_AnimeGroup_User ng = (CL_AnimeGroup_User) cag.DeepCopy();
                            if (cag.Stat_SeriesCount == 1)
                            {
                                if (cag.DefaultAnimeSeriesID.HasValue)
                                    ng.SeriesForNameOverride =
                                        RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID)
                                            .FirstOrDefault(a => a.AnimeSeriesID == cag.DefaultAnimeSeriesID.Value)?
                                            .GetUserContract(userID);
                                if (ng.SeriesForNameOverride == null)
                                    ng.SeriesForNameOverride =
                                        RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID)
                                            .FirstOrDefault()?.GetUserContract(userID);
                            }
                            nGroups.Add(ng);
                        }
                        retGroups = nGroups;
                    }

                    return retGroups;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return retGroups;
        }


        /// <summary>
        /// Can only be used when the group only has one series
        /// </summary>
        /// <param name="animeGroupID"></param>
        /// <param name="allSeries"></param>
        /// <returns></returns>
        public static SVR_AnimeSeries GetSeriesForGroup(int animeGroupID, List<SVR_AnimeSeries> allSeries)
        {
            try
            {
                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    if (ser.AnimeGroupID == animeGroupID) return ser;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public CL_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                    if (gf == null) return null;

                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return null;

                    CL_GroupFilterExtended contract = gf.ToClientExtended(session, user);

                    return contract;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public List<CL_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
        {
            List<CL_GroupFilterExtended> gfs = new List<CL_GroupFilterExtended>();
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return gfs;
                IReadOnlyList<SVR_GroupFilter> allGfs = RepoFactory.GroupFilter.GetAll();
                foreach (SVR_GroupFilter gf in allGfs)
                {
                    CL_GroupFilter gfContract = gf.ToClient();
                    CL_GroupFilterExtended gfeContract = new CL_GroupFilterExtended();
                    gfeContract.GroupFilter = gfContract;
                    gfeContract.GroupCount = 0;
                    gfeContract.SeriesCount = 0;
                    if (gf.GroupsIds.ContainsKey(user.JMMUserID))
                        gfeContract.GroupCount = gf.GroupsIds.Count;
                    gfs.Add(gfeContract);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return gfs;
        }

        public List<CL_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0)
        {
            List<CL_GroupFilterExtended> gfs = new List<CL_GroupFilterExtended>();
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return gfs;
                List<SVR_GroupFilter> allGfs = gfparentid == 0 ? RepoFactory.GroupFilter.GetTopLevel() : RepoFactory.GroupFilter.GetByParentID(gfparentid);
                foreach (SVR_GroupFilter gf in allGfs)
                {
                    CL_GroupFilter gfContract = gf.ToClient();
                    CL_GroupFilterExtended gfeContract = new CL_GroupFilterExtended();
                    gfeContract.GroupFilter = gfContract;
                    gfeContract.GroupCount = 0;
                    gfeContract.SeriesCount = 0;
                    if (gf.GroupsIds.ContainsKey(user.JMMUserID))
                        gfeContract.GroupCount = gf.GroupsIds.Count;
                    gfs.Add(gfeContract);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return gfs;
        }

        public List<CL_GroupFilter> GetAllGroupFilters()
        {
            List<CL_GroupFilter> gfs = new List<CL_GroupFilter>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;

                    IReadOnlyList<SVR_GroupFilter> allGfs = RepoFactory.GroupFilter.GetAll();
                    TimeSpan ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (SVR_GroupFilter gf in allGfs)
                    {
                        gfs.Add(gf.ToClient(session));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return gfs;
        }

        public List<CL_GroupFilter> GetGroupFilters(int gfparentid = 0)
        {
            List<CL_GroupFilter> gfs = new List<CL_GroupFilter>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;

                    List<SVR_GroupFilter> allGfs = gfparentid == 0 ? RepoFactory.GroupFilter.GetTopLevel() : RepoFactory.GroupFilter.GetByParentID(gfparentid);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (SVR_GroupFilter gf in allGfs)
                    {
                        gfs.Add(gf.ToClient(session));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return gfs;
        }

        public CL_GroupFilter GetGroupFilter(int gf)
        {
            try
            {
                return RepoFactory.GroupFilter.GetByID(gf)?.ToClient();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public CL_GroupFilter EvaluateGroupFilter(CL_GroupFilter contract)
        {
            try
            {
                return SVR_GroupFilter.EvaluateContract(contract);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new CL_GroupFilter();
            }
        }

        public List<Playlist> GetAllPlaylists()
        {
            try
            {
                return RepoFactory.Playlist.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Playlist>();
        }

        #region Custom Tags

        public List<CustomTag> GetAllCustomTags()
        {
            try
            {
                return RepoFactory.CustomTag.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public CL_Response<CrossRef_CustomTag> SaveCustomTagCrossRef(CrossRef_CustomTag contract)
        {
            CL_Response<CrossRef_CustomTag> contractRet = new CL_Response<CrossRef_CustomTag>();
            contractRet.ErrorMessage = "";

            try
            {
                // this is an update
                CrossRef_CustomTag xref = null;
                if (contract.CrossRef_CustomTagID!=0)
                {
                    contractRet.ErrorMessage = "Updates are not allowed";
                    return contractRet;
                }
                else
                    xref = new CrossRef_CustomTag();

                //TODO: Custom Tags - check if the CustomTagID is valid
                //TODO: Custom Tags - check if the CrossRefID is valid

                xref.CrossRefID = contract.CrossRefID;
                xref.CrossRefType = contract.CrossRefType;
                xref.CustomTagID = contract.CustomTagID;

                RepoFactory.CrossRef_CustomTag.Save(xref);

                contractRet.Result = xref;
                SVR_AniDB_Anime.UpdateStatsByAnimeID(contract.CrossRefID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteCustomTagCrossRefByID(int xrefID)
        {
            try
            {
                CrossRef_CustomTag pl = RepoFactory.CrossRef_CustomTag.GetByID(xrefID);
                if (pl == null)
                    return "Custom Tag not found";

                RepoFactory.CrossRef_CustomTag.Delete(xrefID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID)
        {
            try
            {
                List<CrossRef_CustomTag> xrefs = RepoFactory.CrossRef_CustomTag.GetByUniqueID(customTagID, crossRefType, crossRefID);

                if (xrefs == null || xrefs.Count == 0)
                    return "Custom Tag not found";

                RepoFactory.CrossRef_CustomTag.Delete(xrefs[0].CrossRef_CustomTagID);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(crossRefID);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<CustomTag> SaveCustomTag(CustomTag contract)
        {
            CL_Response<CustomTag> contractRet = new CL_Response<CustomTag>();
            contractRet.ErrorMessage = "";

            try
            {
                // this is an update
                CustomTag ctag = null;
                if (contract.CustomTagID!=0)
                {
                    ctag = RepoFactory.CustomTag.GetByID(contract.CustomTagID);
                    if (ctag == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing custom tag with ID: " +
                                                   contract.CustomTagID.ToString();
                        return contractRet;
                    }
                }
                else
                    ctag = new CustomTag();

                if (string.IsNullOrEmpty(contract.TagName))
                {
                    contractRet.ErrorMessage = "Custom Tag must have a name";
                    return contractRet;
                }

                ctag.TagName = contract.TagName;
                ctag.TagDescription = contract.TagDescription;

                RepoFactory.CustomTag.Save(ctag);

                contractRet.Result = ctag;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteCustomTag(int customTagID)
        {
            try
            {
                
                CustomTag pl = RepoFactory.CustomTag.GetByID(customTagID);
                if (pl == null)
                    return "Custom Tag not found";

                // first get a list of all the anime that referenced this tag
                List<CrossRef_CustomTag> xrefs = RepoFactory.CrossRef_CustomTag.GetByCustomTagID(customTagID);

                RepoFactory.CustomTag.Delete(customTagID);

                // update cached data for any anime that were affected
                foreach (CrossRef_CustomTag xref in xrefs)
                {
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(xref.CrossRefID);
                }


                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public CustomTag GetCustomTag(int customTagID)
        {
            try
            {
                return RepoFactory.CustomTag.GetByID(customTagID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        #endregion

        public CL_Response<Playlist> SavePlaylist(Playlist contract)
        {
            CL_Response<Playlist> contractRet = new CL_Response<Playlist>();
            contractRet.ErrorMessage = "";

            try
            {
                

                // Process the playlist
                Playlist pl = null;
                if (contract.PlaylistID!=0)
                {
                    pl = RepoFactory.Playlist.GetByID(contract.PlaylistID);
                    if (pl == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Playlist with ID: " +
                                                   contract.PlaylistID.ToString();
                        return contractRet;
                    }
                }
                else
                    pl = new Playlist();

                if (string.IsNullOrEmpty(contract.PlaylistName))
                {
                    contractRet.ErrorMessage = "Playlist must have a name";
                    return contractRet;
                }

                pl.DefaultPlayOrder = contract.DefaultPlayOrder;
                pl.PlaylistItems = contract.PlaylistItems;
                pl.PlaylistName = contract.PlaylistName;
                pl.PlayUnwatched = contract.PlayUnwatched;
                pl.PlayWatched = contract.PlayWatched;

                RepoFactory.Playlist.Save(pl);

                contractRet.Result = pl;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeletePlaylist(int playlistID)
        {
            try
            {

                Playlist pl = RepoFactory.Playlist.GetByID(playlistID);
                if (pl == null)
                    return "Playlist not found";

                RepoFactory.Playlist.Delete(playlistID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public Playlist GetPlaylist(int playlistID)
        {
            try
            {
                return RepoFactory.Playlist.GetByID(playlistID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CL_BookmarkedAnime> GetAllBookmarkedAnime()
        {
            List<CL_BookmarkedAnime> baList = new List<CL_BookmarkedAnime>();
            try
            {
                return RepoFactory.BookmarkedAnime.GetAll().Select(a => ModelClients.ToClient(a)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return baList;
        }

        public CL_Response<CL_BookmarkedAnime> SaveBookmarkedAnime(CL_BookmarkedAnime contract)
        {
            CL_Response<CL_BookmarkedAnime> contractRet = new CL_Response<CL_BookmarkedAnime>();
            contractRet.ErrorMessage = "";

            try
            {

                BookmarkedAnime ba = null;
                if (contract.BookmarkedAnimeID!=0)
                {
                    ba = RepoFactory.BookmarkedAnime.GetByID(contract.BookmarkedAnimeID);
                    if (ba == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Bookmark with ID: " +
                                                   contract.BookmarkedAnimeID.ToString();
                        return contractRet;
                    }
                }
                else
                {
                    // if a new record, check if it is allowed
                    BookmarkedAnime baTemp = RepoFactory.BookmarkedAnime.GetByAnimeID(contract.AnimeID);
                    if (baTemp != null)
                    {
                        contractRet.ErrorMessage = "A bookmark with the AnimeID already exists: " +
                                                   contract.AnimeID.ToString();
                        return contractRet;
                    }

                    ba = new BookmarkedAnime();
                }

                ba.AnimeID = contract.AnimeID;
                ba.Priority = contract.Priority;
                ba.Notes = contract.Notes;
                ba.Downloading = contract.Downloading;

                RepoFactory.BookmarkedAnime.Save(ba);

                contractRet.Result = ModelClients.ToClient(ba);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {

                BookmarkedAnime ba = RepoFactory.BookmarkedAnime.GetByID(bookmarkedAnimeID);
                if (ba == null)
                    return "Bookmarked not found";

                RepoFactory.BookmarkedAnime.Delete(bookmarkedAnimeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public CL_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {
                return ModelClients.ToClient(RepoFactory.BookmarkedAnime.GetByID(bookmarkedAnimeID));
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public CL_Response<CL_GroupFilter> SaveGroupFilter(CL_GroupFilter contract)
        {
            CL_Response<CL_GroupFilter> response = new CL_Response<CL_GroupFilter>();
            response.ErrorMessage = string.Empty;
            response.Result = null;



            // Process the group
            SVR_GroupFilter gf;
            if (contract.GroupFilterID!=0)
            {
                gf = RepoFactory.GroupFilter.GetByID(contract.GroupFilterID);
                if (gf == null)
                {
                    response.ErrorMessage = "Could not find existing Group Filter with ID: " +
                                            contract.GroupFilterID.ToString();
                    return response;
                }
            }
            gf = SVR_GroupFilter.FromClient(contract);
            gf.EvaluateAnimeGroups();
            gf.EvaluateAnimeSeries();
            RepoFactory.GroupFilter.Save(gf);
            response.Result = gf.ToClient();
            return response;
        }

        public string DeleteGroupFilter(int groupFilterID)
        {
            try
            {
                SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                if (gf == null)
                    return "Group Filter not found";

                RepoFactory.GroupFilter.Delete(groupFilterID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<CL_AnimeGroup_User> SaveGroup(CL_AnimeGroup_Save_Request contract, int userID)
        {
            CL_Response<CL_AnimeGroup_User> contractout = new CL_Response<CL_AnimeGroup_User>();
            contractout.ErrorMessage = "";
            contractout.Result = null;
            try
            {
                SVR_AnimeGroup grp = null;
                if (contract.AnimeGroupID.HasValue)
                {
                    grp = RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupID.Value);
                    if (grp == null)
                    {
                        contractout.ErrorMessage = "Could not find existing group with ID: " +
                                                   contract.AnimeGroupID.Value.ToString();
                        return contractout;
                    }
                }
                else
                {
                    grp = new SVR_AnimeGroup();
                    grp.Description = "";
                    grp.IsManuallyNamed = 0;
                    grp.DateTimeCreated = DateTime.Now;
                    grp.DateTimeUpdated = DateTime.Now;
                    grp.SortName = "";
                    grp.MissingEpisodeCount = 0;
                    grp.MissingEpisodeCountGroups = 0;
                    grp.OverrideDescription = 0;
                }

                if (string.IsNullOrEmpty(contract.GroupName))
                {
                    contractout.ErrorMessage = "Must specify a group name";
                    return contractout;
                }

                grp.AnimeGroupParentID = contract.AnimeGroupParentID;
                grp.Description = contract.Description;
                grp.GroupName = contract.GroupName;

                grp.IsManuallyNamed = contract.IsManuallyNamed;
                grp.OverrideDescription = 0;

                if (string.IsNullOrEmpty(contract.SortName))
                    grp.SortName = contract.GroupName;
                else
                    grp.SortName = contract.SortName;

                RepoFactory.AnimeGroup.Save(grp, true, true);

                SVR_AnimeGroup_User userRecord = grp.GetUserRecord(userID);
                if (userRecord == null) userRecord = new SVR_AnimeGroup_User(userID, grp.AnimeGroupID);
                userRecord.IsFave = contract.IsFave;
                RepoFactory.AnimeGroup_User.Save(userRecord);

                contractout.Result = grp.GetUserContract(userID);


                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public CL_Response<CL_AnimeSeries_User> MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID)
        {
            CL_Response<CL_AnimeSeries_User> contractout = new CL_Response<CL_AnimeSeries_User>();
            contractout.ErrorMessage = "";
            contractout.Result = null;
            try
            {
                SVR_AnimeSeries ser = null;

                ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                {
                    contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID.ToString();
                    return contractout;
                }

                // make sure the group exists
                SVR_AnimeGroup grpTemp = RepoFactory.AnimeGroup.GetByID(newAnimeGroupID);
                if (grpTemp == null)
                {
                    contractout.ErrorMessage = "Could not find existing group with ID: " + newAnimeGroupID.ToString();
                    return contractout;
                }

                int oldGroupID = ser.AnimeGroupID;
                ser.AnimeGroupID = newAnimeGroupID;
                ser.DateTimeUpdated = DateTime.Now;

                //				repSeries.Save(ser,false,false);

                // update stats for new groups
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);

                //Update and Save
                ser.UpdateStats(true, true, true);

                // update stats for old groups
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(oldGroupID);
                if (grp != null)
                {
					SVR_AnimeGroup topGroup = grp.TopLevelAnimeGroup;
					if (grp.GetAllSeries().Count == 0)
					{
                        RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);
					}
                    if (topGroup.AnimeGroupID!=grp.AnimeGroupID)
    					topGroup.UpdateStatsFromTopLevel(true, true, true);
                }

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                    return contractout;
                }

                contractout.Result = ser.GetUserContract(userID);

                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public CL_Response<CL_AnimeSeries_User> SaveSeries(CL_AnimeSeries_Save_Request contract, int userID)
        {
            CL_Response<CL_AnimeSeries_User> contractout = new CL_Response<CL_AnimeSeries_User>();
            contractout.ErrorMessage = "";
            contractout.Result = null;
            try
            {
               
                SVR_AnimeSeries ser = null;

                int? oldGroupID = null;
                if (contract.AnimeSeriesID.HasValue)
                {
                    ser = RepoFactory.AnimeSeries.GetByID(contract.AnimeSeriesID.Value);
                    if (ser == null)
                    {
                        contractout.ErrorMessage = "Could not find existing series with ID: " +
                                                   contract.AnimeSeriesID.Value.ToString();
                        return contractout;
                    }

                    // check if we are moving a series
                    oldGroupID = ser.AnimeGroupID;
                }
                else
                {
                    ser = new SVR_AnimeSeries();
                    ser.DateTimeCreated = DateTime.Now;
                    ser.DefaultAudioLanguage = "";
                    ser.DefaultSubtitleLanguage = "";
                    ser.MissingEpisodeCount = 0;
                    ser.MissingEpisodeCountGroups = 0;
                    ser.LatestLocalEpisodeNumber = 0;
                    ser.SeriesNameOverride = "";
                }


                ser.AnimeGroupID = contract.AnimeGroupID;
                ser.AniDB_ID = contract.AniDB_ID;
                ser.DefaultAudioLanguage = contract.DefaultAudioLanguage;
                ser.DefaultSubtitleLanguage = contract.DefaultSubtitleLanguage;
                ser.DateTimeUpdated = DateTime.Now;
                ser.SeriesNameOverride = contract.SeriesNameOverride;
                ser.DefaultFolder = contract.DefaultFolder;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                    return contractout;
                }


                // update stats for groups
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true ,true, true);


                //Update and Save
                ser.UpdateStats(true, true, true);

                if (oldGroupID.HasValue)
                {
                    SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(oldGroupID.Value);
                    if (grp != null)
                    {
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                    }
                }
                contractout.Result = ser.GetUserContract(userID);
                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public CL_AnimeEpisode_User GetEpisode(int animeEpisodeID, int userID)
        {
            try
            {
                return RepoFactory.AnimeEpisode.GetByID(animeEpisodeID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public IReadOnlyList<AnimeEpisode> GetAllEpisodes()
        {
            try
            {
                return RepoFactory.AnimeEpisode.GetAll();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public CL_AnimeEpisode_User GetEpisodeByAniDBEpisodeID(int episodeID, int userID)
        {
            try
            {
                return RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episodeID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public string RemoveAssociationOnFile(int videoLocalID, int aniDBEpisodeID)
        {
            try
            {

                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (string.IsNullOrEmpty(vid.Hash)) //this shouldn't happen
                    return "Could not desassociate a cloud file without hash, hash it locally first";

                int? animeSeriesID = null;
                foreach (AnimeEpisode ep in vid.GetAnimeEpisodes())
                {
                    if (ep.AniDB_EpisodeID != aniDBEpisodeID) continue;

                    animeSeriesID = ep.AnimeSeriesID;
                    CrossRef_File_Episode xref = RepoFactory.CrossRef_File_Episode.GetByHashAndEpisodeID(vid.Hash, ep.AniDB_EpisodeID);
                    if (xref != null)
                    {
                        if (xref.CrossRefSource == (int) CrossRefSource.AniDB)
                            return "Cannot remove associations created from AniDB data";

                        // delete cross ref from web cache 
                        CommandRequest_WebCacheDeleteXRefFileEpisode cr =
                            new CommandRequest_WebCacheDeleteXRefFileEpisode(vid.Hash,
                                ep.AniDB_EpisodeID);
                        cr.Save();

                        RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
                    }
                }

                if (animeSeriesID.HasValue)
                {
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID.Value);
                    if (ser != null)
                        ser.QueueUpdateStats();
                }
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                vid.IsIgnored = isIgnored ? 1 : 0;
                RepoFactory.VideoLocal.Save(vid, false);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string SetVariationStatusOnFile(int videoLocalID, bool isVariation)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                vid.IsVariation = isVariation ? 1 : 0;
                RepoFactory.VideoLocal.Save(vid, false);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string AssociateSingleFile(int videoLocalID, int animeEpisodeID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (string.IsNullOrEmpty(vid.Hash))
                    return "Could not associate a cloud file without hash, hash it locally first";

                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null)
                    return "Could not find episode record";

                CrossRef_File_Episode xref = new CrossRef_File_Episode();
                try
                {
                    xref.PopulateManually(vid, ep);
                }
                catch (Exception ex)
                {
                    string msg = string.Format("Error populating XREF: {0}", vid.ToStringDetailed());
                    throw;
                }
                RepoFactory.CrossRef_File_Episode.Save(xref);
                CommandRequest_WebCacheSendXRefFileEpisode cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                cr.Save();
                vid.Places.ForEach(a =>
                {
                    a.RenameIfRequired();
                    a.MoveFileIfRequired();
                });

                SVR_AnimeSeries ser = ep.GetAnimeSeries();
                ser.EpisodeAddedDate = DateTime.Now;
                RepoFactory.AnimeSeries.Save(ser, false, true);

                //Update will re-save
                ser.QueueUpdateStats();


                foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    RepoFactory.AnimeGroup.Save(grp, false, false);
                }

                CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(vid.Hash);
                cmdAddFile.Save();
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return "";
        }

        public string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startEpNum,
            int endEpNum)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (vid.Hash == null)
                    return "Could not associate a cloud file without hash, hash it locally first";
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";
                for (int i = startEpNum; i <= endEpNum; i++)
                {
                    List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, i);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    AniDB_Episode aniep = anieps[0];

                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                    if (ep == null)
                        return "Could not find episode record";


                    CrossRef_File_Episode xref = new CrossRef_File_Episode();
                    xref.PopulateManually(vid, ep);
                    RepoFactory.CrossRef_File_Episode.Save(xref);

                    CommandRequest_WebCacheSendXRefFileEpisode cr =
                        new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                    cr.Save();

                }
                vid.Places.ForEach(a =>
                {
                    a.RenameIfRequired();
                    a.MoveFileIfRequired();
                });
                ser.EpisodeAddedDate = DateTime.Now;
                RepoFactory.AnimeSeries.Save(ser, false, true);

                foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    RepoFactory.AnimeGroup.Save(grp, false, false);
                }

                //Update will re-save
                ser.QueueUpdateStats();

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return "";
        }

        public string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber,
            bool singleEpisode)
        {
            try
            {


                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";

                int epNumber = startingEpisodeNumber;
                int count = 1;


                foreach (int videoLocalID in videoLocalIDs)
                {
                    SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                    if (vid == null)
                        return "Could not find video local record";
                    if (vid.Hash == null)
                        return "Could not associate a cloud file without hash, hash it locally first";

                    List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, epNumber);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    AniDB_Episode aniep = anieps[0];

                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                    if (ep == null)
                        return "Could not find episode record";


                    CrossRef_File_Episode xref = new CrossRef_File_Episode();
                    xref.PopulateManually(vid, ep);

                    // TODO do this properly
                    if (singleEpisode)
                    {
                        xref.EpisodeOrder = count;
                        if (videoLocalIDs.Count > 5)
                            xref.Percentage = 100;
                        else
                            xref.Percentage = GetEpisodePercentages(videoLocalIDs.Count)[count - 1];
                    }

                    RepoFactory.CrossRef_File_Episode.Save(xref);
                    CommandRequest_WebCacheSendXRefFileEpisode cr =
                        new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                    cr.Save();
                    vid.Places.ForEach(a =>
                    {
                        a.RenameIfRequired();
                        a.MoveFileIfRequired();

                    });
                    count++;
                    if (!singleEpisode) epNumber++;
                }
                ser.EpisodeAddedDate = DateTime.Now;
                RepoFactory.AnimeSeries.Save(ser, false, true);

                foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    RepoFactory.AnimeGroup.Save(grp, false, false);
                }

                // update epidsode added stats
                ser.QueueUpdateStats();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return "";
        }

        private int[] GetEpisodePercentages(int numEpisodes)
        {
            if (numEpisodes == 1) return new int[] {100};
            if (numEpisodes == 2) return new int[] {50, 100};
            if (numEpisodes == 3) return new int[] {33, 66, 100};
            if (numEpisodes == 4) return new int[] {25, 50, 75, 100};
            if (numEpisodes == 5) return new int[] {20, 40, 60, 80, 100};

            return new int[] {100};
        }

        public CL_Response<CL_AnimeSeries_User> CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID)
        {
            CL_Response<CL_AnimeSeries_User> response = new CL_Response<CL_AnimeSeries_User>();
            response.Result = null;
            response.ErrorMessage = "";
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    if (animeGroupID.HasValue)
                    {
                        SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID.Value);
                        if (grp == null)
                        {
                            response.ErrorMessage = "Could not find the specified group";
                            return response;
                        }
                    }

                    // make sure a series doesn't already exists for this anime
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                    if (ser != null)
                    {
                        response.ErrorMessage = "A series already exists for this anime";
                        return response;
                    }

                    // make sure the anime exists first
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null)
                        anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, false, false);

                    if (anime == null)
                    {
                        response.ErrorMessage = "Could not get anime information from AniDB";
                        return response;
                    }

                    if (animeGroupID.HasValue)
                    {
                        ser = new SVR_AnimeSeries();
                        ser.Populate(anime);
                        ser.AnimeGroupID = animeGroupID.Value;
                        RepoFactory.AnimeSeries.Save(ser, false);
                    }
                    else
                    {
                        ser = anime.CreateAnimeSeriesAndGroup(sessionWrapper);
                    }

                    ser.CreateAnimeEpisodes(session);

                    // check if we have any group status data for this associated anime
                    // if not we will download it now
                    if (RepoFactory.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                    {
                        CommandRequest_GetReleaseGroupStatus cmdStatus =
                            new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                        cmdStatus.Save(session);
                    }


                    ser.UpdateStats(true, true, true);

                    // check for TvDB associations
                    CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(anime.AnimeID, false);
                    cmd.Save(session);

                    if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        // check for Trakt associations
                        CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(anime.AnimeID, false);
                        cmd2.Save(session);
                    }
                    response.Result = ser.GetUserContract(userID);
                    return response;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                response.ErrorMessage = ex.Message;
            }

            return response;
        }

        public string UpdateAnimeData(int animeID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ShokoService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, true, false);

                    // also find any files for this anime which don't have proper media info data
                    // we can usually tell this if the Resolution == '0x0'
                    foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                    {
                        AniDB_File aniFile = vid.GetAniDBFile();
                        if (aniFile == null) continue;

                        if (aniFile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase))
                        {
                            CommandRequest_GetFile cmd = new CommandRequest_GetFile(vid.VideoLocalID, true);
                            cmd.Save(session);
                        }
                    }

                    // update group status information
                    CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID,
                        true);
                    cmdStatus.Save(session);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return "";
        }

        public string UpdateCalendarData()
        {
            try
            {
                Importer.CheckForCalendarUpdate(true);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return "";
        }

        public int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
        {
            return Importer.UpdateAniDBFileData(missingInfo, outOfDate, countOnly);
        }

        public string UpdateFileData(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return "File could not be found";
                CommandRequest_GetFile cmd = new CommandRequest_GetFile(vid.VideoLocalID, true);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
            return "";
        }

        public string UpdateEpisodeData(int episodeID)
        {
            try
            {
                CommandRequest_GetEpisode cmd = new CommandRequest_GetEpisode(episodeID);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
            return "";
        }

        public string RescanFile(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return "File could not be found";
                if (string.IsNullOrEmpty(vid.Hash)) return "Could not Update a cloud file without hash, hash it locally first";
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vid.VideoLocalID, true);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.Message);
                return ex.Message;
            }
            return "";
        }

        public string UpdateTvDBData(int seriesID)
        {
            try
            {
                ShokoService.TvdbHelper.UpdateAllInfoAndImages(seriesID, false, true);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return "";
        }

        public string UpdateTraktData(string traktD)
        {
            try
            {
                TraktTVHelper.UpdateAllInfoAndImages(traktD, true);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return "";
        }

        public string SyncTraktSeries(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (ser == null) return "Could not find Anime Series";

                CommandRequest_TraktSyncCollectionSeries cmd =
                    new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID,
                        ser.GetSeriesName());
                cmd.Save();

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string UpdateMovieDBData(int movieD)
        {
            try
            {
                MovieDBHelper.UpdateMovieInfo(movieD, true);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return "";
        }

        public CL_AniDB_Anime GetAnime(int animeID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(session.Wrap(), animeID);
                    return anime?.Contract.AniDBAnime;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public List<CL_AniDB_Anime> GetAllAnime()
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract.AniDBAnime).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_AniDB_Anime>();
        }

        public List<CL_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState,
            int userID)
        {
            List<CL_AnimeRating> contracts = new List<CL_AnimeRating>();

            try
            {
                IReadOnlyList<SVR_AnimeSeries> series = RepoFactory.AnimeSeries.GetAll();
                Dictionary<int, SVR_AnimeSeries> dictSeries = new Dictionary<int, SVR_AnimeSeries>();
                foreach (SVR_AnimeSeries ser in series)
                    dictSeries[ser.AniDB_ID] = ser;

                RatingCollectionState _collectionState = (RatingCollectionState) collectionState;
                RatingWatchedState _watchedState = (RatingWatchedState) watchedState;
                RatingVotedState _ratingVotedState = (RatingVotedState) ratingVotedState;

                DateTime start = DateTime.Now;


                /*
				// build a dictionary of categories
				AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
				AniDB_Anime_CategoryRepository repAnimeCat = new AniDB_Anime_CategoryRepository();

				List<AniDB_Category> allCatgeories = repCats.GetAll();
				Dictionary<int, AniDB_Category> allCatgeoriesDict = new Dictionary<int, AniDB_Category>();
				foreach (AniDB_Category cat in allCatgeories)
					allCatgeoriesDict[cat.CategoryID] = cat;


				List<AniDB_Anime_Category> allAnimeCatgeories = repAnimeCat.GetAll();
				Dictionary<int, List<AniDB_Anime_Category>> allAnimeCatgeoriesDict = new Dictionary<int, List<AniDB_Anime_Category>>(); // 
				foreach (AniDB_Anime_Category aniCat in allAnimeCatgeories)
				{
					if (!allAnimeCatgeoriesDict.ContainsKey(aniCat.AnimeID))
						allAnimeCatgeoriesDict[aniCat.AnimeID] = new List<AniDB_Anime_Category>();

					allAnimeCatgeoriesDict[aniCat.AnimeID].Add(aniCat);
				}

				// build a dictionary of titles
				AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();


				List<AniDB_Anime_Title> allTitles = repTitles.GetAll();
				Dictionary<int, List<AniDB_Anime_Title>> allTitlesDict = new Dictionary<int, List<AniDB_Anime_Title>>();
				foreach (AniDB_Anime_Title title in allTitles)
				{
					if (!allTitlesDict.ContainsKey(title.AnimeID))
						allTitlesDict[title.AnimeID] = new List<AniDB_Anime_Title>();

					allTitlesDict[title.AnimeID].Add(title);
				}


				// build a dictionary of tags
				AniDB_TagRepository repTags = new AniDB_TagRepository();
				AniDB_Anime_TagRepository repAnimeTag = new AniDB_Anime_TagRepository();

				List<AniDB_Tag> allTags = repTags.GetAll();
				Dictionary<int, AniDB_Tag> allTagsDict = new Dictionary<int, AniDB_Tag>();
				foreach (AniDB_Tag tag in allTags)
					allTagsDict[tag.TagID] = tag;


				List<AniDB_Anime_Tag> allAnimeTags = repAnimeTag.GetAll();
				Dictionary<int, List<AniDB_Anime_Tag>> allAnimeTagsDict = new Dictionary<int, List<AniDB_Anime_Tag>>(); // 
				foreach (AniDB_Anime_Tag aniTag in allAnimeTags)
				{
					if (!allAnimeTagsDict.ContainsKey(aniTag.AnimeID))
						allAnimeTagsDict[aniTag.AnimeID] = new List<AniDB_Anime_Tag>();

					allAnimeTagsDict[aniTag.AnimeID].Add(aniTag);
				}

				// build a dictionary of languages
				AdhocRepository rep = new AdhocRepository();
				Dictionary<int, LanguageStat> dictAudioStats = rep.GetAudioLanguageStatsForAnime();
				Dictionary<int, LanguageStat> dictSubtitleStats = rep.GetSubtitleLanguageStatsForAnime();

				Dictionary<int, string> dictAnimeVideoQualStats = rep.GetAllVideoQualityByAnime();
				Dictionary<int, AnimeVideoQualityStat> dictAnimeEpisodeVideoQualStats = rep.GetEpisodeVideoQualityStatsByAnime();
				 * */

                IReadOnlyList<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetAll();

                // user votes
                IReadOnlyList<AniDB_Vote> allVotes = RepoFactory.AniDB_Vote.GetAll();

                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return contracts;

                int i = 0;


                foreach (SVR_AniDB_Anime anime in animes)
                {
                    i++;

                    // evaluate collection states
                    if (_collectionState == RatingCollectionState.AllEpisodesInMyCollection)
                    {
                        if (!anime.GetFinishedAiring()) continue;
                        if (!dictSeries.ContainsKey(anime.AnimeID)) continue;
                        if (dictSeries[anime.AnimeID].MissingEpisodeCount > 0) continue;
                    }

                    if (_collectionState == RatingCollectionState.InMyCollection)
                        if (!dictSeries.ContainsKey(anime.AnimeID)) continue;

                    if (_collectionState == RatingCollectionState.NotInMyCollection)
                        if (dictSeries.ContainsKey(anime.AnimeID)) continue;

                    if (!user.AllowedAnime(anime)) continue;

                    // evaluate watched states
                    if (_watchedState == RatingWatchedState.AllEpisodesWatched)
                    {
                        if (!dictSeries.ContainsKey(anime.AnimeID)) continue;
                        AnimeSeries_User userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                        if (userRec == null) continue;
                        if (userRec.UnwatchedEpisodeCount > 0) continue;
                    }

                    if (_watchedState == RatingWatchedState.NotWatched)
                    {
                        if (dictSeries.ContainsKey(anime.AnimeID))
                        {
                            AnimeSeries_User userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                            if (userRec != null)
                            {
                                if (userRec.UnwatchedEpisodeCount == 0) continue;
                            }
                        }
                    }

                    // evaluate voted states
                    if (_ratingVotedState == RatingVotedState.Voted)
                    {
                        bool voted = false;
                        foreach (AniDB_Vote vote in allVotes)
                        {
                            if (vote.EntityID == anime.AnimeID &&
                                (vote.VoteType == (int) AniDBVoteType.Anime ||
                                 vote.VoteType == (int) AniDBVoteType.AnimeTemp))
                            {
                                voted = true;
                                break;
                            }
                        }

                        if (!voted) continue;
                    }

                    if (_ratingVotedState == RatingVotedState.NotVoted)
                    {
                        bool voted = false;
                        foreach (AniDB_Vote vote in allVotes)
                        {
                            if (vote.EntityID == anime.AnimeID &&
                                (vote.VoteType == (int) AniDBVoteType.Anime ||
                                 vote.VoteType == (int) AniDBVoteType.AnimeTemp))
                            {
                                voted = true;
                                break;
                            }
                        }

                        if (voted) continue;
                    }

                    CL_AnimeRating contract = new CL_AnimeRating();
                    contract.AnimeID = anime.AnimeID;
                    contract.AnimeDetailed = anime.Contract;

                    if (dictSeries.ContainsKey(anime.AnimeID))
                    {
                        contract.AnimeSeries = dictSeries[anime.AnimeID].GetUserContract(userID);
                    }

                    contracts.Add(contract);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<CL_AniDB_AnimeDetailed> GetAllAnimeDetailed()
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_AniDB_AnimeDetailed>();
        }

        public List<CL_AnimeSeries_User> GetAllSeries(int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetAll().Select(a => a.GetUserContract(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_AnimeSeries_User>();
        }
        public CL_Changes<CL_GroupFilter> GetGroupFilterChanges(DateTime date)
        {
            CL_Changes<CL_GroupFilter> c=new CL_Changes<CL_GroupFilter>();
            try
            {
                Changes<int> changes = RepoFactory.GroupFilter.GetChangeTracker().GetChanges(date);
                c.ChangedItems = changes.ChangedItems.Select(a => RepoFactory.GroupFilter.GetByID(a).ToClient()).Where(a => a != null).ToList();
                c.RemovedItems = changes.RemovedItems.ToList();
                c.LastChange = changes.LastChange;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return c;
        }

        public CL_MainChanges GetAllChanges(DateTime date, int userID)
        {
            CL_MainChanges c=new CL_MainChanges();
            try
            {
                List<Changes<int>> changes = ChangeTracker<int>.GetChainedChanges(new List<ChangeTracker<int>>
                {
                    RepoFactory.GroupFilter.GetChangeTracker(),
                    RepoFactory.AnimeGroup.GetChangeTracker(),
                    RepoFactory.AnimeGroup_User.GetChangeTracker(userID),
                    RepoFactory.AnimeSeries.GetChangeTracker(),
                    RepoFactory.AnimeSeries_User.GetChangeTracker(userID)
                }, date);
                c.Filters=new CL_Changes<CL_GroupFilter>();
                c.Filters.ChangedItems=changes[0].ChangedItems.Select(a=> RepoFactory.GroupFilter.GetByID(a).ToClient()).Where(a=>a!=null).ToList();
                c.Filters.RemovedItems= changes[0].RemovedItems.ToList();
                c.Filters.LastChange = changes[0].LastChange;

                //Add Group Filter that one of his child changed.
                bool end;
                do
                {
                    end = true;
                    foreach (CL_GroupFilter ag in c.Filters.ChangedItems.Where(a => a.ParentGroupFilterID.HasValue && a.ParentGroupFilterID.Value != 0).ToList())
                    {
                        if (!c.Filters.ChangedItems.Any(a => a.GroupFilterID == ag.ParentGroupFilterID.Value))
                        {
                            end = false;
                            CL_GroupFilter cag = RepoFactory.GroupFilter.GetByID(ag.ParentGroupFilterID.Value).ToClient();
                            if (cag != null)
                                c.Filters.ChangedItems.Add(cag);
                        }
                    }
                } while (!end);

                c.Groups=new CL_Changes<CL_AnimeGroup_User>();
                changes[1].ChangedItems.UnionWith(changes[2].ChangedItems);
                changes[1].ChangedItems.UnionWith(changes[2].RemovedItems);
                if (changes[2].LastChange > changes[1].LastChange)
                    changes[1].LastChange = changes[2].LastChange;
                c.Groups.ChangedItems=changes[1].ChangedItems.Select(a=> RepoFactory.AnimeGroup.GetByID(a)).Where(a => a != null).Select(a=>a.GetUserContract(userID)).ToList();



                c.Groups.RemovedItems = changes[1].RemovedItems.ToList();
                c.Groups.LastChange = changes[1].LastChange;
                c.Series=new CL_Changes<CL_AnimeSeries_User>();
                changes[3].ChangedItems.UnionWith(changes[4].ChangedItems);
                changes[3].ChangedItems.UnionWith(changes[4].RemovedItems);
                if (changes[4].LastChange > changes[3].LastChange)
                    changes[3].LastChange = changes[4].LastChange;
                c.Series.ChangedItems = changes[3].ChangedItems.Select(a => RepoFactory.AnimeSeries.GetByID(a)).Where(a=>a!=null).Select(a=>a.GetUserContract(userID)).ToList();
                c.Series.RemovedItems = changes[3].RemovedItems.ToList();
                c.Series.LastChange = changes[3].LastChange;
                c.LastChange = c.Filters.LastChange;
                if (c.Groups.LastChange > c.LastChange)
                    c.LastChange = c.Groups.LastChange;
                if (c.Series.LastChange > c.LastChange)
                    c.LastChange = c.Series.LastChange;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return c;
        }

        public CL_AniDB_AnimeDetailed GetAnimeDetailed(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetByAnimeID(animeID)?.Contract;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<string> GetAllTagNames()
        {
            List<string> allTagNames = new List<string>();

            try
            {
                DateTime start = DateTime.Now;

                foreach (AniDB_Tag tag in RepoFactory.AniDB_Tag.GetAll())
                {
                    allTagNames.Add(tag.TagName);
                }
                allTagNames.Sort();


                TimeSpan ts = DateTime.Now - start;
                logger.Info("GetAllTagNames  in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return allTagNames;
        }

        public List<CL_AnimeGroup_User> GetSubGroupsForGroup(int animeGroupID, int userID)
        {
            List<CL_AnimeGroup_User> retGroups = new List<CL_AnimeGroup_User>();
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return retGroups;
                foreach (SVR_AnimeGroup grpChild in grp.GetChildGroups())
                {
                    CL_AnimeGroup_User ugrp = grpChild.GetUserContract(userID);
                    if (ugrp != null)
                        retGroups.Add(ugrp);
                }

                return retGroups;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return retGroups;
        }

        public List<CL_AnimeSeries_User> GetSeriesForGroup(int animeGroupID, int userID)
        {
            List<CL_AnimeSeries_User> series = new List<CL_AnimeSeries_User>();
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (SVR_AnimeSeries ser in grp.GetSeries())
                {
                    CL_AnimeSeries_User s = ser.GetUserContract(userID);
                    if (s != null)
                        series.Add(s);
                }

                return series;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return series;
            }
        }

        public List<CL_AnimeSeries_User> GetSeriesForGroupRecursive(int animeGroupID, int userID)
        {
            List<CL_AnimeSeries_User> series = new List<CL_AnimeSeries_User>();
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (SVR_AnimeSeries ser in grp.GetAllSeries())
                {
                    CL_AnimeSeries_User s = ser.GetUserContract(userID);
                    if (s != null)
                        series.Add(s);
                }

                return series;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return series;
            }
        }

        public List<AniDB_Episode> GetAniDBEpisodesForAnime(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Episode.GetByAnimeID(animeID).OrderBy(a => a.EpisodeType).ThenBy(a => a.EpisodeNumber).Cast<AniDB_Episode>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return new List<AniDB_Episode>();
        }

        public List<string> DirectoriesFromImportFolderPath(int cloudaccountid, string path)
        {
            List<string> result = new List<string>();
            try
            {
                IFileSystem n=null;
                if (cloudaccountid == 0)
                {
                    FileSystemResult<IFileSystem> ff = CloudFileSystemPluginFactory.Instance.List.FirstOrDefault(a => a.Name == "Local File System")?.Init("", null, null);
                    if (ff.IsOk)
                        n = ff.Result;
                }
                else
                {
                    SVR_CloudAccount cl = RepoFactory.CloudAccount.GetByID(cloudaccountid);
                    if (cl != null)
                       n = cl.FileSystem;
                }
                if (n != null)
                {
                    FileSystemResult<IObject> dirr = n.Resolve(path);
                    if (dirr == null || !dirr.IsOk || dirr.Result is IFile)
                        return null;
                    IDirectory dir = dirr.Result as IDirectory;
                    FileSystemResult fr = dir.Populate();
                    if (!fr.IsOk)
                        return result;
                    return dir.Directories.Select(a => a.FullName).OrderBy(a => a).ToList();
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return result;
        }

        public List<CL_CloudAccount> GetCloudProviders()
        {
            List<CL_CloudAccount> ls = new List<CL_CloudAccount>();
            try
            {
                ls.Add(SVR_CloudAccount.CreateLocalFileSystemAccount().ToClient());
                RepoFactory.CloudAccount.GetAll().ForEach(a => ls.Add(a.ToClient()));
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return ls;
        }



        public void TraktScrobble(int animeId, int type, int progress, int status)
        {
            try
            {
                Providers.TraktTV.ScrobblePlayingStatus statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                float progressTrakt;

                switch (status)
                {
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Start:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                        break;
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Pause:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Pause;
                        break;
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Stop:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Stop;
                        break;
                }

                bool isValidProgress = float.TryParse(progress.ToString(), out progressTrakt);

                if (isValidProgress)
                {
                    switch (type)
                    {
                        // Movie
                        case (int) Providers.TraktTV.ScrobblePlayingType.movie:
                            Providers.TraktTV.TraktTVHelper.Scrobble(
                                Providers.TraktTV.ScrobblePlayingType.movie, animeId.ToString(),
                                statusTraktV2, progressTrakt);
                            break;
                        // TV episode
                        case (int) Providers.TraktTV.ScrobblePlayingType.episode:
                            Providers.TraktTV.TraktTVHelper.Scrobble(Providers.TraktTV.ScrobblePlayingType.episode,
                                animeId.ToString(), statusTraktV2, progressTrakt);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<CL_AnimeEpisode_User> GetEpisodesForSeries(int animeSeriesID, int userID)
        {
            List<CL_AnimeEpisode_User> eps = new List<CL_AnimeEpisode_User>();
            try
            {
                return
                    RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                        .Select(a => a.GetUserContract(userID))
                        .Where(a => a != null)
                        .ToList();
                /*
                                DateTime start = DateTime.Now;
                                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
                                VideoLocalRepository repVids = new VideoLocalRepository();
                                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();

                                // get all the data first
                                // we do this to reduce the amount of database calls, which makes it a lot faster
                                AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
                                if (series == null) return eps;

                                List<AnimeEpisode> epList = repEps.GetBySeriesID(animeSeriesID);
                                List<AnimeEpisode_User> userRecordList = repEpUsers.GetByUserIDAndSeriesID(userID, animeSeriesID);
                                Dictionary<int, AnimeEpisode_User> dictUserRecords = new Dictionary<int, AnimeEpisode_User>();
                                foreach (AnimeEpisode_User epuser in userRecordList)
                                    dictUserRecords[epuser.AnimeEpisodeID] = epuser;

                                AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                                List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                                Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                                foreach (AniDB_Episode aniep in aniEpList)
                                    dictAniEps[aniep.EpisodeID] = aniep;

                                // get all the video local records and cross refs
                                List<VideoLocal> vids = repVids.GetByAniDBAnimeID(series.AniDB_ID);
                                List<CrossRef_File_Episode> crossRefs = repCrossRefs.GetByAnimeID(series.AniDB_ID);

                                TimeSpan ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);


                                start = DateTime.Now;
                                foreach (AnimeEpisode ep in epList)
                                {
                                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                                    {
                                        List<VideoLocal> epVids = new List<VideoLocal>();
                                        foreach (CrossRef_File_Episode xref in crossRefs)
                                        {
                                            if (xref.EpisodeID == dictAniEps[ep.AniDB_EpisodeID].EpisodeID)
                                            {
                                                // don't add the same file twice, this will occur when
                                                // one file appears over more than one episodes
                                                Dictionary<string, string> addedFiles = new Dictionary<string, string>();
                                                foreach (VideoLocal vl in vids)
                                                {
                                                    if (string.Equals(xref.Hash, vl.Hash, StringComparison.InvariantCultureIgnoreCase))
                                                    {
                                                        if (!addedFiles.ContainsKey(xref.Hash.Trim().ToUpper()))
                                                        {
                                                            addedFiles[xref.Hash.Trim().ToUpper()] = xref.Hash.Trim().ToUpper();
                                                            epVids.Add(vl);
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        AnimeEpisode_User epuser = null;
                                        if (dictUserRecords.ContainsKey(ep.AnimeEpisodeID))
                                            epuser = dictUserRecords[ep.AnimeEpisodeID];

                                        eps.Add(ep.ToContract(dictAniEps[ep.AniDB_EpisodeID], epVids, epuser, null));
                                    }
                                }

                                ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);
                                */
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return eps;
        }

        public List<CL_AnimeEpisode_User> GetEpisodesForSeriesOld(int animeSeriesID)
        {
            List<CL_AnimeEpisode_User> eps = new List<CL_AnimeEpisode_User>();
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(1) ?? RepoFactory.JMMUser.GetAll().FirstOrDefault(a => a.Username == "Default");
                //HACK (We should have a default user locked)
                if (user != null)
                    return GetEpisodesForSeries(animeSeriesID, user.JMMUserID);
                /*
                                JMMUser u

                                DateTime start = DateTime.Now;
                                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
                                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();


                                // get all the data first
                                // we do this to reduce the amount of database calls, which makes it a lot faster
                                AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
                                if (series == null) return eps;

                                List<AnimeEpisode> epList = repEps.GetBySeriesID(animeSeriesID);

                                AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                                List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                                Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                                foreach (AniDB_Episode aniep in aniEpList)
                                    dictAniEps[aniep.EpisodeID] = aniep;

                                List<CrossRef_File_Episode> crossRefList = repCrossRefs.GetByAnimeID(series.AniDB_ID);




                                TimeSpan ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);


                                start = DateTime.Now;
                                foreach (AnimeEpisode ep in epList)
                                {
                                    List<CrossRef_File_Episode> xrefs = new List<CrossRef_File_Episode>();
                                    foreach (CrossRef_File_Episode xref in crossRefList)
                                    {
                                        if (ep.AniDB_EpisodeID == xref.EpisodeID)
                                            xrefs.Add(xref);
                                    }

                                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                                        eps.Add(ep.ToContractOld(dictAniEps[ep.AniDB_EpisodeID]));
                                }

                                ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);
                                */
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return eps;
        }

        public CL_AnimeSeries_User GetSeries(int animeSeriesID, int userID)
        {

            try
            {
                return RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public List<CL_AnimeSeries_User> GetSeriesByFolderID(int FolderID, int userID, int max)
        {
            try
            {
                int limit = 0;
                List<CL_AnimeSeries_User> list = new List<CL_AnimeSeries_User>();

                foreach (SVR_VideoLocal vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
                {
                    foreach (CL_AnimeEpisode_User ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                    {
                        CL_AnimeSeries_User ase = GetSeries(ae.AnimeSeriesID, userID);
                        if (!list.Contains(ase))
                        {
                            limit++;
                            list.Add(ase);
                            if (limit >= max)
                            {
                                break;
                            }
                        }
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public List<CL_AnimeSeries_FileStats> GetSeriesFileStatsByFolderID(int FolderID, int userID, int max)
        {
            try
            {
                int limit = 0;
                Dictionary<int, CL_AnimeSeries_FileStats> list = new Dictionary<int, CL_AnimeSeries_FileStats>();
                foreach (SVR_VideoLocal vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
                {
                    foreach (CL_AnimeEpisode_User ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                    {
                        CL_AnimeSeries_User ase = GetSeries(ae.AnimeSeriesID, userID);
                        CL_AnimeSeries_FileStats asfs = null;
                        //check if series is in list if not add it
                        if (list.TryGetValue(ase.AnimeSeriesID, out asfs) == false)
                        {
                            limit++;
                            if (limit >= max)
                            {
                                continue;
                            }
                            asfs = new CL_AnimeSeries_FileStats();
                            asfs.AnimeSeriesName = ase.AniDBAnime.AniDBAnime.MainTitle;
                            asfs.FileCount = 0;
                            asfs.FileSize = 0;
                            asfs.Folders = new List<string>();
                            asfs.AnimeSeriesID = ase.AnimeSeriesID;
                            list.Add(ase.AnimeSeriesID, asfs);
                        }

                        asfs.FileCount++;
                        asfs.FileSize += vi.FileSize;

                        //string filePath = Pri.LongPath.Path.GetDirectoryName(vi.FilePath).Replace(importLocation, "");
                        //filePath = filePath.TrimStart('\\');
                        string filePath = RepoFactory.VideoLocalPlace.GetByVideoLocal(vi.VideoLocalID)[0].FilePath;
                        if (!asfs.Folders.Contains(filePath))
                        {
                            asfs.Folders.Add(filePath);
                        }
                    }
                }

                return list.Values.ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public CL_AnimeSeries_User GetSeriesForAnime(int animeID, int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetByAnimeID(animeID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public bool GetSeriesExistingForAnime(int animeID)
        {

            try
            {
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (series == null)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return true;
        }

        public List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
        {
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                    return ep.GetVideoDetailedContracts(userID);
                else
                    return new List<CL_VideoDetailed>();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_VideoDetailed>();
        }

        public List<CL_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                {
                    foreach (SVR_VideoLocal vid in ep.GetVideoLocals())
                    {
                        contracts.Add(vid.ToClient(userID));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<CL_VideoLocal> GetIgnoredFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetIgnoredVideos())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<CL_VideoLocal> GetManuallyLinkedFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<CL_VideoLocal> GetUnrecognisedFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetVideosWithoutEpisode())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public CL_ServerStatus GetServerStatus()
        {
            CL_ServerStatus contract = new CL_ServerStatus();

            try
            {
                contract.HashQueueCount = ShokoService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState = ShokoService.CmdProcessorHasher.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.HashQueueStateId = (int)ShokoService.CmdProcessorHasher.QueueState.queueState;
                contract.HashQueueStateParams = ShokoService.CmdProcessorHasher.QueueState.extraParams;

                contract.GeneralQueueCount = ShokoService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState = ShokoService.CmdProcessorGeneral.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.GeneralQueueStateId = (int)ShokoService.CmdProcessorGeneral.QueueState.queueState;
                contract.GeneralQueueStateParams = ShokoService.CmdProcessorGeneral.QueueState.extraParams;

                contract.ImagesQueueCount = ShokoService.CmdProcessorImages.QueueCount; 
                contract.ImagesQueueState = ShokoService.CmdProcessorImages.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.ImagesQueueStateId = (int)ShokoService.CmdProcessorImages.QueueState.queueState;
                contract.ImagesQueueStateParams = ShokoService.CmdProcessorImages.QueueState.extraParams;

                contract.IsBanned = ShokoService.AnidbProcessor.IsBanned;
                contract.BanReason = ShokoService.AnidbProcessor.BanTime.ToString();
                contract.BanOrigin = ShokoService.AnidbProcessor.BanOrigin;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contract;
        }

        public CL_Response SaveServerSettings(CL_ServerSettings contractIn)
        {
            CL_Response contract = new CL_Response();
            contract.ErrorMessage = "";

            try
            {
                // validate the settings
                bool anidbSettingsChanged = false;
                if (contractIn.AniDB_ClientPort != ServerSettings.AniDB_ClientPort)
                {
                    anidbSettingsChanged = true;
                    int cport = 0;
                    int.TryParse(contractIn.AniDB_ClientPort, out cport);
                    if (cport <= 0)
                    {
                        contract.ErrorMessage = "AniDB Client Port must be numeric and greater than 0" +
                                                Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_ServerPort != ServerSettings.AniDB_ServerPort)
                {
                    anidbSettingsChanged = true;
                    int sport = 0;
                    int.TryParse(contractIn.AniDB_ServerPort, out sport);
                    if (sport <= 0)
                    {
                        contract.ErrorMessage = "AniDB Server Port must be numeric and greater than 0" +
                                                Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_Username != ServerSettings.AniDB_Username)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_Username))
                    {
                        contract.ErrorMessage = "AniDB User Name must have a value" + Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_Password != ServerSettings.AniDB_Password)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_Password))
                    {
                        contract.ErrorMessage = "AniDB Password must have a value" + Environment.NewLine;
                    }
                }

                if (contractIn.AniDB_ServerAddress != ServerSettings.AniDB_ServerAddress)
                {
                    anidbSettingsChanged = true;
                    if (string.IsNullOrEmpty(contractIn.AniDB_ServerAddress))
                    {
                        contract.ErrorMessage = "AniDB Server Address must have a value" + Environment.NewLine;
                    }
                }

                if (contract.ErrorMessage.Length > 0) return contract;

                ServerSettings.AniDB_ClientPort = contractIn.AniDB_ClientPort;
                ServerSettings.AniDB_Password = contractIn.AniDB_Password;
                ServerSettings.AniDB_ServerAddress = contractIn.AniDB_ServerAddress;
                ServerSettings.AniDB_ServerPort = contractIn.AniDB_ServerPort;
                ServerSettings.AniDB_Username = contractIn.AniDB_Username;
                ServerSettings.AniDB_AVDumpClientPort = contractIn.AniDB_AVDumpClientPort;
                ServerSettings.AniDB_AVDumpKey = contractIn.AniDB_AVDumpKey;

                ServerSettings.AniDB_DownloadRelatedAnime = contractIn.AniDB_DownloadRelatedAnime;
                ServerSettings.AniDB_DownloadReleaseGroups = contractIn.AniDB_DownloadReleaseGroups;
                ServerSettings.AniDB_DownloadReviews = contractIn.AniDB_DownloadReviews;
                ServerSettings.AniDB_DownloadSimilarAnime = contractIn.AniDB_DownloadSimilarAnime;

                ServerSettings.AniDB_MyList_AddFiles = contractIn.AniDB_MyList_AddFiles;
                ServerSettings.AniDB_MyList_ReadUnwatched = contractIn.AniDB_MyList_ReadUnwatched;
                ServerSettings.AniDB_MyList_ReadWatched = contractIn.AniDB_MyList_ReadWatched;
                ServerSettings.AniDB_MyList_SetUnwatched = contractIn.AniDB_MyList_SetUnwatched;
                ServerSettings.AniDB_MyList_SetWatched = contractIn.AniDB_MyList_SetWatched;
                ServerSettings.AniDB_MyList_StorageState = (AniDBFileStatus) contractIn.AniDB_MyList_StorageState;
                ServerSettings.AniDB_MyList_DeleteType = (AniDBFileDeleteType) contractIn.AniDB_MyList_DeleteType;

                ServerSettings.AniDB_MyList_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_MyList_UpdateFrequency;
                ServerSettings.AniDB_Calendar_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_Calendar_UpdateFrequency;
                ServerSettings.AniDB_Anime_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_Anime_UpdateFrequency;
                ServerSettings.AniDB_MyListStats_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_MyListStats_UpdateFrequency;
                ServerSettings.AniDB_File_UpdateFrequency =
                    (ScheduledUpdateFrequency) contractIn.AniDB_File_UpdateFrequency;

                ServerSettings.AniDB_DownloadCharacters = contractIn.AniDB_DownloadCharacters;
                ServerSettings.AniDB_DownloadCreators = contractIn.AniDB_DownloadCreators;

                // Web Cache
                ServerSettings.WebCache_Address = contractIn.WebCache_Address;
                ServerSettings.WebCache_Anonymous = contractIn.WebCache_Anonymous;
                ServerSettings.WebCache_XRefFileEpisode_Get = contractIn.WebCache_XRefFileEpisode_Get;
                ServerSettings.WebCache_XRefFileEpisode_Send = contractIn.WebCache_XRefFileEpisode_Send;
                ServerSettings.WebCache_TvDB_Get = contractIn.WebCache_TvDB_Get;
                ServerSettings.WebCache_TvDB_Send = contractIn.WebCache_TvDB_Send;
                ServerSettings.WebCache_Trakt_Get = contractIn.WebCache_Trakt_Get;
                ServerSettings.WebCache_Trakt_Send = contractIn.WebCache_Trakt_Send;
                ServerSettings.WebCache_MAL_Get = contractIn.WebCache_MAL_Get;
                ServerSettings.WebCache_MAL_Send = contractIn.WebCache_MAL_Send;
                ServerSettings.WebCache_UserInfo = contractIn.WebCache_UserInfo;

                // TvDB
                ServerSettings.TvDB_AutoFanart = contractIn.TvDB_AutoFanart;
                ServerSettings.TvDB_AutoFanartAmount = contractIn.TvDB_AutoFanartAmount;
                ServerSettings.TvDB_AutoPosters = contractIn.TvDB_AutoPosters;
                ServerSettings.TvDB_AutoPostersAmount = contractIn.TvDB_AutoPostersAmount;
                ServerSettings.TvDB_AutoWideBanners = contractIn.TvDB_AutoWideBanners;
                ServerSettings.TvDB_AutoWideBannersAmount = contractIn.TvDB_AutoWideBannersAmount;
                ServerSettings.TvDB_UpdateFrequency = (ScheduledUpdateFrequency) contractIn.TvDB_UpdateFrequency;
                ServerSettings.TvDB_Language = contractIn.TvDB_Language;

                // MovieDB
                ServerSettings.MovieDB_AutoFanart = contractIn.MovieDB_AutoFanart;
                ServerSettings.MovieDB_AutoFanartAmount = contractIn.MovieDB_AutoFanartAmount;
                ServerSettings.MovieDB_AutoPosters = contractIn.MovieDB_AutoPosters;
                ServerSettings.MovieDB_AutoPostersAmount = contractIn.MovieDB_AutoPostersAmount;

                // Import settings
                ServerSettings.VideoExtensions = contractIn.VideoExtensions;
                ServerSettings.Import_UseExistingFileWatchedStatus = contractIn.Import_UseExistingFileWatchedStatus;
                ServerSettings.AutoGroupSeries = contractIn.AutoGroupSeries;
				ServerSettings.AutoGroupSeriesUseScoreAlgorithm = contractIn.AutoGroupSeriesUseScoreAlgorithm;
				ServerSettings.AutoGroupSeriesRelationExclusions = contractIn.AutoGroupSeriesRelationExclusions;
                ServerSettings.RunImportOnStart = contractIn.RunImportOnStart;
                ServerSettings.ScanDropFoldersOnStart = contractIn.ScanDropFoldersOnStart;
                ServerSettings.Hash_CRC32 = contractIn.Hash_CRC32;
                ServerSettings.Hash_MD5 = contractIn.Hash_MD5;
                ServerSettings.Hash_SHA1 = contractIn.Hash_SHA1;

                // Language
                ServerSettings.LanguagePreference = contractIn.LanguagePreference;
                ServerSettings.LanguageUseSynonyms = contractIn.LanguageUseSynonyms;
                ServerSettings.EpisodeTitleSource = (DataSourceType) contractIn.EpisodeTitleSource;
                ServerSettings.SeriesDescriptionSource = (DataSourceType) contractIn.SeriesDescriptionSource;
                ServerSettings.SeriesNameSource = (DataSourceType) contractIn.SeriesNameSource;

                // Trakt
                ServerSettings.Trakt_IsEnabled = contractIn.Trakt_IsEnabled;
                ServerSettings.Trakt_AuthToken = contractIn.Trakt_AuthToken;
                ServerSettings.Trakt_RefreshToken = contractIn.Trakt_RefreshToken;
                ServerSettings.Trakt_TokenExpirationDate = contractIn.Trakt_TokenExpirationDate;
                ServerSettings.Trakt_UpdateFrequency = (ScheduledUpdateFrequency) contractIn.Trakt_UpdateFrequency;
                ServerSettings.Trakt_SyncFrequency = (ScheduledUpdateFrequency) contractIn.Trakt_SyncFrequency;
                ServerSettings.Trakt_DownloadEpisodes = contractIn.Trakt_DownloadEpisodes;
                ServerSettings.Trakt_DownloadFanart = contractIn.Trakt_DownloadFanart;
                ServerSettings.Trakt_DownloadPosters = contractIn.Trakt_DownloadPosters;

                // MAL
                ServerSettings.MAL_Username = contractIn.MAL_Username;
                ServerSettings.MAL_Password = contractIn.MAL_Password;
                ServerSettings.MAL_UpdateFrequency = (ScheduledUpdateFrequency) contractIn.MAL_UpdateFrequency;
                ServerSettings.MAL_NeverDecreaseWatchedNums = contractIn.MAL_NeverDecreaseWatchedNums;

                if (anidbSettingsChanged)
                {
                    ShokoService.AnidbProcessor.ForceLogout();
                    ShokoService.AnidbProcessor.CloseConnections();

                    Thread.Sleep(1000);
                    ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                        ServerSettings.AniDB_ServerAddress,
                        ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);
                }
            }
            catch (Exception ex)
            {
                contract.ErrorMessage = ex.Message;
                logger.Error( ex,ex.ToString());
            }
            return contract;
        }

        public CL_ServerSettings GetServerSettings()
        {
            CL_ServerSettings contract = new CL_ServerSettings();

            try
            {
                return ServerSettings.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contract;
        }


        public string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video local record";
                vid.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, true, true, userID, true, true);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            CL_Response<CL_AnimeEpisode_User> response =
                new CL_Response<CL_AnimeEpisode_User>();
            response.ErrorMessage = "";
            response.Result = null;

            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null)
                {
                    response.ErrorMessage = "Could not find anime episode record";
                    return response;
                }

                ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, false, userID, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

                // refresh from db


                response.Result = ep.GetUserContract(userID);

                return response;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        /// <summary>
        /// Set watched status on all normal episodes
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="watchedStatus"></param>
        /// <param name="maxEpisodeNumber">Use this to specify a max episode number to apply to</param>
        /// <returns></returns>
        public string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber,
            int episodeType,
            int userID)
        {
            try
            {
                List<SVR_AnimeEpisode> eps = RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID);

                SVR_AnimeSeries ser = null;
                foreach (SVR_AnimeEpisode ep in eps)
                {
                    if (ep.EpisodeTypeEnum == (enEpisodeType) episodeType &&
                        ep.AniDB_Episode.EpisodeNumber <= maxEpisodeNumber)
                    {
                        // check if this episode is already watched
                        bool currentStatus = false;
                        AnimeEpisode_User epUser = ep.GetUserRecord(userID);
                        if (epUser != null)
                            currentStatus = epUser.WatchedCount > 0 ? true : false;

                        if (currentStatus != watchedStatus)
                        {
                            logger.Info("Updating episode: {0} to {1}", ep.AniDB_Episode.EpisodeNumber, watchedStatus);
                            ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, false, userID, false);
                        }
                    }


                    ser = ep.GetAnimeSeries();
                }

                // now update the stats
                if (ser != null)
                {
                    ser.UpdateStats(true, true, true);
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                }
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                anime.DisableExternalLinksFlag = flags;
                RepoFactory.AniDB_Anime.Save(anime);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public CL_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return null;

                return vid.ToClientDetailed(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeEpisode_User> GetEpisodesForFile(int videoLocalID, int userID)
        {
            List<CL_AnimeEpisode_User> contracts = new List<CL_AnimeEpisode_User>();
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return contracts;

                foreach (SVR_AnimeEpisode ep in vid.GetAnimeEpisodes())
                {
                    CL_AnimeEpisode_User eps = ep.GetUserContract(userID);
                    if (eps != null)
                        contracts.Add(eps);
                }

                return contracts;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return contracts;
            }
        }

        /// <summary>
        /// Get all the release groups for an episode for which the user is collecting
        /// </summary>
        /// <param name="aniDBEpisodeID"></param>
        /// <returns></returns>
        public List<CL_AniDB_GroupStatus> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID)
        {
            DateTime start = DateTime.Now;

            List<CL_AniDB_GroupStatus> relGroups = new List<CL_AniDB_GroupStatus>();

            try
            {
                AniDB_Episode aniEp = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);
                if (aniEp == null) return relGroups;
                if (aniEp.GetEpisodeTypeEnum() != enEpisodeType.Episode) return relGroups;

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(aniEp.AnimeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
                foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    List<SVR_VideoLocal> vids = ep.GetVideoLocals();
                    List<string> hashes = vids.Select(a => a.Hash).Distinct().ToList();
                    foreach (string s in hashes)
                    {
                        SVR_VideoLocal vid = vids.First(a => a.Hash == s);
                        AniDB_File anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                                userReleaseGroups[anifile.GroupID] = 0;

                            userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                        }
                    }
                }

                // get all the release groups for this series
                List<AniDB_GroupStatus> grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(aniEp.AnimeID);
                foreach (AniDB_GroupStatus gs in grpStatuses)
                {
                    if (userReleaseGroups.ContainsKey(gs.GroupID))
                    {
                        if (gs.HasGroupReleasedEpisode(aniEp.EpisodeNumber))
                        {
                            CL_AniDB_GroupStatus cl = gs.ToClient();
                            cl.UserCollecting = true;
                            cl.FileCount = userReleaseGroups[gs.GroupID];
                            relGroups.Add(cl);
                        }
                    }
                }
                TimeSpan ts = DateTime.Now - start;
                logger.Info("GetMyReleaseGroupsForAniDBEpisode  in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return relGroups;
        }

        public List<ImportFolder> GetImportFolders()
        {
            try
            {
                return RepoFactory.ImportFolder.GetAll().Cast<ImportFolder>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<ImportFolder>();
        }

        public CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract)
        {
            CL_Response<ImportFolder> response = new CL_Response<ImportFolder>();
            response.ErrorMessage = "";
            response.Result = null;

            try
            {
                SVR_ImportFolder ns = null;
                if (contract.ImportFolderID != 0)
                {
                    // update
                    ns = RepoFactory.ImportFolder.GetByID(contract.ImportFolderID);
                    if (ns == null)
                    {
                        response.ErrorMessage = "Could not find Import Folder ID: " +
                                                contract.ImportFolderID.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    ns = new SVR_ImportFolder();
                }

                if (string.IsNullOrEmpty(contract.ImportFolderName))
                {
                    response.ErrorMessage = "Must specify an Import Folder name";
                    return response;
                }

                if (string.IsNullOrEmpty(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Must specify an Import Folder location";
                    return response;
                }

                if (contract.CloudID==null && !Directory.Exists(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Cannot find Import Folder location";
                    return response;
                }

                if (contract.ImportFolderID==0)
                {
                    SVR_ImportFolder nsTemp = RepoFactory.ImportFolder.GetByImportLocation(contract.ImportFolderLocation);
                    if (nsTemp != null)
                    {
                        response.ErrorMessage = "An entry already exists for the specified Import Folder location";
                        return response;
                    }
                }

                if (contract.IsDropDestination == 1 && contract.IsDropSource == 1)
                {
                    response.ErrorMessage = "A folder cannot be a drop source and a drop destination at the same time";
                    return response;
                }

                // check to make sure we don't have multiple drop folders
                IReadOnlyList<SVR_ImportFolder> allFolders = RepoFactory.ImportFolder.GetAll();

                if (contract.IsDropDestination == 1)
                {
                    foreach (SVR_ImportFolder imf in allFolders)
                    {
                        if (contract.CloudID==imf.CloudID && imf.IsDropDestination == 1 && (contract.ImportFolderID==0 || (contract.ImportFolderID != imf.ImportFolderID)))
                        {
                            imf.IsDropDestination = 0;
                            RepoFactory.ImportFolder.Save(imf);
                        }
                    }
                }

                ns.ImportFolderName = contract.ImportFolderName;
                ns.ImportFolderLocation = contract.ImportFolderLocation;
                ns.IsDropDestination = contract.IsDropDestination;
                ns.IsDropSource = contract.IsDropSource;
                ns.IsWatched = contract.IsWatched;
                ns.ImportFolderType = contract.ImportFolderType;
                ns.CloudID = contract.CloudID.HasValue && contract.CloudID == 0 ? null : contract.CloudID; ;
                RepoFactory.ImportFolder.Save(ns);

                response.Result = ns;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ServerInfo.Instance.RefreshImportFolders();
                });
                MainWindow.StopWatchingFiles();
                MainWindow.StartWatchingFiles();

                return response;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string DeleteImportFolder(int importFolderID)
        {
            MainWindow.DeleteImportFolder(importFolderID);
            return "";
        }

        public void RunImport()
        {
            MainWindow.RunImport();
        }
        public void SyncHashes()
        {
            MainWindow.SyncHashes();
        }
        public void ScanDropFolders()
        {
            Importer.RunImport_DropFolders();
        }

        public void ScanFolder(int importFolderID)
        {
            MainWindow.ScanFolder(importFolderID);
        }

        public void RemoveMissingFiles()
        {
            MainWindow.RemoveMissingFiles();
        }

        public void SyncMyList()
        {
            MainWindow.SyncMyList();
        }

        public void SyncVotes()
        {
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
        }

        public void SyncMALUpload()
        {
            CommandRequest_MALUploadStatusToMAL cmd = new CommandRequest_MALUploadStatusToMAL();
            cmd.Save();
        }

        public void SyncMALDownload()
        {
            CommandRequest_MALDownloadStatusFromMAL cmd = new CommandRequest_MALDownloadStatusFromMAL();
            cmd.Save();
        }

        public void RescanUnlinkedFiles()
        {
            try
            {
                // files which have been hashed, but don't have an associated episode
                List<SVR_VideoLocal> filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

                foreach (SVR_VideoLocal vl in filesWithoutEpisode.Where(a=>!string.IsNullOrEmpty(a.Hash)))
                {
                    CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.Message);
            }
        }

        public void RescanManuallyLinkedFiles()
        {
            try
            {
                // files which have been hashed, but don't have an associated episode
                List<SVR_VideoLocal> files = RepoFactory.VideoLocal.GetManuallyLinkedVideos();

                foreach (SVR_VideoLocal vl in files.Where(a=>!string.IsNullOrEmpty(a.Hash)))
                {
                    CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.Message);
            }
        }


        public void SetCommandProcessorHasherPaused(bool paused)
        {
            ShokoService.CmdProcessorHasher.Paused = paused;
        }

        public void SetCommandProcessorGeneralPaused(bool paused)
        {
            ShokoService.CmdProcessorGeneral.Paused = paused;
        }

        public void SetCommandProcessorImagesPaused(bool paused)
        {
            ShokoService.CmdProcessorImages.Paused = paused;
        }

        public void ClearHasherQueue()
        {
            try
            {
                ShokoService.CmdProcessorHasher.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestHasher());

                ShokoService.CmdProcessorHasher.Init();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void ClearImagesQueue()
        {
            try
            {
                ShokoService.CmdProcessorImages.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestImages());
                ShokoService.CmdProcessorImages.Init();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void ClearGeneralQueue()
        {
            try
            {
                ShokoService.CmdProcessorGeneral.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestGeneral());
                ShokoService.CmdProcessorGeneral.Init();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void RehashFile(int videoLocalID)
        {
            SVR_VideoLocal vl = RepoFactory.VideoLocal.GetByID(videoLocalID);

            if (vl != null)
            {
                SVR_VideoLocal_Place pl = vl.GetBestVideoLocalPlace();
                if (pl == null)
                {
                    logger.Error("Unable to hash videolocal with id = {videoLocalID}, it has no assigned place");
                    return;
                }
                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(pl.FullServerPath, true);
                cr_hashfile.Save();
            }
        }

        public string TestAniDBConnection()
        {
            string log = "";
            try
            {
                log += "Disposing..." + Environment.NewLine;
                ShokoService.AnidbProcessor.ForceLogout();
                ShokoService.AnidbProcessor.CloseConnections();
                Thread.Sleep(1000);

                log += "Init..." + Environment.NewLine;
                ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                    ServerSettings.AniDB_ServerAddress,
                    ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

                log += "Login..." + Environment.NewLine;
                if (ShokoService.AnidbProcessor.Login())
                {
                    log += "Login Success!" + Environment.NewLine;
                    log += "Logout..." + Environment.NewLine;
                    ShokoService.AnidbProcessor.ForceLogout();
                    log += "Logged out" + Environment.NewLine;
                }
                else
                {
                    log += "Login FAILED!" + Environment.NewLine;
                }

                return log;
            }
            catch (Exception ex)
            {
                log += ex.Message + Environment.NewLine;
            }

            return log;
        }

        public string EnterTraktPIN(string pin)
        {
            try
            {
                return TraktTVHelper.EnterTraktPIN(pin);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in EnterTraktPIN: " + ex.ToString());
                return ex.Message;
            }
        }

        public string TestMALLogin()
        {
            try
            {
                if (MALHelper.VerifyCredentials())
                    return "";

                return "Login is not valid";
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TestMALLogin: " + ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<bool> TraktFriendRequestDeny(string friendUsername)
        {
            return new CL_Response<bool> {Result = false};
            /*
			try
			{
				return TraktTVHelper.FriendRequestDeny(friendUsername, ref returnMessage);
			}
			catch (Exception ex)
			{
				logger.Error( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
				returnMessage = ex.Message;
				return false;
			}*/
        }

        public CL_Response<bool> TraktFriendRequestApprove(string friendUsername)
        {
            return new CL_Response<bool> { Result = false };
            /*
			try
			{
				return TraktTVHelper.FriendRequestApprove(friendUsername, ref returnMessage);
			}
			catch (Exception ex)
			{
				logger.Error( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
				returnMessage = ex.Message;
				return false;
			}*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="voteValue">Must be 1 or 2 (Anime or Anime Temp(</param>
        /// <param name="voteType"></param>
        public void VoteAnime(int animeID, decimal voteValue, int voteType)
        {
            //TODO missing userID ?! - brm
            string msg = string.Format("Voting for anime: {0} - Value: {1}", animeID, voteValue);
            logger.Info(msg);

            // lets save to the database and assume it will work
            List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(animeID);
            AniDB_Vote thisVote = null;
            foreach (AniDB_Vote dbVote in dbVotes)
            {
                // we can only have anime permanent or anime temp but not both
                if (voteType == (int) enAniDBVoteType.Anime || voteType == (int) enAniDBVoteType.AnimeTemp)
                {
                    if (dbVote.VoteType == (int) enAniDBVoteType.Anime ||
                        dbVote.VoteType == (int) enAniDBVoteType.AnimeTemp)
                    {
                        thisVote = dbVote;
                    }
                }
                else
                {
                    thisVote = dbVote;
                }
            }

            if (thisVote == null)
            {
                thisVote = new AniDB_Vote();
                thisVote.EntityID = animeID;
            }
            thisVote.VoteType = voteType;

            int iVoteValue = 0;
            if (voteValue > 0)
                iVoteValue = (int) (voteValue*100);
            else
                iVoteValue = (int) voteValue;

            msg = string.Format("Voting for anime Formatted: {0} - Value: {1}", animeID, iVoteValue);
            logger.Info(msg);

            thisVote.VoteValue = iVoteValue;
            RepoFactory.AniDB_Vote.Save(thisVote);

            CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, voteType, voteValue);
            cmdVote.Save();
        }

        public void VoteAnimeRevoke(int animeID)
        {
            // lets save to the database and assume it will work
     
            List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(animeID);
            AniDB_Vote thisVote = null;
            foreach (AniDB_Vote dbVote in dbVotes)
            {
                // we can only have anime permanent or anime temp but not both
                if (dbVote.VoteType == (int) enAniDBVoteType.Anime || dbVote.VoteType == (int) enAniDBVoteType.AnimeTemp)
                {
                    thisVote = dbVote;
                }
            }

            if (thisVote == null) return;

            CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, thisVote.VoteType, -1);
            cmdVote.Save();

            RepoFactory.AniDB_Vote.Delete(thisVote.AniDB_VoteID);
        }

        public string RenameAllGroups()
        {
            try
            {
                SVR_AnimeGroup.RenameAllGroups();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }

            return string.Empty;
        }

        public List<string> GetAllUniqueVideoQuality()
        {
            try
            {
                return RepoFactory.Adhoc.GetAllVideoQuality();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<string>();
            }
        }

        public List<string> GetAllUniqueAudioLanguages()
        {
            try
            {
                return RepoFactory.Adhoc.GetAllUniqueAudioLanguages();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<string>();
            }
        }

        public List<string> GetAllUniqueSubtitleLanguages()
        {
            try
            {
                return RepoFactory.Adhoc.GetAllUniqueSubtitleLanguages();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<string>();
            }
        }

        public List<CL_DuplicateFile> GetAllDuplicateFiles()
        {
            List<CL_DuplicateFile> dupFiles = new List<CL_DuplicateFile>();
            try
            {
                return RepoFactory.DuplicateFile.GetAll().Select(a=>ModelClients.ToClient(a)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return dupFiles;
            }
        }

        /// <summary>
        /// Delete a duplicate file entry, and also one of the physical files
        /// </summary>
        /// <param name="duplicateFileID"></param>
        /// <param name="fileNumber">0 = Don't delete any physical files, 1 = Delete file 1, 2 = Deleet file 2</param>
        /// <returns></returns>
        public string DeleteDuplicateFile(int duplicateFileID, int fileNumber)
        {
            try
            {
                DuplicateFile df = RepoFactory.DuplicateFile.GetByID(duplicateFileID);
                if (df == null) return "Database entry does not exist";

                if (fileNumber == 1 || fileNumber == 2)
                {
                    string fileName = "";
                    if (fileNumber == 1) fileName = df.GetFullServerPath1();
                    if (fileNumber == 2) fileName = df.GetFullServerPath2();
                    IFile file = SVR_VideoLocal.ResolveFile(fileName);
                    file?.Delete(false);
                }

                RepoFactory.DuplicateFile.Delete(duplicateFileID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        /// <summary>
        /// Delets the VideoLocal record and the associated physical file
        /// </summary>
        /// <param name="videoLocalID"></param>
        /// <returns></returns>
        public string DeleteVideoLocalPlaceAndFile(int videolocalplaceid)
        {
            try
            {
                SVR_VideoLocal_Place place = RepoFactory.VideoLocalPlace.GetByID(videolocalplaceid);
                if ((place==null) || (place.VideoLocal==null))
                    return "Database entry does not exist";
                SVR_VideoLocal vid = place.VideoLocal;
                logger.Info("Deleting video local place record and file: {0}", place.FullServerPath);

                IFileSystem fileSystem = place.ImportFolder?.FileSystem;
                if (fileSystem == null)
                {
                    logger.Error("Unable to delete file, filesystem not found. Removing record.");
					place.RemoveRecord();
                    return "Unable to delete file, filesystem not found. Removing record.";
                }
                FileSystemResult<IObject> fr = fileSystem.Resolve(place.FullServerPath);
                if (fr == null || !fr.IsOk)
                {
                    logger.Error($"Unable to find file. Removing Record: {place.FullServerPath}");
	                place.RemoveRecord();
	                return $"Unable to find file. Removing record.";
                }
                IFile file = fr.Result as IFile;
                if (file == null)
                {
                    logger.Error($"Seems '{place.FullServerPath}' is a directory.");
	                place.RemoveRecord();
	                return $"Seems '{place.FullServerPath}' is a directory.";

                }
                FileSystemResult fs = file.Delete(false);
                if (fs == null || !fs.IsOk)
                {
                    logger.Error($"Unable to delete file '{place.FullServerPath}'");
                    return $"Unable to delete file '{place.FullServerPath}'";
                }
                place.RemoveRecord();
                // For deletion of files from Trakt, we will rely on the Daily sync

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public List<CL_VideoLocal> GetAllManuallyLinkedFiles(int userID)
        {
            List<CL_VideoLocal> manualFiles = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
                {
                    manualFiles.Add(vid.ToClient(userID));
                }

                return manualFiles;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return manualFiles;
            }
        }

        public List<CL_AnimeEpisode_User> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries,
            bool ignoreVariations)
        {
            List<CL_AnimeEpisode_User> eps = new List<CL_AnimeEpisode_User>();
            try
            {

                Dictionary<int, int> dictSeriesAnime = new Dictionary<int, int>();
                Dictionary<int, bool> dictAnimeFinishedAiring = new Dictionary<int, bool>();
                Dictionary<int, bool> dictSeriesFinishedAiring = new Dictionary<int, bool>();

                if (onlyFinishedSeries)
                {
                    IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                    foreach (SVR_AnimeSeries ser in allSeries)
                        dictSeriesAnime[ser.AnimeSeriesID] = ser.AniDB_ID;

                    IReadOnlyList<SVR_AniDB_Anime> allAnime = RepoFactory.AniDB_Anime.GetAll();
                    foreach (SVR_AniDB_Anime anime in allAnime)
                        dictAnimeFinishedAiring[anime.AnimeID] = anime.GetFinishedAiring();

                    foreach (KeyValuePair<int, int> kvp in dictSeriesAnime)
                    {
                        if (dictAnimeFinishedAiring.ContainsKey(kvp.Value))
                            dictSeriesFinishedAiring[kvp.Key] = dictAnimeFinishedAiring[kvp.Value];
                    }
                }

                foreach (SVR_AnimeEpisode ep in RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(ignoreVariations))
                {
                    if (onlyFinishedSeries)
                    {
                        bool finishedAiring = false;
                        if (dictSeriesFinishedAiring.ContainsKey(ep.AnimeSeriesID))
                            finishedAiring = dictSeriesFinishedAiring[ep.AnimeSeriesID];

                        if (!finishedAiring) continue;
                    }
                    CL_AnimeEpisode_User cep = ep.GetUserContract(userID);
                    if (cep != null)
                        eps.Add(cep);
                }

                return eps;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return eps;
            }
        }

        public void ReevaluateDuplicateFiles()
        {
            try
            {
                foreach (DuplicateFile df in RepoFactory.DuplicateFile.GetAll())
                {
                    if (df.GetImportFolder1() == null || df.GetImportFolder2() == null)
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as one of the import folders can't be found: {0} --- {1}",
                                df.FilePathFile1, df.FilePathFile2);
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                        continue;
                    }

                    // make sure that they are not actually the same file
                    if (df.GetFullServerPath1().Equals(df.GetFullServerPath2(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as they are actually point to the same file: {0}",
                                df.GetFullServerPath1());
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                    }

                    // check if both files still exist
                    IFile file1 = SVR_VideoLocal.ResolveFile(df.GetFullServerPath1());
                    IFile file2 = SVR_VideoLocal.ResolveFile(df.GetFullServerPath2());
                    if (file1==null || file2==null)
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as one of the files can't be found: {0} --- {1}",
                                df.GetFullServerPath1(), df.GetFullServerPath2());
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
            string resolution,
            string videoSource, int videoBitDepth, int userID)
        {
            List<CL_VideoDetailed> vids = new List<CL_VideoDetailed>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {
                    int thisBitDepth = 8;

                    int bitDepth = 0;
                    if (int.TryParse(vid.VideoBitDepth, out bitDepth))
                        thisBitDepth = bitDepth;

                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    SVR_AnimeEpisode animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == Shoko.Models.Enums.enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == Shoko.Models.Enums.enEpisodeType.Special)
                    {
                        // get the anibd file info
                        AniDB_File aniFile = vid.GetAniDBFile();
                        if (aniFile != null)
                        {
                            videoSource = SimplifyVideoSource(videoSource);
                            string fileSource = SimplifyVideoSource(aniFile.File_Source);
                            string vidResAniFile = Utils.GetStandardisedVideoResolution(aniFile.File_VideoResolution);

                            // match based on group / video sorce / video res
                            if (
                                relGroupName.Equals(aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase) &&
                                videoSource.Equals(fileSource, StringComparison.InvariantCultureIgnoreCase) &&
                                resolution.Equals(vidResAniFile, StringComparison.InvariantCultureIgnoreCase) &&
                                thisBitDepth == videoBitDepth)
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                        else
                        {
                            string vidResInfo = Utils.GetStandardisedVideoResolution(vid.VideoResolution);

                            // match based on group / video sorce / video res
                            if (
                                relGroupName.Equals(Constants.NO_GROUP_INFO, StringComparison.InvariantCultureIgnoreCase) &&
                                videoSource.Equals(Constants.NO_SOURCE_INFO, StringComparison.InvariantCultureIgnoreCase) &&
                                resolution.Equals(vidResInfo, StringComparison.InvariantCultureIgnoreCase) &&
                                thisBitDepth == videoBitDepth)
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                    }
                }
                return vids;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return vids;
            }
        }

        public List<CL_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID)
        {
            List<CL_VideoDetailed> vids = new List<CL_VideoDetailed>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {

                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    SVR_AnimeEpisode animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                    {
                        // get the anibd file info
                        AniDB_File aniFile = vid.GetAniDBFile();
                        if (aniFile != null)
                        {
                            // match based on group / video sorce / video res
                            if (relGroupName.Equals(aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                        else
                        {
                            if (relGroupName.Equals(Constants.NO_GROUP_INFO, StringComparison.InvariantCultureIgnoreCase))
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                    }
                }
                return vids;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return vids;
            }
        }

        /// <summary>
        /// www is usually not used correctly
        /// </summary>
        /// <param name="origSource"></param>
        /// <returns></returns>
        private string SimplifyVideoSource(string origSource)
        {
            //return origSource;

            if (origSource.Equals("DTV", StringComparison.InvariantCultureIgnoreCase) ||
                origSource.Equals("HDTV", StringComparison.InvariantCultureIgnoreCase) ||
                origSource.Equals("www", StringComparison.InvariantCultureIgnoreCase))
            {
                return "TV";
            }
            else
                return origSource;
        }

        public List<CL_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID)
        {
            List<CL_GroupVideoQuality> vidQuals = new List<CL_GroupVideoQuality>();



            try
            {
                DateTime start = DateTime.Now;
                TimeSpan ts = DateTime.Now - start;

                double totalTiming = 0;
                double timingAnime = 0;
                double timingVids = 0;
                double timingEps = 0;
                double timingAniEps = 0;
                double timingAniFile = 0;
                double timingVidInfo = 0;
                double timingContracts = 0;

                DateTime oStart = DateTime.Now;

                start = DateTime.Now;
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                ts = DateTime.Now - start;
                timingAnime += ts.TotalMilliseconds;

                if (anime == null) return vidQuals;

                start = DateTime.Now;
                ts = DateTime.Now - start;
                timingVids += ts.TotalMilliseconds;


                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {

                    start = DateTime.Now;
                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    ts = DateTime.Now - start;
                    timingEps += ts.TotalMilliseconds;

                    if (eps.Count == 0) continue;
                    foreach (SVR_AnimeEpisode animeEp in eps)
                    {
                        //AnimeEpisode animeEp = eps[0];
                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                            animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                        {
                            start = DateTime.Now;
                            AniDB_Episode anidbEp = animeEp.AniDB_Episode;
                            ts = DateTime.Now - start;
                            timingAniEps += ts.TotalMilliseconds;

                            // get the anibd file info
                            start = DateTime.Now;
                            AniDB_File aniFile = vid.GetAniDBFile();
                            ts = DateTime.Now - start;
                            timingAniFile += ts.TotalMilliseconds;
                            if (aniFile != null)
                            {
                                start = DateTime.Now;
                                ts = DateTime.Now - start;
                                timingVidInfo += ts.TotalMilliseconds;
                                int bitDepth = 8;
                                if (!int.TryParse(vid.VideoBitDepth, out bitDepth))
                                    bitDepth = 8;

                                string vidResAniFile = Utils.GetStandardisedVideoResolution(aniFile.File_VideoResolution);

                                // match based on group / video sorce / video res
                                bool foundSummaryRecord = false;
                                foreach (CL_GroupVideoQuality contract in vidQuals)
                                {
                                    string contractSource = SimplifyVideoSource(contract.VideoSource);
                                    string fileSource = SimplifyVideoSource(aniFile.File_Source);

                                    string vidResContract = Utils.GetStandardisedVideoResolution(contract.Resolution);


                                    if (
                                        contract.GroupName.Equals(aniFile.Anime_GroupName,
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        contractSource.Equals(fileSource, StringComparison.InvariantCultureIgnoreCase) &&
                                        vidResContract.Equals(vidResAniFile, StringComparison.InvariantCultureIgnoreCase) &&
                                        contract.VideoBitDepth == bitDepth)
                                    {
                                        foundSummaryRecord = true;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += vid.FileSize;
                                        contract.TotalRunningTime += aniFile.File_LengthSeconds;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        {
                                            if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                        }
                                    }
                                }
                                if (!foundSummaryRecord)
                                {
                                    CL_GroupVideoQuality contract = new CL_GroupVideoQuality();
                                    contract.FileCountNormal = 0;
                                    contract.FileCountSpecials = 0;
                                    contract.TotalFileSize = 0;
                                    contract.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) contract.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) contract.FileCountSpecials++;
                                    contract.TotalFileSize += vid.FileSize;
                                    contract.TotalRunningTime += aniFile.File_LengthSeconds;

                                    contract.GroupName = aniFile.Anime_GroupName;
                                    contract.GroupNameShort = aniFile.Anime_GroupNameShort;
                                    contract.VideoBitDepth = bitDepth;
                                    contract.Resolution = vidResAniFile;
                                    contract.VideoSource = SimplifyVideoSource(aniFile.File_Source);
                                    contract.Ranking = Utils.GetOverallVideoSourceRanking(contract.Resolution,
                                        contract.VideoSource, bitDepth);
                                    contract.NormalEpisodeNumbers = new List<int>();
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                    {
                                        if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                            contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                    }

                                    vidQuals.Add(contract);
                                }
                            }
                            else
                            {
                                // look at the Video Info record
                                int bitDepth = 8;
                                if (!int.TryParse(vid.VideoBitDepth, out bitDepth))
                                    bitDepth = 8;

                                string vidResInfo = Utils.GetStandardisedVideoResolution(vid.VideoResolution);

                                bool foundSummaryRecord = false;
                                foreach (CL_GroupVideoQuality contract in vidQuals)
                                {
                                    string vidResContract = Utils.GetStandardisedVideoResolution(contract.Resolution);


                                    if (
                                        contract.GroupName.Equals(Constants.NO_GROUP_INFO,
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        contract.VideoSource.Equals(Constants.NO_SOURCE_INFO,
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        vidResContract.Equals(vidResInfo,
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        contract.VideoBitDepth == bitDepth)
                                    {
                                        foundSummaryRecord = true;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += vid.FileSize;
                                        contract.TotalRunningTime += vid.Duration;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        {
                                            if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                        }
                                    }
                                }
                                if (!foundSummaryRecord)
                                {
                                    CL_GroupVideoQuality contract = new CL_GroupVideoQuality();
                                    contract.FileCountNormal = 0;
                                    contract.FileCountSpecials = 0;
                                    contract.TotalFileSize = 0;
                                    contract.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        contract.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                        contract.FileCountSpecials++;
                                    contract.TotalFileSize += vid.FileSize;
                                    contract.TotalRunningTime += vid.Duration;

                                    contract.GroupName = Constants.NO_GROUP_INFO;
                                    contract.GroupNameShort = Constants.NO_GROUP_INFO;
                                    contract.Resolution = vidResInfo;
                                    contract.VideoSource = Constants.NO_SOURCE_INFO;
                                    contract.VideoBitDepth = bitDepth;
                                    contract.Ranking = Utils.GetOverallVideoSourceRanking(contract.Resolution,
                                        contract.VideoSource, bitDepth);
                                    contract.NormalEpisodeNumbers = new List<int>();
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                    {
                                        if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                            contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                    }
                                    vidQuals.Add(contract);
                                }
                            }
                        }
                    }
                }

                start = DateTime.Now;
                foreach (CL_GroupVideoQuality contract in vidQuals)
                {
                    contract.NormalComplete = contract.FileCountNormal >= anime.EpisodeCountNormal;
                    contract.SpecialsComplete = (contract.FileCountSpecials >= anime.EpisodeCountSpecial) &&
                                                (anime.EpisodeCountSpecial > 0);

                    contract.NormalEpisodeNumberSummary = "";
                    contract.NormalEpisodeNumbers.Sort();
                    int lastEpNum = 0;
                    int baseEpNum = 0;
                    foreach (int epNum in contract.NormalEpisodeNumbers)
                    {
                        if (baseEpNum == 0)
                        {
                            baseEpNum = epNum;
                            lastEpNum = epNum;
                        }

                        if (epNum == lastEpNum) continue;

                        int epNumDiff = epNum - lastEpNum;
                        if (epNumDiff == 1)
                        {
                            lastEpNum = epNum;
                            continue;
                        }


                        // this means we have missed an episode
                        if (contract.NormalEpisodeNumberSummary.Length > 0)
                            contract.NormalEpisodeNumberSummary += ", ";

                        if (baseEpNum == lastEpNum)
                            contract.NormalEpisodeNumberSummary += string.Format("{0}", baseEpNum);
                        else
                            contract.NormalEpisodeNumberSummary += string.Format("{0}-{1}", baseEpNum, lastEpNum);

                        lastEpNum = epNum;
                        baseEpNum = epNum;
                    }

                    if (contract.NormalEpisodeNumbers.Count > 0)
                    {
                        if (contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1] >= baseEpNum)
                        {
                            // this means we have missed an episode
                            if (contract.NormalEpisodeNumberSummary.Length > 0)
                                contract.NormalEpisodeNumberSummary += ", ";

                            if (baseEpNum == contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1])
                                contract.NormalEpisodeNumberSummary += string.Format("{0}", baseEpNum);
                            else
                                contract.NormalEpisodeNumberSummary += string.Format("{0}-{1}", baseEpNum,
                                    contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1]);
                        }
                    }
                }
                ts = DateTime.Now - start;
                timingContracts += ts.TotalMilliseconds;

                ts = DateTime.Now - oStart;
                totalTiming = ts.TotalMilliseconds;

                string msg2 =
                    string.Format("Timing for video quality {0} ({1}) : {2}/{3}/{4}/{5}/{6}/{7}/{8}  (AID: {9})",
                        anime.MainTitle, totalTiming, timingAnime, timingVids,
                        timingEps, timingAniEps, timingAniFile, timingVidInfo, timingContracts, anime.AnimeID);
                logger.Debug(msg2);

                return vidQuals.OrderByDescending(a=>a.Ranking).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return vidQuals;
            }
        }


        public List<CL_GroupFileSummary> GetGroupFileSummary(int animeID)
        {
            List<CL_GroupFileSummary> vidQuals = new List<CL_GroupFileSummary>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                if (anime == null) return vidQuals;


                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {

                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();

                    if (eps.Count == 0) continue;

                    foreach (SVR_AnimeEpisode animeEp in eps)
                    {
                        //AnimeEpisode animeEp = eps[0];
                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                            animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                        {
                            AniDB_Episode anidbEp = animeEp.AniDB_Episode;

                            // get the anibd file info
                            AniDB_File aniFile = vid.GetAniDBFile();
                            if (aniFile != null)
                            {
                                // match based on group / video sorce / video res
                                bool foundSummaryRecord = false;
                                foreach (CL_GroupFileSummary contract in vidQuals)
                                {
                                    if (contract.GroupName.Equals(aniFile.Anime_GroupName,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        foundSummaryRecord = true;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += aniFile.FileSize;
                                        contract.TotalRunningTime += aniFile.File_LengthSeconds;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        {
                                            if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                        }
                                    }
                                }
                                if (!foundSummaryRecord)
                                {
                                    CL_GroupFileSummary cl = new CL_GroupFileSummary();
                                    cl.FileCountNormal = 0;
                                    cl.FileCountSpecials = 0;
                                    cl.TotalFileSize = 0;
                                    cl.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) cl.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) cl.FileCountSpecials++;
                                    cl.TotalFileSize += aniFile.FileSize;
                                    cl.TotalRunningTime += aniFile.File_LengthSeconds;

                                    cl.GroupName = aniFile.Anime_GroupName;
                                    cl.GroupNameShort = aniFile.Anime_GroupNameShort;
                                    cl.NormalEpisodeNumbers = new List<int>();
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                    {
                                        if (!cl.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                            cl.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                    }

                                    vidQuals.Add(cl);
                                }
                            }
                            else
                            {
                                // look at the Video Info record
                                bool foundSummaryRecord = false;
                                foreach (CL_GroupFileSummary contract in vidQuals)
                                {
                                    if (contract.GroupName.Equals("NO GROUP INFO",
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        foundSummaryRecord = true;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += vid.FileSize;
                                        contract.TotalRunningTime += vid.Duration;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        {
                                            if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                        }
                                    }
                                }
                                if (!foundSummaryRecord)
                                {
                                    CL_GroupFileSummary cl = new CL_GroupFileSummary();
                                    cl.FileCountNormal = 0;
                                    cl.FileCountSpecials = 0;
                                    cl.TotalFileSize = 0;
                                    cl.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        cl.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                        cl.FileCountSpecials++;
                                    cl.TotalFileSize += vid.FileSize;
                                    cl.TotalRunningTime += vid.Duration;

                                    cl.GroupName = "NO GROUP INFO";
                                    cl.GroupNameShort = "NO GROUP INFO";
                                    cl.NormalEpisodeNumbers = new List<int>();
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                    {
                                        if (!cl.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                            cl.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                    }
                                    vidQuals.Add(cl);
                                }
                            }
                        }
                    }
                }

                foreach (CL_GroupFileSummary contract in vidQuals)
                {
                    contract.NormalComplete = contract.FileCountNormal >= anime.EpisodeCountNormal;
                    contract.SpecialsComplete = (contract.FileCountSpecials >= anime.EpisodeCountSpecial) &&
                                                (anime.EpisodeCountSpecial > 0);

                    contract.NormalEpisodeNumberSummary = "";
                    contract.NormalEpisodeNumbers.Sort();
                    int lastEpNum = 0;
                    int baseEpNum = 0;
                    foreach (int epNum in contract.NormalEpisodeNumbers)
                    {
                        if (baseEpNum == 0)
                        {
                            baseEpNum = epNum;
                            lastEpNum = epNum;
                        }

                        if (epNum == lastEpNum) continue;

                        int epNumDiff = epNum - lastEpNum;
                        if (epNumDiff == 1)
                        {
                            lastEpNum = epNum;
                            continue;
                        }


                        // this means we have missed an episode
                        if (contract.NormalEpisodeNumberSummary.Length > 0)
                            contract.NormalEpisodeNumberSummary += ", ";

                        if (baseEpNum == lastEpNum)
                            contract.NormalEpisodeNumberSummary += string.Format("{0}", baseEpNum);
                        else
                            contract.NormalEpisodeNumberSummary += string.Format("{0}-{1}", baseEpNum, lastEpNum);

                        lastEpNum = epNum;
                        baseEpNum = epNum;
                    }

                    if (contract.NormalEpisodeNumbers.Count > 0)
                    {
                        if (contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1] >= baseEpNum)
                        {
                            // this means we have missed an episode
                            if (contract.NormalEpisodeNumberSummary.Length > 0)
                                contract.NormalEpisodeNumberSummary += ", ";

                            if (baseEpNum == contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1])
                                contract.NormalEpisodeNumberSummary += string.Format("{0}", baseEpNum);
                            else
                                contract.NormalEpisodeNumberSummary += string.Format("{0}-{1}", baseEpNum,
                                    contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1]);
                        }
                    }
                }

                return vidQuals.OrderBy(a=>a.GroupNameShort).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return vidQuals;
            }
        }

        public CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
        {
            CL_AniDB_AnimeCrossRefs result = new CL_AniDB_AnimeCrossRefs
            {
                CrossRef_AniDB_TvDB = new List<CrossRef_AniDB_TvDBV2>(),
                TvDBSeries = new List<TvDB_Series>(),
                TvDBEpisodes = new List<TvDB_Episode>(),
                TvDBImageFanarts = new List<TvDB_ImageFanart>(),
                TvDBImagePosters = new List<TvDB_ImagePoster>(),
                TvDBImageWideBanners = new List<TvDB_ImageWideBanner>(),

                CrossRef_AniDB_MovieDB = null,
                MovieDBMovie = null,
                MovieDBFanarts = new List<MovieDB_Fanart>(),
                MovieDBPosters = new List<MovieDB_Poster>(),

                CrossRef_AniDB_MAL = null,

                CrossRef_AniDB_Trakt = new List<CrossRef_AniDB_TraktV2>(),
                TraktShows = new List<CL_Trakt_Show>(),
                TraktImageFanarts = new List<Trakt_ImageFanart>(),
                TraktImagePosters = new List<Trakt_ImagePoster>(),
                AnimeID = animeID
            };

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime == null) return result;


                    


                    // TvDB
                    foreach (CrossRef_AniDB_TvDBV2 xref in anime.GetCrossRefTvDBV2())
                    {
                        result.CrossRef_AniDB_TvDB.Add(xref);

                        TvDB_Series ser = RepoFactory.TvDB_Series.GetByTvDBID(sessionWrapper, xref.TvDBID);
                        if (ser != null)
                            result.TvDBSeries.Add(ser);

                        foreach (TvDB_Episode ep in anime.GetTvDBEpisodes())
                            result.TvDBEpisodes.Add(ep);

                        foreach (TvDB_ImageFanart fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(sessionWrapper, xref.TvDBID))
                            result.TvDBImageFanarts.Add(fanart);

                        foreach (TvDB_ImagePoster poster in RepoFactory.TvDB_ImagePoster.GetBySeriesID(sessionWrapper, xref.TvDBID))
                            result.TvDBImagePosters.Add(poster);

                        foreach (TvDB_ImageWideBanner banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(xref.TvDBID))
                            result.TvDBImageWideBanners.Add(banner);
                    }

                    // Trakt

                     
                    foreach (CrossRef_AniDB_TraktV2 xref in anime.GetCrossRefTraktV2())
                    {
                        result.CrossRef_AniDB_Trakt.Add(xref);

                        Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(session, xref.TraktID);
                        if (show != null)
                        {
                            result.TraktShows.Add(show.ToClient());

                            foreach (Trakt_ImageFanart fanart in RepoFactory.Trakt_ImageFanart.GetByShowID(session, show.Trakt_ShowID))
                                result.TraktImageFanarts.Add(fanart);

                            foreach (Trakt_ImagePoster poster in RepoFactory.Trakt_ImagePoster.GetByShowID(session, show.Trakt_ShowID)
                                )
                                result.TraktImagePosters.Add(poster);
                        }
                    }


                    // MovieDB
                    CrossRef_AniDB_Other xrefMovie = anime.GetCrossRefMovieDB();
                    if (xrefMovie == null)
                        result.CrossRef_AniDB_MovieDB = null;
                    else
                        result.CrossRef_AniDB_MovieDB = xrefMovie;


                        result.MovieDBMovie = anime.GetMovieDBMovie();


                    foreach (MovieDB_Fanart fanart in anime.GetMovieDBFanarts())
                    {
                        if (fanart.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBFanarts.Add(fanart);
                    }

                    foreach (MovieDB_Poster poster in anime.GetMovieDBPosters())
                    {
                        if (poster.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBPosters.Add(poster);
                    }

                    // MAL
                    List<CrossRef_AniDB_MAL> xrefMAL = anime.GetCrossRefMAL();
                    if (xrefMAL == null)
                        result.CrossRef_AniDB_MAL = null;
                    else
                    {
                        result.CrossRef_AniDB_MAL = new List<Shoko.Models.Server.CrossRef_AniDB_MAL>();
                        foreach (CrossRef_AniDB_MAL xrefTemp in xrefMAL)
                            result.CrossRef_AniDB_MAL.Add(xrefTemp);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return result;
            }
        }

        public string EnableDisableImage(bool enabled, int imageID, int imageType)
        {
            try
            {
                JMMImageType imgType = (JMMImageType) imageType;

                switch (imgType)
                {
                    case JMMImageType.AniDB_Cover:

                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageID);
                        if (anime == null) return "Could not find anime";

                        anime.ImageEnabled = enabled ? 1 : 0;
                        RepoFactory.AniDB_Anime.Save(anime);

                        break;

                    case JMMImageType.TvDB_Banner:

                        TvDB_ImageWideBanner banner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);

                        if (banner == null) return "Could not find image";

                        banner.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImageWideBanner.Save(banner);

                        break;

                    case JMMImageType.TvDB_Cover:

                        TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);

                        if (poster == null) return "Could not find image";

                        poster.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImagePoster.Save(poster);

                        break;

                    case JMMImageType.TvDB_FanArt:

                        TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);

                        if (fanart == null) return "Could not find image";

                        fanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.TvDB_ImageFanart.Save(fanart);

                        break;

                    case JMMImageType.MovieDB_Poster:

                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(imageID);

                        if (moviePoster == null) return "Could not find image";

                        moviePoster.Enabled = enabled ? 1 : 0;
                        RepoFactory.MovieDB_Poster.Save(moviePoster);

                        break;

                    case JMMImageType.MovieDB_FanArt:

                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(imageID);

                        if (movieFanart == null) return "Could not find image";

                        movieFanart.Enabled = enabled ? 1 : 0;
                        RepoFactory.MovieDB_Fanart.Save(movieFanart);

                        break;

                    case JMMImageType.Trakt_Poster:

                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(imageID);

                        if (traktPoster == null) return "Could not find image";

                        traktPoster.Enabled = enabled ? 1 : 0;
                        RepoFactory.Trakt_ImagePoster.Save(traktPoster);

                        break;

                    case JMMImageType.Trakt_Fanart:

                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(imageID);

                        if (traktFanart == null) return "Could not find image";

                        traktFanart.Enabled = enabled ? 1 : 0; 
                        RepoFactory.Trakt_ImageFanart.Save(traktFanart);

                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType)
        {
            try
            {

                JMMImageType imgType = (JMMImageType) imageType;
                ImageSizeType sizeType = ImageSizeType.Poster;

                switch (imgType)
                {
                    case JMMImageType.AniDB_Cover:
                    case JMMImageType.TvDB_Cover:
                    case JMMImageType.MovieDB_Poster:
                    case JMMImageType.Trakt_Poster:
                        sizeType = ImageSizeType.Poster;
                        break;

                    case JMMImageType.TvDB_Banner:
                        sizeType = ImageSizeType.WideBanner;
                        break;

                    case JMMImageType.TvDB_FanArt:
                    case JMMImageType.MovieDB_FanArt:
                    case JMMImageType.Trakt_Fanart:
                        sizeType = ImageSizeType.Fanart;
                        break;
                }

                if (!isDefault)
                {
                    // this mean we are removing an image as deafult
                    // which esssential means deleting the record

                    AniDB_Anime_DefaultImage img = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, (int) sizeType);
                    if (img != null)
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(img.AniDB_Anime_DefaultImageID);
                }
                else
                {
                    // making the image the default for it's type (poster, fanart etc)
                    AniDB_Anime_DefaultImage img = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, (int) sizeType);
                    if (img == null)
                        img = new AniDB_Anime_DefaultImage();

                    img.AnimeID = animeID;
                    img.ImageParentID = imageID;
                    img.ImageParentType = (int) imgType;
                    img.ImageType = (int) sizeType;
                    RepoFactory.AniDB_Anime_DefaultImage.Save(img);
                }

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                RepoFactory.AnimeSeries.Save(series,false);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        #region Web Cache Admin

        public bool IsWebCacheAdmin()
        {
            try
            {
                string res = AzureWebAPI.Admin_AuthUser();
                return string.IsNullOrEmpty(res);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        public Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType)
        {
            try
            {
                AzureLinkType lType = (AzureLinkType) linkType;
                Azure_AnimeLink link = null;

                switch (lType)
                {
                    case AzureLinkType.TvDB:
                        link = AzureWebAPI.Admin_GetRandomTvDBLinkForApproval();
                        break;
                    case AzureLinkType.Trakt:
                        link = AzureWebAPI.Admin_GetRandomTraktLinkForApproval();
                        break;
                }


                if (link != null)
                    return link;

                return null;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Azure_AdminMessage> GetAdminMessages()
        {
            try
            {
                return ServerInfo.Instance.AdminMessages?.ToList() ?? new List<Azure_AdminMessage>();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Azure_AdminMessage>();
            }
        }

        #region Admin - TvDB

        public string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return AzureWebAPI.Admin_Revoke_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Sends the current user's TvDB links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTvDBLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                List<CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<Azure_CrossRef_AniDB_TvDB> results =
                    AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (Azure_CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            foundLinks = true;
                            break;
                        }
                    }
                }
                if (foundLinks) return "Links already exist, please approve them instead";

                // send the links to the web cache
                foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                if (results != null)
                {
                    List<Azure_CrossRef_AniDB_TvDB> linksToApprove =
                        new List<Azure_CrossRef_AniDB_TvDB>();
                    foreach (Azure_CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (Azure_CrossRef_AniDB_TvDB xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(
                            xref.CrossRef_AniDB_TvDBId.Value);
                    }
                    return "Success";
                }
                else
                    return "Failure to send links to web cache";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region Admin - Trakt

        public string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
        {
            try
            {
                return AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
        {
            try
            {
                return AzureWebAPI.Admin_Revoke_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Sends the current user's Trakt links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTraktLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                List<CrossRef_AniDB_TraktV2> xrefs = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<Azure_CrossRef_AniDB_Trakt> results =
                    AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (Azure_CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            foundLinks = true;
                            break;
                        }
                    }
                }
                if (foundLinks) return "Links already exist, please approve them instead";

                // send the links to the web cache
                foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                if (results != null)
                {
                    List<Azure_CrossRef_AniDB_Trakt> linksToApprove =
                        new List<Azure_CrossRef_AniDB_Trakt>();
                    foreach (Azure_CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (Azure_CrossRef_AniDB_Trakt xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(
                            xref.CrossRef_AniDB_TraktV2ID);
                    }
                    return "Success";
                }
                else
                    return "Failure to send links to web cache";

                //return JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #endregion

        #region TvDB

        public List<Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                if (isAdmin)
                    return AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                else
                    return AzureWebAPI.Get_CrossRefAniDBTvDB(animeID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Azure_CrossRef_AniDB_TvDB>();
            }
        }


        public List<CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID).Cast<CrossRef_AniDB_TvDBV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetTVDBCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }


        public List<TVDB_Series_Search_Response> SearchTheTvDB(string criteria)
        {
            try
            {
                return ShokoService.TvdbHelper.SearchSeries(criteria);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<TVDB_Series_Search_Response>();
            }
        }


        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh data from TvDB
                ShokoService.TvdbHelper.UpdateAllInfoAndImages(seriesID, true, false);

                seasonNumbers = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(seriesID);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return seasonNumbers;
            }
        }

        public string LinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber, int? crossRef_AniDB_TvDBV2ID)
        {
            try
            {
                CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvDBID, tvSeasonNumber, tvEpNumber, animeID, aniEpType,
                    aniEpNumber);
                if (xref != null && !crossRef_AniDB_TvDBV2ID.HasValue)
                {
                    string msg = string.Format("You have already linked Anime ID {0} to this TvDB show/season/ep",
                        xref.AnimeID);
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this TvDB show/season/ep",
                            anime.MainTitle,
                            xref.AnimeID);
                    }
                    return msg;
                }

	            // we don't need to proactively remove the link here anymore, as all links are removed when it is not marked as additive

	            CommandRequest_LinkAniDBTvDB cmdRequest = new CommandRequest_LinkAniDBTvDB(animeID, (enEpisodeType)aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber, false, !crossRef_AniDB_TvDBV2ID.HasValue);
                cmdRequest.Save();

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID)
        {
            try
            {
                TvDBHelper.LinkAniDBTvDBEpisode(aniDBID, tvDBID, animeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        /// <summary>
        /// Removes all tvdb links for one anime
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public string RemoveLinkAniDBTvDBForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                List<CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID);
                if (xrefs == null) return "";

                foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
                {
                    // check if there are default images used associated
                    List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                    foreach (AniDB_Anime_DefaultImage image in images)
                    {
                        if (image.ImageParentType == (int) JMMImageType.TvDB_Banner ||
                            image.ImageParentType == (int) JMMImageType.TvDB_Cover ||
                            image.ImageParentType == (int) JMMImageType.TvDB_FanArt)
                        {
                            if (image.ImageParentID == xref.TvDBID)
                                RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                        }
                    }

                    TvDBHelper.RemoveLinkAniDBTvDB(xref.AnimeID, (enEpisodeType) xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) JMMImageType.TvDB_Banner ||
                        image.ImageParentType == (int) JMMImageType.TvDB_Cover ||
                        image.ImageParentType == (int) JMMImageType.TvDB_FanArt)
                    {
                        if (image.ImageParentID == tvDBID)
                            RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TvDBHelper.RemoveLinkAniDBTvDB(animeID, (enEpisodeType) aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID)
        {
            try
            {
                AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);

                if (ep == null) return "Could not find Episode";

                CrossRef_AniDB_TvDB_Episode xref = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(aniDBEpisodeID);
                if (xref == null) return "Could not find Link!";


                RepoFactory.CrossRef_AniDB_TvDB_Episode.Delete(xref.CrossRef_AniDB_TvDB_EpisodeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public List<TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID)
        {
            List<TvDB_ImagePoster> allImages = new List<TvDB_ImagePoster>();
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImagePoster.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImagePoster.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<TvDB_ImagePoster>();
            }
        }

        public List<TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImageWideBanner.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<TvDB_ImageWideBanner>();
            }
        }

        public List<TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImageFanart.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImageFanart.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<TvDB_ImageFanart>();
            }
        }

        public List<TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_Episode.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_Episode.GetAll().ToList();

            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<TvDB_Episode>();
            }
        }

        #endregion

        #region Trakt

        public List<Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID)
        {
            List<Trakt_ImageFanart> allImages = new List<Trakt_ImageFanart>();
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_ImageFanart.GetByShowID(traktShowID.Value);
                else
                    return RepoFactory.Trakt_ImageFanart.GetAll().ToList();

            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Trakt_ImageFanart>();
            }
        }

        public List<Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID)
        {
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_ImagePoster.GetByShowID(traktShowID.Value);
                else
                    return RepoFactory.Trakt_ImagePoster.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Trakt_ImagePoster>();
            }
        }

        public List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
        {
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_Episode.GetByShowID(traktShowID.Value).ToList();
                else
                    return RepoFactory.Trakt_Episode.GetAll().ToList();

            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Trakt_Episode>();
            }
        }

        public List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
        {
            try
            {
                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show != null)
                    return GetAllTraktEpisodes(show.Trakt_ShowID);

                return new List<Trakt_Episode>();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Trakt_Episode>();
            }
        }

        public List<Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                if (isAdmin)
                    return AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                else
                    return AzureWebAPI.Get_CrossRefAniDBTrakt(animeID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Azure_CrossRef_AniDB_Trakt>();
            }
        }

        public string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
            int traktEpNumber, int? crossRef_AniDB_TraktV2ID)
        {
            try
            {

                if (crossRef_AniDB_TraktV2ID.HasValue)
                {
                    CrossRef_AniDB_TraktV2 xrefTemp = RepoFactory.CrossRef_AniDB_TraktV2.GetByID(crossRef_AniDB_TraktV2ID.Value);
                    // delete the existing one if we are updating
                    TraktTVHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (enEpisodeType) xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }

                CrossRef_AniDB_TraktV2 xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(traktID, seasonNumber, traktEpNumber, animeID,
                    aniEpType,
                    aniEpNumber);
                if (xref != null)
                {
                    string msg = string.Format("You have already linked Anime ID {0} to this Trakt show/season/ep",
                        xref.AnimeID);
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this Trakt show/season/ep",
                            anime.MainTitle,
                            xref.AnimeID);
                    }
                    return msg;
                }

                return TraktTVHelper.LinkAniDBTrakt(animeID, (enEpisodeType) aniEpType, aniEpNumber, traktID,
                    seasonNumber,
                    traktEpNumber, false);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }


        public List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID).Cast<CrossRef_AniDB_TraktV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Trakt_Episode.GetByAnimeID(animeID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CL_TraktTVShowResponse> SearchTrakt(string criteria)
        {
            List<CL_TraktTVShowResponse> results = new List<CL_TraktTVShowResponse>();
            try
            {
                List<TraktV2SearchShowResult> traktResults = TraktTVHelper.SearchShowV2(criteria);

                foreach (TraktV2SearchShowResult res in traktResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return results;
            }
        }

        public string RemoveLinkAniDBTraktForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) JMMImageType.Trakt_Fanart ||
                        image.ImageParentType == (int) JMMImageType.Trakt_Poster)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                foreach (CrossRef_AniDB_TraktV2 xref in RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID))
                {
                    TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (enEpisodeType) xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID,
            int traktSeasonNumber,
            int traktEpNumber)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) JMMImageType.Trakt_Fanart ||
                        image.ImageParentType == (int) JMMImageType.Trakt_Poster)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (enEpisodeType) aniEpType, aniEpNumber,
                    traktID, traktSeasonNumber, traktEpNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public List<int> GetSeasonNumbersForTrakt(string traktID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh show info including season numbers from trakt
                TraktV2ShowExtended tvshow = TraktTVHelper.GetShowInfoV2(traktID);

                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show == null) return seasonNumbers;

                foreach (Trakt_Season season in show.GetSeasons())
                    seasonNumbers.Add(season.Season);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return seasonNumbers;
            }
        }

        #endregion

        #region MAL

        public CL_CrossRef_AniDB_MAL_Response GetMALCrossRefWebCache(int animeID)
        {
            try
            {
                return AzureWebAPI.Get_CrossRefAniDBMAL(animeID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CL_MALAnime_Response> SearchMAL(string criteria)
        {
            List<CL_MALAnime_Response> results = new List<CL_MALAnime_Response>();
            try
            {
                anime malResults = MALHelper.SearchAnimesByTitle(criteria);

                foreach (animeEntry res in malResults.entry)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return results;
            }
        }


        public string LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber)
        {
            try
            {
                CrossRef_AniDB_MAL xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByMALID(malID);
                if (xrefTemp != null)
                {
                    string animeName = "";
                    try
                    {
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xrefTemp.AnimeID);
                        if (anime != null) animeName = anime.MainTitle;
                    }
                    catch
                    {
                    }
                    return string.Format("Not using MAL link as this MAL ID ({0}) is already in use by {1} ({2})", malID,
                        xrefTemp.AnimeID, animeName);
                }

                xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeConstraint(animeID, epType, epNumber);
                if (xrefTemp != null)
                {
                    // delete the link first because we are over-writing it
                    RepoFactory.CrossRef_AniDB_MAL.Delete(xrefTemp.CrossRef_AniDB_MALID);
                    //return string.Format("Not using MAL link as this Anime ID ({0}) is already in use by {1}/{2}/{3} ({4})", animeID, xrefTemp.MALID, epType, epNumber, xrefTemp.MALTitle);
                }

                MALHelper.LinkAniDBMAL(animeID, malID, malTitle, epType, epNumber, false);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string LinkAniDBMALUpdated(int animeID, int malID, string malTitle, int oldEpType, int oldEpNumber,
            int newEpType, int newEpNumber)
        {
            try
            {
                CrossRef_AniDB_MAL xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeConstraint(animeID, oldEpType, oldEpNumber);
                if (xrefTemp == null)
                    return string.Format("Could not find MAL link ({0}/{1}/{2})", animeID, oldEpType, oldEpNumber);

                RepoFactory.CrossRef_AniDB_MAL.Delete(xrefTemp.CrossRef_AniDB_MALID);

                return LinkAniDBMAL(animeID, malID, malTitle, newEpType, newEpNumber);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }


        public string RemoveLinkAniDBMAL(int animeID, int epType, int epNumber)
        {
            try
            {
                MALHelper.RemoveLinkAniDBMAL(animeID, epType, epNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region Other Cross Refs

        public CL_CrossRef_AniDB_Other_Response GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
        {
            try
            {
                return AzureWebAPI.Get_CrossRefAniDBOther(animeID, (CrossRefType) crossRefType);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, (CrossRefType) crossRefType);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public string LinkAniDBOther(int animeID, int movieID, int crossRefType)
        {
            try
            {
                CrossRefType xrefType = (CrossRefType) crossRefType;

                switch (xrefType)
                {
                    case CrossRefType.MovieDB:
                        MovieDBHelper.LinkAniDBMovieDB(animeID, movieID, false);
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBOther(int animeID, int crossRefType)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                if (anime == null) return "Could not find Anime!";

                CrossRefType xrefType = (CrossRefType) crossRefType;
                switch (xrefType)
                {
                    case CrossRefType.MovieDB:

                        // check if there are default images used associated
                        List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                        foreach (AniDB_Anime_DefaultImage image in images)
                        {
                            if (image.ImageParentType == (int) JMMImageType.MovieDB_FanArt ||
                                image.ImageParentType == (int) JMMImageType.MovieDB_Poster)
                            {
                                RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                            }
                        }

                        MovieDBHelper.RemoveLinkAniDBMovieDB(animeID);
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region MovieDB

        public List<CL_MovieDBMovieSearch_Response> SearchTheMovieDB(string criteria)
        {
            List<CL_MovieDBMovieSearch_Response> results = new List<CL_MovieDBMovieSearch_Response>();
            try
            {
                List<MovieDB_Movie_Result> movieResults = MovieDBHelper.Search(criteria);

                foreach (MovieDB_Movie_Result res in movieResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return results;
            }
        }

        public List<MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
        {
            try
            {
                if (movieID.HasValue)
                    return RepoFactory.MovieDB_Poster.GetByMovieID(movieID.Value);
                else
                    return RepoFactory.MovieDB_Poster.GetAllOriginal();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<MovieDB_Poster>();
            }
        }

        public List<MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
        {
            try
            {
                if (movieID.HasValue)
                    return RepoFactory.MovieDB_Fanart.GetByMovieID(movieID.Value);
                else
                    return RepoFactory.MovieDB_Fanart.GetAllOriginal();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<MovieDB_Fanart>();
            }
        }

        #endregion

        /// <summary>
        /// Finds the previous episode for use int the next unwatched episode
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public CL_AnimeEpisode_User GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID)
        {
            try
            {
                CL_AnimeEpisode_User nextEp = GetNextUnwatchedEpisode(animeSeriesID, userID);
                if (nextEp == null) return null;

                int epType = nextEp.EpisodeType;
                int epNum = nextEp.EpisodeNumber - 1;

                if (epNum <= 0) return null;

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (series == null) return null;

                List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID,
                    (enEpisodeType) epType,
                    epNum);
                if (anieps.Count == 0) return null;

                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anieps[0].EpisodeID);
                return ep?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public CL_AnimeEpisode_User GetNextUnwatchedEpisode(int animeSeriesID, int userID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetNextUnwatchedEpisode(session.Wrap(), animeSeriesID, userID);
            }
        }

        public CL_AnimeEpisode_User GetNextUnwatchedEpisode(ISessionWrapper session, int animeSeriesID, int userID)
        {
            try
            {

                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (series == null) return null;

                //List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);
                List<AnimeEpisode> epList = new List<AnimeEpisode>();
                Dictionary<int, SVR_AnimeEpisode_User> dictEpUsers = new Dictionary<int, SVR_AnimeEpisode_User>();
                foreach (
                    SVR_AnimeEpisode_User userRecord in RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(userID, animeSeriesID))
                    dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                foreach (AnimeEpisode animeep in RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID))
                {
                    if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                    {
                        epList.Add(animeep);
                        continue;
                    }

                    AnimeEpisode_User usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                    if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                        epList.Add(animeep);
                }

                List<AniDB_Episode> aniEpList = RepoFactory.AniDB_Episode.GetByAnimeID(series.AniDB_ID);
                Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                foreach (AniDB_Episode aniep in aniEpList)
                    dictAniEps[aniep.EpisodeID] = aniep;

                List<CL_AnimeEpisode_User> candidateEps = new List<CL_AnimeEpisode_User>();
                foreach (SVR_AnimeEpisode ep in epList)
                {
                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        AniDB_Episode anidbep = dictAniEps[ep.AniDB_EpisodeID];
                        if (anidbep.EpisodeType == (int) enEpisodeType.Episode ||
                            anidbep.EpisodeType == (int) enEpisodeType.Special)
                        {
                            SVR_AnimeEpisode_User userRecord = null;
                            if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                userRecord = dictEpUsers[ep.AnimeEpisodeID];

                            CL_AnimeEpisode_User epContract = ep.GetUserContract(userID);
                            if (epContract != null)
                                candidateEps.Add(epContract);
                        }
                    }
                }

                if (candidateEps.Count == 0) return null;



                // this will generate a lot of queries when the user doesn have files
                // for these episodes
                foreach (CL_AnimeEpisode_User canEp in candidateEps.OrderBy(a=>a.EpisodeType).ThenBy(a=>a.EpisodeNumber))
                {
                    // now refresh from the database to get file count
                    SVR_AnimeEpisode epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);
                    if (epFresh.GetVideoLocals().Count > 0)
                        return epFresh.GetUserContract(userID);
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeEpisode_User> GetAllUnwatchedEpisodes(int animeSeriesID, int userID)
        {
            List<CL_AnimeEpisode_User> ret = new List<CL_AnimeEpisode_User>();

            try
            {

                return
                    RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID).Select(a => a.GetUserContract(userID)).Where(a => a != null)
                        .Where(a => a.WatchedCount == 0)
                        .OrderBy(a => a.EpisodeType).ThenBy(a => a.EpisodeNumber)
                        .ToList();
                /*
                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
				
				// get all the data first
				// we do this to reduce the amount of database calls, which makes it a lot faster
				AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
				if (series == null) return null;

				//List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);
				List<AnimeEpisode> epList = new List<AnimeEpisode>();
				Dictionary<int, AnimeEpisode_User> dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
				foreach (AnimeEpisode_User userRecord in repEpUser.GetByUserIDAndSeriesID(userID, animeSeriesID))
					dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

				foreach (AnimeEpisode animeep in repEps.GetBySeriesID(animeSeriesID))
				{
					if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
					{
						epList.Add(animeep);
						continue;
					}

					AnimeEpisode_User usrRec = dictEpUsers[animeep.AnimeEpisodeID];
					if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
						epList.Add(animeep);
				}

				AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
				List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
				Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
				foreach (AniDB_Episode aniep in aniEpList)
					dictAniEps[aniep.EpisodeID] = aniep;

				List<Contract_AnimeEpisode> candidateEps = new List<Contract_AnimeEpisode>();
				foreach (AnimeEpisode ep in epList)
				{
					if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
					{
						AniDB_Episode anidbep = dictAniEps[ep.AniDB_EpisodeID];
						if (anidbep.EpisodeType == (int)enEpisodeType.Episode || anidbep.EpisodeType == (int)enEpisodeType.Special)
						{
							AnimeEpisode_User userRecord = null;
							if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
								userRecord = dictEpUsers[ep.AnimeEpisodeID];
                            if
							Contract_AnimeEpisode epContract = ep.ToContract(anidbep, new List<VideoLocal>(), userRecord, series.GetUserRecord(userID));
							candidateEps.Add(epContract);
						}
					}
				}

				if (candidateEps.Count == 0) return null;

				// sort by episode type and number to find the next episode
				List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
				sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
				candidateEps = Sorting.MultiSort<Contract_AnimeEpisode>(candidateEps, sortCriteria);

				// this will generate a lot of queries when the user doesn have files
				// for these episodes
				foreach (Contract_AnimeEpisode canEp in candidateEps)
				{
					// now refresh from the database to get file count
					AnimeEpisode epFresh = repEps.GetByID(canEp.AnimeEpisodeID);
					if (epFresh.GetVideoLocals().Count > 0)
						ret.Add(epFresh.ToContract(true, userID, null));
				}

				return ret;
                */
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ret;
            }
        }

        public CL_AnimeEpisode_User GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
        {
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return null;

                List<SVR_AnimeSeries> allSeries = grp.GetAllSeries().OrderBy(a=>a.AirDate).ToList();


                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    CL_AnimeEpisode_User contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                    if (contract != null) return contract;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeEpisode_User> GetContinueWatchingFilter(int userID, int maxRecords)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return retEps;

                    // find the locked Continue Watching Filter
                    SVR_GroupFilter gf = null;
                    List<SVR_GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
                    if (lockedGFs != null)
                    {
                        // if it already exists we can leave
                        foreach (SVR_GroupFilter gfTemp in lockedGFs)
                        {
                            if (gfTemp.FilterType == (int) GroupFilterType.ContinueWatching)
                            {
                                gf = gfTemp;
                                break;
                            }
                        }
                    }

                    if ((gf == null) || !gf.GroupsIds.ContainsKey(userID))
                        return retEps;
                    IEnumerable<CL_AnimeGroup_User> comboGroups =
                        gf.GroupsIds[userID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                            .Where(a => a != null)
                            .Select(a => a.GetUserContract(userID));
                            


                    // apply sorting
                    comboGroups = GroupFilterHelper.Sort(comboGroups, gf);


                    foreach (CL_AnimeGroup_User grp in comboGroups)
                    {
                        List<SVR_AnimeSeries> sers = RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID).OrderBy(a=>a.AirDate).ToList();

                        List<int> seriesWatching = new List<int>();

                        foreach (SVR_AnimeSeries ser in sers)
                        {
                            if (!user.AllowedSeries(ser)) continue;
                            bool useSeries = true;

                            if (seriesWatching.Count > 0)
                            {
                                if (ser.GetAnime().AnimeType == (int) enAnimeType.TVSeries)
                                {
                                    // make sure this series is not a sequel to an existing series we have already added
                                    foreach (AniDB_Anime_Relation rel in ser.GetAnime().GetRelatedAnime())
                                    {
                                        if (rel.RelationType.ToLower().Trim().Equals("sequel") ||
                                            rel.RelationType.ToLower().Trim().Equals("prequel"))
                                            useSeries = false;
                                    }
                                }
                            }

                            if (!useSeries) continue;


                            CL_AnimeEpisode_User ep = GetNextUnwatchedEpisode(sessionWrapper, ser.AnimeSeriesID, userID);
                            if (ep != null)
                            {
                                retEps.Add(ep);

                                // Lets only return the specified amount
                                if (retEps.Count == maxRecords)
                                    return retEps;

                                if (ser.GetAnime().AnimeType == (int) enAnimeType.TVSeries)
                                    seriesWatching.Add(ser.AniDB_ID);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return retEps;
        }

        /// <summary>
        /// Gets a list of episodes watched based on the most recently watched series
        /// It will return the next episode to watch in the most recent 10 series
        /// </summary>
        /// <returns></returns>
        public List<CL_AnimeEpisode_User> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    DateTime start = DateTime.Now;

                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    // get a list of series that is applicable
                    List<SVR_AnimeSeries_User> allSeriesUser = RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(jmmuserID);

                    TimeSpan ts = DateTime.Now - start;
                    logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Series: {0}", ts.TotalMilliseconds));
                    start = DateTime.Now;

                    foreach (SVR_AnimeSeries_User userRecord in allSeriesUser)
                    {
                        SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(series)) continue;

                        CL_AnimeEpisode_User ep = GetNextUnwatchedEpisode(sessionWrapper, userRecord.AnimeSeriesID, jmmuserID);
                        if (ep != null)
                        {
                            retEps.Add(ep);

                            // Lets only return the specified amount
                            if (retEps.Count == maxRecords)
                            {
                                ts = DateTime.Now - start;
                                logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Episodes: {0}",
                                    ts.TotalMilliseconds));
                                return retEps;
                            }
                        }
                    }
                    ts = DateTime.Now - start;
                    logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Episodes: {0}", ts.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retEps;
        }

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyWatched(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                
                return
                    RepoFactory.AnimeEpisode_User.GetMostRecentlyWatched(jmmuserID, maxRecords)
                        .Select(a => RepoFactory.AnimeEpisode.GetByID(a.AnimeEpisodeID).GetUserContract(jmmuserID))
                        .ToList();
                /*
                                using (var session = JMMService.SessionFactory.OpenSession())
                                {
                                    AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                    JMMUserRepository repUsers = new JMMUserRepository();

                                    JMMUser user = repUsers.GetByID(session, jmmuserID);
                                    if (user == null) return retEps;

                                    // get a list of series that is applicable
                                    List<AnimeEpisode_User> allEpUserRecs = repEpUser.GetMostRecentlyWatched(session, jmmuserID);
                                    foreach (AnimeEpisode_User userRecord in allEpUserRecs)
                                    {
                                        AnimeEpisode ep = repEps.GetByID(session, userRecord.AnimeEpisodeID);
                                        if (ep == null) continue;

                                        Contract_AnimeEpisode epContract = ep.ToContract(session, jmmuserID);
                                        if (epContract != null)
                                        {
                                            retEps.Add(epContract);

                                            // Lets only return the specified amount
                                            if (retEps.Count == maxRecords) return retEps;
                                        }
                                    }
                                }*/
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retEps;
        }

        public IReadOnlyList<SVR_VideoLocal> GetAllFiles()
        {
            try
            {
                return RepoFactory.VideoLocal.GetAll();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<SVR_VideoLocal>();
            }
        }

        public SVR_VideoLocal GetFileByID(int id)
        {
            try
            {
                return RepoFactory.VideoLocal.GetByID(id);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new SVR_VideoLocal();
            }
        }

        public List<SVR_VideoLocal> GetFilesRecentlyAdded(int max_records)
        {
            try
            {
                return RepoFactory.VideoLocal.GetMostRecentlyAdded(max_records);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<SVR_VideoLocal>();
            }
        }

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

	                // We will deal with a large list, don't perform ops on the whole thing!
                    List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(-1);
                    int numEps = 0;
                    foreach (SVR_VideoLocal vid in vids)
                    {
	                    if (string.IsNullOrEmpty(vid.Hash)) continue;

                        foreach (SVR_AnimeEpisode ep in vid.GetAnimeEpisodes())
                        {
                            if (user.AllowedSeries(ep.GetAnimeSeries(sessionWrapper)))
                            {
                                CL_AnimeEpisode_User epContract = ep.GetUserContract(jmmuserID);
                                if (epContract != null)
                                {
                                    retEps.Add(epContract);
                                    numEps++;

                                    // Lets only return the specified amount
                                    if (retEps.Count >= maxRecords) return retEps;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retEps;
        }

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {

                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    DateTime start = DateTime.Now;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseFactory.Instance.GetData(sql);

                    TimeSpan ts2 = DateTime.Now - start;
                    logger.Info("GetEpisodesRecentlyAddedSummary:RawData in {0} ms", ts2.TotalMilliseconds);
                    start = DateTime.Now;

                    int numEps = 0;
                    foreach (object[] res in results)
                    {
                        int animeSeriesID = int.Parse(res[0].ToString());

                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(ser)) continue;

                        List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        List<SVR_AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
                        if (eps.Count == 0) continue;

                        CL_AnimeEpisode_User epContract = eps[0].GetUserContract(jmmuserID);
                        if (epContract != null)
                        {
                            retEps.Add(epContract);
                            numEps++;

                            // Lets only return the specified amount
                            if (retEps.Count == maxRecords)
                            {
                                ts2 = DateTime.Now - start;
                                logger.Info("GetEpisodesRecentlyAddedSummary:Episodes in {0} ms", ts2.TotalMilliseconds);
                                start = DateTime.Now;
                                return retEps;
                            }
                        }
                    }
                    ts2 = DateTime.Now - start;
                    logger.Info("GetEpisodesRecentlyAddedSummary:Episodes in {0} ms", ts2.TotalMilliseconds);
                    start = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retEps;
        }

        public List<CL_AnimeSeries_User> GetSeriesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeSeries_User> retSeries = new List<CL_AnimeSeries_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {


                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retSeries;

                    List<SVR_AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(maxRecords);
                    int numSeries = 0;
                    foreach (SVR_AnimeSeries ser in series)
                    {
                        if (user.AllowedSeries(ser))
                        {
                            CL_AnimeSeries_User serContract = ser.GetUserContract(jmmuserID);
                            if (serContract != null)
                            {
                                retSeries.Add(serContract);
                                numSeries++;

                                // Lets only return the specified amount
                                if (retSeries.Count == maxRecords) return retSeries;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retSeries;
        }

        public CL_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID)
        {
            try
            {
                return RepoFactory.AnimeEpisode_User.GetLastWatchedEpisodeForSeries(animeSeriesID, jmmuserID)?.Contract;
                /*
                                using (var session = JMMService.SessionFactory.OpenSession())
                                {
                                    AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                    JMMUserRepository repUsers = new JMMUserRepository();

                                    JMMUser user = repUsers.GetByID(session, jmmuserID);
                                    if (user == null) return null;

                                    List<AnimeEpisode_User> userRecords = repEpUser.GetLastWatchedEpisodeForSeries(session, animeSeriesID, jmmuserID);
                                    if (userRecords == null || userRecords.Count == 0) return null;

                                    AnimeEpisode ep = repEps.GetByID(session, userRecords[0].AnimeEpisodeID);
                                    if (ep == null) return null;

                                    return ep.ToContract(session, jmmuserID);
                                }*/
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return null;
        }


        /// <summary>
        /// Delete a series, and everything underneath it (episodes, files)
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="deleteFiles">also delete the physical files</param>
        /// <returns></returns>
        public string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null) return "Series does not exist";

                int animeGroupID = ser.AnimeGroupID;

                foreach (SVR_AnimeEpisode ep in ser.GetAnimeEpisodes())
                {
                    foreach (SVR_VideoLocal vid in ep.GetVideoLocals())
                    {
                        foreach (SVR_VideoLocal_Place place in vid.Places)
                        {
                            if (deleteFiles)
                            {
                                logger.Info("Deleting video local record and file: {0}", place.FullServerPath);
                                IFileSystem fileSystem = place.ImportFolder.FileSystem;
                                if (fileSystem == null)
                                {
                                    logger.Error("Unable to delete file, filesystem not found");
                                    return "Unable to delete file, filesystem not found";
                                }
                                FileSystemResult<IObject> fr = fileSystem.Resolve(place.FullServerPath);
                                if (fr == null || !fr.IsOk)
                                {
                                    logger.Error($"Unable to find file '{place.FullServerPath}'");
                                    return $"Unable to find file '{place.FullServerPath}'";
                                }
                                IFile file = fr.Result as IFile;
                                if (file == null)
                                {
                                    logger.Error($"Seems '{place.FullServerPath}' is a directory");
                                    return $"Seems '{place.FullServerPath}' is a directory";

                                }
                                FileSystemResult fs = file.Delete(false);
                                if (fs == null || !fs.IsOk)
                                {
                                    logger.Error($"Unable to delete file '{place.FullServerPath}'");
                                    return $"Unable to delete file '{place.FullServerPath}'";
                                }
                            }
                            RepoFactory.VideoLocalPlace.Delete(place);
                        }
                        CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
                        cmdDel.Save();
                        RepoFactory.VideoLocal.Delete(vid);

                    }
                    RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
                }
                RepoFactory.AnimeSeries.Delete(ser.AnimeSeriesID);

                // finally update stats
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp != null)
                {
                    if (grp.GetAllSeries().Count == 0)
                    {
                        DeleteAnimeGroup(grp.AnimeGroupID, false);
                    }
                    else
                    {
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                        //StatsCache.Instance.UpdateUsingGroup(grp.TopLevelAnimeGroup.AnimeGroupID);
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }


        public List<CL_AnimeSeries_User> GetSeriesWithMissingEpisodes(int maxRecords, int jmmuserID)
        {

            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user != null)
                    return
                        RepoFactory.AnimeSeries.GetWithMissingEpisodes()
                            .Select(a => a.GetUserContract(jmmuserID))
                            .Where(a => a != null)
                            .ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_AnimeSeries_User>();
        }

        public List<CL_AniDB_Anime> GetMiniCalendar(int jmmuserID, int numberOfDays)
        {


            // get all the series
            List<CL_AniDB_Anime> animeList = new List<CL_AniDB_Anime>();

            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                List<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays),
                    DateTime.Today.AddDays(numberOfDays));
                foreach (SVR_AniDB_Anime anime in animes)
                {
                    if (anime?.Contract?.AniDBAnime == null)
                        continue;
                    if (!user.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags()))
                        animeList.Add(anime.Contract.AniDBAnime);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return animeList;
        }

        public List<CL_AniDB_Anime> GetAnimeForMonth(int jmmuserID, int month, int year)
        {
            // get all the series
            List<CL_AniDB_Anime> animeList = new List<CL_AniDB_Anime>();

            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                DateTime startDate = new DateTime(year, month, 1, 0, 0, 0);
                DateTime endDate = startDate.AddMonths(1);
                endDate = endDate.AddMinutes(-10);

                List<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(startDate, endDate);
                foreach (SVR_AniDB_Anime anime in animes)
                {
                    if (anime?.Contract?.AniDBAnime == null)
                        continue;
                    if (!user.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags()))
                        animeList.Add(anime.Contract.AniDBAnime);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return animeList;
        }

        /*public List<Contract_AniDBAnime> GetMiniCalendar(int numberOfDays)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			JMMUserRepository repUsers = new JMMUserRepository();

			// get all the series
			List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

			try
			{

				List<AniDB_Anime> animes = repAnime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays), DateTime.Today.AddDays(numberOfDays));
				foreach (AniDB_Anime anime in animes)
				{

						animeList.Add(anime.ToContract());
				}

			}
			catch (Exception ex)
			{
				logger.Error( ex,ex.ToString());
			}
			return animeList;
		}*/

        public List<JMMUser> GetAllUsers()
        {
            try
            {
                return RepoFactory.JMMUser.GetAll().Cast<JMMUser>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<JMMUser>();
            }
        }

        public JMMUser AuthenticateUser(string username, string password)
        {

            try
            {
                return RepoFactory.JMMUser.AuthenticateUser(username, password);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string ChangePassword(int userID, string newPassword)
        {
            return ChangePassword(userID, newPassword, true);
        }

        public string ChangePassword(int userID, string newPassword, bool revokeapikey)
        {
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                jmmUser.Password = Digest.Hash(newPassword);
                RepoFactory.JMMUser.Save(jmmUser, false);
                if (revokeapikey) { UserDatabase.RemoveApiKeysForUserID(userID); }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public string SaveUser(JMMUser user)
        {
            
            try
            {
                bool existingUser = false;
                bool updateStats = false;
                bool updateGf = false;
                SVR_JMMUser jmmUser = null;
                if (user.JMMUserID!=0)
                {
                    jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID);
                    if (jmmUser == null) return "User not found";
                    existingUser = true;
                }
                else
                {
                    jmmUser = new SVR_JMMUser();
                    updateStats = true;
                    updateGf = true;
                }

                if (existingUser && jmmUser.IsAniDBUser != user.IsAniDBUser)
                    updateStats = true;

                string hcat = string.Join(",", user.HideCategories);
                if (jmmUser.HideCategories != hcat)
                    updateGf = true;
                jmmUser.HideCategories = hcat;
                jmmUser.IsAniDBUser = user.IsAniDBUser;
                jmmUser.IsTraktUser = user.IsTraktUser;
                jmmUser.IsAdmin = user.IsAdmin;
                jmmUser.Username = user.Username;
                jmmUser.CanEditServerSettings = user.CanEditServerSettings;
                jmmUser.PlexUsers = string.Join(",", user.PlexUsers);
                if (string.IsNullOrEmpty(user.Password))
                {
                    jmmUser.Password = "";
                }
                else
                {
                    // Additional check for hashed password, if not hashed we hash it
                    if (user.Password.Length < 64)
                        jmmUser.Password = Digest.Hash(user.Password);
                    else
                        jmmUser.Password = user.Password;
                }

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 0)
                {
                    bool adminExists = false;
                    IReadOnlyList<SVR_JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (SVR_JMMUser userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (existingUser)
                            {
                                if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                            }
                            else
                            {
                                //one admin account is needed
                                adminExists = true;
                                break;
                            }
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                RepoFactory.JMMUser.Save(jmmUser, updateGf);

                // update stats
                if (updateStats)
                {
                    foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                        ser.QueueUpdateStats();
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public string DeleteUser(int userID)
        {
            
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 1)
                {
                    bool adminExists = false;
                    IReadOnlyList<SVR_JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (SVR_JMMUser userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                RepoFactory.JMMUser.Delete(userID);

                // delete all user records
                RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetByUserID(userID));
                RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByUserID(userID));
                RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByUserID(userID));
                RepoFactory.VideoLocalUser.Delete(RepoFactory.VideoLocalUser.GetByUserID(userID));
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public List<CL_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID)
        {
            List<CL_AniDB_Anime_Similar> links = new List<CL_AniDB_Anime_Similar>();
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return links;

                SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return links;


                foreach (AniDB_Anime_Similar link in anime.GetSimilarAnime())
                {
                    SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                    links.Add(link.ToClient(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return links;
            }
        }

        public List<CL_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID)
        {
            List<CL_AniDB_Anime_Relation> links = new List<CL_AniDB_Anime_Relation>();
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return links;

                SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return links;


                foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
                {
                    SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                    links.Add(link.ToClient(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return links;
            }
        }

        /// <summary>
        /// Returns a list of recommendations based on the users votes
        /// </summary>
        /// <param name="maxResults"></param>
        /// <param name="userID"></param>
        /// <param name="recommendationType">1 = to watch, 2 = to download</param>
        public List<CL_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType)
        {
            List<CL_Recommendation> recs = new List<CL_Recommendation>();

            try
            {

                SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return recs;

                // get all the anime the user has chosen to ignore
                int ignoreType = 1;
                switch (recommendationType)
                {
                    case 1:
                        ignoreType = 1;
                        break;
                    case 2:
                        ignoreType = 2;
                        break;
                }
                List<IgnoreAnime> ignored = RepoFactory.IgnoreAnime.GetByUserAndType(userID, ignoreType);
                Dictionary<int, IgnoreAnime> dictIgnored = new Dictionary<int, IgnoreAnime>();
                foreach (IgnoreAnime ign in ignored)
                    dictIgnored[ign.AnimeID] = ign;


                // find all the series which the user has rated
                List<AniDB_Vote> allVotes = RepoFactory.AniDB_Vote.GetAll().OrderByDescending(a=>a.VoteValue).ToList();
                if (allVotes.Count == 0) return recs;


                Dictionary<int, CL_Recommendation> dictRecs = new Dictionary<int, CL_Recommendation>();

                List<AniDB_Vote> animeVotes = new List<AniDB_Vote>();
                foreach (AniDB_Vote vote in allVotes)
                {
                    if (vote.VoteType != (int) enAniDBVoteType.Anime && vote.VoteType != (int) enAniDBVoteType.AnimeTemp)
                        continue;

                    if (dictIgnored.ContainsKey(vote.EntityID)) continue;

                    // check if the user has this anime
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(vote.EntityID);
                    if (anime == null) continue;

                    // get similar anime
                    List<AniDB_Anime_Similar> simAnime = anime.GetSimilarAnime().OrderByDescending(a=>a.GetApprovalPercentage()).ToList();
                    // sort by the highest approval

                    foreach (AniDB_Anime_Similar link in simAnime)
                    {
                        if (dictIgnored.ContainsKey(link.SimilarAnimeID)) continue;

                        SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                        if (animeLink != null)
                            if (!juser.AllowedAnime(animeLink)) continue;

                        // don't recommend to watch anime that the user doesn't have
                        if (animeLink == null && recommendationType == 1) continue;

                        // don't recommend to watch series that the user doesn't have
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);
                        if (ser == null && recommendationType == 1) continue;


                        if (ser != null)
                        {
                            // don't recommend to watch series that the user has already started watching
                            AnimeSeries_User userRecord = ser.GetUserRecord(userID);
                            if (userRecord != null)
                            {
                                if (userRecord.WatchedEpisodeCount > 0 && recommendationType == 1) continue;
                            }

                            // don't recommend to download anime that the user has files for
                            if (ser.LatestLocalEpisodeNumber > 0 && recommendationType == 2) continue;
                        }

                        CL_Recommendation rec = new CL_Recommendation();
                        rec.BasedOnAnimeID = anime.AnimeID;
                        rec.RecommendedAnimeID = link.SimilarAnimeID;

                        // if we don't have the anime locally. lets assume the anime has a high rating
                        decimal animeRating = 850;
                        if (animeLink != null) animeRating = animeLink.GetAniDBRating();

                        rec.Score = CalculateRecommendationScore(vote.VoteValue, link.GetApprovalPercentage(), animeRating);
                        rec.BasedOnVoteValue = vote.VoteValue;
                        rec.RecommendedApproval = link.GetApprovalPercentage();

                        // check if we have added this recommendation before
                        // this might happen where animes are recommended based on different votes
                        // and could end up with different scores
                        if (dictRecs.ContainsKey(rec.RecommendedAnimeID))
                        {
                            if (rec.Score < dictRecs[rec.RecommendedAnimeID].Score) continue;
                        }

                        rec.Recommended_AniDB_Anime = null;
                        if (animeLink != null)
                            rec.Recommended_AniDB_Anime = animeLink.Contract.AniDBAnime;

                        rec.BasedOn_AniDB_Anime = anime.Contract.AniDBAnime;

                        rec.Recommended_AnimeSeries = null;
                        if (ser != null)
                            rec.Recommended_AnimeSeries = ser.GetUserContract(userID);

                        SVR_AnimeSeries serBasedOn = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                        if (serBasedOn == null) continue;

                        rec.BasedOn_AnimeSeries = serBasedOn.GetUserContract(userID);

                        dictRecs[rec.RecommendedAnimeID] = rec;
                    }
                }

                List<CL_Recommendation> tempRecs = new List<CL_Recommendation>();
                foreach (CL_Recommendation rec in dictRecs.Values)
                    tempRecs.Add(rec);

                // sort by the highest score

                int numRecs = 0;
                foreach (CL_Recommendation rec in tempRecs.OrderByDescending(a=>a.Score))
                {
                    if (numRecs == maxResults) break;
                    recs.Add(rec);
                    numRecs++;
                }

                if (recs.Count == 0) return recs;

                return recs;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return recs;
            }
        }

        private double CalculateRecommendationScore(int userVoteValue, double approvalPercentage, decimal animeRating)
        {
            double score = (double) userVoteValue;

            score = score + approvalPercentage;

            if (approvalPercentage > 90) score = score + 100;
            if (approvalPercentage > 80) score = score + 100;
            if (approvalPercentage > 70) score = score + 100;
            if (approvalPercentage > 60) score = score + 100;
            if (approvalPercentage > 50) score = score + 100;

            if (animeRating > 900) score = score + 100;
            if (animeRating > 800) score = score + 100;
            if (animeRating > 700) score = score + 100;
            if (animeRating > 600) score = score + 100;
            if (animeRating > 500) score = score + 100;

            return score;
        }

        public List<CL_AniDB_GroupStatus> GetReleaseGroupsForAnime(int animeID)
        {
            List<CL_AniDB_GroupStatus> relGroups = new List<CL_AniDB_GroupStatus>();

            try
            {
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                //List<int> userReleaseGroups = new List<int>();
                Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
                foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    List<SVR_VideoLocal> vids = ep.GetVideoLocals();
                    List<string> hashes = vids.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                    foreach(string h in hashes)
                    {
                        SVR_VideoLocal vid = vids.First(a => a.Hash == h);
                        AniDB_File anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                                userReleaseGroups[anifile.GroupID] = 0;

                            userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                        }
                    }
                }

                // get all the release groups for this series
                List<AniDB_GroupStatus> grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(animeID);
                foreach (AniDB_GroupStatus gs in grpStatuses)
                {
                    CL_AniDB_GroupStatus cl = gs.ToClient();
                    if (userReleaseGroups.ContainsKey(gs.GroupID))
                    {
                        cl.UserCollecting = true;
                        cl.FileCount = userReleaseGroups[gs.GroupID];
                    }
                    else
                    {
                        cl.UserCollecting = false;
                        cl.FileCount = 0;
                    }

                    relGroups.Add(cl);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return relGroups;
        }

        public List<CL_AniDB_Character> GetCharactersForAnime(int animeID)
        {
            List<CL_AniDB_Character> chars = new List<CL_AniDB_Character>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                return anime.GetCharactersContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return chars;
        }

        public List<CL_AniDB_Character> GetCharactersForSeiyuu(int aniDB_SeiyuuID)
        {
            List<CL_AniDB_Character> chars = new List<CL_AniDB_Character>();

            try
            {
                AniDB_Seiyuu seiyuu = RepoFactory.AniDB_Seiyuu.GetByID(aniDB_SeiyuuID);
                if (seiyuu == null) return chars;

                List<AniDB_Character_Seiyuu> links = RepoFactory.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID);

                foreach (AniDB_Character_Seiyuu chrSei in links)
                {
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(chrSei.CharID);
                    if (chr != null)
                    {
                        List<AniDB_Anime_Character> aniChars = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID);
                        if (aniChars.Count > 0)
                        {
                            SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniChars[0].AnimeID);
                            if (anime != null)
                            {
                                CL_AniDB_Character cl = chr.ToClient(aniChars[0].CharType);
                                cl.Anime = anime.Contract.AniDBAnime;
                                chars.Add(cl);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return chars;
        }

        public void ForceAddFileToMyList(string hash)
        {
            try
            {
                CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(hash);
                cmdAddFile.Save();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<CL_MissingFile> GetMyListFilesForRemoval(int userID)
        {
            List<CL_MissingFile> contracts = new List<CL_MissingFile>();

            /*Contract_MissingFile missingFile2 = new Contract_MissingFile();
			missingFile2.AnimeID = 1;
			missingFile2.AnimeTitle = "Gundam Age";
			missingFile2.EpisodeID = 2;
			missingFile2.EpisodeNumber = 7;
			missingFile2.FileID = 8;
			missingFile2.AnimeSeries = null;
			contracts.Add(missingFile2);

			Thread.Sleep(5000);

			return contracts;*/


            Dictionary<int, SVR_AniDB_Anime> animeCache = new Dictionary<int, SVR_AniDB_Anime>();
            Dictionary<int, SVR_AnimeSeries> animeSeriesCache = new Dictionary<int, SVR_AnimeSeries>();

            try
            {
                AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                enHelperActivityType ev = cmd.Process();
                if (ev == enHelperActivityType.GotMyListHTTP)
                {
                    foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
                    {
                        // let's check if the file on AniDB actually exists in the user's local collection
                        string hash = string.Empty;

                        AniDB_File anifile = RepoFactory.AniDB_File.GetByFileID(myitem.FileID);
                        if (anifile != null)
                            hash = anifile.Hash;
                        else
                        {
                            // look for manually linked files
                            List<CrossRef_File_Episode> xrefs = RepoFactory.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID);
                            foreach (CrossRef_File_Episode xref in xrefs)
                            {
                                if (xref.CrossRefSource != (int) CrossRefSource.AniDB)
                                {
                                    hash = xref.Hash;
                                    break;
                                }
                            }
                        }

                        bool fileMissing = false;
                        if (string.IsNullOrEmpty(hash))
                            fileMissing = true;
                        else
                        {
                            // now check if the file actually exists on disk
                            SVR_VideoLocal v = RepoFactory.VideoLocal.GetByHash(hash);
                            fileMissing = true;
                            foreach (SVR_VideoLocal_Place p in v.Places)
                            {
                                IFileSystem fs = p.ImportFolder.FileSystem;
                                if (fs != null)
                                {
                                    FileSystemResult<IObject> res = fs.Resolve(p.FullServerPath);
                                    if (res != null && res.IsOk)
                                    {
                                        fileMissing = false;
                                        break;
                                    }
                                }
                            }
                        }

                        if (fileMissing)
                        {
                            // this means we can't find the file
                            SVR_AniDB_Anime anime = null;
                            if (animeCache.ContainsKey(myitem.AnimeID))
                                anime = animeCache[myitem.AnimeID];
                            else
                            {
                                anime = RepoFactory.AniDB_Anime.GetByAnimeID(myitem.AnimeID);
                                animeCache[myitem.AnimeID] = anime;
                            }

                            SVR_AnimeSeries ser = null;
                            if (animeSeriesCache.ContainsKey(myitem.AnimeID))
                                ser = animeSeriesCache[myitem.AnimeID];
                            else
                            {
                                ser = RepoFactory.AnimeSeries.GetByAnimeID(myitem.AnimeID);
                                animeSeriesCache[myitem.AnimeID] = ser;
                            }


                            CL_MissingFile missingFile = new CL_MissingFile();
                            missingFile.AnimeID = myitem.AnimeID;
                            missingFile.AnimeTitle = "Data Missing";
                            if (anime != null) missingFile.AnimeTitle = anime.MainTitle;
                            missingFile.EpisodeID = myitem.EpisodeID;
                            AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(myitem.EpisodeID);
                            missingFile.EpisodeNumber = -1;
                            missingFile.EpisodeType = 1;
                            if (ep != null)
                            {
                                missingFile.EpisodeNumber = ep.EpisodeNumber;
                                missingFile.EpisodeType = ep.EpisodeType;
                            }
                            missingFile.FileID = myitem.FileID;

                            if (ser == null) missingFile.AnimeSeries = null;
                            else missingFile.AnimeSeries = ser.GetUserContract(userID);

                            contracts.Add(missingFile);
                        }
                    }
                }
                contracts = contracts.OrderBy(a => a.AnimeTitle).ThenBy(a => a.EpisodeID).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public void RemoveMissingMyListFiles(List<CL_MissingFile> myListFiles)
        {
            foreach (CL_MissingFile missingFile in myListFiles)
            {
                CommandRequest_DeleteFileFromMyList cmd = new CommandRequest_DeleteFileFromMyList(missingFile.FileID);
                cmd.Save();

                // For deletion of files from Trakt, we will rely on the Daily sync
                // lets also try removing from the users trakt collecion
            }
        }

        public List<CL_AnimeSeries_User> GetSeriesWithoutAnyFiles(int userID)
        {
            List<CL_AnimeSeries_User> contracts = new List<CL_AnimeSeries_User>();


            try
            {
                foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                {
                    if (RepoFactory.VideoLocal.GetByAniDBAnimeID(ser.AniDB_ID).Count == 0)
                    {
                        CL_AnimeSeries_User can = ser.GetUserContract(userID);
                        if (can != null)
                            contracts.Add(can);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }


        public void DeleteFileFromMyList(int fileID)
        {
            CommandRequest_DeleteFileFromMyList cmd = new CommandRequest_DeleteFileFromMyList(fileID);
            cmd.Save();
        }

        public List<CL_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly,
            int airingState)
        {
            List<CL_MissingEpisode> contracts = new List<CL_MissingEpisode>();

            AiringState airState = (AiringState) airingState;

            Dictionary<int, SVR_AniDB_Anime> animeCache = new Dictionary<int, SVR_AniDB_Anime>();
            Dictionary<int, List<CL_GroupVideoQuality>> gvqCache =
                new Dictionary<int, List<CL_GroupVideoQuality>>();
            Dictionary<int, List<CL_GroupFileSummary>> gfqCache =
                new Dictionary<int, List<CL_GroupFileSummary>>();

            try
            {
                int i = 0;
                IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    i++;
                    //string msg = string.Format("Updating series {0} of {1} ({2}) -  {3}", i, allSeries.Count, ser.Anime.MainTitle, DateTime.Now);
                    //logger.Debug(msg);

                    //if (ser.Anime.AnimeID != 69) continue;

                    int missingEps = ser.MissingEpisodeCount;
                    if (onlyMyGroups) missingEps = ser.MissingEpisodeCountGroups;

                    bool finishedAiring = ser.GetAnime().GetFinishedAiring();

                    if (!finishedAiring && airState == AiringState.FinishedAiring) continue;
                    if (finishedAiring && airState == AiringState.StillAiring) continue;

                    DateTime start = DateTime.Now;
                    TimeSpan ts = DateTime.Now - start;

                    double totalTiming = 0;
                    double timingVids = 0;
                    double timingSeries = 0;
                    double timingAnime = 0;
                    double timingQuality = 0;
                    double timingEps = 0;
                    double timingAniEps = 0;
                    int epCount = 0;

                    DateTime oStart = DateTime.Now;

                    if (missingEps > 0)
                    {
                        // find the missing episodes
                        start = DateTime.Now;
                        List<SVR_AnimeEpisode> eps = ser.GetAnimeEpisodes();
                        ts = DateTime.Now - start;
                        timingEps += ts.TotalMilliseconds;

                        epCount = eps.Count;
                        foreach (SVR_AnimeEpisode aep in ser.GetAnimeEpisodes())
                        {
                            if (regularEpisodesOnly && aep.EpisodeTypeEnum != enEpisodeType.Episode) continue;

                            AniDB_Episode aniep = aep.AniDB_Episode;
                            if (aniep.GetFutureDated()) continue;

                            start = DateTime.Now;
                            List<SVR_VideoLocal> vids = aep.GetVideoLocals();
                            ts = DateTime.Now - start;
                            timingVids += ts.TotalMilliseconds;

                            if (vids.Count == 0)
                            {
                                CL_MissingEpisode cl = new CL_MissingEpisode();
                                cl.AnimeID = ser.AniDB_ID;
                                start = DateTime.Now;
                                cl.AnimeSeries = ser.GetUserContract(userID);
                                ts = DateTime.Now - start;
                                timingSeries += ts.TotalMilliseconds;

                                SVR_AniDB_Anime anime = null;
                                if (animeCache.ContainsKey(ser.AniDB_ID))
                                    anime = animeCache[ser.AniDB_ID];
                                else
                                {
                                    start = DateTime.Now;
                                    anime = ser.GetAnime();
                                    ts = DateTime.Now - start;
                                    timingAnime += ts.TotalMilliseconds;
                                    animeCache[ser.AniDB_ID] = anime;
                                }

                                cl.AnimeTitle = anime.MainTitle;

                                start = DateTime.Now;
                                cl.GroupFileSummary = "";
                                List<CL_GroupVideoQuality> summ = null;
                                if (gvqCache.ContainsKey(ser.AniDB_ID))
                                    summ = gvqCache[ser.AniDB_ID];
                                else
                                {
                                    summ = GetGroupVideoQualitySummary(anime.AnimeID);
                                    gvqCache[ser.AniDB_ID] = summ;
                                }

                                foreach (CL_GroupVideoQuality gvq in summ)
                                {
                                    if (cl.GroupFileSummary.Length > 0)
                                        cl.GroupFileSummary += " --- ";

                                    cl.GroupFileSummary += string.Format("{0} - {1}/{2}/{3}bit ({4})",
                                        gvq.GroupNameShort, gvq.Resolution,
                                        gvq.VideoSource, gvq.VideoBitDepth, gvq.NormalEpisodeNumberSummary);
                                }

                                cl.GroupFileSummarySimple = "";
                                List<CL_GroupFileSummary> summFiles = null;
                                if (gfqCache.ContainsKey(ser.AniDB_ID))
                                    summFiles = gfqCache[ser.AniDB_ID];
                                else
                                {
                                    summFiles = GetGroupFileSummary(anime.AnimeID);
                                    gfqCache[ser.AniDB_ID] = summFiles;
                                }

                                foreach (CL_GroupFileSummary gfq in summFiles)
                                {
                                    if (cl.GroupFileSummarySimple.Length > 0)
                                        cl.GroupFileSummarySimple += ", ";

                                    cl.GroupFileSummarySimple += string.Format("{0} ({1})", gfq.GroupNameShort,
                                        gfq.NormalEpisodeNumberSummary);
                                }

                                ts = DateTime.Now - start;
                                timingQuality += ts.TotalMilliseconds;
                                animeCache[ser.AniDB_ID] = anime;

                                start = DateTime.Now;
                                cl.EpisodeID = aniep.EpisodeID;
                                cl.EpisodeNumber = aniep.EpisodeNumber;
                                cl.EpisodeType = aniep.EpisodeType;
                                contracts.Add(cl);
                                ts = DateTime.Now - start;
                                timingAniEps += ts.TotalMilliseconds;
                            }
                        }

                        ts = DateTime.Now - oStart;
                        totalTiming = ts.TotalMilliseconds;

                        string msg2 =
                            string.Format("Timing for series {0} ({1}) : {2}/{3}/{4}/{5}/{6}/{7} - {8} eps (AID: {9})",
                                ser.GetAnime().MainTitle, totalTiming, timingVids, timingSeries,
                                timingAnime, timingQuality, timingEps, timingAniEps, epCount, ser.GetAnime().AnimeID);
                        //logger.Debug(msg2);
                    }
                }
                contracts = contracts.OrderBy(a=>a.AnimeTitle).ThenBy(a=>a.EpisodeType).ThenBy(a=>a.EpisodeNumber).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public void IgnoreAnime(int animeID, int ignoreType, int userID)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return;

                IgnoreAnime ignore = RepoFactory.IgnoreAnime.GetByAnimeUserType(animeID, userID, ignoreType);
                if (ignore != null) return; // record already exists

                ignore = new IgnoreAnime();
                ignore.AnimeID = animeID;
                ignore.IgnoreType = ignoreType;
                ignore.JMMUserID = userID;

                RepoFactory.IgnoreAnime.Save(ignore);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }


        public bool CheckTraktLinkValidity(string slug, bool removeDBEntries)
        {
            try
            {
                return TraktTVHelper.CheckTraktValidity(slug, removeDBEntries);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return false;
        }

        public List<CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs()
        {
            try
            { 
                return RepoFactory.CrossRef_AniDB_TraktV2.GetAll().Cast<CrossRef_AniDB_TraktV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CrossRef_AniDB_TraktV2>();
        }

        public List<CL_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID)
        {
            List<CL_Trakt_CommentUser> comments = new List<CL_Trakt_CommentUser>();

            try
            {
                
                List<TraktV2Comment> commentsTemp = TraktTVHelper.GetShowCommentsV2(animeID);
                if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                foreach (TraktV2Comment sht in commentsTemp)
                {
                    CL_Trakt_CommentUser comment = new CL_Trakt_CommentUser();

                    Trakt_Friend traktFriend = RepoFactory.Trakt_Friend.GetByUsername(sht.user.username);

                    // user details
                    comment.User = new CL_Trakt_User();
                    if (traktFriend == null)
                        comment.User.Trakt_FriendID = 0;
                    else
                        comment.User.Trakt_FriendID = traktFriend.Trakt_FriendID;

                    comment.User.Username = sht.user.username;
                    comment.User.Full_name = sht.user.name;

                    // comment details
                    comment.Comment = new CL_Trakt_Comment();
                    comment.Comment.CommentType = (int) TraktActivityType.Show; // episode or show
                    comment.Comment.Text = sht.comment;
                    comment.Comment.Spoiler = sht.spoiler;
                    comment.Comment.Inserted = sht.CreatedAtDate;

                    // urls
                    comment.Comment.Comment_Url = string.Format(TraktURIs.WebsiteComment, sht.id);

                    comments.Add(comment);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return comments;
        }

        public AniDB_Vote GetUserVote(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Vote.GetByEntity(animeID).FirstOrDefault();

            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType)
        {
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null) return;

                SVR_AnimeEpisode_User epUserRecord = ep.GetUserRecord(userID);

                if (epUserRecord == null)
                {
                    epUserRecord = new SVR_AnimeEpisode_User();
                    epUserRecord.PlayedCount = 0;
                    epUserRecord.StoppedCount = 0;
                    epUserRecord.WatchedCount = 0;
                }
                epUserRecord.AnimeEpisodeID = ep.AnimeEpisodeID;
                epUserRecord.AnimeSeriesID = ep.AnimeSeriesID;
                epUserRecord.JMMUserID = userID;
                //epUserRecord.WatchedDate = DateTime.Now;

                switch ((StatCountType) statCountType)
                {
                    case StatCountType.Played:
                        epUserRecord.PlayedCount++;
                        break;
                    case StatCountType.Stopped:
                        epUserRecord.StoppedCount++;
                        break;
                    case StatCountType.Watched:
                        epUserRecord.WatchedCount++;
                        break;
                }

                RepoFactory.AnimeEpisode_User.Save(epUserRecord);

                SVR_AnimeSeries ser = ep.GetAnimeSeries();
                if (ser == null) return;

                SVR_AnimeSeries_User userRecord = ser.GetUserRecord(userID);
                if (userRecord == null)
                    userRecord = new SVR_AnimeSeries_User(userID, ser.AnimeSeriesID);

                switch ((StatCountType) statCountType)
                {
                    case StatCountType.Played:
                        userRecord.PlayedCount++;
                        break;
                    case StatCountType.Stopped:
                        userRecord.StoppedCount++;
                        break;
                    case StatCountType.Watched:
                        userRecord.WatchedCount++;
                        break;
                }

                RepoFactory.AnimeSeries_User.Save(userRecord);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<CL_IgnoreAnime> GetIgnoredAnime(int userID)
        {

            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return new List<CL_IgnoreAnime>();

                return RepoFactory.IgnoreAnime.GetByUser(userID).Select(a=>a.ToClient()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return new List<CL_IgnoreAnime>();
        }


        public void RemoveIgnoreAnime(int ignoreAnimeID)
        {
            try
            {
                IgnoreAnime ignore = RepoFactory.IgnoreAnime.GetByID(ignoreAnimeID);
                if (ignore == null) return;

                RepoFactory.IgnoreAnime.Delete(ignoreAnimeID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID)
        {
            try
            {

                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return;

                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null) return;

                grp.DefaultAnimeSeriesID = animeSeriesID;
                RepoFactory.AnimeGroup.Save(grp, false, false);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void RemoveDefaultSeriesForGroup(int animeGroupID)
        {
            try
            {

                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return;

                grp.DefaultAnimeSeriesID = null;
                RepoFactory.AnimeGroup.Save(grp, false, false);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<TvDB_Language> GetTvDBLanguages()
        {
            try
            {
                return ShokoService.TvdbHelper.GetLanguages();

            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return new List<TvDB_Language>();
        }

        public void RefreshAllMediaInfo()
        {
            MainWindow.RefreshAllMediaInfo();
        }

        public CL_AnimeGroup_User GetTopLevelGroupForSeries(int animeSeriesID, int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.TopLevelAnimeGroup?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return null;
        }

        public void RecreateAllGroups(bool resume=false)
        {
            try
            {
                new AnimeGroupCreator().RecreateAllGroups();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public CL_AppVersions GetAppVersions()
        {
            try
            {
                //TODO WHEN WE HAVE A STABLE VERSION REPO, WE NEED TO CODE THE RETRIEVAL HERE.
                return new CL_AppVersions();

            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return null;
        }

        public AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID)
        {

            try
            {
                return RepoFactory.AniDB_Seiyuu.GetByID(seiyuuID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public FileFfdshowPreset GetFFDPreset(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return null;

                return RepoFactory.FileFfdshowPreset.GetByHashAndSize(vid.Hash, vid.FileSize);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public void DeleteFFDPreset(int videoLocalID)
        {
            try
            {

                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return;

                FileFfdshowPreset ffd = RepoFactory.FileFfdshowPreset.GetByHashAndSize(vid.Hash, vid.FileSize);
                if (ffd == null) return;

                RepoFactory.FileFfdshowPreset.Delete(ffd.FileFfdshowPresetID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void SaveFFDPreset(FileFfdshowPreset preset)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByHashAndSize(preset.Hash, preset.FileSize);
                if (vid == null) return;

                FileFfdshowPreset ffd = RepoFactory.FileFfdshowPreset.GetByHashAndSize(preset.Hash, preset.FileSize);
                if (ffd == null) ffd = new FileFfdshowPreset();

                ffd.FileSize = preset.FileSize;
                ffd.Hash = preset.Hash;
                ffd.Preset = preset.Preset;

                RepoFactory.FileFfdshowPreset.Save(ffd);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<CL_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID)
        {
            try
            {
                List<CL_VideoLocal> vids = new List<CL_VideoLocal>();

                FileSearchCriteria sType = (FileSearchCriteria) searchType;


                switch (sType)
                {
                    case FileSearchCriteria.Name:

                        List<SVR_VideoLocal> results1 = RepoFactory.VideoLocal.GetByName(searchCriteria.Trim());
                        foreach (SVR_VideoLocal vid in results1)
                            vids.Add(vid.ToClient(userID));

                        break;

                    case FileSearchCriteria.ED2KHash:

                        SVR_VideoLocal vidl = RepoFactory.VideoLocal.GetByHash(searchCriteria.Trim());
                        if (vidl!=null)
                            vids.Add(vidl.ToClient(userID));
                        
                        break;

                    case FileSearchCriteria.Size:

                        break;

                    case FileSearchCriteria.LastOneHundred:

                        List<SVR_VideoLocal> results2 = RepoFactory.VideoLocal.GetMostRecentlyAdded(100);
                        foreach (SVR_VideoLocal vid in results2)
                            vids.Add(vid.ToClient(userID));

                        break;
                }

                return vids;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_VideoLocal>();
        }

        /*public List<Contract_VideoLocalRenamed> RandomFileRenamePreview(int maxResults, int userID, string renameRules)
		{
			List<Contract_VideoLocalRenamed> ret = new List<Contract_VideoLocalRenamed>();
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetRandomFiles(maxResults))
				{
					Contract_VideoLocalRenamed vidRen = new Contract_VideoLocalRenamed();
					vidRen.VideoLocalID = vid.VideoLocalID;
					vidRen.VideoLocal = vid.ToContract(userID);
					vidRen.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);
					ret.Add(vidRen);
				}
			}
			catch (Exception ex)
			{
				logger.Error( ex,ex.ToString());
				
			}
			return ret;
		}*/

        public List<CL_VideoLocal> RandomFileRenamePreview(int maxResults, int userID)
        {
            try
            {
                return RepoFactory.VideoLocal.GetRandomFiles(maxResults).Select(a => a.ToClient(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<CL_VideoLocal>();
            }
        }

        public CL_VideoLocal_Renamed RenameFilePreview(int videoLocalID, string renameRules)
        {
            CL_VideoLocal_Renamed ret = new CL_VideoLocal_Renamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;

            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = string.Format("ERROR: Could not find file record");
                    ret.Success = false;
                }
                else
                {
                    if (videoLocalID == 726)
                        Debug.Write("test");

                    ret.VideoLocal = null;
                    ret.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);
                    if (string.IsNullOrEmpty(ret.NewFileName)) ret.Success = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                ret.VideoLocal = null;
                ret.NewFileName = string.Format("ERROR: {0}", ex.Message);
                ret.Success = false;
            }
            return ret;
        }

        public CL_VideoLocal_Renamed RenameFile(int videoLocalID, string renameRules)
        {
            CL_VideoLocal_Renamed ret = new CL_VideoLocal_Renamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = string.Format("ERROR: Could not find file record");
                    ret.Success = false;
                }
                else
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);

                    if (!string.IsNullOrEmpty(ret.NewFileName))
                    {
                        string name = string.Empty;
                        if (vid.Places.Count > 0)
                        {
                            foreach (SVR_VideoLocal_Place place in vid.Places)
                            {
                                // check if the file exists
                                string fullFileName = place.FullServerPath;
                                IFileSystem fs = place.ImportFolder.FileSystem;
                                FileSystemResult<IObject> obj = fs.Resolve(fullFileName);
                                if (!obj.IsOk || obj.Result is IDirectory)
                                {
                                    ret.NewFileName = "Error could not find the original file";
                                    ret.Success = false;
                                    return ret;
                                }

                                // actually rename the file
                                string path = Path.GetDirectoryName(fullFileName);
                                string newFullName = Path.Combine(path, ret.NewFileName);

                                try
                                {
                                    logger.Info(string.Format("Renaming file From ({0}) to ({1})....", fullFileName,
                                        newFullName));

                                    if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        logger.Info(string.Format(
                                            "Renaming file SKIPPED, no change From ({0}) to ({1})",
                                            fullFileName, newFullName));
                                        ret.NewFileName = newFullName;
                                    }
                                    else
                                    {
                                        string dir = Path.GetDirectoryName(newFullName);

                                        ((IFile) obj.Result).Rename(ret.NewFileName);
                                        logger.Info(string.Format("Renaming file SUCCESS From ({0}) to ({1})",
                                            fullFileName,
                                            newFullName));
                                        ret.NewFileName = newFullName;
                                        var tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
                                        place.FilePath = tup.Item2;
                                        name = Path.GetFileName(tup.Item2);
                                        RepoFactory.VideoLocalPlace.Save(place);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Info(string.Format("Renaming file FAIL From ({0}) to ({1}) - {2}",
                                        fullFileName,
                                        newFullName, ex.Message));
                                    logger.Error( ex,ex.ToString());
                                    ret.Success = false;
                                    ret.NewFileName = ex.Message;
                                }
                            }
                            vid.FileName = name;
                            RepoFactory.VideoLocal.Save(vid,true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                ret.VideoLocal = null;
                ret.NewFileName = string.Format("ERROR: {0}", ex.Message);
                ret.Success = false;
            }
            return ret;
        }

        public List<CL_VideoLocal_Renamed> RenameFiles(List<int> videoLocalIDs, string renameRules)
        {
            List<CL_VideoLocal_Renamed> ret = new List<CL_VideoLocal_Renamed>();
            try
            {
                foreach (int vid in videoLocalIDs)
                {
                    RenameFile(vid, renameRules);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return ret;
        }

        public List<CL_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID)
        {
            try
            {
                return RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Select(a => a.ToClient(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<CL_VideoLocal>();
        }

        public List<RenameScript> GetAllRenameScripts()
        {
            try
            {
                return RepoFactory.RenameScript.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<RenameScript>();
        }

        public CL_Response<RenameScript> SaveRenameScript(RenameScript contract)
        {
            CL_Response<RenameScript> response = new CL_Response<RenameScript>();
            response.ErrorMessage = "";
            response.Result = null;

            try
            {
                RenameScript script = null;
                if (contract.RenameScriptID!=0)
                {
                    // update
                    script = RepoFactory.RenameScript.GetByID(contract.RenameScriptID);
                    if (script == null)
                    {
                        response.ErrorMessage = "Could not find Rename Script ID: " +
                                                contract.RenameScriptID.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    script = new RenameScript();
                }

                if (string.IsNullOrEmpty(contract.ScriptName))
                {
                    response.ErrorMessage = "Must specify a Script Name";
                    return response;
                }

                // check to make sure we multiple scripts enable on import (only one can be selected)
                IReadOnlyList<RenameScript> allScripts = RepoFactory.RenameScript.GetAll();

                if (contract.IsEnabledOnImport == 1)
                {
                    foreach (RenameScript rs in allScripts)
                    {
                        if (rs.IsEnabledOnImport == 1 &&
                            (contract.RenameScriptID==0 || (contract.RenameScriptID != rs.RenameScriptID)))
                        {
                            rs.IsEnabledOnImport = 0;
                            RepoFactory.RenameScript.Save(rs);
                        }
                    }
                }

                script.IsEnabledOnImport = contract.IsEnabledOnImport;
                script.Script = contract.Script;
                script.ScriptName = contract.ScriptName;
                RepoFactory.RenameScript.Save(script);

                response.Result = script;
                
                return response;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string DeleteRenameScript(int renameScriptID)
        {
            try
            {

                RenameScript df = RepoFactory.RenameScript.GetByID(renameScriptID);
                if (df == null) return "Database entry does not exist";
                RepoFactory.RenameScript.Delete(renameScriptID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public List<AniDB_Recommendation> GetAniDBRecommendations(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Recommendation.GetByAnimeID(animeID).Cast<AniDB_Recommendation>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<AniDB_Recommendation>();
            }
        }

        public List<CL_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery)
        {
            List<CL_AnimeSearch> retTitles = new List<CL_AnimeSearch>();


            try
            {
                // check if it is a title search or an ID search
                int aid = 0;
                if (int.TryParse(titleQuery, out aid))
                {
                    // user is direct entering the anime id

                    // try the local database first
                    // if not download the data from AniDB now
                    SVR_AniDB_Anime anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(aid, false,
                        ServerSettings.AniDB_DownloadRelatedAnime);
                    if (anime != null)
                    {
                        CL_AnimeSearch res = new CL_AnimeSearch();
                        res.AnimeID = anime.AnimeID;
                        res.MainTitle = anime.MainTitle;
                        res.Titles =
                            new HashSet<string>(anime.AllTitles.Split(new char[] {'|'},
                                StringSplitOptions.RemoveEmptyEntries));

                        // check for existing series and group details
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                        if (ser != null)
                        {
                            res.SeriesExists = true;
                            res.AnimeSeriesID = ser.AnimeSeriesID;
                            res.AnimeSeriesName = anime.GetFormattedTitle();
                        }
                        else
                        {
                            res.SeriesExists = false;
                        }
                        retTitles.Add(res);
                    }
                }
                else
                {
                    // title search so look at the web cache
                    List<Shoko.Models.Azure.Azure_AnimeIDTitle> titles = AzureWebAPI.Get_AnimeTitle(titleQuery);

                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        ISessionWrapper sessionWrapper = session.Wrap();

                        foreach (Shoko.Models.Azure.Azure_AnimeIDTitle tit in titles)
                        {
                            CL_AnimeSearch res = new CL_AnimeSearch();
                            res.AnimeID = tit.AnimeID;
                            res.MainTitle = tit.MainTitle;
                            res.Titles =
                                new HashSet<string>(tit.Titles.Split(new char[] {'|'},
                                    StringSplitOptions.RemoveEmptyEntries));

                            // check for existing series and group details
                            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(tit.AnimeID);
                            if (ser != null)
                            {
                                res.SeriesExists = true;
                                res.AnimeSeriesID = ser.AnimeSeriesID;
                                res.AnimeSeriesName = ser.GetAnime().GetFormattedTitle();
                            }
                            else
                            {
                                res.SeriesExists = false;
                            }


                            retTitles.Add(res);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retTitles;
        }

        public string SetResumePosition(int videoLocalID, long resumeposition, int userID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video local record";
                vid.SetResumePosition(resumeposition, userID);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

        }




        public CL_Response<bool> PostTraktCommentShow(string traktID, string commentText, bool isSpoiler)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler);
        }


    }
}