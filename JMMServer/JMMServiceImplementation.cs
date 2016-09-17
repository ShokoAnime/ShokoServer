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

using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Commands;
using JMMServer.Commands.AniDB;
using JMMServer.Commands.MAL;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Providers.MovieDB;
using JMMServer.Providers.MyAnimeList;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TraktTV.Contracts;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;
using JMMServer.WebCache;
using NHibernate;
using NLog;
using NutzCode.CloudFileSystem;
using Directory = System.IO.Directory;
using JMMServer.Commands.TvDB;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;

namespace JMMServer
{
    [ServiceBehavior(MaxItemsInObjectGraph = int.MaxValue)]
    public class JMMServiceImplementation : IJMMServer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public List<Contract_AnimeGroup> GetAllGroups(int userID)
        {
            List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
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

        public List<Contract_AnimeGroup> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
        {
            List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
            try
            {
                int? grpid = animeGroupID;
                while (grpid.HasValue)
                {
                    grpid = null;
                    AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
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

        public List<Contract_AnimeGroup> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
        {
            List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {           
                    AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (series == null)
                        return grps;

                    foreach (AnimeGroup grp in series.AllGroupsAbove)
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

        public Contract_AnimeGroup GetGroup(int animeGroupID, int userID)
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

                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return "Group does not exist";

                int? parentGroupID = grp.AnimeGroupParentID;

                foreach (AnimeSeries ser in grp.GetAllSeries())
                {
                    DeleteAnimeSeries(ser.AnimeSeriesID, deleteFiles, false);
                }

                // delete all sub groups
                foreach (AnimeGroup subGroup in grp.GetAllChildGroups())
                {
                    DeleteAnimeGroup(subGroup.AnimeGroupID, deleteFiles);
                }
                List<GroupFilter> gfs =
                    RepoFactory.GroupFilter.GetWithConditionsTypes(new HashSet<GroupFilterConditionType>()
                    {
                        GroupFilterConditionType.AnimeGroup
                    });
                foreach (GroupFilter gf in gfs)
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
                    AnimeGroup grpParent = RepoFactory.AnimeGroup.GetByID(parentGroupID.Value);

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

        public List<Contract_AnimeGroup> GetAnimeGroupsForFilter(int groupFilterID, int userID,
            bool getSingleSeriesGroups)
        {
            List<Contract_AnimeGroup> retGroups = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return retGroups;
                    GroupFilter gf;
                    gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                    if ((gf != null) && gf.GroupsIds.ContainsKey(userID))
                        retGroups =
                            gf.GroupsIds[userID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                                .Where(a => a != null)
                                .Select(a => a.GetUserContract(userID))
                                .ToList();
                    if (getSingleSeriesGroups)
                    {
                        List<Contract_AnimeGroup> nGroups = new List<Contract_AnimeGroup>();
                        foreach (Contract_AnimeGroup cag in retGroups)
                        {
                            Contract_AnimeGroup ng = (Contract_AnimeGroup) cag.DeepCopy();
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
        public static AnimeSeries GetSeriesForGroup(int animeGroupID, List<AnimeSeries> allSeries)
        {
            try
            {
                foreach (AnimeSeries ser in allSeries)
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

        public Contract_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                    if (gf == null) return null;

                    JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return null;

                    Contract_GroupFilterExtended contract = gf.ToContractExtended(session, user);

                    return contract;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public List<Contract_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
        {
            List<Contract_GroupFilterExtended> gfs = new List<Contract_GroupFilterExtended>();
            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return gfs;
                List<GroupFilter> allGfs = RepoFactory.GroupFilter.GetAll();
                foreach (GroupFilter gf in allGfs)
                {
                    Contract_GroupFilter gfContract = gf.ToContract();
                    Contract_GroupFilterExtended gfeContract = new Contract_GroupFilterExtended();
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

        public List<Contract_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0)
        {
            List<Contract_GroupFilterExtended> gfs = new List<Contract_GroupFilterExtended>();
            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return gfs;
                List<GroupFilter> allGfs = gfparentid == 0 ? RepoFactory.GroupFilter.GetTopLevel() : RepoFactory.GroupFilter.GetByParentID(gfparentid);
                foreach (GroupFilter gf in allGfs)
                {
                    Contract_GroupFilter gfContract = gf.ToContract();
                    Contract_GroupFilterExtended gfeContract = new Contract_GroupFilterExtended();
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

        public List<Contract_GroupFilter> GetAllGroupFilters()
        {
            List<Contract_GroupFilter> gfs = new List<Contract_GroupFilter>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;

                    List<GroupFilter> allGfs = RepoFactory.GroupFilter.GetAll();
                    TimeSpan ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (GroupFilter gf in allGfs)
                    {
                        gfs.Add(gf.ToContract(session));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return gfs;
        }

        public List<Contract_GroupFilter> GetGroupFilters(int gfparentid = 0)
        {
            List<Contract_GroupFilter> gfs = new List<Contract_GroupFilter>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;

                    List<GroupFilter> allGfs = gfparentid == 0 ? RepoFactory.GroupFilter.GetTopLevel() : RepoFactory.GroupFilter.GetByParentID(gfparentid);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (GroupFilter gf in allGfs)
                    {
                        gfs.Add(gf.ToContract(session));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return gfs;
        }

        public Contract_GroupFilter GetGroupFilter(int gf)
        {
            try
            {
                return RepoFactory.GroupFilter.GetByID(gf)?.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public Contract_GroupFilter EvaluateGroupFilter(Contract_GroupFilter contract)
        {
            try
            {
                return GroupFilter.EvaluateContract(contract);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new Contract_GroupFilter();
            }
        }

        public List<Contract_Playlist> GetAllPlaylists()
        {
            List<Contract_Playlist> pls = new List<Contract_Playlist>();
            try
            {
                List<Playlist> allPls = RepoFactory.Playlist.GetAll();
                foreach (Playlist pl in allPls)
                    pls.Add(pl.ToContract());
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return pls;
        }

        #region Custom Tags

        public List<Contract_CustomTag> GetAllCustomTags()
        {
            try
            {
                List<Contract_CustomTag> ret = new List<Contract_CustomTag>();
                foreach (CustomTag ctag in RepoFactory.CustomTag.GetAll())
                    ret.Add(ctag.ToContract());

                return ret;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public Contract_CrossRef_CustomTag_SaveResponse SaveCustomTagCrossRef(Contract_CrossRef_CustomTag contract)
        {
            Contract_CrossRef_CustomTag_SaveResponse contractRet = new Contract_CrossRef_CustomTag_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                // this is an update
                CrossRef_CustomTag xref = null;
                if (contract.CrossRef_CustomTagID.HasValue)
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

                contractRet.CrossRef_CustomTag = xref.ToContract();
                AniDB_Anime.UpdateStatsByAnimeID(contract.CrossRefID);
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
                AniDB_Anime.UpdateStatsByAnimeID(crossRefID);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public Contract_CustomTag_SaveResponse SaveCustomTag(Contract_CustomTag contract)
        {
            Contract_CustomTag_SaveResponse contractRet = new Contract_CustomTag_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                // this is an update
                CustomTag ctag = null;
                if (contract.CustomTagID.HasValue)
                {
                    ctag = RepoFactory.CustomTag.GetByID(contract.CustomTagID.Value);
                    if (ctag == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing custom tag with ID: " +
                                                   contract.CustomTagID.Value.ToString();
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

                contractRet.CustomTag = ctag.ToContract();
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
                    AniDB_Anime.UpdateStatsByAnimeID(xref.CrossRefID);
                }


                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public Contract_CustomTag GetCustomTag(int customTagID)
        {
            try
            {
                return RepoFactory.CustomTag.GetByID(customTagID)?.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        #endregion

        public Contract_Playlist_SaveResponse SavePlaylist(Contract_Playlist contract)
        {
            Contract_Playlist_SaveResponse contractRet = new Contract_Playlist_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                

                // Process the playlist
                Playlist pl = null;
                if (contract.PlaylistID.HasValue)
                {
                    pl = RepoFactory.Playlist.GetByID(contract.PlaylistID.Value);
                    if (pl == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Playlist with ID: " +
                                                   contract.PlaylistID.Value.ToString();
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

                contractRet.Playlist = pl.ToContract();
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

        public Contract_Playlist GetPlaylist(int playlistID)
        {
            try
            {
                Playlist pl = RepoFactory.Playlist.GetByID(playlistID);
                if (pl == null)
                    return null;

                return pl.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_BookmarkedAnime> GetAllBookmarkedAnime()
        {
            List<Contract_BookmarkedAnime> baList = new List<Contract_BookmarkedAnime>();
            try
            {
                return RepoFactory.BookmarkedAnime.GetAll().Select(a => a.ToContract()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return baList;
        }

        public Contract_BookmarkedAnime_SaveResponse SaveBookmarkedAnime(Contract_BookmarkedAnime contract)
        {
            Contract_BookmarkedAnime_SaveResponse contractRet = new Contract_BookmarkedAnime_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {

                BookmarkedAnime ba = null;
                if (contract.BookmarkedAnimeID.HasValue)
                {
                    ba = RepoFactory.BookmarkedAnime.GetByID(contract.BookmarkedAnimeID.Value);
                    if (ba == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Bookmark with ID: " +
                                                   contract.BookmarkedAnimeID.Value.ToString();
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

                contractRet.BookmarkedAnime = ba.ToContract();
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

        public Contract_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {
                return RepoFactory.BookmarkedAnime.GetByID(bookmarkedAnimeID)?.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public Contract_GroupFilter_SaveResponse SaveGroupFilter(Contract_GroupFilter contract)
        {
            Contract_GroupFilter_SaveResponse response = new Contract_GroupFilter_SaveResponse();
            response.ErrorMessage = string.Empty;
            response.GroupFilter = null;



            // Process the group
            GroupFilter gf;
            if (contract.GroupFilterID.HasValue && contract.GroupFilterID.Value!=0)
            {
                gf = RepoFactory.GroupFilter.GetByID(contract.GroupFilterID.Value);
                if (gf == null)
                {
                    response.ErrorMessage = "Could not find existing Group Filter with ID: " +
                                            contract.GroupFilterID.Value.ToString();
                    return response;
                }
            }
            gf = GroupFilter.FromContract(contract);
            gf.EvaluateAnimeGroups();
            gf.EvaluateAnimeSeries();
            RepoFactory.GroupFilter.Save(gf);
            response.GroupFilter = gf.ToContract();
            return response;
        }

        public string DeleteGroupFilter(int groupFilterID)
        {
            try
            {
                GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
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

        public Contract_AnimeGroup_SaveResponse SaveGroup(Contract_AnimeGroup_Save contract, int userID)
        {
            Contract_AnimeGroup_SaveResponse contractout = new Contract_AnimeGroup_SaveResponse();
            contractout.ErrorMessage = "";
            contractout.AnimeGroup = null;
            try
            {
                AnimeGroup grp = null;
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
                    grp = new AnimeGroup();
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

                AnimeGroup_User userRecord = grp.GetUserRecord(userID);
                if (userRecord == null) userRecord = new AnimeGroup_User(userID, grp.AnimeGroupID);
                userRecord.IsFave = contract.IsFave;
                RepoFactory.AnimeGroup_User.Save(userRecord);

                contractout.AnimeGroup = grp.GetUserContract(userID);


                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public Contract_AnimeSeries_SaveResponse MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID)
        {
            Contract_AnimeSeries_SaveResponse contractout = new Contract_AnimeSeries_SaveResponse();
            contractout.ErrorMessage = "";
            contractout.AnimeSeries = null;
            try
            {
                AnimeSeries ser = null;

                ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                {
                    contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID.ToString();
                    return contractout;
                }

                // make sure the group exists
                AnimeGroup grpTemp = RepoFactory.AnimeGroup.GetByID(newAnimeGroupID);
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
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(oldGroupID);
                if (grp != null)
                {
					AnimeGroup topGroup = grp.TopLevelAnimeGroup;
					if (grp.GetAllSeries().Count == 0)
					{
                        RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);
					}
                    if (topGroup.AnimeGroupID!=grp.AnimeGroupID)
    					topGroup.UpdateStatsFromTopLevel(true, true, true);
                }

                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                    return contractout;
                }

                contractout.AnimeSeries = ser.GetUserContract(userID);

                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public Contract_AnimeSeries_SaveResponse SaveSeries(Contract_AnimeSeries_Save contract, int userID)
        {
            Contract_AnimeSeries_SaveResponse contractout = new Contract_AnimeSeries_SaveResponse();
            contractout.ErrorMessage = "";
            contractout.AnimeSeries = null;
            try
            {
               
                AnimeSeries ser = null;

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
                    ser = new AnimeSeries();
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

                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
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
                    AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(oldGroupID.Value);
                    if (grp != null)
                    {
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                    }
                }
                contractout.AnimeSeries = ser.GetUserContract(userID);
                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public Contract_AnimeEpisode GetEpisode(int animeEpisodeID, int userID)
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

        public List<AnimeEpisode> GetAllEpisodes()
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

        public Contract_AnimeEpisode GetEpisodeByAniDBEpisodeID(int episodeID, int userID)
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

                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
                    AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID.Value);
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
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (string.IsNullOrEmpty(vid.Hash))
                    return "Could not associate a cloud file without hash, hash it locally first";

                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
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

                AnimeSeries ser = ep.GetAnimeSeries();
                ser.EpisodeAddedDate = DateTime.Now;
                RepoFactory.AnimeSeries.Save(ser, false, true);

                //Update will re-save
                ser.QueueUpdateStats();


                foreach (AnimeGroup grp in ser.AllGroupsAbove)
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
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (vid.Hash == null)
                    return "Could not associate a cloud file without hash, hash it locally first";
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";
                for (int i = startEpNum; i <= endEpNum; i++)
                {
                    List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, i);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    AniDB_Episode aniep = anieps[0];

                    AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
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

                foreach (AnimeGroup grp in ser.AllGroupsAbove)
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


                AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";

                int epNumber = startingEpisodeNumber;
                int count = 1;


                foreach (int videoLocalID in videoLocalIDs)
                {
                    VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                    if (vid == null)
                        return "Could not find video local record";
                    if (vid.Hash == null)
                        return "Could not associate a cloud file without hash, hash it locally first";

                    List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, epNumber);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    AniDB_Episode aniep = anieps[0];

                    AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
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

                foreach (AnimeGroup grp in ser.AllGroupsAbove)
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

        public Contract_AnimeSeries_SaveResponse CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID)
        {
            Contract_AnimeSeries_SaveResponse response = new Contract_AnimeSeries_SaveResponse();
            response.AnimeSeries = null;
            response.ErrorMessage = "";
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    if (animeGroupID.HasValue)
                    {
                        AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID.Value);
                        if (grp == null)
                        {
                            response.ErrorMessage = "Could not find the specified group";
                            return response;
                        }
                    }

                    // make sure a series doesn't already exists for this anime
                    AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                    if (ser != null)
                    {
                        response.ErrorMessage = "A series already exists for this anime";
                        return response;
                    }

                    // make sure the anime exists first
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null)
                        anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, false, false);

                    if (anime == null)
                    {
                        response.ErrorMessage = "Could not get anime information from AniDB";
                        return response;
                    }

                    if (animeGroupID.HasValue)
                    {
                        ser = new AnimeSeries();
                        ser.Populate(anime);
                        ser.AnimeGroupID = animeGroupID.Value;
                        RepoFactory.AnimeSeries.Save(ser, false);
                    }
                    else
                    {
                        ser = anime.CreateAnimeSeriesAndGroup(session);
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
                    response.AnimeSeries = ser.GetUserContract(userID);
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
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, true, false);

                    // also find any files for this anime which don't have proper media info data
                    // we can usually tell this if the Resolution == '0x0'
                    foreach (VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
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
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
                JMMService.TvdbHelper.UpdateAllInfoAndImages(seriesID, false, true);
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
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
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

        public Contract_AniDBAnime GetAnime(int animeID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(session.Wrap(), animeID);
                    return anime?.Contract.AniDBAnime;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public List<Contract_AniDBAnime> GetAllAnime()
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract.AniDBAnime).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_AniDBAnime>();
        }

        public List<Contract_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState,
            int userID)
        {
            List<Contract_AnimeRating> contracts = new List<Contract_AnimeRating>();

            try
            {
                List<AnimeSeries> series = RepoFactory.AnimeSeries.GetAll();
                Dictionary<int, AnimeSeries> dictSeries = new Dictionary<int, AnimeSeries>();
                foreach (AnimeSeries ser in series)
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

                List<AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetAll();

                // user votes
                List<AniDB_Vote> allVotes = RepoFactory.AniDB_Vote.GetAll();

                JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return contracts;

                int i = 0;


                foreach (AniDB_Anime anime in animes)
                {
                    i++;

                    // evaluate collection states
                    if (_collectionState == RatingCollectionState.AllEpisodesInMyCollection)
                    {
                        if (!anime.FinishedAiring) continue;
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

                    Contract_AnimeRating contract = new Contract_AnimeRating();
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

        public List<Contract_AniDB_AnimeDetailed> GetAllAnimeDetailed()
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_AniDB_AnimeDetailed>();
        }

        public List<Contract_AnimeSeries> GetAllSeries(int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetAll().Select(a => a.GetUserContract(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_AnimeSeries>();
        }
        public Contract_Changes<Contract_GroupFilter> GetGroupFilterChanges(DateTime date)
        {
            Contract_Changes<Contract_GroupFilter> c=new Contract_Changes<Contract_GroupFilter>();
            try
            {
                Changes<int> changes = RepoFactory.GroupFilter.GetChangeTracker().GetChanges(date);
                c.ChangedItems = changes.ChangedItems.Select(a => RepoFactory.GroupFilter.GetByID(a).ToContract()).Where(a => a != null).ToList();
                c.RemovedItems = changes.RemovedItems.ToList();
                c.LastChange = changes.LastChange;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return c;
        }

        public Contract_MainChanges GetAllChanges(DateTime date, int userID)
        {
            Contract_MainChanges c=new Contract_MainChanges();
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
                c.Filters=new Contract_Changes<Contract_GroupFilter>();
                c.Filters.ChangedItems=changes[0].ChangedItems.Select(a=> RepoFactory.GroupFilter.GetByID(a).ToContract()).Where(a=>a!=null).ToList();
                c.Filters.RemovedItems= changes[0].RemovedItems.ToList();
                c.Filters.LastChange = changes[0].LastChange;

                //Add Group Filter that one of his child changed.
                bool end;
                do
                {
                    end = true;
                    foreach (Contract_GroupFilter ag in c.Filters.ChangedItems.Where(a => a.ParentGroupFilterID.HasValue && a.ParentGroupFilterID.Value != 0).ToList())
                    {
                        if (!c.Filters.ChangedItems.Any(a => a.GroupFilterID == ag.ParentGroupFilterID.Value))
                        {
                            end = false;
                            Contract_GroupFilter cag = RepoFactory.GroupFilter.GetByID(ag.ParentGroupFilterID.Value).ToContract();
                            if (cag != null)
                                c.Filters.ChangedItems.Add(cag);
                        }
                    }
                } while (!end);

                c.Groups=new Contract_Changes<Contract_AnimeGroup>();
                changes[1].ChangedItems.UnionWith(changes[2].ChangedItems);
                changes[1].ChangedItems.UnionWith(changes[2].RemovedItems);
                if (changes[2].LastChange > changes[1].LastChange)
                    changes[1].LastChange = changes[2].LastChange;
                c.Groups.ChangedItems=changes[1].ChangedItems.Select(a=> RepoFactory.AnimeGroup.GetByID(a)).Where(a => a != null).Select(a=>a.GetUserContract(userID)).ToList();



                c.Groups.RemovedItems = changes[1].RemovedItems.ToList();
                c.Groups.LastChange = changes[1].LastChange;
                c.Series=new Contract_Changes<Contract_AnimeSeries>();
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

        public Contract_AniDB_AnimeDetailed GetAnimeDetailed(int animeID)
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

        public List<Contract_AnimeGroup> GetSubGroupsForGroup(int animeGroupID, int userID)
        {
            List<Contract_AnimeGroup> retGroups = new List<Contract_AnimeGroup>();
            try
            {
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return retGroups;
                foreach (AnimeGroup grpChild in grp.GetChildGroups())
                {
                    Contract_AnimeGroup ugrp = grpChild.GetUserContract(userID);
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

        public List<Contract_AnimeSeries> GetSeriesForGroup(int animeGroupID, int userID)
        {
            List<Contract_AnimeSeries> series = new List<Contract_AnimeSeries>();
            try
            {
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (AnimeSeries ser in grp.GetSeries())
                {
                    Contract_AnimeSeries s = ser.GetUserContract(userID);
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

        public List<Contract_AnimeSeries> GetSeriesForGroupRecursive(int animeGroupID, int userID)
        {
            List<Contract_AnimeSeries> series = new List<Contract_AnimeSeries>();
            try
            {
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (AnimeSeries ser in grp.GetAllSeries())
                {
                    Contract_AnimeSeries s = ser.GetUserContract(userID);
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

        public List<Contract_AniDB_Episode> GetAniDBEpisodesForAnime(int animeID)
        {
            List<Contract_AniDB_Episode> eps = new List<Contract_AniDB_Episode>();
            try
            {
                List<AniDB_Episode> aniEpList = RepoFactory.AniDB_Episode.GetByAnimeID(animeID);

                foreach (AniDB_Episode ep in aniEpList)
                    eps.Add(ep.ToContract());
                eps = eps.OrderBy(a => a.EpisodeType).ThenBy(a => a.EpisodeNumber).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return eps;
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
                    CloudAccount cl = RepoFactory.CloudAccount.GetByID(cloudaccountid);
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

        public List<Contract_CloudAccount> GetCloudProviders()
        {
            List<Contract_CloudAccount> ls = new List<Contract_CloudAccount>();
            try
            {
                ls.Add(new CloudAccount() {Name = "NA", Provider = "Local File System"}.ToContactCloudProvider());
                RepoFactory.CloudAccount.GetAll().ForEach(a => ls.Add(a.ToContactCloudProvider()));
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return ls;
        }

        public void SetResumePosition(int videolocalid, int jmmuserID, long position)
        {
            try
            {
                VideoLocal_User vlu= RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(jmmuserID, videolocalid);
                if (vlu == null)
                {
                    vlu = new VideoLocal_User();
                    vlu.JMMUserID = jmmuserID;
                    vlu.VideoLocalID = videolocalid;
                    vlu.WatchedDate = null;
                }
                vlu.ResumePosition = position;
                RepoFactory.VideoLocalUser.Save(vlu);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<Contract_AnimeEpisode> GetEpisodesForSeries(int animeSeriesID, int userID)
        {
            List<Contract_AnimeEpisode> eps = new List<Contract_AnimeEpisode>();
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

        public List<Contract_AnimeEpisode> GetEpisodesForSeriesOld(int animeSeriesID)
        {
            List<Contract_AnimeEpisode> eps = new List<Contract_AnimeEpisode>();
            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(1) ?? RepoFactory.JMMUser.GetAll().FirstOrDefault(a => a.Username == "Default");
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

        public Contract_AnimeSeries GetSeries(int animeSeriesID, int userID)
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

        public List<Contract_AnimeSeries> GetSeriesByFolderID(int FolderID, int userID, int max)
        {
            try
            {
                int limit = 0;
                List<Contract_AnimeSeries> list = new List<Contract_AnimeSeries>();

                foreach (VideoLocal vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
                {
                    foreach (Contract_AnimeEpisode ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                    {
                        Contract_AnimeSeries ase = GetSeries(ae.AnimeSeriesID, userID);
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

        public List<Contract_AnimeSeriesFileStats> GetSeriesFileStatsByFolderID(int FolderID, int userID, int max)
        {
            try
            {
                int limit = 0;
                Dictionary<int, Contract_AnimeSeriesFileStats> list = new Dictionary<int, Contract_AnimeSeriesFileStats>();
                foreach (VideoLocal vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
                {
                    foreach (Contract_AnimeEpisode ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                    {
                        Contract_AnimeSeries ase = GetSeries(ae.AnimeSeriesID, userID);
                        Contract_AnimeSeriesFileStats asfs = null;
                        if (list.TryGetValue(ase.AnimeSeriesID, out asfs) == false)
                        {
                            limit++;
                            if (limit >= max)
                            {
                                continue;
                            }
                            asfs = new Contract_AnimeSeriesFileStats();
                            asfs.AnimeSeriesName = ase.AniDBAnime.AniDBAnime.MainTitle;
                            asfs.FileCount = 0;
                            asfs.FileSize = 0;
                            asfs.Folders = new List<string>();
                            asfs.AnimeSeriesID = ase.AnimeSeriesID;
                            list.Add(ase.AnimeSeriesID, asfs);
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

        public Contract_AnimeSeries GetSeriesForAnime(int animeID, int userID)
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
                AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
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

        public List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
        {
            try
            {
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                    return ep.GetVideoDetailedContracts(userID);
                else
                    return new List<Contract_VideoDetailed>();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_VideoDetailed>();
        }

        public List<Contract_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID)
        {
            List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
            try
            {
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                {
                    foreach (VideoLocal vid in ep.GetVideoLocals())
                    {
                        contracts.Add(vid.ToContract(userID));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<Contract_VideoLocal> GetIgnoredFiles(int userID)
        {
            List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
            try
            {
                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetIgnoredVideos())
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<Contract_VideoLocal> GetManuallyLinkedFiles(int userID)
        {
            List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
            try
            {
                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<Contract_VideoLocal> GetUnrecognisedFiles(int userID)
        {
            List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
            try
            {
                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetVideosWithoutEpisode())
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public Contract_ServerStatus GetServerStatus()
        {
            Contract_ServerStatus contract = new Contract_ServerStatus();

            try
            {
                contract.HashQueueCount = JMMService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState = JMMService.CmdProcessorHasher.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.HashQueueStateId = (int)JMMService.CmdProcessorHasher.QueueState.queueState;
                contract.HashQueueStateParams = JMMService.CmdProcessorHasher.QueueState.extraParams;

                contract.GeneralQueueCount = JMMService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState = JMMService.CmdProcessorGeneral.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.GeneralQueueStateId = (int)JMMService.CmdProcessorGeneral.QueueState.queueState;
                contract.GeneralQueueStateParams = JMMService.CmdProcessorGeneral.QueueState.extraParams;

                contract.ImagesQueueCount = JMMService.CmdProcessorImages.QueueCount; 
                contract.ImagesQueueState = JMMService.CmdProcessorImages.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.ImagesQueueStateId = (int)JMMService.CmdProcessorImages.QueueState.queueState;
                contract.ImagesQueueStateParams = JMMService.CmdProcessorImages.QueueState.extraParams;

                contract.IsBanned = JMMService.AnidbProcessor.IsBanned;
                contract.BanReason = JMMService.AnidbProcessor.BanTime.ToString();
                contract.BanOrigin = JMMService.AnidbProcessor.BanOrigin;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contract;
        }

        public Contract_ServerSettings_SaveResponse SaveServerSettings(Contract_ServerSettings contractIn)
        {
            Contract_ServerSettings_SaveResponse contract = new Contract_ServerSettings_SaveResponse();
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
                    JMMService.AnidbProcessor.ForceLogout();
                    JMMService.AnidbProcessor.CloseConnections();

                    Thread.Sleep(1000);
                    JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
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

        public Contract_ServerSettings GetServerSettings()
        {
            Contract_ServerSettings contract = new Contract_ServerSettings();

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

        public string SetResumePositionOnVideo(int videoLocalID, long resumeposition, int userID)
        {
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video local record";
                vid.SetResumePosition(resumeposition, userID);
                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }

        }
        public string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID)
        {
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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

        public Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            Contract_ToggleWatchedStatusOnEpisode_Response response =
                new Contract_ToggleWatchedStatusOnEpisode_Response();
            response.ErrorMessage = "";
            response.AnimeEpisode = null;

            try
            {
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null)
                {
                    response.ErrorMessage = "Could not find anime episode record";
                    return response;
                }

                ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, false, userID, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

                // refresh from db


                response.AnimeEpisode = ep.GetUserContract(userID);

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
                List<AnimeEpisode> eps = RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID);

                AnimeSeries ser = null;
                foreach (AnimeEpisode ep in eps)
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
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                anime.DisableExternalLinksFlag = flags;
                RepoFactory.AniDB_Anime.Save(anime);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public Contract_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
        {
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return null;

                return vid.ToContractDetailed(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_AnimeEpisode> GetEpisodesForFile(int videoLocalID, int userID)
        {
            List<Contract_AnimeEpisode> contracts = new List<Contract_AnimeEpisode>();
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return contracts;

                foreach (AnimeEpisode ep in vid.GetAnimeEpisodes())
                {
                    Contract_AnimeEpisode eps = ep.GetUserContract(userID);
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
        public List<Contract_AniDBReleaseGroup> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID)
        {
            DateTime start = DateTime.Now;

            List<Contract_AniDBReleaseGroup> relGroups = new List<Contract_AniDBReleaseGroup>();

            try
            {
                AniDB_Episode aniEp = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);
                if (aniEp == null) return relGroups;
                if (aniEp.EpisodeTypeEnum != AniDBAPI.enEpisodeType.Episode) return relGroups;

                AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(aniEp.AnimeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
                foreach (AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    List<VideoLocal> vids = ep.GetVideoLocals();
                    List<string> hashes = vids.Select(a => a.Hash).Distinct().ToList();
                    foreach (string s in hashes)
                    {
                        VideoLocal vid = vids.First(a => a.Hash == s);
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
                            Contract_AniDBReleaseGroup contract = new Contract_AniDBReleaseGroup();
                            contract.GroupID = gs.GroupID;
                            contract.GroupName = gs.GroupName;
                            contract.UserCollecting = true;
                            contract.EpisodeRange = gs.EpisodeRange;
                            contract.FileCount = userReleaseGroups[gs.GroupID];
                            relGroups.Add(contract);
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

        public List<Contract_ImportFolder> GetImportFolders()
        {
            List<Contract_ImportFolder> ifolders = new List<Contract_ImportFolder>();
            try
            {
                return RepoFactory.ImportFolder.GetAll().Select(a => a.ToContract()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return ifolders;
        }

        public Contract_ImportFolder_SaveResponse SaveImportFolder(Contract_ImportFolder contract)
        {
            Contract_ImportFolder_SaveResponse response = new Contract_ImportFolder_SaveResponse();
            response.ErrorMessage = "";
            response.ImportFolder = null;

            try
            {
                ImportFolder ns = null;
                if (contract.ImportFolderID.HasValue && contract.ImportFolderID != 0)
                {
                    // update
                    ns = RepoFactory.ImportFolder.GetByID(contract.ImportFolderID.Value);
                    if (ns == null)
                    {
                        response.ErrorMessage = "Could not find Import Folder ID: " +
                                                contract.ImportFolderID.Value.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    ns = new ImportFolder();
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

                if (!contract.ImportFolderID.HasValue)
                {
                    ImportFolder nsTemp = RepoFactory.ImportFolder.GetByImportLocation(contract.ImportFolderLocation);
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
                List<ImportFolder> allFolders = RepoFactory.ImportFolder.GetAll();

                if (contract.IsDropDestination == 1)
                {
                    foreach (ImportFolder imf in allFolders)
                    {
                        if (contract.CloudID==imf.CloudID && imf.IsDropDestination == 1 && (!contract.ImportFolderID.HasValue || (contract.ImportFolderID.Value != imf.ImportFolderID)))
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
                ns.CloudID = contract.CloudID;
                RepoFactory.ImportFolder.Save(ns);

                response.ImportFolder = ns.ToContract();

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
                List<VideoLocal> filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

                foreach (VideoLocal vl in filesWithoutEpisode.Where(a=>!string.IsNullOrEmpty(a.Hash)))
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
                List<VideoLocal> files = RepoFactory.VideoLocal.GetManuallyLinkedVideos();

                foreach (VideoLocal vl in files.Where(a=>!string.IsNullOrEmpty(a.Hash)))
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
            JMMService.CmdProcessorHasher.Paused = paused;
        }

        public void SetCommandProcessorGeneralPaused(bool paused)
        {
            JMMService.CmdProcessorGeneral.Paused = paused;
        }

        public void SetCommandProcessorImagesPaused(bool paused)
        {
            JMMService.CmdProcessorImages.Paused = paused;
        }

        public void ClearHasherQueue()
        {
            try
            {
                JMMService.CmdProcessorHasher.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestHasher());

                JMMService.CmdProcessorHasher.Init();
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
                JMMService.CmdProcessorImages.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestImages());
                JMMService.CmdProcessorImages.Init();
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
                JMMService.CmdProcessorGeneral.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestGeneral());
                JMMService.CmdProcessorGeneral.Init();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public void RehashFile(int videoLocalID)
        {
            VideoLocal vl = RepoFactory.VideoLocal.GetByID(videoLocalID);

            if (vl != null)
            {
                VideoLocal_Place pl = vl.GetBestVideoLocalPlace();
                if (pl == null)
                {
                    logger.Error("Unable to hash videolocal with id = {videLocalID}, it has no assigned place");
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
                JMMService.AnidbProcessor.ForceLogout();
                JMMService.AnidbProcessor.CloseConnections();
                Thread.Sleep(1000);

                log += "Init..." + Environment.NewLine;
                JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                    ServerSettings.AniDB_ServerAddress,
                    ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

                log += "Login..." + Environment.NewLine;
                if (JMMService.AnidbProcessor.Login())
                {
                    log += "Login Success!" + Environment.NewLine;
                    log += "Logout..." + Environment.NewLine;
                    JMMService.AnidbProcessor.ForceLogout();
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

        public bool TraktFriendRequestDeny(string friendUsername, ref string returnMessage)
        {
            return false;
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

        public bool TraktFriendRequestApprove(string friendUsername, ref string returnMessage)
        {
            return false;
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
                AnimeGroup.RenameAllGroups();
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

        public List<Contract_DuplicateFile> GetAllDuplicateFiles()
        {
            List<Contract_DuplicateFile> dupFiles = new List<Contract_DuplicateFile>();
            try
            {
                return RepoFactory.DuplicateFile.GetAll().Select(a=>a.ToContract()).ToList();
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
                    if (fileNumber == 1) fileName = df.FullServerPath1;
                    if (fileNumber == 2) fileName = df.FullServerPath2;
                    IFile file = VideoLocal.ResolveFile(fileName);
                    file?.Delete(true);
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
                VideoLocal_Place place = RepoFactory.VideoLocalPlace.GetByID(videolocalplaceid);
                if ((place==null) || (place.VideoLocal==null))
                    return "Database entry does not exist";
                VideoLocal vid = place.VideoLocal;
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
                FileSystemResult fs = file.Delete(true);
                if (fs == null || !fs.IsOk)
                {
                    logger.Error($"Unable to delete file '{place.FullServerPath}'");
                    return $"Unable to delete file '{place.FullServerPath}'";
                }
                if (place.VideoLocal.Places.Count > 1)
                    return "";
                AnimeSeries ser = null;
                List<AnimeEpisode> animeEpisodes = vid.GetAnimeEpisodes();
                if (animeEpisodes.Count > 0)
                    ser = animeEpisodes[0].GetAnimeSeries();


                CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
                cmdDel.Save();
                RepoFactory.VideoLocalPlace.Delete(place.VideoLocal_Place_ID);
                RepoFactory.VideoLocal.Delete(vid.VideoLocalID);
                if (ser != null)
                {
                    ser.QueueUpdateStats();
                }

                // For deletion of files from Trakt, we will rely on the Daily sync


                return "";
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }
        }

        public List<Contract_VideoLocal> GetAllManuallyLinkedFiles(int userID)
        {
            List<Contract_VideoLocal> manualFiles = new List<Contract_VideoLocal>();
            try
            {
                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
                {
                    manualFiles.Add(vid.ToContract(userID));
                }

                return manualFiles;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return manualFiles;
            }
        }

        public List<Contract_AnimeEpisode> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries,
            bool ignoreVariations)
        {
            List<Contract_AnimeEpisode> eps = new List<Contract_AnimeEpisode>();
            try
            {

                Dictionary<int, int> dictSeriesAnime = new Dictionary<int, int>();
                Dictionary<int, bool> dictAnimeFinishedAiring = new Dictionary<int, bool>();
                Dictionary<int, bool> dictSeriesFinishedAiring = new Dictionary<int, bool>();

                if (onlyFinishedSeries)
                {
                    List<AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                    foreach (AnimeSeries ser in allSeries)
                        dictSeriesAnime[ser.AnimeSeriesID] = ser.AniDB_ID;

                    List<AniDB_Anime> allAnime = RepoFactory.AniDB_Anime.GetAll();
                    foreach (AniDB_Anime anime in allAnime)
                        dictAnimeFinishedAiring[anime.AnimeID] = anime.FinishedAiring;

                    foreach (KeyValuePair<int, int> kvp in dictSeriesAnime)
                    {
                        if (dictAnimeFinishedAiring.ContainsKey(kvp.Value))
                            dictSeriesFinishedAiring[kvp.Key] = dictAnimeFinishedAiring[kvp.Value];
                    }
                }

                foreach (AnimeEpisode ep in RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(ignoreVariations))
                {
                    if (onlyFinishedSeries)
                    {
                        bool finishedAiring = false;
                        if (dictSeriesFinishedAiring.ContainsKey(ep.AnimeSeriesID))
                            finishedAiring = dictSeriesFinishedAiring[ep.AnimeSeriesID];

                        if (!finishedAiring) continue;
                    }
                    Contract_AnimeEpisode cep = ep.GetUserContract(userID);
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
                    if (df.ImportFolder1 == null || df.ImportFolder2 == null)
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
                    if (df.FullServerPath1.Equals(df.FullServerPath2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as they are actually point to the same file: {0}",
                                df.FullServerPath1);
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                    }

                    // check if both files still exist
                    IFile file1 = VideoLocal.ResolveFile(df.FullServerPath1);
                    IFile file2 = VideoLocal.ResolveFile(df.FullServerPath2);
                    if (file1==null || file2==null)
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as one of the files can't be found: {0} --- {1}",
                                df.FullServerPath1, df.FullServerPath2);
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

        public List<Contract_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
            string resolution,
            string videoSource, int videoBitDepth, int userID)
        {
            List<Contract_VideoDetailed> vids = new List<Contract_VideoDetailed>();

            try
            {
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {
                    int thisBitDepth = 8;

                    int bitDepth = 0;
                    if (int.TryParse(vid.VideoBitDepth, out bitDepth))
                        thisBitDepth = bitDepth;

                    List<AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    AnimeEpisode animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == enEpisodeType.Special)
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
                                vids.Add(vid.ToContractDetailed(userID));
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
                                vids.Add(vid.ToContractDetailed(userID));
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

        public List<Contract_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID)
        {
            List<Contract_VideoDetailed> vids = new List<Contract_VideoDetailed>();

            try
            {
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {

                    List<AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    AnimeEpisode animeEp = eps[0];
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
                                vids.Add(vid.ToContractDetailed(userID));
                            }
                        }
                        else
                        {
                            if (relGroupName.Equals(Constants.NO_GROUP_INFO, StringComparison.InvariantCultureIgnoreCase))
                            {
                                vids.Add(vid.ToContractDetailed(userID));
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

        public List<Contract_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID)
        {
            List<Contract_GroupVideoQuality> vidQuals = new List<Contract_GroupVideoQuality>();



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
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                ts = DateTime.Now - start;
                timingAnime += ts.TotalMilliseconds;

                if (anime == null) return vidQuals;

                start = DateTime.Now;
                ts = DateTime.Now - start;
                timingVids += ts.TotalMilliseconds;


                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {

                    start = DateTime.Now;
                    List<AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    ts = DateTime.Now - start;
                    timingEps += ts.TotalMilliseconds;

                    if (eps.Count == 0) continue;
                    foreach (AnimeEpisode animeEp in eps)
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
                                foreach (Contract_GroupVideoQuality contract in vidQuals)
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
                                    Contract_GroupVideoQuality contract = new Contract_GroupVideoQuality();
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
                                foreach (Contract_GroupVideoQuality contract in vidQuals)
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
                                    Contract_GroupVideoQuality contract = new Contract_GroupVideoQuality();
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
                foreach (Contract_GroupVideoQuality contract in vidQuals)
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

                vidQuals.Sort();
                return vidQuals;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return vidQuals;
            }
        }


        public List<Contract_GroupFileSummary> GetGroupFileSummary(int animeID)
        {
            List<Contract_GroupFileSummary> vidQuals = new List<Contract_GroupFileSummary>();

            try
            {
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                if (anime == null) return vidQuals;


                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {

                    List<AnimeEpisode> eps = vid.GetAnimeEpisodes();

                    if (eps.Count == 0) continue;

                    foreach (AnimeEpisode animeEp in eps)
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
                                foreach (Contract_GroupFileSummary contract in vidQuals)
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
                                    Contract_GroupFileSummary contract = new Contract_GroupFileSummary();
                                    contract.FileCountNormal = 0;
                                    contract.FileCountSpecials = 0;
                                    contract.TotalFileSize = 0;
                                    contract.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) contract.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) contract.FileCountSpecials++;
                                    contract.TotalFileSize += aniFile.FileSize;
                                    contract.TotalRunningTime += aniFile.File_LengthSeconds;

                                    contract.GroupName = aniFile.Anime_GroupName;
                                    contract.GroupNameShort = aniFile.Anime_GroupNameShort;
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
                                bool foundSummaryRecord = false;
                                foreach (Contract_GroupFileSummary contract in vidQuals)
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
                                    Contract_GroupFileSummary contract = new Contract_GroupFileSummary();
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

                                    contract.GroupName = "NO GROUP INFO";
                                    contract.GroupNameShort = "NO GROUP INFO";
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

                foreach (Contract_GroupFileSummary contract in vidQuals)
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

                vidQuals.Sort();
                return vidQuals;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return vidQuals;
            }
        }

        public Contract_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
        {
            Contract_AniDB_AnimeCrossRefs result = new Contract_AniDB_AnimeCrossRefs();
            result.AnimeID = animeID;

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime == null) return result;


                    


                    // TvDB
                    foreach (CrossRef_AniDB_TvDBV2 xref in anime.GetCrossRefTvDBV2())
                    {
                        result.CrossRef_AniDB_TvDB.Add(xref.ToContract());

                        TvDB_Series ser = RepoFactory.TvDB_Series.GetByTvDBID(sessionWrapper, xref.TvDBID);
                        if (ser != null)
                            result.TvDBSeries.Add(ser.ToContract());

                        foreach (TvDB_Episode ep in anime.GetTvDBEpisodes())
                            result.TvDBEpisodes.Add(ep.ToContract());

                        foreach (TvDB_ImageFanart fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(sessionWrapper, xref.TvDBID))
                            result.TvDBImageFanarts.Add(fanart.ToContract());

                        foreach (TvDB_ImagePoster poster in RepoFactory.TvDB_ImagePoster.GetBySeriesID(sessionWrapper, xref.TvDBID))
                            result.TvDBImagePosters.Add(poster.ToContract());

                        foreach (TvDB_ImageWideBanner banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(xref.TvDBID))
                            result.TvDBImageWideBanners.Add(banner.ToContract());
                    }

                    // Trakt

                     
                    foreach (CrossRef_AniDB_TraktV2 xref in anime.GetCrossRefTraktV2())
                    {
                        result.CrossRef_AniDB_Trakt.Add(xref.ToContract());

                        Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(session, xref.TraktID);
                        if (show != null)
                        {
                            result.TraktShows.Add(show.ToContract());

                            foreach (Trakt_ImageFanart fanart in RepoFactory.Trakt_ImageFanart.GetByShowID(session, show.Trakt_ShowID))
                                result.TraktImageFanarts.Add(fanart.ToContract());

                            foreach (Trakt_ImagePoster poster in RepoFactory.Trakt_ImagePoster.GetByShowID(session, show.Trakt_ShowID)
                                )
                                result.TraktImagePosters.Add(poster.ToContract());
                        }
                    }


                    // MovieDB
                    CrossRef_AniDB_Other xrefMovie = anime.GetCrossRefMovieDB();
                    if (xrefMovie == null)
                        result.CrossRef_AniDB_MovieDB = null;
                    else
                        result.CrossRef_AniDB_MovieDB = xrefMovie.ToContract();


                    MovieDB_Movie movie = anime.GetMovieDBMovie();
                    if (movie == null)
                        result.MovieDBMovie = null;
                    else
                        result.MovieDBMovie = movie.ToContract();

                    foreach (MovieDB_Fanart fanart in anime.GetMovieDBFanarts())
                    {
                        if (fanart.ImageSize.Equals(Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBFanarts.Add(fanart.ToContract());
                    }

                    foreach (MovieDB_Poster poster in anime.GetMovieDBPosters())
                    {
                        if (poster.ImageSize.Equals(Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBPosters.Add(poster.ToContract());
                    }

                    // MAL
                    List<CrossRef_AniDB_MAL> xrefMAL = anime.GetCrossRefMAL();
                    if (xrefMAL == null)
                        result.CrossRef_AniDB_MAL = null;
                    else
                    {
                        result.CrossRef_AniDB_MAL = new List<Contract_CrossRef_AniDB_MAL>();
                        foreach (CrossRef_AniDB_MAL xrefTemp in xrefMAL)
                            result.CrossRef_AniDB_MAL.Add(xrefTemp.ToContract());
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

                        AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageID);
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

                AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
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
                string res = JMMServer.Providers.Azure.AzureWebAPI.Admin_AuthUser();
                return string.IsNullOrEmpty(res);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return false;
            }
        }

        public Contract_Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType)
        {
            try
            {
                AzureLinkType lType = (AzureLinkType) linkType;
                JMMServer.Providers.Azure.Azure_AnimeLink link = null;

                switch (lType)
                {
                    case AzureLinkType.TvDB:
                        link = JMMServer.Providers.Azure.AzureWebAPI.Admin_GetRandomTvDBLinkForApproval();
                        break;
                    case AzureLinkType.Trakt:
                        link = JMMServer.Providers.Azure.AzureWebAPI.Admin_GetRandomTraktLinkForApproval();
                        break;
                }


                if (link != null)
                    return link.ToContract();

                return null;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_AdminMessage> GetAdminMessages()
        {
            try
            {
                List<Contract_AdminMessage> msgs = new List<Contract_AdminMessage>();

                if (ServerInfo.Instance.AdminMessages != null)
                {
                    foreach (JMMServer.Providers.Azure.AdminMessage msg in ServerInfo.Instance.AdminMessages)
                        msgs.Add(msg.ToContract());
                }

                return msgs;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        #region Admin - TvDB

        public string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
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
                return JMMServer.Providers.Azure.AzureWebAPI.Admin_Revoke_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
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

                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<JMMServer.Providers.Azure.CrossRef_AniDB_TvDB> results =
                    JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (JMMServer.Providers.Azure.CrossRef_AniDB_TvDB xref in results)
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
                    Providers.Azure.AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                if (results != null)
                {
                    List<JMMServer.Providers.Azure.CrossRef_AniDB_TvDB> linksToApprove =
                        new List<Providers.Azure.CrossRef_AniDB_TvDB>();
                    foreach (JMMServer.Providers.Azure.CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (JMMServer.Providers.Azure.CrossRef_AniDB_TvDB xref in linksToApprove)
                    {
                        JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(
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
                return JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
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
                return JMMServer.Providers.Azure.AzureWebAPI.Admin_Revoke_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
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

                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<JMMServer.Providers.Azure.CrossRef_AniDB_Trakt> results =
                    JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (JMMServer.Providers.Azure.CrossRef_AniDB_Trakt xref in results)
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
                    Providers.Azure.AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                if (results != null)
                {
                    List<JMMServer.Providers.Azure.CrossRef_AniDB_Trakt> linksToApprove =
                        new List<Providers.Azure.CrossRef_AniDB_Trakt>();
                    foreach (JMMServer.Providers.Azure.CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (JMMServer.Providers.Azure.CrossRef_AniDB_Trakt xref in linksToApprove)
                    {
                        JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(
                            xref.CrossRef_AniDB_TraktId.Value);
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

        public List<Contract_Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                List<Contract_Azure_CrossRef_AniDB_TvDB> contracts = new List<Contract_Azure_CrossRef_AniDB_TvDB>();
                List<JMMServer.Providers.Azure.CrossRef_AniDB_TvDB> results = null;

                if (isAdmin)
                    results = JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                else
                    results = JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBTvDB(animeID);
                if (results == null || results.Count == 0) return contracts;

                foreach (JMMServer.Providers.Azure.CrossRef_AniDB_TvDB xref in results)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }


        public List<Contract_CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID).Select(a => a.ToContract()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_CrossRef_AniDB_TvDB_Episode> GetTVDBCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID).Select(a=>a.ToContract()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }


        public List<Contract_TVDBSeriesSearchResult> SearchTheTvDB(string criteria)
        {
            List<Contract_TVDBSeriesSearchResult> results = new List<Contract_TVDBSeriesSearchResult>();
            try
            {
                List<TVDBSeriesSearchResult> tvResults = JMMService.TvdbHelper.SearchSeries(criteria);

                foreach (TVDBSeriesSearchResult res in tvResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return results;
            }
        }


        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh data from TvDB
                JMMService.TvdbHelper.UpdateAllInfoAndImages(seriesID, true, false);

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
            
                if (crossRef_AniDB_TvDBV2ID.HasValue)
                {
                    CrossRef_AniDB_TvDBV2 xrefTemp = RepoFactory.CrossRef_AniDB_TvDBV2.GetByID(crossRef_AniDB_TvDBV2ID.Value);
                    // delete the existing one if we are updating
                    TvDBHelper.RemoveLinkAniDBTvDB(xrefTemp.AnimeID, (enEpisodeType) xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TvDBID, xrefTemp.TvDBSeasonNumber, xrefTemp.TvDBStartEpisodeNumber);
                }

                CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvDBID, tvSeasonNumber, tvEpNumber, animeID, aniEpType,
                    aniEpNumber);
                if (xref != null)
                {
                    string msg = string.Format("You have already linked Anime ID {0} to this TvDB show/season/ep",
                        xref.AnimeID);
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this TvDB show/season/ep",
                            anime.MainTitle,
                            xref.AnimeID);
                    }
                    return msg;
                }

                CommandRequest_LinkAniDBTvDB cmdRequest = new CommandRequest_LinkAniDBTvDB(animeID, (enEpisodeType)aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber,false);
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
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

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
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

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

        public List<Contract_TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID)
        {
            List<Contract_TvDB_ImagePoster> allImages = new List<Contract_TvDB_ImagePoster>();
            try
            {
                List<TvDB_ImagePoster> allPosters;
                if (tvDBID.HasValue)
                    allPosters = RepoFactory.TvDB_ImagePoster.GetBySeriesID(tvDBID.Value);
                else
                    allPosters = RepoFactory.TvDB_ImagePoster.GetAll();

                foreach (TvDB_ImagePoster img in allPosters)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        public List<Contract_TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID)
        {
            List<Contract_TvDB_ImageWideBanner> allImages = new List<Contract_TvDB_ImageWideBanner>();
            try
            {
                List<TvDB_ImageWideBanner> allBanners;
                if (tvDBID.HasValue)
                    allBanners = RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(tvDBID.Value);
                else
                    allBanners = RepoFactory.TvDB_ImageWideBanner.GetAll();

                foreach (TvDB_ImageWideBanner img in allBanners)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        public List<Contract_TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID)
        {
            List<Contract_TvDB_ImageFanart> allImages = new List<Contract_TvDB_ImageFanart>();
            try
            {
                List<TvDB_ImageFanart> allFanart;
                if (tvDBID.HasValue)
                    allFanart = RepoFactory.TvDB_ImageFanart.GetBySeriesID(tvDBID.Value);
                else
                    allFanart = RepoFactory.TvDB_ImageFanart.GetAll();

                foreach (TvDB_ImageFanart img in allFanart)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        public List<Contract_TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID)
        {
            List<Contract_TvDB_Episode> allImages = new List<Contract_TvDB_Episode>();
            try
            {
                List<TvDB_Episode> allEpisodes;
                if (tvDBID.HasValue)
                    allEpisodes = RepoFactory.TvDB_Episode.GetBySeriesID(tvDBID.Value);
                else
                    allEpisodes = RepoFactory.TvDB_Episode.GetAll();

                foreach (TvDB_Episode img in allEpisodes)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        #endregion

        #region Trakt

        public List<Contract_Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID)
        {
            List<Contract_Trakt_ImageFanart> allImages = new List<Contract_Trakt_ImageFanart>();
            try
            {
                List<Trakt_ImageFanart> allFanart;
                if (traktShowID.HasValue)
                    allFanart = RepoFactory.Trakt_ImageFanart.GetByShowID(traktShowID.Value);
                else
                    allFanart = RepoFactory.Trakt_ImageFanart.GetAll();

                foreach (Trakt_ImageFanart img in allFanart)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        public List<Contract_Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID)
        {
            List<Contract_Trakt_ImagePoster> allImages = new List<Contract_Trakt_ImagePoster>();
            try
            {
                List<Trakt_ImagePoster> allPosters;
                if (traktShowID.HasValue)
                    allPosters = RepoFactory.Trakt_ImagePoster.GetByShowID(traktShowID.Value);
                else
                    allPosters = RepoFactory.Trakt_ImagePoster.GetAll();

                foreach (Trakt_ImagePoster img in allPosters)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        public List<Contract_Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
        {
            List<Contract_Trakt_Episode> allEps = new List<Contract_Trakt_Episode>();
            try
            {
                List<Trakt_Episode> allEpisodes;
                if (traktShowID.HasValue)
                    allEpisodes = RepoFactory.Trakt_Episode.GetByShowID(traktShowID.Value);
                else
                    allEpisodes = RepoFactory.Trakt_Episode.GetAll();

                foreach (Trakt_Episode ep in allEpisodes)
                    allEps.Add(ep.ToContract());

                return allEps;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allEps;
            }
        }

        public List<Contract_Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
        {
            List<Contract_Trakt_Episode> allEps = new List<Contract_Trakt_Episode>();
            try
            {
                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show != null)
                    allEps = GetAllTraktEpisodes(show.Trakt_ShowID);

                return allEps;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allEps;
            }
        }

        public List<Contract_Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                List<Contract_Azure_CrossRef_AniDB_Trakt> contracts = new List<Contract_Azure_CrossRef_AniDB_Trakt>();
                List<JMMServer.Providers.Azure.CrossRef_AniDB_Trakt> results = null;

                if (isAdmin)
                    results = JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                else
                    results = JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBTrakt(animeID);

                if (results == null || results.Count == 0) return contracts;

                foreach (JMMServer.Providers.Azure.CrossRef_AniDB_Trakt xref in results)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
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
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
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


        public List<Contract_CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
        {
            try
            {
                List<Contract_CrossRef_AniDB_TraktV2> contracts = new List<Contract_CrossRef_AniDB_TraktV2>();

                List<CrossRef_AniDB_TraktV2> xrefs = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
                if (xrefs == null) return contracts;

                foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Trakt_Episode.GetByAnimeID(animeID).Select(a=>a.ToContract()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_TraktTVShowResponse> SearchTrakt(string criteria)
        {
            List<Contract_TraktTVShowResponse> results = new List<Contract_TraktTVShowResponse>();
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
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

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
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

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

                foreach (Trakt_Season season in show.Seasons)
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

        public Contract_CrossRef_AniDB_MALResult GetMALCrossRefWebCache(int animeID)
        {
            try
            {
                JMMServer.Providers.Azure.CrossRef_AniDB_MAL result =
                    JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBMAL(animeID);
                if (result == null) return null;

                return result.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_MALAnimeResponse> SearchMAL(string criteria)
        {
            List<Contract_MALAnimeResponse> results = new List<Contract_MALAnimeResponse>();
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
                        AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xrefTemp.AnimeID);
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

        public Contract_CrossRef_AniDB_OtherResult GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
        {
            try
            {
                JMMServer.Providers.Azure.CrossRef_AniDB_Other result =
                    JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBOther(animeID, (CrossRefType) crossRefType);
                if (result == null) return null;

                return result.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public Contract_CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, (CrossRefType) crossRefType)?.ToContract();
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
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

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

        public List<Contract_MovieDBMovieSearchResult> SearchTheMovieDB(string criteria)
        {
            List<Contract_MovieDBMovieSearchResult> results = new List<Contract_MovieDBMovieSearchResult>();
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

        public List<Contract_MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
        {
            List<Contract_MovieDB_Poster> allImages = new List<Contract_MovieDB_Poster>();
            try
            {
                List<MovieDB_Poster> allPosters;
                if (movieID.HasValue)
                    allPosters = RepoFactory.MovieDB_Poster.GetByMovieID(movieID.Value);
                else
                    allPosters = RepoFactory.MovieDB_Poster.GetAllOriginal();

                foreach (MovieDB_Poster img in allPosters)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        public List<Contract_MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
        {
            List<Contract_MovieDB_Fanart> allImages = new List<Contract_MovieDB_Fanart>();
            try
            {
                List<MovieDB_Fanart> allFanart;
                if (movieID.HasValue)
                    allFanart = RepoFactory.MovieDB_Fanart.GetByMovieID(movieID.Value);
                else
                    allFanart = RepoFactory.MovieDB_Fanart.GetAllOriginal();

                foreach (MovieDB_Fanart img in allFanart)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return allImages;
            }
        }

        #endregion

        /// <summary>
        /// Finds the previous episode for use int the next unwatched episode
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public Contract_AnimeEpisode GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID)
        {
            try
            {
                Contract_AnimeEpisode nextEp = GetNextUnwatchedEpisode(animeSeriesID, userID);
                if (nextEp == null) return null;

                int epType = nextEp.EpisodeType;
                int epNum = nextEp.EpisodeNumber - 1;

                if (epNum <= 0) return null;

                AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (series == null) return null;

                List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID,
                    (enEpisodeType) epType,
                    epNum);
                if (anieps.Count == 0) return null;

                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anieps[0].EpisodeID);
                return ep?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public Contract_AnimeEpisode GetNextUnwatchedEpisode(int animeSeriesID, int userID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetNextUnwatchedEpisode(session.Wrap(), animeSeriesID, userID);
            }
        }

        public Contract_AnimeEpisode GetNextUnwatchedEpisode(ISessionWrapper session, int animeSeriesID, int userID)
        {
            try
            {

                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (series == null) return null;

                //List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);
                List<AnimeEpisode> epList = new List<AnimeEpisode>();
                Dictionary<int, AnimeEpisode_User> dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
                foreach (
                    AnimeEpisode_User userRecord in RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(userID, animeSeriesID))
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

                List<Contract_AnimeEpisode> candidateEps = new List<Contract_AnimeEpisode>();
                foreach (AnimeEpisode ep in epList)
                {
                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        AniDB_Episode anidbep = dictAniEps[ep.AniDB_EpisodeID];
                        if (anidbep.EpisodeType == (int) enEpisodeType.Episode ||
                            anidbep.EpisodeType == (int) enEpisodeType.Special)
                        {
                            AnimeEpisode_User userRecord = null;
                            if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                userRecord = dictEpUsers[ep.AnimeEpisodeID];

                            Contract_AnimeEpisode epContract = ep.GetUserContract(userID);
                            if (epContract != null)
                                candidateEps.Add(epContract);
                        }
                    }
                }

                if (candidateEps.Count == 0) return null;



                // this will generate a lot of queries when the user doesn have files
                // for these episodes
                foreach (Contract_AnimeEpisode canEp in candidateEps.OrderBy(a=>a.EpisodeType).ThenBy(a=>a.EpisodeNumber))
                {
                    // now refresh from the database to get file count
                    AnimeEpisode epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);
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

        public List<Contract_AnimeEpisode> GetAllUnwatchedEpisodes(int animeSeriesID, int userID)
        {
            List<Contract_AnimeEpisode> ret = new List<Contract_AnimeEpisode>();

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

        public Contract_AnimeEpisode GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
        {
            try
            {
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return null;

                List<AnimeSeries> allSeries = grp.GetAllSeries().OrderBy(a=>a.AirDate).ToList();


                foreach (AnimeSeries ser in allSeries)
                {
                    Contract_AnimeEpisode contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
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

        public List<Contract_AnimeEpisode> GetContinueWatchingFilter(int userID, int maxRecords)
        {
            List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return retEps;

                    // find the locked Continue Watching Filter
                    GroupFilter gf = null;
                    List<GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
                    if (lockedGFs != null)
                    {
                        // if it already exists we can leave
                        foreach (GroupFilter gfTemp in lockedGFs)
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
                    IEnumerable<Contract_AnimeGroup> comboGroups =
                        gf.GroupsIds[userID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                            .Where(a => a != null)
                            .Select(a => a.GetUserContract(userID));
                            


                    // apply sorting
                    comboGroups = GroupFilterHelper.Sort(comboGroups, gf);


                    foreach (Contract_AnimeGroup grp in comboGroups)
                    {
                        List<AnimeSeries> sers = RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID).OrderBy(a=>a.AirDate).ToList();

                        List<int> seriesWatching = new List<int>();

                        foreach (AnimeSeries ser in sers)
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


                            Contract_AnimeEpisode ep = GetNextUnwatchedEpisode(sessionWrapper, ser.AnimeSeriesID, userID);
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
        public List<Contract_AnimeEpisode> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int jmmuserID)
        {
            List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    DateTime start = DateTime.Now;

                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    // get a list of series that is applicable
                    List<AnimeSeries_User> allSeriesUser = RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(jmmuserID);

                    TimeSpan ts = DateTime.Now - start;
                    logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Series: {0}", ts.TotalMilliseconds));
                    start = DateTime.Now;

                    foreach (AnimeSeries_User userRecord in allSeriesUser)
                    {
                        AnimeSeries series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(series)) continue;

                        Contract_AnimeEpisode ep = GetNextUnwatchedEpisode(sessionWrapper, userRecord.AnimeSeriesID, jmmuserID);
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

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyWatched(int maxRecords, int jmmuserID)
        {
            List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
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

        public List<VideoLocal> GetAllFiles()
        {
            try
            {
                return RepoFactory.VideoLocal.GetAll();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<VideoLocal>();
            }
        }

        public VideoLocal GetFileByID(int id)
        {
            try
            {
                return RepoFactory.VideoLocal.GetByID(id);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new VideoLocal();
            }
        }

        public List<VideoLocal> GetFilesRecentlyAdded(int max_records)
        {
            try
            {
                return RepoFactory.VideoLocal.GetMostRecentlyAdded(max_records);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<VideoLocal>();
            }
        }

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    List<VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords);
                    int numEps = 0;
                    List<string> hashes = vids.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                    foreach (string s in hashes)
                    {
                        VideoLocal vid = vids.FirstOrDefault(a => a.Hash == s);
                        foreach (AnimeEpisode ep in vid.GetAnimeEpisodes())
                        {
                            if (user.AllowedSeries(ep.GetAnimeSeries(sessionWrapper)))
                            {
                                Contract_AnimeEpisode epContract = ep.GetUserContract(jmmuserID);
                                if (epContract != null)
                                {
                                    retEps.Add(epContract);
                                    numEps++;

                                    // Lets only return the specified amount
                                    if (retEps.Count == maxRecords) return retEps;
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

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
            try
            {

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    DateTime start = DateTime.Now;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseExtensions.Instance.GetData(sql);

                    TimeSpan ts2 = DateTime.Now - start;
                    logger.Info("GetEpisodesRecentlyAddedSummary:RawData in {0} ms", ts2.TotalMilliseconds);
                    start = DateTime.Now;

                    int numEps = 0;
                    foreach (object[] res in results)
                    {
                        int animeSeriesID = int.Parse(res[0].ToString());

                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(ser)) continue;

                        List<VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        List<AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
                        if (eps.Count == 0) continue;

                        Contract_AnimeEpisode epContract = eps[0].GetUserContract(jmmuserID);
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

        public List<Contract_AnimeSeries> GetSeriesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            List<Contract_AnimeSeries> retSeries = new List<Contract_AnimeSeries>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {


                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retSeries;

                    List<AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(maxRecords);
                    int numSeries = 0;
                    foreach (AnimeSeries ser in series)
                    {
                        if (user.AllowedSeries(ser))
                        {
                            Contract_AnimeSeries serContract = ser.GetUserContract(jmmuserID);
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

        public Contract_AnimeEpisode GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID)
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
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null) return "Series does not exist";

                int animeGroupID = ser.AnimeGroupID;

                foreach (AnimeEpisode ep in ser.GetAnimeEpisodes())
                {
                    foreach (VideoLocal vid in ep.GetVideoLocals())
                    {
                        foreach (VideoLocal_Place place in vid.Places)
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
                                FileSystemResult fs = file.Delete(true);
                                if (fs == null || !fs.IsOk)
                                {
                                    logger.Error($"Unable to delete file '{place.FullServerPath}'");
                                    return $"Unable to delete file '{place.FullServerPath}'";
                                }
                            }
                            RepoFactory.VideoLocalPlace.Delete(place.VideoLocal_Place_ID);
                        }
                        CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
                        cmdDel.Save();
                        RepoFactory.VideoLocal.Delete(vid.VideoLocalID);
                    }
                    RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
                }
                RepoFactory.AnimeSeries.Delete(ser.AnimeSeriesID);

                // finally update stats
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
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


        public List<Contract_AnimeSeries> GetSeriesWithMissingEpisodes(int maxRecords, int jmmuserID)
        {

            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
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
            return new List<Contract_AnimeSeries>();
        }

        public List<Contract_AniDBAnime> GetMiniCalendar(int jmmuserID, int numberOfDays)
        {


            // get all the series
            List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                List<AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays),
                    DateTime.Today.AddDays(numberOfDays));
                foreach (AniDB_Anime anime in animes)
                {
                    if (anime?.Contract?.AniDBAnime == null)
                        continue;
                    if (!user.Contract.HideCategories.FindInEnumerable(anime.Contract.AniDBAnime.AllTags))
                        animeList.Add(anime.Contract.AniDBAnime);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return animeList;
        }

        public List<Contract_AniDBAnime> GetAnimeForMonth(int jmmuserID, int month, int year)
        {
            // get all the series
            List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null) return animeList;

                DateTime startDate = new DateTime(year, month, 1, 0, 0, 0);
                DateTime endDate = startDate.AddMonths(1);
                endDate = endDate.AddMinutes(-10);

                List<AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(startDate, endDate);
                foreach (AniDB_Anime anime in animes)
                {
                    if (anime?.Contract?.AniDBAnime == null)
                        continue;
                    if (!user.Contract.HideCategories.FindInEnumerable(anime.Contract.AniDBAnime.AllTags))
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

        public List<Contract_JMMUser> GetAllUsers()
        {
            try
            {
                return RepoFactory.JMMUser.GetAll().Select(a => a.Contract).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Contract_JMMUser>();
            }
        }

        public Contract_JMMUser AuthenticateUser(string username, string password)
        {

            try
            {
                return RepoFactory.JMMUser.AuthenticateUser(username, password)?.Contract;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public string ChangePassword(int userID, string newPassword)
        {
            try
            {
                JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                jmmUser.Password = Digest.Hash(newPassword);
                RepoFactory.JMMUser.Save(jmmUser, false);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return ex.Message;
            }

            return "";
        }

        public string SaveUser(Contract_JMMUser user)
        {
            
            try
            {
                bool existingUser = false;
                bool updateStats = false;
                bool updateGf = false;
                JMMUser jmmUser = null;
                if (user.JMMUserID.HasValue)
                {
                    jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID.Value);
                    if (jmmUser == null) return "User not found";
                    existingUser = true;
                }
                else
                {
                    jmmUser = new JMMUser();
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
                    jmmUser.Password = user.Password;
                }

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 0)
                {
                    bool adminExists = false;
                    List<JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (JMMUser userOld in users)
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
                    foreach (AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
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
                JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 1)
                {
                    bool adminExists = false;
                    List<JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (JMMUser userOld in users)
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

        public List<Contract_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID)
        {
            List<Contract_AniDB_Anime_Similar> links = new List<Contract_AniDB_Anime_Similar>();
            try
            {
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return links;

                JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return links;


                foreach (AniDB_Anime_Similar link in anime.GetSimilarAnime())
                {
                    AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                    links.Add(link.ToContract(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return links;
            }
        }

        public List<Contract_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID)
        {
            List<Contract_AniDB_Anime_Relation> links = new List<Contract_AniDB_Anime_Relation>();
            try
            {
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return links;

                JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return links;


                foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
                {
                    AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                    links.Add(link.ToContract(animeLink, ser, userID));
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
        public List<Contract_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType)
        {
            List<Contract_Recommendation> recs = new List<Contract_Recommendation>();

            try
            {

                JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
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
                Dictionary<int, IgnoreAnime> dictIgnored = new Dictionary<int, Entities.IgnoreAnime>();
                foreach (IgnoreAnime ign in ignored)
                    dictIgnored[ign.AnimeID] = ign;


                // find all the series which the user has rated
                List<AniDB_Vote> allVotes = RepoFactory.AniDB_Vote.GetAll().OrderByDescending(a=>a.VoteValue).ToList();
                if (allVotes.Count == 0) return recs;


                Dictionary<int, Contract_Recommendation> dictRecs = new Dictionary<int, Contract_Recommendation>();

                List<AniDB_Vote> animeVotes = new List<AniDB_Vote>();
                foreach (AniDB_Vote vote in allVotes)
                {
                    if (vote.VoteType != (int) enAniDBVoteType.Anime && vote.VoteType != (int) enAniDBVoteType.AnimeTemp)
                        continue;

                    if (dictIgnored.ContainsKey(vote.EntityID)) continue;

                    // check if the user has this anime
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(vote.EntityID);
                    if (anime == null) continue;

                    // get similar anime
                    List<AniDB_Anime_Similar> simAnime = anime.GetSimilarAnime().OrderByDescending(a=>a.ApprovalPercentage).ToList();
                    // sort by the highest approval

                    foreach (AniDB_Anime_Similar link in simAnime)
                    {
                        if (dictIgnored.ContainsKey(link.SimilarAnimeID)) continue;

                        AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                        if (animeLink != null)
                            if (!juser.AllowedAnime(animeLink)) continue;

                        // don't recommend to watch anime that the user doesn't have
                        if (animeLink == null && recommendationType == 1) continue;

                        // don't recommend to watch series that the user doesn't have
                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);
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

                        Contract_Recommendation rec = new Contract_Recommendation();
                        rec.BasedOnAnimeID = anime.AnimeID;
                        rec.RecommendedAnimeID = link.SimilarAnimeID;

                        // if we don't have the anime locally. lets assume the anime has a high rating
                        decimal animeRating = 850;
                        if (animeLink != null) animeRating = animeLink.AniDBRating;

                        rec.Score = CalculateRecommendationScore(vote.VoteValue, link.ApprovalPercentage, animeRating);
                        rec.BasedOnVoteValue = vote.VoteValue;
                        rec.RecommendedApproval = link.ApprovalPercentage;

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

                        AnimeSeries serBasedOn = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                        if (serBasedOn == null) continue;

                        rec.BasedOn_AnimeSeries = serBasedOn.GetUserContract(userID);

                        dictRecs[rec.RecommendedAnimeID] = rec;
                    }
                }

                List<Contract_Recommendation> tempRecs = new List<Contract_Recommendation>();
                foreach (Contract_Recommendation rec in dictRecs.Values)
                    tempRecs.Add(rec);

                // sort by the highest score

                int numRecs = 0;
                foreach (Contract_Recommendation rec in tempRecs.OrderByDescending(a=>a.Score))
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

        public List<Contract_AniDBReleaseGroup> GetReleaseGroupsForAnime(int animeID)
        {
            List<Contract_AniDBReleaseGroup> relGroups = new List<Contract_AniDBReleaseGroup>();

            try
            {
                AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                //List<int> userReleaseGroups = new List<int>();
                Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
                foreach (AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    List<VideoLocal> vids = ep.GetVideoLocals();
                    List<string> hashes = vids.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                    foreach(string h in hashes)
                    {
                        VideoLocal vid = vids.First(a => a.Hash == h);
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
                    Contract_AniDBReleaseGroup contract = new Contract_AniDBReleaseGroup();
                    contract.GroupID = gs.GroupID;
                    contract.GroupName = gs.GroupName;
                    contract.EpisodeRange = gs.EpisodeRange;

                    if (userReleaseGroups.ContainsKey(gs.GroupID))
                    {
                        contract.UserCollecting = true;
                        contract.FileCount = userReleaseGroups[gs.GroupID];
                    }
                    else
                    {
                        contract.UserCollecting = false;
                        contract.FileCount = 0;
                    }

                    relGroups.Add(contract);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return relGroups;
        }

        public List<Contract_AniDB_Character> GetCharactersForAnime(int animeID)
        {
            List<Contract_AniDB_Character> chars = new List<Contract_AniDB_Character>();

            try
            {
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                return anime.GetCharactersContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return chars;
        }

        public List<Contract_AniDB_Character> GetCharactersForSeiyuu(int aniDB_SeiyuuID)
        {
            List<Contract_AniDB_Character> chars = new List<Contract_AniDB_Character>();

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
                            AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniChars[0].AnimeID);
                            if (anime != null)
                            {
                                Contract_AniDB_Character contract = chr.ToContract(aniChars[0].CharType);
                                contract.Anime = anime.Contract.AniDBAnime;
                                chars.Add(contract);
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

        public List<Contract_MissingFile> GetMyListFilesForRemoval(int userID)
        {
            List<Contract_MissingFile> contracts = new List<Contract_MissingFile>();

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


            Dictionary<int, AniDB_Anime> animeCache = new Dictionary<int, AniDB_Anime>();
            Dictionary<int, AnimeSeries> animeSeriesCache = new Dictionary<int, AnimeSeries>();

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
                            VideoLocal v = RepoFactory.VideoLocal.GetByHash(hash);
                            fileMissing = true;
                            foreach (VideoLocal_Place p in v.Places)
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
                            AniDB_Anime anime = null;
                            if (animeCache.ContainsKey(myitem.AnimeID))
                                anime = animeCache[myitem.AnimeID];
                            else
                            {
                                anime = RepoFactory.AniDB_Anime.GetByAnimeID(myitem.AnimeID);
                                animeCache[myitem.AnimeID] = anime;
                            }

                            AnimeSeries ser = null;
                            if (animeSeriesCache.ContainsKey(myitem.AnimeID))
                                ser = animeSeriesCache[myitem.AnimeID];
                            else
                            {
                                ser = RepoFactory.AnimeSeries.GetByAnimeID(myitem.AnimeID);
                                animeSeriesCache[myitem.AnimeID] = ser;
                            }


                            Contract_MissingFile missingFile = new Contract_MissingFile();
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

        public void RemoveMissingMyListFiles(List<Contract_MissingFile> myListFiles)
        {
            foreach (Contract_MissingFile missingFile in myListFiles)
            {
                CommandRequest_DeleteFileFromMyList cmd = new CommandRequest_DeleteFileFromMyList(missingFile.FileID);
                cmd.Save();

                // For deletion of files from Trakt, we will rely on the Daily sync
                // lets also try removing from the users trakt collecion
            }
        }

        public List<Contract_AnimeSeries> GetSeriesWithoutAnyFiles(int userID)
        {
            List<Contract_AnimeSeries> contracts = new List<Contract_AnimeSeries>();


            try
            {
                foreach (AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                {
                    if (RepoFactory.VideoLocal.GetByAniDBAnimeID(ser.AniDB_ID).Count == 0)
                    {
                        Contract_AnimeSeries can = ser.GetUserContract(userID);
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

        public List<Contract_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly,
            int airingState)
        {
            List<Contract_MissingEpisode> contracts = new List<Contract_MissingEpisode>();

            AiringState airState = (AiringState) airingState;

            Dictionary<int, AniDB_Anime> animeCache = new Dictionary<int, AniDB_Anime>();
            Dictionary<int, List<Contract_GroupVideoQuality>> gvqCache =
                new Dictionary<int, List<Contract_GroupVideoQuality>>();
            Dictionary<int, List<Contract_GroupFileSummary>> gfqCache =
                new Dictionary<int, List<Contract_GroupFileSummary>>();

            try
            {
                int i = 0;
                List<AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                foreach (AnimeSeries ser in allSeries)
                {
                    i++;
                    //string msg = string.Format("Updating series {0} of {1} ({2}) -  {3}", i, allSeries.Count, ser.Anime.MainTitle, DateTime.Now);
                    //logger.Debug(msg);

                    //if (ser.Anime.AnimeID != 69) continue;

                    int missingEps = ser.MissingEpisodeCount;
                    if (onlyMyGroups) missingEps = ser.MissingEpisodeCountGroups;

                    bool finishedAiring = ser.GetAnime().FinishedAiring;

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
                        List<AnimeEpisode> eps = ser.GetAnimeEpisodes();
                        ts = DateTime.Now - start;
                        timingEps += ts.TotalMilliseconds;

                        epCount = eps.Count;
                        foreach (AnimeEpisode aep in ser.GetAnimeEpisodes())
                        {
                            if (regularEpisodesOnly && aep.EpisodeTypeEnum != enEpisodeType.Episode) continue;

                            AniDB_Episode aniep = aep.AniDB_Episode;
                            if (aniep.FutureDated) continue;

                            start = DateTime.Now;
                            List<VideoLocal> vids = aep.GetVideoLocals();
                            ts = DateTime.Now - start;
                            timingVids += ts.TotalMilliseconds;

                            if (vids.Count == 0)
                            {
                                Contract_MissingEpisode contract = new Contract_MissingEpisode();
                                contract.AnimeID = ser.AniDB_ID;
                                start = DateTime.Now;
                                contract.AnimeSeries = ser.GetUserContract(userID);
                                ts = DateTime.Now - start;
                                timingSeries += ts.TotalMilliseconds;

                                AniDB_Anime anime = null;
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

                                contract.AnimeTitle = anime.MainTitle;

                                start = DateTime.Now;
                                contract.GroupFileSummary = "";
                                List<Contract_GroupVideoQuality> summ = null;
                                if (gvqCache.ContainsKey(ser.AniDB_ID))
                                    summ = gvqCache[ser.AniDB_ID];
                                else
                                {
                                    summ = GetGroupVideoQualitySummary(anime.AnimeID);
                                    gvqCache[ser.AniDB_ID] = summ;
                                }

                                foreach (Contract_GroupVideoQuality gvq in summ)
                                {
                                    if (contract.GroupFileSummary.Length > 0)
                                        contract.GroupFileSummary += " --- ";

                                    contract.GroupFileSummary += string.Format("{0} - {1}/{2}/{3}bit ({4})",
                                        gvq.GroupNameShort, gvq.Resolution,
                                        gvq.VideoSource, gvq.VideoBitDepth, gvq.NormalEpisodeNumberSummary);
                                }

                                contract.GroupFileSummarySimple = "";
                                List<Contract_GroupFileSummary> summFiles = null;
                                if (gfqCache.ContainsKey(ser.AniDB_ID))
                                    summFiles = gfqCache[ser.AniDB_ID];
                                else
                                {
                                    summFiles = GetGroupFileSummary(anime.AnimeID);
                                    gfqCache[ser.AniDB_ID] = summFiles;
                                }

                                foreach (Contract_GroupFileSummary gfq in summFiles)
                                {
                                    if (contract.GroupFileSummarySimple.Length > 0)
                                        contract.GroupFileSummarySimple += ", ";

                                    contract.GroupFileSummarySimple += string.Format("{0} ({1})", gfq.GroupNameShort,
                                        gfq.NormalEpisodeNumberSummary);
                                }

                                ts = DateTime.Now - start;
                                timingQuality += ts.TotalMilliseconds;
                                animeCache[ser.AniDB_ID] = anime;

                                start = DateTime.Now;
                                contract.EpisodeID = aniep.EpisodeID;
                                contract.EpisodeNumber = aniep.EpisodeNumber;
                                contract.EpisodeType = aniep.EpisodeType;
                                contracts.Add(contract);
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
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                JMMUser user = RepoFactory.JMMUser.GetByID(userID);
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

        public bool PostTraktCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler, ref returnMessage);
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

        public List<Contract_CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs()
        {
            List<Contract_CrossRef_AniDB_TraktV2> contracts = new List<Contract_CrossRef_AniDB_TraktV2>();
            try
            {
                List<CrossRef_AniDB_TraktV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TraktV2.GetAll();

                foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
                {
                    contracts.Add(xref.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contracts;
        }

        public List<Contract_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID)
        {
            List<Contract_Trakt_CommentUser> comments = new List<Contract_Trakt_CommentUser>();

            try
            {
                
                List<TraktV2Comment> commentsTemp = TraktTVHelper.GetShowCommentsV2(animeID);
                if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                foreach (TraktV2Comment sht in commentsTemp)
                {
                    Contract_Trakt_CommentUser comment = new Contract_Trakt_CommentUser();

                    Trakt_Friend traktFriend = RepoFactory.Trakt_Friend.GetByUsername(sht.user.username);

                    // user details
                    comment.User = new Contract_Trakt_User();
                    if (traktFriend == null)
                        comment.User.Trakt_FriendID = 0;
                    else
                        comment.User.Trakt_FriendID = traktFriend.Trakt_FriendID;

                    comment.User.Username = sht.user.username;
                    comment.User.Full_name = sht.user.name;

                    // comment details
                    comment.Comment = new Contract_Trakt_Comment();
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

        public Contract_AniDBVote GetUserVote(int animeID)
        {
            try
            {
                List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(animeID);
                if (dbVotes.Count == 0) return null;
                return dbVotes[0].ToContract();
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
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null) return;

                AnimeEpisode_User epUserRecord = ep.GetUserRecord(userID);

                if (epUserRecord == null)
                {
                    epUserRecord = new AnimeEpisode_User();
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

                AnimeSeries ser = ep.GetAnimeSeries();
                if (ser == null) return;

                AnimeSeries_User userRecord = ser.GetUserRecord(userID);
                if (userRecord == null)
                    userRecord = new AnimeSeries_User(userID, ser.AnimeSeriesID);

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

        public List<Contract_IgnoreAnime> GetIgnoredAnime(int userID)
        {
            List<Contract_IgnoreAnime> retAnime = new List<Contract_IgnoreAnime>();
            try
            {
                JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return retAnime;

                List<IgnoreAnime> ignoredAnime = RepoFactory.IgnoreAnime.GetByUser(userID);
                foreach (IgnoreAnime ign in ignoredAnime)
                    retAnime.Add(ign.ToContract());
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retAnime;
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

                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return;

                AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
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

                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return;

                grp.DefaultAnimeSeriesID = null;
                RepoFactory.AnimeGroup.Save(grp, false, false);
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public List<Contract_TvDBLanguage> GetTvDBLanguages()
        {
            List<Contract_TvDBLanguage> retLanguages = new List<Contract_TvDBLanguage>();

            try
            {
                foreach (TvDBLanguage lan in JMMService.TvdbHelper.GetLanguages())
                {
                    retLanguages.Add(lan.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return retLanguages;
        }

        public void RefreshAllMediaInfo()
        {
            MainWindow.RefreshAllMediaInfo();
        }

        public Contract_AnimeGroup GetTopLevelGroupForSeries(int animeSeriesID, int userID)
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
				// pause queues
				JMMService.CmdProcessorGeneral.Paused = true;
				JMMService.CmdProcessorHasher.Paused = true;
				JMMService.CmdProcessorImages.Paused = true;

				

				// get all the old groups
				List<AnimeGroup> oldGroups = RepoFactory.AnimeGroup.GetAll();
				List<AnimeGroup_User> oldGroupUsers = RepoFactory.AnimeGroup_User.GetAll();

				AnimeGroup tempGroup = null;
				foreach(AnimeGroup temp4 in RepoFactory.AnimeGroup.GetAllTopLevelGroups())
				{
					if(temp4.GroupName.Equals("AAA Migrating Groups AAA"))
					{
						tempGroup = temp4;
						break;
					}
				}
				// We can't resume if there is nothing to resume
				if (resume && tempGroup == null) resume = false;

				if (tempGroup == null)
				{
					// create a new group, where we will place all the series temporarily
					tempGroup = new AnimeGroup();
					tempGroup.GroupName = "AAA Migrating Groups AAA";
					tempGroup.Description = "AAA Migrating Groups AAA";
					tempGroup.SortName = "AAA Migrating Groups AAA";
					tempGroup.DateTimeUpdated = DateTime.Now;
					tempGroup.DateTimeCreated = DateTime.Now;
					RepoFactory.AnimeGroup.Save(tempGroup, true, false);
				}

				if (!resume) {  
					// move all series to the new group
					foreach (AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
					{
						ser.AnimeGroupID = tempGroup.AnimeGroupID;
                        RepoFactory.AnimeSeries.Save(ser, true);
					}

					// delete all the old groups
					foreach (AnimeGroup grp in oldGroups)
						RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);

					// delete all the old group user records
					RepoFactory.AnimeGroup_User.Delete(oldGroupUsers);
				}


                // recreate groups
                foreach (AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                {
                    bool createNewGroup = true;

                    if (ServerSettings.AutoGroupSeries)
                    {
                        if (ser.AnimeGroupID != tempGroup.AnimeGroupID) continue;

                        List<AnimeGroup> grps = AnimeGroup.GetRelatedGroupsFromAnimeID(ser.AniDB_ID, true);

                        if (grps != null && grps.Count > 0)
                        {
                            int groupID = -1;
                            AnimeSeries name = null;
                            string customGroupName = null;
                            foreach (AnimeGroup grp in grps.ToList())
                            {
                                if (grp.AnimeGroupID == tempGroup.AnimeGroupID) continue;
                                if (groupID == -1) groupID = grp.AnimeGroupID;
                                ser.AnimeGroupID = groupID;
                                bool groupHasCustomName = true;

                                createNewGroup = false;

                                if (groupID != grp.AnimeGroupID)
                                {
                                    if (grp.DefaultAnimeSeriesID.HasValue)
                                    {
                                        name = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value);
										if (name == null)
										{
											grp.DefaultAnimeSeriesID = null;
											//TODO this do nothing, only in memory is not saved
											// Actually it is used in if (!grp.DefaultAnimeSeriesID.HasValue) down below
										}
										else
										{
											groupHasCustomName = false;
										}
									}
                                    foreach (AnimeSeries series in grp.GetAllSeries())
                                    {
                                        if (series.AnimeGroupID == groupID) continue;
                                        series.AnimeGroupID = groupID;

                                        #region Naming

                                        if (!grp.DefaultAnimeSeriesID.HasValue)
                                        {
                                            if (name == null)
                                            {
                                                name = series;
                                            }

                                            // Check all titles for custom naming, in case user changed language preferences
                                            if (series.SeriesNameOverride.Equals(grp.GroupName))
                                            {
                                                groupHasCustomName = false;
                                            }
                                            else
                                            {
												// massive speedup
                                                foreach (Contract_AnimeTitle title in series.Contract.AniDBAnime.AnimeTitles)
                                                {
                                                    if (title.Title.Equals(grp.GroupName))
                                                    {
                                                        groupHasCustomName = false;
                                                        break;
                                                    }
                                                }

												#region tvdb names
												List<TvDB_Series> tvdbs = series.GetTvDBSeries();
												if (tvdbs != null && tvdbs.Count != 0)
												{
													foreach (TvDB_Series tvdbser in tvdbs)
													{
														if (tvdbser.SeriesName.Equals(grp.GroupName))
														{
															groupHasCustomName = false;
															break;
														}
													}
												}
												#endregion
											}
										}

                                        RepoFactory.AnimeSeries.Save(series, false, true, true);
										// I didn't see this called anywhere, it should also fix the new issue with recreated
										// groups missing all episodes
										series.UpdateStats(true, true, false);
                                    }
                                }

                                if (groupHasCustomName) customGroupName = grp.GroupName;
                            }

                            //after moving everything, rename and repopulate
                            if (name != null)
                            {
                                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(groupID);
								string newTitle = name.GetSeriesName();
								if (grp.DefaultAnimeSeriesID.HasValue &&
									grp.DefaultAnimeSeriesID.Value != name.AnimeSeriesID)
									newTitle = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value).GetSeriesName();
								if (customGroupName != null) newTitle = customGroupName;
                                // reset tags, description, etc to new series
                                grp.Populate(name);
                                grp.GroupName = newTitle;
                                grp.SortName = newTitle;
                                RepoFactory.AnimeGroup.Save(grp, false, false);
                            }

                            #endregion

                            foreach (AnimeGroup grp in grps)
                            {
                                if (grp.GetAllSeries().Count == 0)
                                {
                                    RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);
                                }
                            }
                        }
                    }

                    if (createNewGroup)
                    {
                        AnimeGroup anGroup = new AnimeGroup();
                        anGroup.Populate(ser);
                        RepoFactory.AnimeGroup.Save(anGroup, true, true);

                        ser.AnimeGroupID = anGroup.AnimeGroupID;
                    }

                    RepoFactory.AnimeSeries.Save(ser, false);
                }

                // delete the temp group
                if (tempGroup.GetAllSeries().Count == 0)
                    RepoFactory.AnimeGroup.Delete(tempGroup.AnimeGroupID);

                // create group user records and update group stats
                foreach (AnimeGroup grp in RepoFactory.AnimeGroup.GetAll())
                    grp.UpdateStatsFromTopLevel(true, true, true);


                // un-pause queues
                JMMService.CmdProcessorGeneral.Paused = false;
                JMMService.CmdProcessorHasher.Paused = false;
                JMMService.CmdProcessorImages.Paused = false;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public Contract_AppVersions GetAppVersions()
        {
            try
            {
                AppVersionsResult appv = XMLService.GetAppVersions();
                if (appv == null) return null;

                return appv.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return null;
        }

        public Contract_AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID)
        {

            try
            {
                AniDB_Seiyuu seiyuu = RepoFactory.AniDB_Seiyuu.GetByID(seiyuuID);
                if (seiyuu == null) return null;

                return seiyuu.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return null;
        }

        public Contract_FileFfdshowPreset GetFFDPreset(int videoLocalID)
        {
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return null;

                FileFfdshowPreset ffd = RepoFactory.FileFfdshowPreset.GetByHashAndSize(vid.Hash, vid.FileSize);
                if (ffd == null) return null;

                return ffd.ToContract();
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

                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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

        public void SaveFFDPreset(Contract_FileFfdshowPreset preset)
        {
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByHashAndSize(preset.Hash, preset.FileSize);
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

        public List<Contract_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID)
        {
            try
            {
                List<Contract_VideoLocal> vids = new List<Contract_VideoLocal>();

                FileSearchCriteria sType = (FileSearchCriteria) searchType;


                switch (sType)
                {
                    case FileSearchCriteria.Name:

                        List<VideoLocal> results1 = RepoFactory.VideoLocal.GetByName(searchCriteria.Trim());
                        foreach (VideoLocal vid in results1)
                            vids.Add(vid.ToContract(userID));

                        break;

                    case FileSearchCriteria.ED2KHash:

                        VideoLocal vidl = RepoFactory.VideoLocal.GetByHash(searchCriteria.Trim());
                        if (vidl!=null)
                            vids.Add(vidl.ToContract(userID));
                        
                        break;

                    case FileSearchCriteria.Size:

                        break;

                    case FileSearchCriteria.LastOneHundred:

                        List<VideoLocal> results2 = RepoFactory.VideoLocal.GetMostRecentlyAdded(100);
                        foreach (VideoLocal vid in results2)
                            vids.Add(vid.ToContract(userID));

                        break;
                }

                return vids;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_VideoLocal>();
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

        public List<Contract_VideoLocal> RandomFileRenamePreview(int maxResults, int userID)
        {
            try
            {
                return RepoFactory.VideoLocal.GetRandomFiles(maxResults).Select(a => a.ToContract(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return new List<Contract_VideoLocal>();
            }
        }

        public Contract_VideoLocalRenamed RenameFilePreview(int videoLocalID, string renameRules)
        {
            Contract_VideoLocalRenamed ret = new Contract_VideoLocalRenamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;

            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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

        public Contract_VideoLocalRenamed RenameFile(int videoLocalID, string renameRules)
        {
            Contract_VideoLocalRenamed ret = new Contract_VideoLocalRenamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;
            try
            {
                VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
                            foreach (VideoLocal_Place place in vid.Places)
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

        public List<Contract_VideoLocalRenamed> RenameFiles(List<int> videoLocalIDs, string renameRules)
        {
            List<Contract_VideoLocalRenamed> ret = new List<Contract_VideoLocalRenamed>();
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

        public List<Contract_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID)
        {
            try
            {
                return RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Select(a => a.ToContract(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_VideoLocal>();
        }

        public List<Contract_RenameScript> GetAllRenameScripts()
        {
            List<Contract_RenameScript> ret = new List<Contract_RenameScript>();
            try
            {
                List<RenameScript> allScripts = RepoFactory.RenameScript.GetAll();
                foreach (RenameScript scr in allScripts)
                    ret.Add(scr.ToContract());
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return ret;
        }

        public Contract_RenameScript_SaveResponse SaveRenameScript(Contract_RenameScript contract)
        {
            Contract_RenameScript_SaveResponse response = new Contract_RenameScript_SaveResponse();
            response.ErrorMessage = "";
            response.RenameScript = null;

            try
            {
                RenameScript script = null;
                if (contract.RenameScriptID.HasValue)
                {
                    // update
                    script = RepoFactory.RenameScript.GetByID(contract.RenameScriptID.Value);
                    if (script == null)
                    {
                        response.ErrorMessage = "Could not find Rename Script ID: " +
                                                contract.RenameScriptID.Value.ToString();
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
                List<RenameScript> allScripts = RepoFactory.RenameScript.GetAll();

                if (contract.IsEnabledOnImport == 1)
                {
                    foreach (RenameScript rs in allScripts)
                    {
                        if (rs.IsEnabledOnImport == 1 &&
                            (!contract.RenameScriptID.HasValue || (contract.RenameScriptID.Value != rs.RenameScriptID)))
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

                response.RenameScript = script.ToContract();

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

        public List<Contract_AniDB_Recommendation> GetAniDBRecommendations(int animeID)
        {
            List<Contract_AniDB_Recommendation> contracts = new List<Contract_AniDB_Recommendation>();
            try
            {
                foreach (AniDB_Recommendation rec in RepoFactory.AniDB_Recommendation.GetByAnimeID(animeID))
                    contracts.Add(rec.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return contracts;
            }
        }

        public List<Contract_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery)
        {
            List<Contract_AnimeSearch> retTitles = new List<Contract_AnimeSearch>();


            try
            {
                // check if it is a title search or an ID search
                int aid = 0;
                if (int.TryParse(titleQuery, out aid))
                {
                    // user is direct entering the anime id

                    // try the local database first
                    // if not download the data from AniDB now
                    AniDB_Anime anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(aid, false,
                        ServerSettings.AniDB_DownloadRelatedAnime);
                    if (anime != null)
                    {
                        Contract_AnimeSearch res = new Contract_AnimeSearch();
                        res.AnimeID = anime.AnimeID;
                        res.MainTitle = anime.MainTitle;
                        res.Titles =
                            new HashSet<string>(anime.AllTitles.Split(new char[] {'|'},
                                StringSplitOptions.RemoveEmptyEntries));

                        // check for existing series and group details
                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
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
                    List<Providers.Azure.AnimeIDTitle> titles = Providers.Azure.AzureWebAPI.Get_AnimeTitle(titleQuery);

                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        ISessionWrapper sessionWrapper = session.Wrap();

                        foreach (Providers.Azure.AnimeIDTitle tit in titles)
                        {
                            Contract_AnimeSearch res = new Contract_AnimeSearch();
                            res.AnimeID = tit.AnimeID;
                            res.MainTitle = tit.MainTitle;
                            res.Titles =
                                new HashSet<string>(tit.Titles.Split(new char[] {'|'},
                                    StringSplitOptions.RemoveEmptyEntries));

                            // check for existing series and group details
                            AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(tit.AnimeID);
                            if (ser != null)
                            {
                                res.SeriesExists = true;
                                res.AnimeSeriesID = ser.AnimeSeriesID;
                                res.AnimeSeriesName = ser.GetAnime(sessionWrapper).GetFormattedTitle(sessionWrapper);
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
    }
}