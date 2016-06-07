using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AniDBAPI;
using AniDBAPI.Commands;
using BinaryNorthwest;
using JMMContracts;
using JMMServer.Commands;
using JMMServer.Commands.AniDB;
using JMMServer.Commands.MAL;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Providers.MovieDB;
using JMMServer.Providers.MyAnimeList;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;
using JMMServer.WebCache;
using NHibernate;
using NLog;
using CrossRef_AniDB_MAL = JMMServer.Entities.CrossRef_AniDB_MAL;
using CrossRef_AniDB_Other = JMMServer.Entities.CrossRef_AniDB_Other;
using CrossRef_AniDB_Trakt = JMMServer.Providers.Azure.CrossRef_AniDB_Trakt;
using CrossRef_AniDB_TvDB = JMMServer.Providers.Azure.CrossRef_AniDB_TvDB;
using CrossRef_File_Episode = JMMServer.Entities.CrossRef_File_Episode;

namespace JMMServer
{
    [ServiceBehavior(MaxItemsInObjectGraph = int.MaxValue)]
    public class JMMServiceImplementation : IJMMServer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public List<Contract_AnimeGroup> GetAllGroups(int userID)
        {
            var grps = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var start = DateTime.Now;
                    var repGroups = new AnimeGroupRepository();
                    var repUserGroups = new AnimeGroup_UserRepository();

                    var allGrps = repGroups.GetAll(session);
                    var ts = DateTime.Now - start;
                    logger.Info("GetAllGroups (Database) in {0} ms", ts.TotalMilliseconds);
                    start = DateTime.Now;

                    // user records
                    var repGroupUser = new AnimeGroup_UserRepository();
                    var userRecordList = repGroupUser.GetByUserID(session, userID);
                    var dictUserRecords = new Dictionary<int, AnimeGroup_User>();
                    foreach (var grpUser in userRecordList)
                        dictUserRecords[grpUser.AnimeGroupID] = grpUser;

                    foreach (var ag in allGrps)
                    {
                        AnimeGroup_User userRec = null;
                        if (dictUserRecords.ContainsKey(ag.AnimeGroupID))
                            userRec = dictUserRecords[ag.AnimeGroupID];

                        // calculate stats
                        var contract = ag.ToContract(userRec);
                        grps.Add(contract);
                    }

                    grps.Sort();
                    ts = DateTime.Now - start;
                    logger.Info("GetAllGroups (Contracts) in {0} ms", ts.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return grps;
        }

        public List<Contract_AnimeGroup> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
        {
            var grps = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGroups = new AnimeGroupRepository();

                    var grp = repGroups.GetByID(session, animeGroupID);
                    if (grp == null)
                        return grps;

                    var contractGrp = grp.ToContract(grp.GetUserRecord(session, userID));
                    grps.Add(contractGrp);

                    var groupID = grp.AnimeGroupParentID;
                    while (groupID.HasValue)
                    {
                        var grpTemp = repGroups.GetByID(session, groupID.Value);
                        if (grpTemp != null)
                        {
                            var contractGrpTemp = grpTemp.ToContract(grpTemp.GetUserRecord(session, userID));
                            grps.Add(contractGrpTemp);
                            groupID = grpTemp.AnimeGroupParentID;
                        }
                        else
                        {
                            groupID = null;
                        }
                    }

                    return grps;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return grps;
        }

        public List<Contract_AnimeGroup> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
        {
            var grps = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repSeries = new AnimeSeriesRepository();
                    var series = repSeries.GetByID(session, animeSeriesID);
                    if (series == null)
                        return grps;

                    foreach (var grp in series.AllGroupsAbove)
                    {
                        var contractGrpTemp = grp.ToContract(grp.GetUserRecord(session, userID));
                        grps.Add(contractGrpTemp);
                    }

                    return grps;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return grps;
        }

        public Contract_AnimeGroup GetGroup(int animeGroupID, int userID)
        {
            try
            {
                var repGroups = new AnimeGroupRepository();
                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return null;

                var contractGrpTemp = grp.ToContract(grp.GetUserRecord(userID));

                return contractGrpTemp;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public string DeleteAnimeGroup(int animeGroupID, bool deleteFiles)
        {
            try
            {
                var repAnimeSer = new AnimeSeriesRepository();
                var repGroups = new AnimeGroupRepository();

                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return "Group does not exist";

                var parentGroupID = grp.AnimeGroupParentID;

                foreach (var ser in grp.GetAllSeries())
                {
                    DeleteAnimeSeries(ser.AnimeSeriesID, deleteFiles, false);
                }

                // delete all sub groups
                foreach (var subGroup in grp.GetAllChildGroups())
                {
                    DeleteAnimeGroup(subGroup.AnimeGroupID, deleteFiles);
                }

                var repConditions = new GroupFilterConditionRepository();
                // delete any group filter conditions which reference these groups
                foreach (var gfc in repConditions.GetByConditionType(GroupFilterConditionType.AnimeGroup))
                {
                    var thisGrpID = 0;
                    int.TryParse(gfc.ConditionParameter, out thisGrpID);
                    if (thisGrpID == animeGroupID)
                        repConditions.Delete(gfc.GroupFilterConditionID);
                }


                repGroups.Delete(grp.AnimeGroupID);

                // finally update stats

                if (parentGroupID.HasValue)
                {
                    var grpParent = repGroups.GetByID(parentGroupID.Value);

                    if (grpParent != null)
                    {
                        grpParent.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                        StatsCache.Instance.UpdateUsingGroup(grpParent.TopLevelAnimeGroup.AnimeGroupID);
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public List<Contract_AnimeGroup> GetAnimeGroupsForFilter(int groupFilterID, int userID,
            bool getSingleSeriesGroups)
        {
            var retGroups = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var start = DateTime.Now;
                    var repGF = new GroupFilterRepository();

                    var repUsers = new JMMUserRepository();
                    var user = repUsers.GetByID(session, userID);
                    if (user == null) return retGroups;

                    GroupFilter gf = null;

                    if (groupFilterID == -999)
                    {
                        // all groups
                        gf = new GroupFilter();
                        gf.GroupFilterName = "All";
                    }
                    else
                    {
                        gf = repGF.GetByID(session, groupFilterID);
                        if (gf == null) return retGroups;
                    }

                    //Contract_GroupFilterExtended contract = gf.ToContractExtended(user);

                    var repGroups = new AnimeGroupRepository();
                    var allGrps = repGroups.GetAll(session);

                    var repUserRecords = new AnimeGroup_UserRepository();
                    var userRecords = repUserRecords.GetByUserID(session, userID);
                    var dictUserRecords = new Dictionary<int, AnimeGroup_User>();
                    foreach (var userRec in userRecords)
                        dictUserRecords[userRec.AnimeGroupID] = userRec;

                    var ts = DateTime.Now - start;
                    var msg = string.Format("Got groups for filter DB: {0} - {1} in {2} ms", gf.GroupFilterName,
                        allGrps.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    start = DateTime.Now;

                    var repSeries = new AnimeSeriesRepository();
                    var allSeries = new List<AnimeSeries>();
                    if (getSingleSeriesGroups)
                        allSeries = repSeries.GetAll(session);
                    if (StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID) &&
                        StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(gf.GroupFilterID))
                    {
                        var groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][gf.GroupFilterID];

                        foreach (var grp in allGrps)
                        {
                            AnimeGroup_User userRec = null;
                            if (dictUserRecords.ContainsKey(grp.AnimeGroupID))
                                userRec = dictUserRecords[grp.AnimeGroupID];
                            if (groups.Contains(grp.AnimeGroupID))
                            {
                                var contractGrp = grp.ToContract(userRec);
                                if (getSingleSeriesGroups)
                                {
                                    if (contractGrp.Stat_SeriesCount == 1)
                                    {
                                        var ser = GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                                        if (ser != null)
                                            contractGrp.SeriesForNameOverride =
                                                ser.ToContract(ser.GetUserRecord(session, userID));
                                    }
                                }
                                retGroups.Add(contractGrp);
                            }
                        }
                    }
                    ts = DateTime.Now - start;
                    msg = string.Format("Got groups for filter EVAL: {0} - {1} in {2} ms", gf.GroupFilterName,
                        retGroups.Count, ts.TotalMilliseconds);
                    logger.Info(msg);

                    return retGroups;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return retGroups;
        }

        public Contract_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGF = new GroupFilterRepository();
                    var gf = repGF.GetByID(session, groupFilterID);
                    if (gf == null) return null;

                    var repUsers = new JMMUserRepository();
                    var user = repUsers.GetByID(session, userID);
                    if (user == null) return null;

                    var contract = gf.ToContractExtended(session, user);

                    return contract;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public List<Contract_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
        {
            var gfs = new List<Contract_GroupFilterExtended>();
            try
            {
                var start = DateTime.Now;
                var repGF = new GroupFilterRepository();

                var repUsers = new JMMUserRepository();
                var user = repUsers.GetByID(userID);
                if (user == null) return gfs;

                var allGfs = repGF.GetAll();
                var ts = DateTime.Now - start;
                logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);
                start = DateTime.Now;

                var repGroups = new AnimeGroupRepository();
                var allGrps = repGroups.GetAllTopLevelGroups();
                ts = DateTime.Now - start;
                logger.Info("GetAllGroups (Database) in {0} ms", ts.TotalMilliseconds);
                start = DateTime.Now;

                foreach (var gf in allGfs)
                {
                    var gfContract = gf.ToContract();
                    var gfeContract = new Contract_GroupFilterExtended();
                    gfeContract.GroupFilter = gfContract;
                    gfeContract.GroupCount = 0;
                    gfeContract.SeriesCount = 0;
                    if (StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID) &&
                        StatsCache.Instance.StatUserGroupFilter[userID].ContainsKey(gf.GroupFilterID))
                    {
                        var groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][gf.GroupFilterID];

                        foreach (var grp in allGrps)
                        {
                            if (groups.Contains(grp.AnimeGroupID))
                            {
                                // calculate stats
                                gfeContract.GroupCount++;
                            }
                        }
                    }
                    gfs.Add(gfeContract);
                }

                ts = DateTime.Now - start;
                logger.Info("GetAllGroupFiltersExtended (FILTER) in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return gfs;
        }

        public List<Contract_GroupFilter> GetAllGroupFilters()
        {
            var gfs = new List<Contract_GroupFilter>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var start = DateTime.Now;
                    var repGF = new GroupFilterRepository();


                    var allGfs = repGF.GetAll(session);
                    var ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (var gf in allGfs)
                    {
                        gfs.Add(gf.ToContract(session));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return gfs;
        }

        public List<Contract_Playlist> GetAllPlaylists()
        {
            var pls = new List<Contract_Playlist>();
            try
            {
                var repPlaylist = new PlaylistRepository();


                var allPls = repPlaylist.GetAll();
                foreach (var pl in allPls)
                    pls.Add(pl.ToContract());
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return pls;
        }

        public Contract_Playlist_SaveResponse SavePlaylist(Contract_Playlist contract)
        {
            var contractRet = new Contract_Playlist_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                var repPlaylist = new PlaylistRepository();

                // Process the playlist
                Playlist pl = null;
                if (contract.PlaylistID.HasValue)
                {
                    pl = repPlaylist.GetByID(contract.PlaylistID.Value);
                    if (pl == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Playlist with ID: " +
                                                   contract.PlaylistID.Value;
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

                repPlaylist.Save(pl);

                contractRet.Playlist = pl.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeletePlaylist(int playlistID)
        {
            try
            {
                var repPlaylist = new PlaylistRepository();

                var pl = repPlaylist.GetByID(playlistID);
                if (pl == null)
                    return "Playlist not found";

                repPlaylist.Delete(playlistID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public Contract_Playlist GetPlaylist(int playlistID)
        {
            try
            {
                var repPlaylist = new PlaylistRepository();

                var pl = repPlaylist.GetByID(playlistID);
                if (pl == null)
                    return null;

                return pl.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_BookmarkedAnime> GetAllBookmarkedAnime()
        {
            var baList = new List<Contract_BookmarkedAnime>();
            try
            {
                var repBA = new BookmarkedAnimeRepository();


                var allBAs = repBA.GetAll();
                foreach (var ba in allBAs)
                    baList.Add(ba.ToContract());
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return baList;
        }

        public Contract_BookmarkedAnime_SaveResponse SaveBookmarkedAnime(Contract_BookmarkedAnime contract)
        {
            var contractRet = new Contract_BookmarkedAnime_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                var repBA = new BookmarkedAnimeRepository();

                BookmarkedAnime ba = null;
                if (contract.BookmarkedAnimeID.HasValue)
                {
                    ba = repBA.GetByID(contract.BookmarkedAnimeID.Value);
                    if (ba == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Bookmark with ID: " +
                                                   contract.BookmarkedAnimeID.Value;
                        return contractRet;
                    }
                }
                else
                {
                    // if a new record, check if it is allowed
                    var baTemp = repBA.GetByAnimeID(contract.AnimeID);
                    if (baTemp != null)
                    {
                        contractRet.ErrorMessage = "A bookmark with the AnimeID already exists: " + contract.AnimeID;
                        return contractRet;
                    }

                    ba = new BookmarkedAnime();
                }

                ba.AnimeID = contract.AnimeID;
                ba.Priority = contract.Priority;
                ba.Notes = contract.Notes;
                ba.Downloading = contract.Downloading;

                repBA.Save(ba);

                contractRet.BookmarkedAnime = ba.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {
                var repBA = new BookmarkedAnimeRepository();

                var ba = repBA.GetByID(bookmarkedAnimeID);
                if (ba == null)
                    return "Bookmarked not found";

                repBA.Delete(bookmarkedAnimeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public Contract_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID)
        {
            try
            {
                var repBA = new BookmarkedAnimeRepository();

                var ba = repBA.GetByID(bookmarkedAnimeID);
                if (ba == null)
                    return null;

                return ba.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public Contract_GroupFilter_SaveResponse SaveGroupFilter(Contract_GroupFilter contract)
        {
            var response = new Contract_GroupFilter_SaveResponse();
            response.ErrorMessage = string.Empty;
            response.GroupFilter = null;

            var repGF = new GroupFilterRepository();
            var repGFC = new GroupFilterConditionRepository();

            // Process the group
            GroupFilter gf = null;
            if (contract.GroupFilterID.HasValue)
            {
                gf = repGF.GetByID(contract.GroupFilterID.Value);
                if (gf == null)
                {
                    response.ErrorMessage = "Could not find existing Group Filter with ID: " +
                                            contract.GroupFilterID.Value;
                    return response;
                }
            }
            else
                gf = new GroupFilter();

            gf.GroupFilterName = contract.GroupFilterName;
            gf.ApplyToSeries = contract.ApplyToSeries;
            gf.BaseCondition = contract.BaseCondition;
            gf.SortingCriteria = contract.SortingCriteria;

            if (string.IsNullOrEmpty(gf.GroupFilterName))
            {
                response.ErrorMessage = "Must specify a group filter name";
                return response;
            }

            repGF.Save(gf);

            // Process the filter conditions

            // check for any that have been deleted
            foreach (var gfc in gf.FilterConditions)
            {
                var gfcExists = false;
                foreach (var gfc_con in contract.FilterConditions)
                {
                    if (gfc_con.GroupFilterConditionID.HasValue &&
                        gfc_con.GroupFilterConditionID.Value == gfc.GroupFilterConditionID)
                    {
                        gfcExists = true;
                        break;
                    }
                }
                if (!gfcExists)
                    repGFC.Delete(gfc.GroupFilterConditionID);
            }

            // save newly added or modified ones
            foreach (var gfc_con in contract.FilterConditions)
            {
                GroupFilterCondition gfc = null;
                if (gfc_con.GroupFilterConditionID.HasValue)
                {
                    gfc = repGFC.GetByID(gfc_con.GroupFilterConditionID.Value);
                    if (gfc == null)
                    {
                        response.ErrorMessage = "Could not find existing Group Filter Condition with ID: " +
                                                gfc_con.GroupFilterConditionID;
                        return response;
                    }
                }
                else
                    gfc = new GroupFilterCondition();

                gfc.ConditionOperator = gfc_con.ConditionOperator;
                gfc.ConditionParameter = gfc_con.ConditionParameter;
                gfc.ConditionType = gfc_con.ConditionType;
                gfc.GroupFilterID = gf.GroupFilterID;

                repGFC.Save(gfc);
            }

            response.GroupFilter = gf.ToContract();

            return response;
        }

        public string DeleteGroupFilter(int groupFilterID)
        {
            try
            {
                var repGF = new GroupFilterRepository();
                var repGFC = new GroupFilterConditionRepository();

                var gf = repGF.GetByID(groupFilterID);
                if (gf == null)
                    return "Group Filter not found";

                // delete all the conditions first
                foreach (var gfc in gf.FilterConditions)
                    repGFC.Delete(gfc.GroupFilterConditionID);

                repGF.Delete(groupFilterID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public Contract_AnimeGroup_SaveResponse SaveGroup(Contract_AnimeGroup_Save contract, int userID)
        {
            var contractout = new Contract_AnimeGroup_SaveResponse();
            contractout.ErrorMessage = "";
            contractout.AnimeGroup = null;
            try
            {
                var repGroup = new AnimeGroupRepository();
                AnimeGroup grp = null;
                if (contract.AnimeGroupID.HasValue)
                {
                    grp = repGroup.GetByID(contract.AnimeGroupID.Value);
                    if (grp == null)
                    {
                        contractout.ErrorMessage = "Could not find existing group with ID: " +
                                                   contract.AnimeGroupID.Value;
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

                repGroup.Save(grp);

                var userRecord = grp.GetUserRecord(userID);
                if (userRecord == null) userRecord = new AnimeGroup_User(userID, grp.AnimeGroupID);
                userRecord.IsFave = contract.IsFave;
                var repUserRecords = new AnimeGroup_UserRepository();
                repUserRecords.Save(userRecord);

                var contractGrp = grp.ToContract(grp.GetUserRecord(userID));
                contractout.AnimeGroup = contractGrp;


                return contractout;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public Contract_AnimeSeries_SaveResponse MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID)
        {
            var contractout = new Contract_AnimeSeries_SaveResponse();
            contractout.ErrorMessage = "";
            contractout.AnimeSeries = null;
            try
            {
                var repSeries = new AnimeSeriesRepository();
                AnimeSeries ser = null;

                ser = repSeries.GetByID(animeSeriesID);
                if (ser == null)
                {
                    contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID;
                    return contractout;
                }

                // make sure the group exists
                var repGroups = new AnimeGroupRepository();
                var grpTemp = repGroups.GetByID(newAnimeGroupID);
                if (grpTemp == null)
                {
                    contractout.ErrorMessage = "Could not find existing group with ID: " + newAnimeGroupID;
                    return contractout;
                }

                var oldGroupID = ser.AnimeGroupID;
                ser.AnimeGroupID = newAnimeGroupID;
                ser.DateTimeUpdated = DateTime.Now;

                repSeries.Save(ser);

                // update stats for new groups
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                ser.QueueUpdateStats();

                // update stats for old groups
                var grp = repGroups.GetByID(oldGroupID);
                if (grp != null)
                {
                    grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                }

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                    return contractout;
                }
                var xrefs = ser.GetCrossRefTvDBV2();
                var xrefMAL = ser.CrossRefMAL;

                var sers = new List<TvDB_Series>();
                foreach (var xref in xrefs)
                    sers.Add(xref.GetTvDBSeries());
                var xrefMovie = ser.CrossRefMovieDB;
                MovieDB_Movie movie = null;
                if (xrefMovie != null)
                    movie = xrefMovie.GetMovieDB_Movie();
                contractout.AnimeSeries = ser.ToContract(anime, xrefs, xrefMovie,
                    ser.GetUserRecord(userID), sers, xrefMAL, false, null, null, null, null, movie);

                return contractout;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public Contract_AnimeSeries_SaveResponse SaveSeries(Contract_AnimeSeries_Save contract, int userID)
        {
            var contractout = new Contract_AnimeSeries_SaveResponse();
            contractout.ErrorMessage = "";
            contractout.AnimeSeries = null;
            try
            {
                var repSeries = new AnimeSeriesRepository();
                AnimeSeries ser = null;

                int? oldGroupID = null;
                if (contract.AnimeSeriesID.HasValue)
                {
                    ser = repSeries.GetByID(contract.AnimeSeriesID.Value);
                    if (ser == null)
                    {
                        contractout.ErrorMessage = "Could not find existing series with ID: " +
                                                   contract.AnimeSeriesID.Value;
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

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                    return contractout;
                }

                repSeries.Save(ser);

                // update stats for groups
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true ,true, true);
                ser.QueueUpdateStats();

                if (oldGroupID.HasValue)
                {
                    var repGroups = new AnimeGroupRepository();
                    var grp = repGroups.GetByID(oldGroupID.Value);
                    if (grp != null)
                    {
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                    }
                }
                var xrefs = ser.GetCrossRefTvDBV2();
                var xrefMAL = ser.CrossRefMAL;

                var sers = new List<TvDB_Series>();
                foreach (var xref in xrefs)
                    sers.Add(xref.GetTvDBSeries());
                var xrefMovie = ser.CrossRefMovieDB;
                MovieDB_Movie movie = null;
                if (xrefMovie != null)
                    movie = xrefMovie.GetMovieDB_Movie();
                contractout.AnimeSeries = ser.ToContract(anime, xrefs, ser.CrossRefMovieDB, ser.GetUserRecord(userID),
                    sers, xrefMAL, false, null, null, null, null, movie);

                return contractout;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public Contract_AnimeEpisode GetEpisode(int animeEpisodeID, int userID)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(animeEpisodeID);
                if (ep == null) return null;

                return ep.ToContract(userID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public Contract_AnimeEpisode GetEpisodeByAniDBEpisodeID(int episodeID, int userID)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByAniDBEpisodeID(episodeID);
                if (ep == null) return null;

                return ep.ToContract(userID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public string RemoveAssociationOnFile(int videoLocalID, int aniDBEpisodeID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var repXRefs = new CrossRef_File_EpisodeRepository();

                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";

                int? animeSeriesID = null;
                foreach (var ep in vid.GetAnimeEpisodes())
                {
                    if (ep.AniDB_EpisodeID != aniDBEpisodeID) continue;

                    animeSeriesID = ep.AnimeSeriesID;
                    var xref = repXRefs.GetByHashAndEpisodeID(vid.Hash, ep.AniDB_EpisodeID);
                    if (xref != null)
                    {
                        if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                            return "Cannot remove associations created from AniDB data";

                        // delete cross ref from web cache 
                        var cr = new CommandRequest_WebCacheDeleteXRefFileEpisode(vid.Hash, ep.AniDB_EpisodeID);
                        cr.Save();

                        repXRefs.Delete(xref.CrossRef_File_EpisodeID);
                    }
                }

                if (animeSeriesID.HasValue)
                {
                    var repSeries = new AnimeSeriesRepository();
                    var ser = repSeries.GetByID(animeSeriesID.Value);
                    if (ser != null)
                        ser.QueueUpdateStats();
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";

                vid.IsIgnored = isIgnored ? 1 : 0;
                repVids.Save(vid);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string SetVariationStatusOnFile(int videoLocalID, bool isVariation)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";

                vid.IsVariation = isVariation ? 1 : 0;
                repVids.Save(vid);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string AssociateSingleFile(int videoLocalID, int animeEpisodeID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";

                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(animeEpisodeID);
                if (ep == null)
                    return "Could not find episode record";

                var repXRefs = new CrossRef_File_EpisodeRepository();
                var xref = new CrossRef_File_Episode();
                try
                {
                    xref.PopulateManually(vid, ep);
                }
                catch (Exception ex)
                {
                    var msg = string.Format("Error populating XREF: {0}", vid.ToStringDetailed());
                    throw;
                }
                repXRefs.Save(xref);

                vid.RenameIfRequired();
                vid.MoveFileIfRequired();

                var cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                cr.Save();

                var ser = ep.GetAnimeSeries();
                ser.QueueUpdateStats();

                // update epidsode added stats
                var repSeries = new AnimeSeriesRepository();
                ser.EpisodeAddedDate = DateTime.Now;
                repSeries.Save(ser);

                var repGroups = new AnimeGroupRepository();
                foreach (var grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    repGroups.Save(grp);
                }

                var cmdAddFile = new CommandRequest_AddFileToMyList(vid.Hash);
                cmdAddFile.Save();

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return "";
        }

        public string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startEpNum,
            int endEpNum)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";

                var repEps = new AnimeEpisodeRepository();
                var repSeries = new AnimeSeriesRepository();
                var repAniEps = new AniDB_EpisodeRepository();
                var repXRefs = new CrossRef_File_EpisodeRepository();

                var ser = repSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";

                for (var i = startEpNum; i <= endEpNum; i++)
                {
                    var anieps = repAniEps.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, i);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    var aniep = anieps[0];

                    var eps = repEps.GetByAniEpisodeIDAndSeriesID(aniep.EpisodeID, ser.AnimeSeriesID);
                    if (eps.Count == 0)
                        return "Could not find episode record";

                    var ep = eps[0];

                    var xref = new CrossRef_File_Episode();
                    xref.PopulateManually(vid, ep);
                    repXRefs.Save(xref);

                    var cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                    cr.Save();
                }

                vid.RenameIfRequired();
                vid.MoveFileIfRequired();

                ser.QueueUpdateStats();

                // update epidsode added stats
                ser.EpisodeAddedDate = DateTime.Now;
                repSeries.Save(ser);

                var repGroups = new AnimeGroupRepository();
                foreach (var grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    repGroups.Save(grp);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return "";
        }

        public string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber,
            bool singleEpisode)
        {
            try
            {
                var repXRefs = new CrossRef_File_EpisodeRepository();
                var repSeries = new AnimeSeriesRepository();
                var repVids = new VideoLocalRepository();
                var repAniEps = new AniDB_EpisodeRepository();
                var repEps = new AnimeEpisodeRepository();

                var ser = repSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";

                var epNumber = startingEpisodeNumber;
                var count = 1;


                foreach (var videoLocalID in videoLocalIDs)
                {
                    var vid = repVids.GetByID(videoLocalID);
                    if (vid == null)
                        return "Could not find video local record";

                    var anieps = repAniEps.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, epNumber);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    var aniep = anieps[0];

                    var eps = repEps.GetByAniEpisodeIDAndSeriesID(aniep.EpisodeID, ser.AnimeSeriesID);
                    if (eps.Count == 0)
                        return "Could not find episode record";

                    var ep = eps[0];

                    var xref = new CrossRef_File_Episode();
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

                    repXRefs.Save(xref);

                    vid.RenameIfRequired();
                    vid.MoveFileIfRequired();

                    var cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
                    cr.Save();

                    count++;
                    if (!singleEpisode) epNumber++;
                }

                ser.QueueUpdateStats();

                // update epidsode added stats
                ser.EpisodeAddedDate = DateTime.Now;
                repSeries.Save(ser);

                var repGroups = new AnimeGroupRepository();
                foreach (var grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    repGroups.Save(grp);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return "";
        }

        public Contract_AnimeSeries_SaveResponse CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID)
        {
            var response = new Contract_AnimeSeries_SaveResponse();
            response.AnimeSeries = null;
            response.ErrorMessage = "";
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGroups = new AnimeGroupRepository();
                    if (animeGroupID.HasValue)
                    {
                        var grp = repGroups.GetByID(session, animeGroupID.Value);
                        if (grp == null)
                        {
                            response.ErrorMessage = "Could not find the specified group";
                            return response;
                        }
                    }

                    // make sure a series doesn't already exists for this anime
                    var repSeries = new AnimeSeriesRepository();
                    var ser = repSeries.GetByAnimeID(session, animeID);
                    if (ser != null)
                    {
                        response.ErrorMessage = "A series already exists for this anime";
                        return response;
                    }

                    // make sure the anime exists first
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(session, animeID);
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
                        repSeries.Save(ser);
                    }
                    else
                    {
                        ser = anime.CreateAnimeSeriesAndGroup(session);
                    }

                    ser.CreateAnimeEpisodes(session);

                    // check if we have any group status data for this associated anime
                    // if not we will download it now
                    var repStatus = new AniDB_GroupStatusRepository();
                    if (repStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                    {
                        var cmdStatus = new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                        cmdStatus.Save(session);
                    }


                    ser.QueueUpdateStats();

                    // check for TvDB associations
                    var cmd = new CommandRequest_TvDBSearchAnime(anime.AnimeID, false);
                    cmd.Save(session);

                    if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        // check for Trakt associations
                        var cmd2 = new CommandRequest_TraktSearchAnime(anime.AnimeID, false);
                        cmd2.Save(session);
                    }

                    var xrefs = ser.GetCrossRefTvDBV2();
                    var xrefMAL = ser.CrossRefMAL;

                    var sers = new List<TvDB_Series>();
                    foreach (var xref in xrefs)
                        sers.Add(xref.GetTvDBSeries());
                    var xrefMovie = ser.CrossRefMovieDB;
                    MovieDB_Movie movie = null;
                    if (xrefMovie != null)
                        movie = xrefMovie.GetMovieDB_Movie();
                    response.AnimeSeries = ser.ToContract(anime, xrefs, xrefMovie, ser.GetUserRecord(userID),
                        sers, xrefMAL, false, null, null, null, null, movie);
                    return response;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                    var repVids = new VideoLocalRepository();
                    var repFiles = new AniDB_FileRepository();

                    foreach (var vid in repVids.GetByAniDBAnimeID(session, animeID))
                    {
                        var aniFile = vid.GetAniDBFile(session);
                        if (aniFile == null) continue;

                        if (aniFile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var cmd = new CommandRequest_GetFile(vid.VideoLocalID, true);
                            cmd.Save(session);
                        }
                    }

                    // update group status information
                    var cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID, true);
                    cmdStatus.Save(session);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
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
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null) return "File could not be found";

                var cmd = new CommandRequest_GetFile(vid.VideoLocalID, true);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
            return "";
        }

        public string UpdateEpisodeData(int episodeID)
        {
            try
            {
                var cmd = new CommandRequest_GetEpisode(episodeID);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
            return "";
        }

        public string RescanFile(int videoLocalID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null) return "File could not be found";

                var cmd = new CommandRequest_ProcessFile(vid.VideoLocalID, true);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
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
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return "";
        }

        public string SyncTraktSeries(int animeID)
        {
            try
            {
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(animeID);
                if (ser == null) return "Could not find Anime Series";

                var cmd = new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID, ser.GetSeriesName());
                cmd.Save();

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return "";
        }

        public Contract_AniDBAnime GetAnime(int animeID)
        {
            var repAnime = new AniDB_AnimeRepository();

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var anime = repAnime.GetByAnimeID(session, animeID);
                    if (anime == null) return null;

                    var contract = anime.ToContract();

                    var defaultPoster = anime.GetDefaultPoster(session);
                    if (defaultPoster == null)
                        contract.DefaultImagePoster = null;
                    else
                        contract.DefaultImagePoster = defaultPoster.ToContract(session);

                    var defaultFanart = anime.GetDefaultFanart(session);
                    if (defaultFanart == null)
                        contract.DefaultImageFanart = null;
                    else
                        contract.DefaultImageFanart = defaultFanart.ToContract(session);

                    var defaultWideBanner = anime.GetDefaultWideBanner(session);
                    if (defaultWideBanner == null)
                        contract.DefaultImageWideBanner = null;
                    else
                        contract.DefaultImageWideBanner = defaultWideBanner.ToContract(session);


                    return contract;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public List<Contract_AniDBAnime> GetAllAnime()
        {
            var contracts = new List<Contract_AniDBAnime>();

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var start = DateTime.Now;

                    var repAnime = new AniDB_AnimeRepository();
                    var animes = repAnime.GetAll(session);

                    var repDefaults = new AniDB_Anime_DefaultImageRepository();
                    var allDefaultImages = repDefaults.GetAll(session);

                    var dictDefaultsPosters = new Dictionary<int, AniDB_Anime_DefaultImage>();
                    var dictDefaultsFanart = new Dictionary<int, AniDB_Anime_DefaultImage>();
                    var dictDefaultsWideBanner = new Dictionary<int, AniDB_Anime_DefaultImage>();

                    foreach (var defaultImage in allDefaultImages)
                    {
                        var sizeType = (ImageSizeType)defaultImage.ImageType;

                        if (sizeType == ImageSizeType.Fanart)
                            dictDefaultsFanart[defaultImage.AnimeID] = defaultImage;

                        if (sizeType == ImageSizeType.Poster)
                            dictDefaultsPosters[defaultImage.AnimeID] = defaultImage;

                        if (sizeType == ImageSizeType.WideBanner)
                            dictDefaultsWideBanner[defaultImage.AnimeID] = defaultImage;
                    }

                    foreach (var anime in animes)
                    {
                        var contract = anime.ToContract();

                        if (dictDefaultsFanart.ContainsKey(anime.AnimeID))
                            contract.DefaultImageFanart = dictDefaultsFanart[anime.AnimeID].ToContract();
                        else contract.DefaultImageFanart = null;

                        if (dictDefaultsPosters.ContainsKey(anime.AnimeID))
                            contract.DefaultImagePoster = dictDefaultsPosters[anime.AnimeID].ToContract();
                        else contract.DefaultImagePoster = null;

                        if (dictDefaultsWideBanner.ContainsKey(anime.AnimeID))
                            contract.DefaultImageWideBanner = dictDefaultsWideBanner[anime.AnimeID].ToContract();
                        else contract.DefaultImageWideBanner = null;

                        contracts.Add(contract);
                        //anime.ToContractDetailed();
                    }

                    var ts = DateTime.Now - start;
                    logger.Info("GetAllAnimein {0} ms", ts.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState,
            int userID)
        {
            var contracts = new List<Contract_AnimeRating>();

            try
            {
                var repSeries = new AnimeSeriesRepository();
                var series = repSeries.GetAll();
                var dictSeries = new Dictionary<int, AnimeSeries>();
                foreach (var ser in series)
                    dictSeries[ser.AniDB_ID] = ser;

                var _collectionState = (RatingCollectionState)collectionState;
                var _watchedState = (RatingWatchedState)watchedState;
                var _ratingVotedState = (RatingVotedState)ratingVotedState;

                var start = DateTime.Now;

                var repAnime = new AniDB_AnimeRepository();

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

                var animes = repAnime.GetAll();

                // user votes
                var repVotes = new AniDB_VoteRepository();
                var allVotes = repVotes.GetAll();

                var repUsers = new JMMUserRepository();
                var user = repUsers.GetByID(userID);
                if (user == null) return contracts;

                var i = 0;


                foreach (var anime in animes)
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
                        var userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                        if (userRec == null) continue;
                        if (userRec.UnwatchedEpisodeCount > 0) continue;
                    }

                    if (_watchedState == RatingWatchedState.NotWatched)
                    {
                        if (dictSeries.ContainsKey(anime.AnimeID))
                        {
                            var userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                            if (userRec != null)
                            {
                                if (userRec.UnwatchedEpisodeCount == 0) continue;
                            }
                        }
                    }

                    // evaluate voted states
                    if (_ratingVotedState == RatingVotedState.Voted)
                    {
                        var voted = false;
                        foreach (var vote in allVotes)
                        {
                            if (vote.EntityID == anime.AnimeID &&
                                (vote.VoteType == (int)AniDBVoteType.Anime ||
                                 vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                            {
                                voted = true;
                                break;
                            }
                        }

                        if (!voted) continue;
                    }

                    if (_ratingVotedState == RatingVotedState.NotVoted)
                    {
                        var voted = false;
                        foreach (var vote in allVotes)
                        {
                            if (vote.EntityID == anime.AnimeID &&
                                (vote.VoteType == (int)AniDBVoteType.Anime ||
                                 vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                            {
                                voted = true;
                                break;
                            }
                        }

                        if (voted) continue;
                    }

                    var contract = new Contract_AnimeRating();
                    contract.AnimeID = anime.AnimeID;


                    var contractAnimeDetailed = new Contract_AniDB_AnimeDetailed();

                    contractAnimeDetailed.AnimeTitles = new List<Contract_AnimeTitle>();
                    contractAnimeDetailed.Tags = new List<Contract_AnimeTag>();
                    contractAnimeDetailed.CustomTags = new List<Contract_CustomTag>();
                    contractAnimeDetailed.UserVote = null;

                    contractAnimeDetailed.AniDBAnime = anime.ToContract();


                    // get user vote
                    foreach (var vote in allVotes)
                    {
                        if (vote.EntityID == anime.AnimeID &&
                            (vote.VoteType == (int)AniDBVoteType.Anime ||
                             vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                        {
                            contractAnimeDetailed.UserVote = vote.ToContract();
                            break;
                        }
                    }

                    contract.AnimeDetailed = contractAnimeDetailed;

                    if (dictSeries.ContainsKey(anime.AnimeID))
                    {
                        contract.AnimeSeries =
                            dictSeries[anime.AnimeID].ToContract(dictSeries[anime.AnimeID].GetUserRecord(userID));
                    }

                    contracts.Add(contract);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_AniDB_AnimeDetailed> GetAllAnimeDetailed()
        {
            var contracts = new List<Contract_AniDB_AnimeDetailed>();
            var countElements = 0;
            try
            {
                var start = DateTime.Now;

                var repAnime = new AniDB_AnimeRepository();

                // build a dictionary of titles
                var repTitles = new AniDB_Anime_TitleRepository();


                var allTitles = repTitles.GetAll();
                var allTitlesDict = new Dictionary<int, List<AniDB_Anime_Title>>();
                foreach (var title in allTitles)
                {
                    if (!allTitlesDict.ContainsKey(title.AnimeID))
                        allTitlesDict[title.AnimeID] = new List<AniDB_Anime_Title>();

                    allTitlesDict[title.AnimeID].Add(title);
                }


                // build a dictionary of tags
                var repTags = new AniDB_TagRepository();
                var repAnimeTag = new AniDB_Anime_TagRepository();

                var allTags = repTags.GetAll();
                var allTagsDict = new Dictionary<int, AniDB_Tag>();
                foreach (var tag in allTags)
                    allTagsDict[tag.TagID] = tag;


                var allAnimeTags = repAnimeTag.GetAll();
                var allAnimeTagsDict = new Dictionary<int, List<AniDB_Anime_Tag>>(); // 
                foreach (var aniTag in allAnimeTags)
                {
                    if (!allAnimeTagsDict.ContainsKey(aniTag.AnimeID))
                        allAnimeTagsDict[aniTag.AnimeID] = new List<AniDB_Anime_Tag>();

                    allAnimeTagsDict[aniTag.AnimeID].Add(aniTag);
                }

                // build a dictionary of custom tags
                var repCustomTags = new CustomTagRepository();
                var repXRefCustomTags = new CrossRef_CustomTagRepository();

                var allCustomTags = repCustomTags.GetAll();
                var allCustomTagsDict = new Dictionary<int, CustomTag>();
                foreach (var tag in allCustomTags)
                    allCustomTagsDict[tag.CustomTagID] = tag;

                var allCustomTagsXRefs = repXRefCustomTags.GetAll();
                var allCustomTagsXRefDict = new Dictionary<int, List<CrossRef_CustomTag>>(); // 
                foreach (var aniTag in allCustomTagsXRefs)
                {
                    if (!allCustomTagsXRefDict.ContainsKey(aniTag.CrossRefID))
                        allCustomTagsXRefDict[aniTag.CrossRefID] = new List<CrossRef_CustomTag>();

                    allCustomTagsXRefDict[aniTag.CrossRefID].Add(aniTag);
                }

                // build a dictionary of languages
                var rep = new AdhocRepository();
                var dictAudioStats = rep.GetAudioLanguageStatsForAnime();
                var dictSubtitleStats = rep.GetSubtitleLanguageStatsForAnime();

                var dictAnimeVideoQualStats = rep.GetAllVideoQualityByAnime();
                var dictAnimeEpisodeVideoQualStats = rep.GetEpisodeVideoQualityStatsByAnime();

                var animes = repAnime.GetAll();

                // user votes
                var repVotes = new AniDB_VoteRepository();
                var allVotes = repVotes.GetAll();

                var i = 0;


                foreach (var anime in animes)
                {
                    i++;
                    //if (i >= 10) continue;

                    countElements++;

                    var contract = new Contract_AniDB_AnimeDetailed();

                    contract.AnimeTitles = new List<Contract_AnimeTitle>();
                    contract.Tags = new List<Contract_AnimeTag>();
                    contract.CustomTags = new List<Contract_CustomTag>();
                    contract.UserVote = null;

                    contract.AniDBAnime = anime.ToContract();

                    if (dictAnimeVideoQualStats.ContainsKey(anime.AnimeID))
                        contract.Stat_AllVideoQuality = dictAnimeVideoQualStats[anime.AnimeID];
                    else contract.Stat_AllVideoQuality = "";

                    contract.Stat_AllVideoQuality_Episodes = "";

                    // All Video Quality Episodes
                    // Try to determine if this anime has all the episodes available at a certain video quality
                    // e.g.  the series has all episodes in blu-ray
                    if (dictAnimeEpisodeVideoQualStats.ContainsKey(anime.AnimeID))
                    {
                        var stat = dictAnimeEpisodeVideoQualStats[anime.AnimeID];
                        foreach (var kvp in stat.VideoQualityEpisodeCount)
                        {
                            if (kvp.Value >= anime.EpisodeCountNormal)
                            {
                                if (contract.Stat_AllVideoQuality_Episodes.Length > 0)
                                    contract.Stat_AllVideoQuality_Episodes += ",";
                                contract.Stat_AllVideoQuality_Episodes += kvp.Key;
                            }
                        }
                    }

                    var audioLanguageList = new List<string>();
                    var subtitleLanguageList = new List<string>();

                    // get audio languages
                    if (dictAudioStats.ContainsKey(anime.AnimeID))
                    {
                        foreach (var lanName in dictAudioStats[anime.AnimeID].LanguageNames)
                        {
                            if (!audioLanguageList.Contains(lanName)) audioLanguageList.Add(lanName);
                        }
                    }

                    // get subtitle languages
                    if (dictSubtitleStats.ContainsKey(anime.AnimeID))
                    {
                        foreach (var lanName in dictSubtitleStats[anime.AnimeID].LanguageNames)
                        {
                            if (!subtitleLanguageList.Contains(lanName)) subtitleLanguageList.Add(lanName);
                        }
                    }

                    contract.Stat_AudioLanguages = "";
                    foreach (var audioLan in audioLanguageList)
                    {
                        if (contract.Stat_AudioLanguages.Length > 0) contract.Stat_AudioLanguages += ",";
                        contract.Stat_AudioLanguages += audioLan;
                    }

                    contract.Stat_SubtitleLanguages = "";
                    foreach (var subLan in subtitleLanguageList)
                    {
                        if (contract.Stat_SubtitleLanguages.Length > 0) contract.Stat_SubtitleLanguages += ",";
                        contract.Stat_SubtitleLanguages += subLan;
                    }


                    if (allTitlesDict.ContainsKey(anime.AnimeID))
                    {
                        foreach (var title in allTitlesDict[anime.AnimeID])
                        {
                            var ctitle = new Contract_AnimeTitle();
                            ctitle.AnimeID = title.AnimeID;
                            ctitle.Language = title.Language;
                            ctitle.Title = title.Title;
                            ctitle.TitleType = title.TitleType;
                            contract.AnimeTitles.Add(ctitle);
                            countElements++;
                        }
                    }


                    if (allAnimeTagsDict.ContainsKey(anime.AnimeID))
                    {
                        var aniTags = allAnimeTagsDict[anime.AnimeID];
                        foreach (var aniTag in aniTags)
                        {
                            if (allTagsDict.ContainsKey(aniTag.TagID))
                            {
                                var tag = allTagsDict[aniTag.TagID];

                                var ctag = new Contract_AnimeTag();
                                ctag.Weight = aniTag.Weight;
                                ctag.GlobalSpoiler = tag.GlobalSpoiler;
                                ctag.LocalSpoiler = tag.LocalSpoiler;
                                //ctag.Spoiler = tag.Spoiler;
                                //ctag.TagCount = tag.TagCount;
                                ctag.TagDescription = tag.TagDescription;
                                ctag.TagID = tag.TagID;
                                ctag.TagName = tag.TagName;
                                contract.Tags.Add(ctag);
                                countElements++;
                            }
                        }
                    }

                    //TODO - Custom Tags: add custom tags

                    if (allCustomTagsXRefDict.ContainsKey(anime.AnimeID))
                    {
                        var aniTags = allCustomTagsXRefDict[anime.AnimeID];
                        foreach (var aniTag in aniTags)
                        {
                            if (allCustomTagsDict.ContainsKey(aniTag.CustomTagID))
                            {
                                contract.CustomTags.Add(allCustomTagsDict[aniTag.CustomTagID].ToContract());
                                countElements++;
                            }
                        }
                    }

                    // get user vote
                    foreach (var vote in allVotes)
                    {
                        if (vote.EntityID == anime.AnimeID &&
                            (vote.VoteType == (int)AniDBVoteType.Anime ||
                             vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                        {
                            contract.UserVote = vote.ToContract();
                            break;
                        }
                    }

                    contracts.Add(contract);
                }


                var ts = DateTime.Now - start;
                logger.Info("GetAllAnimeDetailed in {0} ms {1}", ts.TotalMilliseconds, countElements);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_AnimeSeries> GetAllSeries(int userID)
        {
            var repSeries = new AnimeSeriesRepository();
            var repAnime = new AniDB_AnimeRepository();
            var repTitles = new AniDB_Anime_TitleRepository();

            //TODO: Custom Tags: Do I need to add custom tags for searches

            // get all the series
            var seriesContractList = new List<Contract_AnimeSeries>();

            try
            {
                var start = DateTime.Now;
                var start2 = DateTime.Now;

                var series = repSeries.GetAll();

                var animes = repAnime.GetAll();
                var dictAnimes = new Dictionary<int, AniDB_Anime>();
                foreach (var anime in animes)
                    dictAnimes[anime.AnimeID] = anime;

                var ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:Anime:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // tvdb - cross refs
                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var allCrossRefs = repCrossRef.GetAll();
                var dictCrossRefsV2 = new Dictionary<int, List<CrossRef_AniDB_TvDBV2>>();
                foreach (var xref in allCrossRefs)
                {
                    if (!dictCrossRefsV2.ContainsKey(xref.AnimeID))
                        dictCrossRefsV2[xref.AnimeID] = new List<CrossRef_AniDB_TvDBV2>();
                    dictCrossRefsV2[xref.AnimeID].Add(xref);
                }

                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:TvDB CrossRefs:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // tvdb - series info
                var repTvSeries = new TvDB_SeriesRepository();
                var allTvSeries = repTvSeries.GetAll();
                var dictTvSeries = new Dictionary<int, TvDB_Series>();
                foreach (var tvs in allTvSeries)
                    dictTvSeries[tvs.SeriesID] = tvs;

                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:TvDB:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // moviedb
                var repOtherCrossRef = new CrossRef_AniDB_OtherRepository();
                var allOtherCrossRefs = repOtherCrossRef.GetAll();
                var dictMovieCrossRefs = new Dictionary<int, CrossRef_AniDB_Other>();
                foreach (var xref in allOtherCrossRefs)
                {
                    if (xref.CrossRefType == (int)CrossRefType.MovieDB)
                        dictMovieCrossRefs[xref.AnimeID] = xref;
                }
                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:MovieDB:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // MAL
                var repMALCrossRef = new CrossRef_AniDB_MALRepository();
                var allMALCrossRefs = repMALCrossRef.GetAll();
                var dictMALCrossRefs = new Dictionary<int, List<CrossRef_AniDB_MAL>>();
                foreach (var xref in allMALCrossRefs)
                {
                    if (!dictMALCrossRefs.ContainsKey(xref.AnimeID))
                        dictMALCrossRefs[xref.AnimeID] = new List<CrossRef_AniDB_MAL>();
                    dictMALCrossRefs[xref.AnimeID].Add(xref);
                }
                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:MAL:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // user records
                var repSeriesUser = new AnimeSeries_UserRepository();
                var userRecordList = repSeriesUser.GetByUserID(userID);
                var dictUserRecords = new Dictionary<int, AnimeSeries_User>();
                foreach (var serUser in userRecordList)
                    dictUserRecords[serUser.AnimeSeriesID] = serUser;

                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:UserRecs:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // default images
                var repDefImages = new AniDB_Anime_DefaultImageRepository();
                var allDefaultImages = repDefImages.GetAll();

                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:DefaultImages:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                // titles
                var allTitles = repTitles.GetAllForLocalSeries();
                var dictTitles = new Dictionary<int, List<AniDB_Anime_Title>>();
                foreach (var atit in allTitles)
                {
                    if (!dictTitles.ContainsKey(atit.AnimeID))
                        dictTitles[atit.AnimeID] = new List<AniDB_Anime_Title>();

                    dictTitles[atit.AnimeID].Add(atit);
                }

                ts2 = DateTime.Now - start2;
                logger.Info("GetAllSeries:Titles:RawData in {0} ms", ts2.TotalMilliseconds);
                start2 = DateTime.Now;

                var ts = DateTime.Now - start;
                logger.Info("GetAllSeries:RawData in {0} ms", ts.TotalMilliseconds);

                var dictDefaultsPosters = new Dictionary<int, AniDB_Anime_DefaultImage>();
                var dictDefaultsFanart = new Dictionary<int, AniDB_Anime_DefaultImage>();
                var dictDefaultsWideBanner = new Dictionary<int, AniDB_Anime_DefaultImage>();

                start = DateTime.Now;

                foreach (var defaultImage in allDefaultImages)
                {
                    var sizeType = (ImageSizeType)defaultImage.ImageType;

                    if (sizeType == ImageSizeType.Fanart)
                        dictDefaultsFanart[defaultImage.AnimeID] = defaultImage;

                    if (sizeType == ImageSizeType.Poster)
                        dictDefaultsPosters[defaultImage.AnimeID] = defaultImage;

                    if (sizeType == ImageSizeType.WideBanner)
                        dictDefaultsWideBanner[defaultImage.AnimeID] = defaultImage;
                }

                foreach (var aser in series)
                {
                    if (!dictAnimes.ContainsKey(aser.AniDB_ID)) continue;

                    var xrefs = new List<CrossRef_AniDB_TvDBV2>();
                    if (dictCrossRefsV2.ContainsKey(aser.AniDB_ID)) xrefs = dictCrossRefsV2[aser.AniDB_ID];

                    var tvseriesV2 = new List<TvDB_Series>();
                    if (xrefs != null)
                    {
                        foreach (var xref in xrefs)
                        {
                            if (dictTvSeries.ContainsKey(xref.TvDBID))
                                tvseriesV2.Add(dictTvSeries[xref.TvDBID]);
                        }
                    }

                    CrossRef_AniDB_Other xrefMovie = null;
                    if (dictMovieCrossRefs.ContainsKey(aser.AniDB_ID)) xrefMovie = dictMovieCrossRefs[aser.AniDB_ID];

                    List<CrossRef_AniDB_MAL> xrefMAL = null;
                    if (dictMALCrossRefs.ContainsKey(aser.AniDB_ID))
                        xrefMAL = dictMALCrossRefs[aser.AniDB_ID];

                    MovieDB_Movie movie = null;
                    if (xrefMovie != null)
                        movie = xrefMovie.GetMovieDB_Movie();

                    AnimeSeries_User userRec = null;
                    if (dictUserRecords.ContainsKey(aser.AnimeSeriesID))
                        userRec = dictUserRecords[aser.AnimeSeriesID];

                    List<AniDB_Anime_Title> titles = null;
                    if (dictTitles.ContainsKey(aser.AniDB_ID))
                        titles = dictTitles[aser.AniDB_ID];

                    AniDB_Anime_DefaultImage defPoster = null;
                    AniDB_Anime_DefaultImage defFanart = null;
                    AniDB_Anime_DefaultImage defWideBanner = null;

                    if (dictDefaultsPosters.ContainsKey(aser.AniDB_ID)) defPoster = dictDefaultsPosters[aser.AniDB_ID];
                    if (dictDefaultsFanart.ContainsKey(aser.AniDB_ID)) defFanart = dictDefaultsFanart[aser.AniDB_ID];
                    if (dictDefaultsWideBanner.ContainsKey(aser.AniDB_ID))
                        defWideBanner = dictDefaultsWideBanner[aser.AniDB_ID];

                    seriesContractList.Add(aser.ToContract(dictAnimes[aser.AniDB_ID], xrefs, xrefMovie, userRec,
                        tvseriesV2, xrefMAL, true, defPoster, defFanart, defWideBanner, titles, movie));
                }

                ts = DateTime.Now - start;
                logger.Info("GetAllSeries:ProcessedData in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return seriesContractList;
        }

        public Contract_AniDB_AnimeDetailed GetAnimeDetailed(int animeID)
        {
            try
            {
                var start = DateTime.Now;

                Contract_AniDB_AnimeDetailed contract = null;

                var repAnime = new AniDB_AnimeRepository();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    if (StatsCache.Instance.StatAnimeContracts.ContainsKey(animeID))
                        contract = StatsCache.Instance.StatAnimeContracts[animeID];
                    else
                    {
                        var anime = repAnime.GetByAnimeID(session, animeID);
                        if (anime == null) return null;

                        StatsCache.Instance.UpdateAnimeContract(session, animeID);
                        if (StatsCache.Instance.StatAnimeContracts.ContainsKey(animeID))
                            contract = StatsCache.Instance.StatAnimeContracts[animeID];
                    }
                }

                var ts = DateTime.Now - start;
                logger.Trace("GetAnimeDetailed  in {0} ms", ts.TotalMilliseconds);

                return contract;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<string> GetAllTagNames()
        {
            var repTags = new AniDB_TagRepository();
            var allTagNames = new List<string>();

            try
            {
                var start = DateTime.Now;

                foreach (var tag in repTags.GetAll())
                {
                    allTagNames.Add(tag.TagName);
                }
                allTagNames.Sort();


                var ts = DateTime.Now - start;
                logger.Info("GetAllTagNames  in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return allTagNames;
        }

        public List<Contract_AnimeGroup> GetSubGroupsForGroup(int animeGroupID, int userID)
        {
            var retGroups = new List<Contract_AnimeGroup>();
            try
            {
                var repGroups = new AnimeGroupRepository();
                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return retGroups;

                foreach (var grpChild in grp.GetChildGroups())
                    retGroups.Add(grpChild.ToContract(grpChild.GetUserRecord(userID)));

                return retGroups;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return retGroups;
        }

        public List<Contract_AnimeSeries> GetSeriesForGroup(int animeGroupID, int userID)
        {
            var series = new List<Contract_AnimeSeries>();
            try
            {
                var repGroups = new AnimeGroupRepository();
                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (var ser in grp.GetSeries())
                    series.Add(ser.ToContract(ser.GetUserRecord(userID)));

                return series;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return series;
            }
        }

        public List<Contract_AnimeSeries> GetSeriesForGroupRecursive(int animeGroupID, int userID)
        {
            var series = new List<Contract_AnimeSeries>();
            try
            {
                var repGroups = new AnimeGroupRepository();
                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (var ser in grp.GetAllSeries())
                    series.Add(ser.ToContract(ser.GetUserRecord(userID)));

                return series;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return series;
            }
        }

        public List<Contract_AniDB_Episode> GetAniDBEpisodesForAnime(int animeID)
        {
            var eps = new List<Contract_AniDB_Episode>();
            try
            {
                var repAniEps = new AniDB_EpisodeRepository();
                var aniEpList = repAniEps.GetByAnimeID(animeID);

                foreach (var ep in aniEpList)
                    eps.Add(ep.ToContract());

                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                eps = Sorting.MultiSort(eps, sortCriteria);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return eps;
        }

        public List<Contract_AnimeEpisode> GetEpisodesForSeries(int animeSeriesID, int userID)
        {
            var eps = new List<Contract_AnimeEpisode>();
            try
            {
                var start = DateTime.Now;
                var repEps = new AnimeEpisodeRepository();
                var repEpUsers = new AnimeEpisode_UserRepository();
                var repAnimeSer = new AnimeSeriesRepository();
                var repVids = new VideoLocalRepository();
                var repCrossRefs = new CrossRef_File_EpisodeRepository();

                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                var series = repAnimeSer.GetByID(animeSeriesID);
                if (series == null) return eps;

                var epList = repEps.GetBySeriesID(animeSeriesID);
                var userRecordList = repEpUsers.GetByUserIDAndSeriesID(userID, animeSeriesID);
                var dictUserRecords = new Dictionary<int, AnimeEpisode_User>();
                foreach (var epuser in userRecordList)
                    dictUserRecords[epuser.AnimeEpisodeID] = epuser;

                var repAniEps = new AniDB_EpisodeRepository();
                var aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                var dictAniEps = new Dictionary<int, AniDB_Episode>();
                foreach (var aniep in aniEpList)
                    dictAniEps[aniep.EpisodeID] = aniep;

                // get all the video local records and cross refs
                var vids = repVids.GetByAniDBAnimeID(series.AniDB_ID);
                var crossRefs = repCrossRefs.GetByAnimeID(series.AniDB_ID);

                var ts = DateTime.Now - start;
                logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle,
                    ts.TotalMilliseconds);


                start = DateTime.Now;
                foreach (var ep in epList)
                {
                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        var epVids = new List<VideoLocal>();
                        foreach (var xref in crossRefs)
                        {
                            if (xref.EpisodeID == dictAniEps[ep.AniDB_EpisodeID].EpisodeID)
                            {
                                // don't add the same file twice, this will occur when
                                // one file appears over more than one episodes
                                var addedFiles = new Dictionary<string, string>();
                                foreach (var vl in vids)
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
                logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle,
                    ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return eps;
        }

        public List<Contract_AnimeEpisode> GetEpisodesForSeriesOld(int animeSeriesID)
        {
            var eps = new List<Contract_AnimeEpisode>();
            try
            {
                var start = DateTime.Now;
                var repEps = new AnimeEpisodeRepository();
                var repAnimeSer = new AnimeSeriesRepository();
                var repCrossRefs = new CrossRef_File_EpisodeRepository();


                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                var series = repAnimeSer.GetByID(animeSeriesID);
                if (series == null) return eps;

                var epList = repEps.GetBySeriesID(animeSeriesID);

                var repAniEps = new AniDB_EpisodeRepository();
                var aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                var dictAniEps = new Dictionary<int, AniDB_Episode>();
                foreach (var aniep in aniEpList)
                    dictAniEps[aniep.EpisodeID] = aniep;

                var crossRefList = repCrossRefs.GetByAnimeID(series.AniDB_ID);


                var ts = DateTime.Now - start;
                logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle,
                    ts.TotalMilliseconds);


                start = DateTime.Now;
                foreach (var ep in epList)
                {
                    var xrefs = new List<CrossRef_File_Episode>();
                    foreach (var xref in crossRefList)
                    {
                        if (ep.AniDB_EpisodeID == xref.EpisodeID)
                            xrefs.Add(xref);
                    }

                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                        eps.Add(ep.ToContractOld(dictAniEps[ep.AniDB_EpisodeID]));
                }

                ts = DateTime.Now - start;
                logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle,
                    ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return eps;
        }

        public Contract_AnimeSeries GetSeries(int animeSeriesID, int userID)
        {
            var repAnimeSer = new AnimeSeriesRepository();

            try
            {
                var series = repAnimeSer.GetByID(animeSeriesID);
                if (series == null) return null;

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(series.AniDB_ID);
                if (anime == null) return null;

                var xrefs = series.GetCrossRefTvDBV2();
                var xrefMAL = series.CrossRefMAL;

                var sers = new List<TvDB_Series>();
                foreach (var xref in xrefs)
                    sers.Add(xref.GetTvDBSeries());
                var xrefMovie = series.CrossRefMovieDB;
                MovieDB_Movie movie = null;
                if (xrefMovie != null)
                    movie = xrefMovie.GetMovieDB_Movie();
                return series.ToContract(anime, xrefs, xrefMovie, series.GetUserRecord(userID),
                    sers, xrefMAL, false, null, null, null, null, movie);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public Contract_AnimeSeries GetSeriesForAnime(int animeID, int userID)
        {
            var repAnimeSer = new AnimeSeriesRepository();

            try
            {
                var series = repAnimeSer.GetByAnimeID(animeID);
                if (series == null) return null;

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(series.AniDB_ID);
                if (anime == null) return null;

                var xrefs = series.GetCrossRefTvDBV2();
                var xrefMAL = series.CrossRefMAL;

                var sers = new List<TvDB_Series>();
                foreach (var xref in xrefs)
                    sers.Add(xref.GetTvDBSeries());
                var xrefMovie = series.CrossRefMovieDB;
                MovieDB_Movie movie = null;
                if (xrefMovie != null)
                    movie = xrefMovie.GetMovieDB_Movie();
                return series.ToContract(anime, xrefs, xrefMovie, series.GetUserRecord(userID),
                    sers, xrefMAL, false, null, null, null, null, movie);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public bool GetSeriesExistingForAnime(int animeID)
        {
            var repAnimeSer = new AnimeSeriesRepository();

            try
            {
                var series = repAnimeSer.GetByAnimeID(animeID);
                if (series == null)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return true;
        }

        public List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(episodeID);
                if (ep != null)
                    return ep.GetVideoDetailedContracts(userID);
                return new List<Contract_VideoDetailed>();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return new List<Contract_VideoDetailed>();
        }

        public List<Contract_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID)
        {
            var contracts = new List<Contract_VideoLocal>();
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(episodeID);
                if (ep != null)
                {
                    foreach (var vid in ep.GetVideoLocals())
                    {
                        contracts.Add(vid.ToContract(userID));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_VideoLocal> GetIgnoredFiles(int userID)
        {
            var contracts = new List<Contract_VideoLocal>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetIgnoredVideos())
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_VideoLocal> GetManuallyLinkedFiles(int userID)
        {
            var contracts = new List<Contract_VideoLocal>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetManuallyLinkedVideos())
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_VideoLocal> GetUnrecognisedFiles(int userID)
        {
            var contracts = new List<Contract_VideoLocal>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetVideosWithoutEpisode())
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public Contract_ServerStatus GetServerStatus()
        {
            var contract = new Contract_ServerStatus();

            try
            {
                contract.HashQueueCount = JMMService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState = JMMService.CmdProcessorHasher.QueueState;

                contract.GeneralQueueCount = JMMService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState = JMMService.CmdProcessorGeneral.QueueState;

                contract.ImagesQueueCount = JMMService.CmdProcessorImages.QueueCount;
                contract.ImagesQueueState = JMMService.CmdProcessorImages.QueueState;

                contract.IsBanned = JMMService.AnidbProcessor.IsBanned;
                contract.BanReason = JMMService.AnidbProcessor.BanTime.ToString();
                contract.BanOrigin = JMMService.AnidbProcessor.BanOrigin;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contract;
        }

        public Contract_ServerSettings_SaveResponse SaveServerSettings(Contract_ServerSettings contractIn)
        {
            var contract = new Contract_ServerSettings_SaveResponse();
            contract.ErrorMessage = "";

            try
            {
                // validate the settings
                var anidbSettingsChanged = false;
                if (contractIn.AniDB_ClientPort != ServerSettings.AniDB_ClientPort)
                {
                    anidbSettingsChanged = true;
                    var cport = 0;
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
                    var sport = 0;
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
                ServerSettings.AniDB_MyList_StorageState = (AniDBFileStatus)contractIn.AniDB_MyList_StorageState;
                ServerSettings.AniDB_MyList_DeleteType = (AniDBFileDeleteType)contractIn.AniDB_MyList_DeleteType;

                ServerSettings.AniDB_MyList_UpdateFrequency =
                    (ScheduledUpdateFrequency)contractIn.AniDB_MyList_UpdateFrequency;
                ServerSettings.AniDB_Calendar_UpdateFrequency =
                    (ScheduledUpdateFrequency)contractIn.AniDB_Calendar_UpdateFrequency;
                ServerSettings.AniDB_Anime_UpdateFrequency =
                    (ScheduledUpdateFrequency)contractIn.AniDB_Anime_UpdateFrequency;
                ServerSettings.AniDB_MyListStats_UpdateFrequency =
                    (ScheduledUpdateFrequency)contractIn.AniDB_MyListStats_UpdateFrequency;
                ServerSettings.AniDB_File_UpdateFrequency =
                    (ScheduledUpdateFrequency)contractIn.AniDB_File_UpdateFrequency;

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
                ServerSettings.TvDB_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.TvDB_UpdateFrequency;
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
                ServerSettings.RunImportOnStart = contractIn.RunImportOnStart;
                ServerSettings.ScanDropFoldersOnStart = contractIn.ScanDropFoldersOnStart;
                ServerSettings.Hash_CRC32 = contractIn.Hash_CRC32;
                ServerSettings.Hash_MD5 = contractIn.Hash_MD5;
                ServerSettings.Hash_SHA1 = contractIn.Hash_SHA1;

                // Language
                ServerSettings.LanguagePreference = contractIn.LanguagePreference;
                ServerSettings.LanguageUseSynonyms = contractIn.LanguageUseSynonyms;
                ServerSettings.EpisodeTitleSource = (DataSourceType)contractIn.EpisodeTitleSource;
                ServerSettings.SeriesDescriptionSource = (DataSourceType)contractIn.SeriesDescriptionSource;
                ServerSettings.SeriesNameSource = (DataSourceType)contractIn.SeriesNameSource;

                // Trakt
                ServerSettings.Trakt_IsEnabled = contractIn.Trakt_IsEnabled;
                ServerSettings.Trakt_AuthToken = contractIn.Trakt_AuthToken;
                ServerSettings.Trakt_RefreshToken = contractIn.Trakt_RefreshToken;
                ServerSettings.Trakt_TokenExpirationDate = contractIn.Trakt_TokenExpirationDate;
                ServerSettings.Trakt_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.Trakt_UpdateFrequency;
                ServerSettings.Trakt_SyncFrequency = (ScheduledUpdateFrequency)contractIn.Trakt_SyncFrequency;
                ServerSettings.Trakt_DownloadEpisodes = contractIn.Trakt_DownloadEpisodes;
                ServerSettings.Trakt_DownloadFanart = contractIn.Trakt_DownloadFanart;
                ServerSettings.Trakt_DownloadPosters = contractIn.Trakt_DownloadPosters;

                // MAL
                ServerSettings.MAL_Username = contractIn.MAL_Username;
                ServerSettings.MAL_Password = contractIn.MAL_Password;
                ServerSettings.MAL_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.MAL_UpdateFrequency;
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return contract;
        }

        public Contract_ServerSettings GetServerSettings()
        {
            var contract = new Contract_ServerSettings();

            try
            {
                return ServerSettings.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contract;
        }

        public string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video local record";

                vid.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, true, true, userID, true, true);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            var response = new Contract_ToggleWatchedStatusOnEpisode_Response();
            response.ErrorMessage = "";
            response.AnimeEpisode = null;

            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(animeEpisodeID);
                if (ep == null)
                {
                    response.ErrorMessage = "Could not find anime episode record";
                    return response;
                }

                ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, false, userID, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

                // refresh from db
                ep = repEps.GetByID(animeEpisodeID);

                response.AnimeEpisode = ep.ToContract(userID);

                return response;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        /// <summary>
        ///     Set watched status on all normal episodes
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="watchedStatus"></param>
        /// <param name="maxEpisodeNumber">Use this to specify a max episode number to apply to</param>
        /// <returns></returns>
        public string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber,
            int episodeType, int userID)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var eps = repEps.GetBySeriesID(animeSeriesID);

                AnimeSeries ser = null;
                foreach (var ep in eps)
                {
                    if (ep.EpisodeTypeEnum == (enEpisodeType)episodeType &&
                        ep.AniDB_Episode.EpisodeNumber <= maxEpisodeNumber)
                    {
                        // check if this episode is already watched
                        var currentStatus = false;
                        var epUser = ep.GetUserRecord(userID);
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
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags)
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return;

                anime.DisableExternalLinksFlag = flags;
                repAnime.Save(anime);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public Contract_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return null;

                return vid.ToContractDetailed(userID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_AnimeEpisode> GetEpisodesForFile(int videoLocalID, int userID)
        {
            var contracts = new List<Contract_AnimeEpisode>();
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                    return contracts;

                foreach (var ep in vid.GetAnimeEpisodes())
                {
                    contracts.Add(ep.ToContract(userID));
                }

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return contracts;
            }
        }

        /// <summary>
        ///     Get all the release groups for an episode for which the user is collecting
        /// </summary>
        /// <param name="aniDBEpisodeID"></param>
        /// <returns></returns>
        public List<Contract_AniDBReleaseGroup> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID)
        {
            var start = DateTime.Now;

            var relGroups = new List<Contract_AniDBReleaseGroup>();

            try
            {
                var repAniEps = new AniDB_EpisodeRepository();
                var aniEp = repAniEps.GetByEpisodeID(aniDBEpisodeID);
                if (aniEp == null) return relGroups;
                if (aniEp.EpisodeTypeEnum != enEpisodeType.Episode) return relGroups;

                var repSeries = new AnimeSeriesRepository();
                var series = repSeries.GetByAnimeID(aniEp.AnimeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                var userReleaseGroups = new Dictionary<int, int>();
                foreach (var ep in series.GetAnimeEpisodes())
                {
                    var vids = ep.GetVideoLocals();
                    foreach (var vid in vids)
                    {
                        var anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                                userReleaseGroups[anifile.GroupID] = 0;

                            userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                        }
                    }
                }

                // get all the release groups for this series
                var repGrpStatus = new AniDB_GroupStatusRepository();
                var grpStatuses = repGrpStatus.GetByAnimeID(aniEp.AnimeID);
                foreach (var gs in grpStatuses)
                {
                    if (userReleaseGroups.ContainsKey(gs.GroupID))
                    {
                        if (gs.HasGroupReleasedEpisode(aniEp.EpisodeNumber))
                        {
                            var contract = new Contract_AniDBReleaseGroup();
                            contract.GroupID = gs.GroupID;
                            contract.GroupName = gs.GroupName;
                            contract.UserCollecting = true;
                            contract.EpisodeRange = gs.EpisodeRange;
                            contract.FileCount = userReleaseGroups[gs.GroupID];
                            relGroups.Add(contract);
                        }
                    }
                }
                var ts = DateTime.Now - start;
                logger.Info("GetMyReleaseGroupsForAniDBEpisode  in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return relGroups;
        }

        public List<Contract_ImportFolder> GetImportFolders()
        {
            var ifolders = new List<Contract_ImportFolder>();
            try
            {
                var repNS = new ImportFolderRepository();
                foreach (var ns in repNS.GetAll())
                {
                    ifolders.Add(ns.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return ifolders;
        }

        public Contract_ImportFolder_SaveResponse SaveImportFolder(Contract_ImportFolder contract)
        {
            var response = new Contract_ImportFolder_SaveResponse();
            response.ErrorMessage = "";
            response.ImportFolder = null;

            try
            {
                var repNS = new ImportFolderRepository();
                ImportFolder ns = null;
                if (contract.ImportFolderID.HasValue)
                {
                    // update
                    ns = repNS.GetByID(contract.ImportFolderID.Value);
                    if (ns == null)
                    {
                        response.ErrorMessage = "Could not find Import Folder ID: " + contract.ImportFolderID.Value;
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

                if (!Directory.Exists(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Cannot find Import Folder location";
                    return response;
                }

                if (!contract.ImportFolderID.HasValue)
                {
                    var nsTemp = repNS.GetByImportLocation(contract.ImportFolderLocation);
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
                var allFolders = repNS.GetAll();

                if (contract.IsDropDestination == 1)
                {
                    foreach (var imf in allFolders)
                    {
                        if (imf.IsDropDestination == 1 &&
                            (!contract.ImportFolderID.HasValue || (contract.ImportFolderID.Value != imf.ImportFolderID)))
                        {
                            imf.IsDropDestination = 0;
                            repNS.Save(imf);
                        }
                    }
                }

                ns.ImportFolderName = contract.ImportFolderName;
                ns.ImportFolderLocation = contract.ImportFolderLocation;
                ns.IsDropDestination = contract.IsDropDestination;
                ns.IsDropSource = contract.IsDropSource;
                ns.IsWatched = contract.IsWatched;
                repNS.Save(ns);

                response.ImportFolder = ns.ToContract();

                MainWindow.StopWatchingFiles();
                MainWindow.StartWatchingFiles();

                return response;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
            var cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
        }

        public void SyncMALUpload()
        {
            var cmd = new CommandRequest_MALUploadStatusToMAL();
            cmd.Save();
        }

        public void SyncMALDownload()
        {
            var cmd = new CommandRequest_MALDownloadStatusFromMAL();
            cmd.Save();
        }

        public void RescanUnlinkedFiles()
        {
            try
            {
                // files which have been hashed, but don't have an associated episode
                var repVidLocals = new VideoLocalRepository();
                var filesWithoutEpisode = repVidLocals.GetVideosWithoutEpisode();

                foreach (var vl in filesWithoutEpisode)
                {
                    var cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        public void RescanManuallyLinkedFiles()
        {
            try
            {
                // files which have been hashed, but don't have an associated episode
                var repVidLocals = new VideoLocalRepository();
                var files = repVidLocals.GetManuallyLinkedVideos();

                foreach (var vl in files)
                {
                    var cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
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

                var repCR = new CommandRequestRepository();
                foreach (var cr in repCR.GetAllCommandRequestHasher())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorHasher.Init();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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

                var repCR = new CommandRequestRepository();
                foreach (var cr in repCR.GetAllCommandRequestImages())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorImages.Init();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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

                var repCR = new CommandRequestRepository();
                foreach (var cr in repCR.GetAllCommandRequestGeneral())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorGeneral.Init();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void RehashFile(int videoLocalID)
        {
            var repVidLocals = new VideoLocalRepository();
            var vl = repVidLocals.GetByID(videoLocalID);

            if (vl != null)
            {
                var cr_hashfile = new CommandRequest_HashFile(vl.FullServerPath, true);
                cr_hashfile.Save();
            }
        }

        public string TestAniDBConnection()
        {
            var log = "";
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
                logger.ErrorException("Error in EnterTraktPIN: " + ex, ex);
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
                logger.ErrorException("Error in TestMALLogin: " + ex, ex);
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
				logger.ErrorException("Error in TraktFriendRequestDeny: " + ex.ToString(), ex);
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
				logger.ErrorException("Error in TraktFriendRequestDeny: " + ex.ToString(), ex);
				returnMessage = ex.Message;
				return false;
			}*/
        }

        /// <summary>
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="voteValue">Must be 1 or 2 (Anime or Anime Temp(</param>
        /// <param name="voteType"></param>
        public void VoteAnime(int animeID, decimal voteValue, int voteType)
        {
            var msg = string.Format("Voting for anime: {0} - Value: {1}", animeID, voteValue);
            logger.Info(msg);

            // lets save to the database and assume it will work
            var repVotes = new AniDB_VoteRepository();
            var dbVotes = repVotes.GetByEntity(animeID);
            AniDB_Vote thisVote = null;
            foreach (var dbVote in dbVotes)
            {
                // we can only have anime permanent or anime temp but not both
                if (voteType == (int)enAniDBVoteType.Anime || voteType == (int)enAniDBVoteType.AnimeTemp)
                {
                    if (dbVote.VoteType == (int)enAniDBVoteType.Anime ||
                        dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
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

            var iVoteValue = 0;
            if (voteValue > 0)
                iVoteValue = (int)(voteValue * 100);
            else
                iVoteValue = (int)voteValue;

            msg = string.Format("Voting for anime Formatted: {0} - Value: {1}", animeID, iVoteValue);
            logger.Info(msg);

            thisVote.VoteValue = iVoteValue;
            repVotes.Save(thisVote);

            var cmdVote = new CommandRequest_VoteAnime(animeID, voteType, voteValue);
            cmdVote.Save();
        }

        public void VoteAnimeRevoke(int animeID)
        {
            // lets save to the database and assume it will work
            var repVotes = new AniDB_VoteRepository();
            var dbVotes = repVotes.GetByEntity(animeID);
            AniDB_Vote thisVote = null;
            foreach (var dbVote in dbVotes)
            {
                // we can only have anime permanent or anime temp but not both
                if (dbVote.VoteType == (int)enAniDBVoteType.Anime || dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
                {
                    thisVote = dbVote;
                }
            }

            if (thisVote == null) return;

            var cmdVote = new CommandRequest_VoteAnime(animeID, thisVote.VoteType, -1);
            cmdVote.Save();

            repVotes.Delete(thisVote.AniDB_VoteID);
        }

        public string RenameAllGroups()
        {
            try
            {
                AnimeGroup.RenameAllGroups();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }

            return string.Empty;
        }

        public List<string> GetAllUniqueVideoQuality()
        {
            try
            {
                var rep = new AdhocRepository();
                return rep.GetAllVideoQuality();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new List<string>();
            }
        }

        public List<string> GetAllUniqueAudioLanguages()
        {
            try
            {
                var rep = new AdhocRepository();
                return rep.GetAllUniqueAudioLanguages();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new List<string>();
            }
        }

        public List<string> GetAllUniqueSubtitleLanguages()
        {
            try
            {
                var rep = new AdhocRepository();
                return rep.GetAllUniqueSubtitleLanguages();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new List<string>();
            }
        }

        public List<Contract_DuplicateFile> GetAllDuplicateFiles()
        {
            var dupFiles = new List<Contract_DuplicateFile>();
            try
            {
                var repDupFiles = new DuplicateFileRepository();
                foreach (var df in repDupFiles.GetAll())
                {
                    dupFiles.Add(df.ToContract());
                }

                return dupFiles;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return dupFiles;
            }
        }

        /// <summary>
        ///     Delete a duplicate file entry, and also one of the physical files
        /// </summary>
        /// <param name="duplicateFileID"></param>
        /// <param name="fileNumber">0 = Don't delete any physical files, 1 = Delete file 1, 2 = Deleet file 2</param>
        /// <returns></returns>
        public string DeleteDuplicateFile(int duplicateFileID, int fileNumber)
        {
            try
            {
                var repDupFiles = new DuplicateFileRepository();
                var df = repDupFiles.GetByID(duplicateFileID);
                if (df == null) return "Database entry does not exist";

                if (fileNumber == 1 || fileNumber == 2)
                {
                    var fileName = "";
                    if (fileNumber == 1) fileName = df.FullServerPath1;
                    if (fileNumber == 2) fileName = df.FullServerPath2;

                    if (!File.Exists(fileName)) return "File could not be found";

                    File.Delete(fileName);
                }

                repDupFiles.Delete(duplicateFileID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        /// <summary>
        ///     Delets the VideoLocal record and the associated physical file
        /// </summary>
        /// <param name="videoLocalID"></param>
        /// <returns></returns>
        public string DeleteVideoLocalAndFile(int videoLocalID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null) return "Database entry does not exist";

                logger.Info("Deleting video local record and file: {0}", vid.FullServerPath);
                if (File.Exists(vid.FullServerPath))
                {
                    try
                    {
                        File.Delete(vid.FullServerPath);
                    }
                    catch
                    {
                    }
                }

                AnimeSeries ser = null;
                var animeEpisodes = vid.GetAnimeEpisodes();
                if (animeEpisodes.Count > 0)
                    ser = animeEpisodes[0].GetAnimeSeries();


                var cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
                cmdDel.Save();

                repVids.Delete(videoLocalID);

                if (ser != null)
                {
                    ser.QueueUpdateStats();
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                }

                // For deletion of files from Trakt, we will rely on the Daily sync


                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public List<Contract_VideoLocal> GetAllManuallyLinkedFiles(int userID)
        {
            var manualFiles = new List<Contract_VideoLocal>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetManuallyLinkedVideos())
                {
                    manualFiles.Add(vid.ToContract(userID));
                }

                return manualFiles;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return manualFiles;
            }
        }

        public List<Contract_AnimeEpisode> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries,
            bool ignoreVariations)
        {
            var eps = new List<Contract_AnimeEpisode>();
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var repSeries = new AnimeSeriesRepository();
                var repAnime = new AniDB_AnimeRepository();

                var dictSeriesAnime = new Dictionary<int, int>();
                var dictAnimeFinishedAiring = new Dictionary<int, bool>();
                var dictSeriesFinishedAiring = new Dictionary<int, bool>();

                if (onlyFinishedSeries)
                {
                    var allSeries = repSeries.GetAll();
                    foreach (var ser in allSeries)
                        dictSeriesAnime[ser.AnimeSeriesID] = ser.AniDB_ID;

                    var allAnime = repAnime.GetAll();
                    foreach (var anime in allAnime)
                        dictAnimeFinishedAiring[anime.AnimeID] = anime.FinishedAiring;

                    foreach (var kvp in dictSeriesAnime)
                    {
                        if (dictAnimeFinishedAiring.ContainsKey(kvp.Value))
                            dictSeriesFinishedAiring[kvp.Key] = dictAnimeFinishedAiring[kvp.Value];
                    }
                }

                foreach (var ep in repEps.GetEpisodesWithMultipleFiles(ignoreVariations))
                {
                    if (onlyFinishedSeries)
                    {
                        var finishedAiring = false;
                        if (dictSeriesFinishedAiring.ContainsKey(ep.AnimeSeriesID))
                            finishedAiring = dictSeriesFinishedAiring[ep.AnimeSeriesID];

                        if (!finishedAiring) continue;
                    }

                    eps.Add(ep.ToContract(true, userID, null));
                }

                return eps;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return eps;
            }
        }

        public void ReevaluateDuplicateFiles()
        {
            try
            {
                var repDupFiles = new DuplicateFileRepository();
                foreach (var df in repDupFiles.GetAll())
                {
                    if (df.ImportFolder1 == null || df.ImportFolder2 == null)
                    {
                        var msg =
                            string.Format(
                                "Deleting duplicate file record as one of the import folders can't be found: {0} --- {1}",
                                df.FilePathFile1, df.FilePathFile2);
                        logger.Info(msg);
                        repDupFiles.Delete(df.DuplicateFileID);
                        continue;
                    }

                    // make sure that they are not actually the same file
                    if (df.FullServerPath1.Equals(df.FullServerPath2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var msg =
                            string.Format(
                                "Deleting duplicate file record as they are actually point to the same file: {0}",
                                df.FullServerPath1);
                        logger.Info(msg);
                        repDupFiles.Delete(df.DuplicateFileID);
                    }

                    // check if both files still exist
                    if (!File.Exists(df.FullServerPath1) || !File.Exists(df.FullServerPath2))
                    {
                        var msg =
                            string.Format(
                                "Deleting duplicate file record as one of the files can't be found: {0} --- {1}",
                                df.FullServerPath1, df.FullServerPath2);
                        logger.Info(msg);
                        repDupFiles.Delete(df.DuplicateFileID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public List<Contract_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
            string resolution, string videoSource, int videoBitDepth, int userID)
        {
            var vids = new List<Contract_VideoDetailed>();

            var vidQuals = new List<Contract_GroupVideoQuality>();
            var repAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();
            var repVids = new VideoLocalRepository();
            var repAniFile = new AniDB_FileRepository();


            try
            {
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (var vid in repVids.GetByAniDBAnimeID(animeID))
                {
                    var thisBitDepth = 8;

                    var vidInfo = vid.VideoInfo;
                    if (vidInfo != null)
                    {
                        var bitDepth = 0;
                        if (int.TryParse(vidInfo.VideoBitDepth, out bitDepth))
                            thisBitDepth = bitDepth;
                    }

                    var eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    var animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                    {
                        // get the anibd file info
                        var aniFile = vid.GetAniDBFile();
                        if (aniFile != null)
                        {
                            videoSource = SimplifyVideoSource(videoSource);
                            var fileSource = SimplifyVideoSource(aniFile.File_Source);
                            var vidResAniFile = Utils.GetStandardisedVideoResolution(aniFile.File_VideoResolution);

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
                            var vidResInfo = Utils.GetStandardisedVideoResolution(vidInfo.VideoResolution);

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
                logger.ErrorException(ex.ToString(), ex);
                return vids;
            }
        }

        public List<Contract_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID)
        {
            var vids = new List<Contract_VideoDetailed>();

            var vidQuals = new List<Contract_GroupVideoQuality>();
            var repAnime = new AniDB_AnimeRepository();
            var repVids = new VideoLocalRepository();


            try
            {
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (var vid in repVids.GetByAniDBAnimeID(animeID))
                {
                    var eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    var animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                    {
                        // get the anibd file info
                        var aniFile = vid.GetAniDBFile();
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
                logger.ErrorException(ex.ToString(), ex);
                return vids;
            }
        }

        public List<Contract_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID)
        {
            var vidQuals = new List<Contract_GroupVideoQuality>();
            var repAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();
            var repVids = new VideoLocalRepository();
            var repAniFile = new AniDB_FileRepository();

            try
            {
                var start = DateTime.Now;
                var ts = DateTime.Now - start;

                double totalTiming = 0;
                double timingAnime = 0;
                double timingVids = 0;
                double timingEps = 0;
                double timingAniEps = 0;
                double timingAniFile = 0;
                double timingVidInfo = 0;
                double timingContracts = 0;

                var oStart = DateTime.Now;

                start = DateTime.Now;
                var anime = repAnime.GetByAnimeID(animeID);
                ts = DateTime.Now - start;
                timingAnime += ts.TotalMilliseconds;

                if (anime == null) return vidQuals;

                start = DateTime.Now;
                var vids = repVids.GetByAniDBAnimeID(animeID);
                ts = DateTime.Now - start;
                timingVids += ts.TotalMilliseconds;

                foreach (var vid in vids)
                {
                    start = DateTime.Now;
                    var eps = vid.GetAnimeEpisodes();
                    ts = DateTime.Now - start;
                    timingEps += ts.TotalMilliseconds;

                    if (eps.Count == 0) continue;
                    foreach (var animeEp in eps)
                    {
                        //AnimeEpisode animeEp = eps[0];
                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                            animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                        {
                            start = DateTime.Now;
                            var anidbEp = animeEp.AniDB_Episode;
                            ts = DateTime.Now - start;
                            timingAniEps += ts.TotalMilliseconds;

                            // get the anibd file info
                            start = DateTime.Now;
                            var aniFile = vid.GetAniDBFile();
                            ts = DateTime.Now - start;
                            timingAniFile += ts.TotalMilliseconds;
                            if (aniFile != null)
                            {
                                start = DateTime.Now;
                                var vinfo = vid.VideoInfo;
                                ts = DateTime.Now - start;
                                timingVidInfo += ts.TotalMilliseconds;
                                var bitDepth = 8;
                                if (vinfo != null)
                                {
                                    if (!int.TryParse(vinfo.VideoBitDepth, out bitDepth))
                                        bitDepth = 8;
                                }

                                var vidResAniFile = Utils.GetStandardisedVideoResolution(aniFile.File_VideoResolution);

                                // match based on group / video sorce / video res
                                var foundSummaryRecord = false;
                                foreach (var contract in vidQuals)
                                {
                                    var contractSource = SimplifyVideoSource(contract.VideoSource);
                                    var fileSource = SimplifyVideoSource(aniFile.File_Source);

                                    var vidResContract = Utils.GetStandardisedVideoResolution(contract.Resolution);


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
                                    var contract = new Contract_GroupVideoQuality();
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
                                var vinfo = vid.VideoInfo;
                                if (vinfo != null)
                                {
                                    var bitDepth = 8;
                                    if (vinfo != null)
                                    {
                                        if (!int.TryParse(vinfo.VideoBitDepth, out bitDepth))
                                            bitDepth = 8;
                                    }

                                    var vidResInfo = Utils.GetStandardisedVideoResolution(vinfo.VideoResolution);

                                    var foundSummaryRecord = false;
                                    foreach (var contract in vidQuals)
                                    {
                                        var vidResContract = Utils.GetStandardisedVideoResolution(contract.Resolution);


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
                                            contract.TotalFileSize += vinfo.FileSize;
                                            contract.TotalRunningTime += vinfo.Duration;

                                            if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            {
                                                if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                    contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                            }
                                        }
                                    }
                                    if (!foundSummaryRecord)
                                    {
                                        var contract = new Contract_GroupVideoQuality();
                                        contract.FileCountNormal = 0;
                                        contract.FileCountSpecials = 0;
                                        contract.TotalFileSize = 0;
                                        contract.TotalRunningTime = 0;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += vinfo.FileSize;
                                        contract.TotalRunningTime += vinfo.Duration;

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
                }

                start = DateTime.Now;
                foreach (var contract in vidQuals)
                {
                    contract.NormalComplete = contract.FileCountNormal >= anime.EpisodeCountNormal;
                    contract.SpecialsComplete = (contract.FileCountSpecials >= anime.EpisodeCountSpecial) &&
                                                (anime.EpisodeCountSpecial > 0);

                    contract.NormalEpisodeNumberSummary = "";
                    contract.NormalEpisodeNumbers.Sort();
                    var lastEpNum = 0;
                    var baseEpNum = 0;
                    foreach (var epNum in contract.NormalEpisodeNumbers)
                    {
                        if (baseEpNum == 0)
                        {
                            baseEpNum = epNum;
                            lastEpNum = epNum;
                        }

                        if (epNum == lastEpNum) continue;

                        var epNumDiff = epNum - lastEpNum;
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

                var msg2 = string.Format(
                    "Timing for video quality {0} ({1}) : {2}/{3}/{4}/{5}/{6}/{7}/{8}  (AID: {9})", anime.MainTitle,
                    totalTiming, timingAnime, timingVids,
                    timingEps, timingAniEps, timingAniFile, timingVidInfo, timingContracts, anime.AnimeID);
                logger.Debug(msg2);

                vidQuals.Sort();
                return vidQuals;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return vidQuals;
            }
        }


        public List<Contract_GroupFileSummary> GetGroupFileSummary(int animeID)
        {
            var vidQuals = new List<Contract_GroupFileSummary>();
            var repAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();
            var repVids = new VideoLocalRepository();
            var repAniFile = new AniDB_FileRepository();

            try
            {
                var anime = repAnime.GetByAnimeID(animeID);

                if (anime == null) return vidQuals;

                var vids = repVids.GetByAniDBAnimeID(animeID);

                foreach (var vid in vids)
                {
                    if (vid.FilePath.Contains(@"[DB]_Naruto_Shippuuden_078-079_[0DFB6FE0]"))
                        Debug.Write("Test");

                    var eps = vid.GetAnimeEpisodes();

                    if (eps.Count == 0) continue;

                    foreach (var animeEp in eps)
                    {
                        //AnimeEpisode animeEp = eps[0];
                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                            animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                        {
                            var anidbEp = animeEp.AniDB_Episode;

                            // get the anibd file info
                            var aniFile = vid.GetAniDBFile();
                            if (aniFile != null)
                            {
                                // match based on group / video sorce / video res
                                var foundSummaryRecord = false;
                                foreach (var contract in vidQuals)
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
                                    var contract = new Contract_GroupFileSummary();
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
                                var vinfo = vid.VideoInfo;
                                if (vinfo != null)
                                {
                                    var foundSummaryRecord = false;
                                    foreach (var contract in vidQuals)
                                    {
                                        if (contract.GroupName.Equals("NO GROUP INFO",
                                            StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            foundSummaryRecord = true;
                                            if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                                contract.FileCountNormal++;
                                            if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                                contract.FileCountSpecials++;
                                            contract.TotalFileSize += vinfo.FileSize;
                                            contract.TotalRunningTime += vinfo.Duration;

                                            if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            {
                                                if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                    contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                            }
                                        }
                                    }
                                    if (!foundSummaryRecord)
                                    {
                                        var contract = new Contract_GroupFileSummary();
                                        contract.FileCountNormal = 0;
                                        contract.FileCountSpecials = 0;
                                        contract.TotalFileSize = 0;
                                        contract.TotalRunningTime = 0;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += vinfo.FileSize;
                                        contract.TotalRunningTime += vinfo.Duration;

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
                }

                foreach (var contract in vidQuals)
                {
                    contract.NormalComplete = contract.FileCountNormal >= anime.EpisodeCountNormal;
                    contract.SpecialsComplete = (contract.FileCountSpecials >= anime.EpisodeCountSpecial) &&
                                                (anime.EpisodeCountSpecial > 0);

                    contract.NormalEpisodeNumberSummary = "";
                    contract.NormalEpisodeNumbers.Sort();
                    var lastEpNum = 0;
                    var baseEpNum = 0;
                    foreach (var epNum in contract.NormalEpisodeNumbers)
                    {
                        if (baseEpNum == 0)
                        {
                            baseEpNum = epNum;
                            lastEpNum = epNum;
                        }

                        if (epNum == lastEpNum) continue;

                        var epNumDiff = epNum - lastEpNum;
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
                logger.ErrorException(ex.ToString(), ex);
                return vidQuals;
            }
        }

        public Contract_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
        {
            var result = new Contract_AniDB_AnimeCrossRefs();
            result.AnimeID = animeID;

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repSeries = new TvDB_SeriesRepository();
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(animeID);
                    if (anime == null) return result;


                    var repFanart = new TvDB_ImageFanartRepository();
                    var repPosters = new TvDB_ImagePosterRepository();
                    var repBanners = new TvDB_ImageWideBannerRepository();

                    // TvDB
                    foreach (var xref in anime.GetCrossRefTvDBV2())
                    {
                        result.CrossRef_AniDB_TvDB.Add(xref.ToContract());

                        var ser = repSeries.GetByTvDBID(session, xref.TvDBID);
                        if (ser != null)
                            result.TvDBSeries.Add(ser.ToContract());

                        foreach (var ep in anime.GetTvDBEpisodes())
                            result.TvDBEpisodes.Add(ep.ToContract());

                        foreach (var fanart in repFanart.GetBySeriesID(session, xref.TvDBID))
                            result.TvDBImageFanarts.Add(fanart.ToContract());

                        foreach (var poster in repPosters.GetBySeriesID(session, xref.TvDBID))
                            result.TvDBImagePosters.Add(poster.ToContract());

                        foreach (var banner in repBanners.GetBySeriesID(xref.TvDBID))
                            result.TvDBImageWideBanners.Add(banner.ToContract());
                    }

                    // Trakt

                    var repTraktFanart = new Trakt_ImageFanartRepository();
                    var repTraktPosters = new Trakt_ImagePosterRepository();
                    var repTrakt = new Trakt_ShowRepository();

                    foreach (var xref in anime.GetCrossRefTraktV2())
                    {
                        result.CrossRef_AniDB_Trakt.Add(xref.ToContract());

                        var show = repTrakt.GetByTraktSlug(session, xref.TraktID);
                        if (show != null)
                        {
                            result.TraktShows.Add(show.ToContract());

                            foreach (var fanart in repTraktFanart.GetByShowID(session, show.Trakt_ShowID))
                                result.TraktImageFanarts.Add(fanart.ToContract());

                            foreach (var poster in repTraktPosters.GetByShowID(session, show.Trakt_ShowID))
                                result.TraktImagePosters.Add(poster.ToContract());
                        }
                    }


                    // MovieDB
                    var xrefMovie = anime.GetCrossRefMovieDB();
                    if (xrefMovie == null)
                        result.CrossRef_AniDB_MovieDB = null;
                    else
                        result.CrossRef_AniDB_MovieDB = xrefMovie.ToContract();


                    var movie = anime.GetMovieDBMovie();
                    if (movie == null)
                        result.MovieDBMovie = null;
                    else
                        result.MovieDBMovie = movie.ToContract();

                    foreach (var fanart in anime.GetMovieDBFanarts())
                    {
                        if (fanart.ImageSize.Equals(Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBFanarts.Add(fanart.ToContract());
                    }

                    foreach (var poster in anime.GetMovieDBPosters())
                    {
                        if (poster.ImageSize.Equals(Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBPosters.Add(poster.ToContract());
                    }

                    // MAL
                    var xrefMAL = anime.GetCrossRefMAL();
                    if (xrefMAL == null)
                        result.CrossRef_AniDB_MAL = null;
                    else
                    {
                        result.CrossRef_AniDB_MAL = new List<Contract_CrossRef_AniDB_MAL>();
                        foreach (var xrefTemp in xrefMAL)
                            result.CrossRef_AniDB_MAL.Add(xrefTemp.ToContract());
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return result;
            }
        }

        public string EnableDisableImage(bool enabled, int imageID, int imageType)
        {
            try
            {
                var imgType = (JMMImageType)imageType;

                switch (imgType)
                {
                    case JMMImageType.AniDB_Cover:

                        var repAnime = new AniDB_AnimeRepository();
                        var anime = repAnime.GetByAnimeID(imageID);
                        if (anime == null) return "Could not find anime";

                        anime.ImageEnabled = enabled ? 1 : 0;
                        repAnime.Save(anime);

                        break;

                    case JMMImageType.TvDB_Banner:

                        var repBanners = new TvDB_ImageWideBannerRepository();
                        var banner = repBanners.GetByID(imageID);

                        if (banner == null) return "Could not find image";

                        banner.Enabled = enabled ? 1 : 0;
                        repBanners.Save(banner);

                        break;

                    case JMMImageType.TvDB_Cover:

                        var repPosters = new TvDB_ImagePosterRepository();
                        var poster = repPosters.GetByID(imageID);

                        if (poster == null) return "Could not find image";

                        poster.Enabled = enabled ? 1 : 0;
                        repPosters.Save(poster);

                        break;

                    case JMMImageType.TvDB_FanArt:

                        var repFanart = new TvDB_ImageFanartRepository();
                        var fanart = repFanart.GetByID(imageID);

                        if (fanart == null) return "Could not find image";

                        fanart.Enabled = enabled ? 1 : 0;
                        repFanart.Save(fanart);

                        break;

                    case JMMImageType.MovieDB_Poster:

                        var repMoviePosters = new MovieDB_PosterRepository();
                        var moviePoster = repMoviePosters.GetByID(imageID);

                        if (moviePoster == null) return "Could not find image";

                        moviePoster.Enabled = enabled ? 1 : 0;
                        repMoviePosters.Save(moviePoster);

                        break;

                    case JMMImageType.MovieDB_FanArt:

                        var repMovieFanart = new MovieDB_FanartRepository();
                        var movieFanart = repMovieFanart.GetByID(imageID);

                        if (movieFanart == null) return "Could not find image";

                        movieFanart.Enabled = enabled ? 1 : 0;
                        repMovieFanart.Save(movieFanart);

                        break;

                    case JMMImageType.Trakt_Poster:

                        var repTraktPosters = new Trakt_ImagePosterRepository();
                        var traktPoster = repTraktPosters.GetByID(imageID);

                        if (traktPoster == null) return "Could not find image";

                        traktPoster.Enabled = enabled ? 1 : 0;
                        repTraktPosters.Save(traktPoster);

                        break;

                    case JMMImageType.Trakt_Fanart:

                        var repTraktFanart = new Trakt_ImageFanartRepository();
                        var traktFanart = repTraktFanart.GetByID(imageID);

                        if (traktFanart == null) return "Could not find image";

                        traktFanart.Enabled = enabled ? 1 : 0;
                        repTraktFanart.Save(traktFanart);

                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType)
        {
            try
            {
                var repDefaults = new AniDB_Anime_DefaultImageRepository();

                var imgType = (JMMImageType)imageType;
                var sizeType = ImageSizeType.Poster;

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

                    var img = repDefaults.GetByAnimeIDAndImagezSizeType(animeID, (int)sizeType);
                    if (img != null)
                        repDefaults.Delete(img.AniDB_Anime_DefaultImageID);
                }
                else
                {
                    // making the image the default for it's type (poster, fanart etc)
                    var img = repDefaults.GetByAnimeIDAndImagezSizeType(animeID, (int)sizeType);
                    if (img == null)
                        img = new AniDB_Anime_DefaultImage();

                    img.AnimeID = animeID;
                    img.ImageParentID = imageID;
                    img.ImageParentType = (int)imgType;
                    img.ImageType = (int)sizeType;
                    repDefaults.Save(img);
                }


                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        /// <summary>
        ///     Finds the previous episode for use int the next unwatched episode
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public Contract_AnimeEpisode GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID)
        {
            try
            {
                var nextEp = GetNextUnwatchedEpisode(animeSeriesID, userID);
                if (nextEp == null) return null;

                var epType = nextEp.EpisodeType;
                var epNum = nextEp.EpisodeNumber - 1;

                if (epNum <= 0) return null;

                var repAniEps = new AniDB_EpisodeRepository();
                var repAnimeSer = new AnimeSeriesRepository();
                var series = repAnimeSer.GetByID(animeSeriesID);
                if (series == null) return null;

                var anieps = repAniEps.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, (enEpisodeType)epType, epNum);
                if (anieps.Count == 0) return null;

                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByAniDBEpisodeID(anieps[0].EpisodeID);
                if (ep == null) return null;

                return ep.ToContract(true, userID, null);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public Contract_AnimeEpisode GetNextUnwatchedEpisode(int animeSeriesID, int userID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetNextUnwatchedEpisode(session, animeSeriesID, userID);
            }
        }

        public List<Contract_AnimeEpisode> GetAllUnwatchedEpisodes(int animeSeriesID, int userID)
        {
            var ret = new List<Contract_AnimeEpisode>();

            try
            {
                var repEps = new AnimeEpisodeRepository();
                var repAnimeSer = new AnimeSeriesRepository();
                var repEpUser = new AnimeEpisode_UserRepository();

                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                var series = repAnimeSer.GetByID(animeSeriesID);
                if (series == null) return null;

                //List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);
                var epList = new List<AnimeEpisode>();
                var dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
                foreach (var userRecord in repEpUser.GetByUserIDAndSeriesID(userID, animeSeriesID))
                    dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                foreach (var animeep in repEps.GetBySeriesID(animeSeriesID))
                {
                    if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                    {
                        epList.Add(animeep);
                        continue;
                    }

                    var usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                    if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                        epList.Add(animeep);
                }

                var repAniEps = new AniDB_EpisodeRepository();
                var aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                var dictAniEps = new Dictionary<int, AniDB_Episode>();
                foreach (var aniep in aniEpList)
                    dictAniEps[aniep.EpisodeID] = aniep;

                var candidateEps = new List<Contract_AnimeEpisode>();
                foreach (var ep in epList)
                {
                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        var anidbep = dictAniEps[ep.AniDB_EpisodeID];
                        if (anidbep.EpisodeType == (int)enEpisodeType.Episode ||
                            anidbep.EpisodeType == (int)enEpisodeType.Special)
                        {
                            AnimeEpisode_User userRecord = null;
                            if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                userRecord = dictEpUsers[ep.AnimeEpisodeID];

                            var epContract = ep.ToContract(anidbep, new List<VideoLocal>(), userRecord,
                                series.GetUserRecord(userID));
                            candidateEps.Add(epContract);
                        }
                    }
                }

                if (candidateEps.Count == 0) return null;

                // sort by episode type and number to find the next episode
                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                candidateEps = Sorting.MultiSort(candidateEps, sortCriteria);

                // this will generate a lot of queries when the user doesn have files
                // for these episodes
                foreach (var canEp in candidateEps)
                {
                    // now refresh from the database to get file count
                    var epFresh = repEps.GetByID(canEp.AnimeEpisodeID);
                    if (epFresh.GetVideoLocals().Count > 0)
                        ret.Add(epFresh.ToContract(true, userID, null));
                }

                return ret;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ret;
            }
        }

        public Contract_AnimeEpisode GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
        {
            try
            {
                var repGroups = new AnimeGroupRepository();
                var repEps = new AnimeEpisodeRepository();
                var repAnimeSer = new AnimeSeriesRepository();

                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return null;

                var allSeries = grp.GetAllSeries();

                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("AirDate", false, SortType.eDateTime));
                allSeries = Sorting.MultiSort(allSeries, sortCriteria);

                foreach (var ser in allSeries)
                {
                    var contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                    if (contract != null) return contract;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_AnimeEpisode> GetContinueWatchingFilter(int userID, int maxRecords)
        {
            var retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGF = new GroupFilterRepository();

                    var repUsers = new JMMUserRepository();
                    var user = repUsers.GetByID(session, userID);
                    if (user == null) return retEps;

                    // find the locked Continue Watching Filter
                    GroupFilter gf = null;
                    var lockedGFs = repGF.GetLockedGroupFilters(session);
                    if (lockedGFs != null)
                    {
                        // if it already exists we can leave
                        foreach (var gfTemp in lockedGFs)
                        {
                            if (gfTemp.FilterType == (int)GroupFilterType.ContinueWatching)
                            {
                                gf = gfTemp;
                                break;
                            }
                        }
                    }

                    if (gf == null) return retEps;

                    // Get all the groups 
                    // it is more efficient to just get the full list of groups and then filter them later
                    var repGroups = new AnimeGroupRepository();
                    var allGrps = repGroups.GetAll(session);

                    // get all the user records
                    var repUserRecords = new AnimeGroup_UserRepository();
                    var userRecords = repUserRecords.GetByUserID(session, userID);
                    var dictUserRecords = new Dictionary<int, AnimeGroup_User>();
                    foreach (var userRec in userRecords)
                        dictUserRecords[userRec.AnimeGroupID] = userRec;

                    // get all the groups in this filter for this user
                    var groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][gf.GroupFilterID];

                    var comboGroups = new List<Contract_AnimeGroup>();
                    foreach (var grp in allGrps)
                    {
                        if (groups.Contains(grp.AnimeGroupID))
                        {
                            AnimeGroup_User userRec = null;
                            if (dictUserRecords.ContainsKey(grp.AnimeGroupID))
                                userRec = dictUserRecords[grp.AnimeGroupID];

                            var rec = grp.ToContract(userRec);
                            comboGroups.Add(rec);
                        }
                    }

                    // apply sorting
                    var sortCriteria = GroupFilterHelper.GetSortDescriptions(gf);
                    comboGroups = Sorting.MultiSort(comboGroups, sortCriteria);

                    if (StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID) &&
                        StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(gf.GroupFilterID))
                    {
                        var repSeries = new AnimeSeriesRepository();
                        foreach (var grp in comboGroups)
                        {
                            var sers = repSeries.GetByGroupID(session, grp.AnimeGroupID);

                            // sort the series by air date
                            var sortCriteria2 = new List<SortPropOrFieldAndDirection>();
                            sortCriteria2.Add(new SortPropOrFieldAndDirection("AirDate", false, SortType.eDateTime));
                            sers = Sorting.MultiSort(sers, sortCriteria2);

                            var seriesWatching = new List<int>();

                            foreach (var ser in sers)
                            {
                                if (!user.AllowedSeries(ser)) continue;
                                var useSeries = true;

                                if (seriesWatching.Count > 0)
                                {
                                    if (ser.GetAnime().AnimeType == (int)enAnimeType.TVSeries)
                                    {
                                        // make sure this series is not a sequel to an existing series we have already added
                                        foreach (var rel in ser.GetAnime().GetRelatedAnime())
                                        {
                                            if (rel.RelationType.ToLower().Trim().Equals("sequel") ||
                                                rel.RelationType.ToLower().Trim().Equals("prequel"))
                                                useSeries = false;
                                        }
                                    }
                                }

                                if (!useSeries) continue;


                                var ep = GetNextUnwatchedEpisode(session, ser.AnimeSeriesID, userID);
                                if (ep != null)
                                {
                                    retEps.Add(ep);

                                    // Lets only return the specified amount
                                    if (retEps.Count == maxRecords)
                                        return retEps;

                                    if (ser.GetAnime().AnimeType == (int)enAnimeType.TVSeries)
                                        seriesWatching.Add(ser.AniDB_ID);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return retEps;
        }

        /// <summary>
        ///     Gets a list of episodes watched based on the most recently watched series
        ///     It will return the next episode to watch in the most recent 10 series
        /// </summary>
        /// <returns></returns>
        public List<Contract_AnimeEpisode> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int jmmuserID)
        {
            var retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repAnimeSer = new AnimeSeriesRepository();
                    var repSeriesUser = new AnimeSeries_UserRepository();
                    var repUsers = new JMMUserRepository();

                    var start = DateTime.Now;

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retEps;

                    // get a list of series that is applicable
                    var allSeriesUser = repSeriesUser.GetMostRecentlyWatched(session, jmmuserID);

                    var ts = DateTime.Now - start;
                    logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Series: {0}", ts.TotalMilliseconds));
                    start = DateTime.Now;

                    foreach (var userRecord in allSeriesUser)
                    {
                        var series = repAnimeSer.GetByID(session, userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(series)) continue;

                        var ep = GetNextUnwatchedEpisode(session, userRecord.AnimeSeriesID, jmmuserID);
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retEps;
        }

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyWatched(int maxRecords, int jmmuserID)
        {
            var retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repEpUser = new AnimeEpisode_UserRepository();
                    var repUsers = new JMMUserRepository();

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retEps;

                    // get a list of series that is applicable
                    var allEpUserRecs = repEpUser.GetMostRecentlyWatched(session, jmmuserID);
                    foreach (var userRecord in allEpUserRecs)
                    {
                        var ep = repEps.GetByID(session, userRecord.AnimeEpisodeID);
                        if (ep == null) continue;

                        var epContract = ep.ToContract(session, jmmuserID);
                        if (epContract != null)
                        {
                            retEps.Add(epContract);

                            // Lets only return the specified amount
                            if (retEps.Count == maxRecords) return retEps;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return retEps;
        }

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            var retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repEpUser = new AnimeEpisode_UserRepository();
                    var repUsers = new JMMUserRepository();
                    var repVids = new VideoLocalRepository();

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retEps;

                    var vids = repVids.GetMostRecentlyAdded(session, maxRecords);
                    var numEps = 0;
                    foreach (var vid in vids)
                    {
                        foreach (var ep in vid.GetAnimeEpisodes(session))
                        {
                            if (user.AllowedSeries(ep.GetAnimeSeries(session)))
                            {
                                var epContract = ep.ToContract(session, jmmuserID);
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retEps;
        }

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            var retEps = new List<Contract_AnimeEpisode>();
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var repEpUser = new AnimeEpisode_UserRepository();
                var repSeries = new AnimeSeriesRepository();
                var repUsers = new JMMUserRepository();
                var repVids = new VideoLocalRepository();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retEps;

                    var start = DateTime.Now;

                    var sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                              "From VideoLocal vl " +
                              "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                              "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                              "GROUP BY ae.AnimeSeriesID " +
                              "ORDER BY MaxDate desc ";
                    var results = DatabaseHelper.GetData(sql);

                    var ts2 = DateTime.Now - start;
                    logger.Info("GetEpisodesRecentlyAddedSummary:RawData in {0} ms", ts2.TotalMilliseconds);
                    start = DateTime.Now;

                    var numEps = 0;
                    foreach (object[] res in results)
                    {
                        var animeSeriesID = int.Parse(res[0].ToString());

                        var ser = repSeries.GetByID(session, animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(ser)) continue;

                        var vids = repVids.GetMostRecentlyAddedForAnime(session, 1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        var eps = vids[0].GetAnimeEpisodes(session);
                        if (eps.Count == 0) continue;

                        var epContract = eps[0].ToContract(session, jmmuserID);
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retEps;
        }

        public List<Contract_AnimeSeries> GetSeriesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            var retSeries = new List<Contract_AnimeSeries>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repUsers = new JMMUserRepository();
                    var repSeries = new AnimeSeriesRepository();

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retSeries;

                    var series = repSeries.GetMostRecentlyAdded(session, maxRecords);
                    var numSeries = 0;
                    foreach (var ser in series)
                    {
                        if (user.AllowedSeries(ser))
                        {
                            var serContract = ser.ToContract(ser.GetUserRecord(session, jmmuserID));
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retSeries;
        }

        public Contract_AnimeEpisode GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repEpUser = new AnimeEpisode_UserRepository();
                    var repUsers = new JMMUserRepository();

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return null;

                    var userRecords = repEpUser.GetLastWatchedEpisodeForSeries(session, animeSeriesID, jmmuserID);
                    if (userRecords == null || userRecords.Count == 0) return null;

                    var ep = repEps.GetByID(session, userRecords[0].AnimeEpisodeID);
                    if (ep == null) return null;

                    return ep.ToContract(session, jmmuserID);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return null;
        }


        /// <summary>
        ///     Delete a series, and everything underneath it (episodes, files)
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="deleteFiles">also delete the physical files</param>
        /// <returns></returns>
        public string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var repAnimeSer = new AnimeSeriesRepository();
                var repVids = new VideoLocalRepository();
                var repGroups = new AnimeGroupRepository();

                var ser = repAnimeSer.GetByID(animeSeriesID);
                if (ser == null) return "Series does not exist";

                var animeGroupID = ser.AnimeGroupID;

                foreach (var ep in ser.GetAnimeEpisodes())
                {
                    foreach (var vid in ep.GetVideoLocals())
                    {
                        if (deleteFiles)
                        {
                            logger.Info("Deleting video local record and file: {0}", vid.FullServerPath);
                            if (!File.Exists(vid.FullServerPath)) return "File could not be found";
                            File.Delete(vid.FullServerPath);
                        }
                        var cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
                        cmdDel.Save();

                        repVids.Delete(vid.VideoLocalID);
                    }

                    repEps.Delete(ep.AnimeEpisodeID);
                }
                repAnimeSer.Delete(ser.AnimeSeriesID);

                // finally update stats
                var grp = repGroups.GetByID(animeGroupID);
                if (grp != null)
                {
                    if (grp.GetAllSeries().Count == 0)
                    {
                        DeleteAnimeGroup(grp.AnimeGroupID, false);
                    }
                    else
                    {
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                        StatsCache.Instance.UpdateUsingGroup(grp.TopLevelAnimeGroup.AnimeGroupID);
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }


        public List<Contract_AnimeSeries> GetSeriesWithMissingEpisodes(int maxRecords, int jmmuserID)
        {
            var start = DateTime.Now;
            var repSeries = new AnimeSeriesRepository();
            var repAnime = new AniDB_AnimeRepository();
            var repUsers = new JMMUserRepository();


            // get all the series
            var seriesContractList = new List<Contract_AnimeSeries>();

            try
            {
                var user = repUsers.GetByID(jmmuserID);
                if (user == null) return seriesContractList;

                var series = repSeries.GetWithMissingEpisodes();

                var animes = repAnime.GetAll();
                var dictAnimes = new Dictionary<int, AniDB_Anime>();
                foreach (var anime in animes)
                    dictAnimes[anime.AnimeID] = anime;

                // tvdb
                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var allCrossRefs = repCrossRef.GetAll();
                var dictCrossRefsV2 = new Dictionary<int, List<CrossRef_AniDB_TvDBV2>>();
                foreach (var xref in allCrossRefs)
                {
                    if (!dictCrossRefsV2.ContainsKey(xref.AnimeID))
                        dictCrossRefsV2[xref.AnimeID] = new List<CrossRef_AniDB_TvDBV2>();
                    dictCrossRefsV2[xref.AnimeID].Add(xref);
                }


                // moviedb
                var repOtherCrossRef = new CrossRef_AniDB_OtherRepository();
                var allOtherCrossRefs = repOtherCrossRef.GetAll();
                var dictMovieCrossRefs = new Dictionary<int, CrossRef_AniDB_Other>();
                foreach (var xref in allOtherCrossRefs)
                {
                    if (xref.CrossRefType == (int)CrossRefType.MovieDB)
                        dictMovieCrossRefs[xref.AnimeID] = xref;
                }

                // MAL
                var repMALCrossRef = new CrossRef_AniDB_MALRepository();
                var allMALCrossRefs = repMALCrossRef.GetAll();
                var dictMALCrossRefs = new Dictionary<int, List<CrossRef_AniDB_MAL>>();
                foreach (var xref in allMALCrossRefs)
                {
                    if (!dictMALCrossRefs.ContainsKey(xref.AnimeID))
                        dictMALCrossRefs[xref.AnimeID] = new List<CrossRef_AniDB_MAL>();
                    dictMALCrossRefs[xref.AnimeID].Add(xref);
                }

                // user records
                var repSeriesUser = new AnimeSeries_UserRepository();
                var userRecordList = repSeriesUser.GetByUserID(jmmuserID);
                var dictUserRecords = new Dictionary<int, AnimeSeries_User>();
                foreach (var serUser in userRecordList)
                    dictUserRecords[serUser.AnimeSeriesID] = serUser;

                var i = 1;
                foreach (var aser in series)
                {
                    if (!dictAnimes.ContainsKey(aser.AniDB_ID)) continue;

                    var anime = dictAnimes[aser.AniDB_ID];
                    if (!user.AllowedAnime(anime)) continue;

                    var xrefs = new List<CrossRef_AniDB_TvDBV2>();
                    if (dictCrossRefsV2.ContainsKey(aser.AniDB_ID)) xrefs = dictCrossRefsV2[aser.AniDB_ID];

                    CrossRef_AniDB_Other xrefMovie = null;
                    if (dictMovieCrossRefs.ContainsKey(aser.AniDB_ID)) xrefMovie = dictMovieCrossRefs[aser.AniDB_ID];

                    AnimeSeries_User userRec = null;
                    if (dictUserRecords.ContainsKey(aser.AnimeSeriesID))
                        userRec = dictUserRecords[aser.AnimeSeriesID];

                    List<CrossRef_AniDB_MAL> xrefMAL = null;
                    if (dictMALCrossRefs.ContainsKey(aser.AniDB_ID))
                        xrefMAL = dictMALCrossRefs[aser.AniDB_ID];

                    var sers = new List<TvDB_Series>();
                    foreach (var xref in xrefs)
                        sers.Add(xref.GetTvDBSeries());
                    MovieDB_Movie movie = null;
                    if (xrefMovie != null)
                        movie = xrefMovie.GetMovieDB_Movie();
                    seriesContractList.Add(aser.ToContract(dictAnimes[aser.AniDB_ID], xrefs, xrefMovie, userRec,
                        sers, xrefMAL, false, null, null, null, null, movie));

                    if (i == maxRecords) break;

                    i++;
                }

                var ts = DateTime.Now - start;
                logger.Info("GetSeriesWithMissingEpisodes in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return seriesContractList;
        }

        public List<Contract_AniDBAnime> GetMiniCalendar(int jmmuserID, int numberOfDays)
        {
            var repAnime = new AniDB_AnimeRepository();
            var repUsers = new JMMUserRepository();

            // get all the series
            var animeList = new List<Contract_AniDBAnime>();

            try
            {
                var user = repUsers.GetByID(jmmuserID);
                if (user == null) return animeList;

                var animes = repAnime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays),
                    DateTime.Today.AddDays(numberOfDays));
                foreach (var anime in animes)
                {
                    var useAnime = true;

                    var cats = user.HideCategories.ToLower().Split(',');
                    var animeCats = anime.AllCategories.ToLower().Split('|');
                    foreach (var cat in cats)
                    {
                        if (!string.IsNullOrEmpty(cat) && animeCats.Contains(cat))
                        {
                            useAnime = false;
                            break;
                        }
                    }

                    if (useAnime)
                        animeList.Add(anime.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return animeList;
        }

        public List<Contract_AniDBAnime> GetAnimeForMonth(int jmmuserID, int month, int year)
        {
            var repAnime = new AniDB_AnimeRepository();
            var repUsers = new JMMUserRepository();

            // get all the series
            var animeList = new List<Contract_AniDBAnime>();

            try
            {
                var user = repUsers.GetByID(jmmuserID);
                if (user == null) return animeList;

                var startDate = new DateTime(year, month, 1, 0, 0, 0);
                var endDate = startDate.AddMonths(1);
                endDate = endDate.AddMinutes(-10);

                var animes = repAnime.GetForDate(startDate, endDate);
                foreach (var anime in animes)
                {
                    var useAnime = true;

                    var cats = user.HideCategories.ToLower().Split(',');
                    var animeCats = anime.AllCategories.ToLower().Split('|');
                    foreach (var cat in cats)
                    {
                        if (!string.IsNullOrEmpty(cat) && animeCats.Contains(cat))
                        {
                            useAnime = false;
                            break;
                        }
                    }

                    if (useAnime)
                        animeList.Add(anime.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
				logger.ErrorException(ex.ToString(), ex);
			}
			return animeList;
		}*/

        public List<Contract_JMMUser> GetAllUsers()
        {
            var repUsers = new JMMUserRepository();

            // get all the users
            var userList = new List<Contract_JMMUser>();

            try
            {
                var users = repUsers.GetAll();
                foreach (var user in users)
                    userList.Add(user.ToContract());
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return userList;
        }

        public Contract_JMMUser AuthenticateUser(string username, string password)
        {
            var repUsers = new JMMUserRepository();

            try
            {
                var user = repUsers.AuthenticateUser(username, password);
                if (user == null) return null;

                return user.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public string ChangePassword(int userID, string newPassword)
        {
            var repUsers = new JMMUserRepository();

            try
            {
                var jmmUser = repUsers.GetByID(userID);
                if (jmmUser == null) return "User not found";

                jmmUser.Password = Digest.Hash(newPassword);
                repUsers.Save(jmmUser);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }

            return "";
        }

        public string SaveUser(Contract_JMMUser user)
        {
            var repUsers = new JMMUserRepository();

            try
            {
                var existingUser = false;
                var updateStats = false;
                JMMUser jmmUser = null;
                if (user.JMMUserID.HasValue)
                {
                    jmmUser = repUsers.GetByID(user.JMMUserID.Value);
                    if (jmmUser == null) return "User not found";
                    existingUser = true;
                }
                else
                {
                    jmmUser = new JMMUser();
                    updateStats = true;
                }

                if (existingUser && jmmUser.IsAniDBUser != user.IsAniDBUser)
                    updateStats = true;

                jmmUser.HideCategories = user.HideCategories;
                jmmUser.IsAniDBUser = user.IsAniDBUser;
                jmmUser.IsTraktUser = user.IsTraktUser;
                jmmUser.IsAdmin = user.IsAdmin;
                jmmUser.Username = user.Username;
                jmmUser.CanEditServerSettings = user.CanEditServerSettings;
                jmmUser.PlexUsers = user.PlexUsers;
                if (string.IsNullOrEmpty(user.Password))
                    jmmUser.Password = "";

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 0)
                {
                    var adminExists = false;
                    var users = repUsers.GetAll();
                    foreach (var userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (existingUser)
                            {
                                if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                            }
                            else
                                adminExists = true;
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                repUsers.Save(jmmUser);

                // update stats
                if (updateStats)
                {
                    var repSeries = new AnimeSeriesRepository();
                    foreach (var ser in repSeries.GetAll())
                        ser.QueueUpdateStats();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }

            return "";
        }

        public string DeleteUser(int userID)
        {
            var repUsers = new JMMUserRepository();

            try
            {
                var jmmUser = repUsers.GetByID(userID);
                if (jmmUser == null) return "User not found";

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 1)
                {
                    var adminExists = false;
                    var users = repUsers.GetAll();
                    foreach (var userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                repUsers.Delete(userID);

                // delete all user records
                var repSeries = new AnimeSeries_UserRepository();
                foreach (var ser in repSeries.GetByUserID(userID))
                    repSeries.Delete(ser.AnimeSeries_UserID);

                var repGroup = new AnimeGroup_UserRepository();
                foreach (var grp in repGroup.GetByUserID(userID))
                    repGroup.Delete(grp.AnimeGroup_UserID);

                var repEpisode = new AnimeEpisode_UserRepository();
                foreach (var ep in repEpisode.GetByUserID(userID))
                    repEpisode.Delete(ep.AnimeEpisode_UserID);

                var repVids = new VideoLocal_UserRepository();
                foreach (var vid in repVids.GetByUserID(userID))
                    repVids.Delete(vid.VideoLocal_UserID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }

            return "";
        }

        public List<Contract_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID)
        {
            var links = new List<Contract_AniDB_Anime_Similar>();
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return links;

                var repUsers = new JMMUserRepository();
                var juser = repUsers.GetByID(userID);
                if (juser == null) return links;

                var repSeries = new AnimeSeriesRepository();

                foreach (var link in anime.GetSimilarAnime())
                {
                    var animeLink = repAnime.GetByAnimeID(link.SimilarAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    var ser = repSeries.GetByAnimeID(link.SimilarAnimeID);

                    links.Add(link.ToContract(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return links;
            }
        }

        public List<Contract_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID)
        {
            var links = new List<Contract_AniDB_Anime_Relation>();
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return links;

                var repUsers = new JMMUserRepository();
                var juser = repUsers.GetByID(userID);
                if (juser == null) return links;

                var repSeries = new AnimeSeriesRepository();

                foreach (var link in anime.GetRelatedAnime())
                {
                    var animeLink = repAnime.GetByAnimeID(link.RelatedAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    var ser = repSeries.GetByAnimeID(link.RelatedAnimeID);

                    links.Add(link.ToContract(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return links;
            }
        }

        /// <summary>
        ///     Returns a list of recommendations based on the users votes
        /// </summary>
        /// <param name="maxResults"></param>
        /// <param name="userID"></param>
        /// <param name="recommendationType">1 = to watch, 2 = to download</param>
        public List<Contract_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType)
        {
            var recs = new List<Contract_Recommendation>();

            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var repVotes = new AniDB_VoteRepository();
                var repSeries = new AnimeSeriesRepository();

                var repUsers = new JMMUserRepository();
                var juser = repUsers.GetByID(userID);
                if (juser == null) return recs;

                // get all the anime the user has chosen to ignore
                var ignoreType = 1;
                switch (recommendationType)
                {
                    case 1:
                        ignoreType = 1;
                        break;
                    case 2:
                        ignoreType = 2;
                        break;
                }
                var repIgnore = new IgnoreAnimeRepository();
                var ignored = repIgnore.GetByUserAndType(userID, ignoreType);
                var dictIgnored = new Dictionary<int, IgnoreAnime>();
                foreach (var ign in ignored)
                    dictIgnored[ign.AnimeID] = ign;


                // find all the series which the user has rated
                var allVotes = repVotes.GetAll();
                if (allVotes.Count == 0) return recs;

                // sort by the highest rated
                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("VoteValue", true, SortType.eInteger));
                allVotes = Sorting.MultiSort(allVotes, sortCriteria);

                var dictRecs = new Dictionary<int, Contract_Recommendation>();

                var animeVotes = new List<AniDB_Vote>();
                foreach (var vote in allVotes)
                {
                    if (vote.VoteType != (int)enAniDBVoteType.Anime && vote.VoteType != (int)enAniDBVoteType.AnimeTemp)
                        continue;

                    if (dictIgnored.ContainsKey(vote.EntityID)) continue;

                    // check if the user has this anime
                    var anime = repAnime.GetByAnimeID(vote.EntityID);
                    if (anime == null) continue;

                    // get similar anime
                    var simAnime = anime.GetSimilarAnime();
                    // sort by the highest approval
                    sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("ApprovalPercentage", true, SortType.eDoubleOrFloat));
                    simAnime = Sorting.MultiSort(simAnime, sortCriteria);

                    foreach (var link in simAnime)
                    {
                        if (dictIgnored.ContainsKey(link.SimilarAnimeID)) continue;

                        var animeLink = repAnime.GetByAnimeID(link.SimilarAnimeID);
                        if (animeLink != null)
                            if (!juser.AllowedAnime(animeLink)) continue;

                        // don't recommend to watch anime that the user doesn't have
                        if (animeLink == null && recommendationType == 1) continue;

                        // don't recommend to watch series that the user doesn't have
                        var ser = repSeries.GetByAnimeID(link.SimilarAnimeID);
                        if (ser == null && recommendationType == 1) continue;


                        if (ser != null)
                        {
                            // don't recommend to watch series that the user has already started watching
                            var userRecord = ser.GetUserRecord(userID);
                            if (userRecord != null)
                            {
                                if (userRecord.WatchedEpisodeCount > 0 && recommendationType == 1) continue;
                            }

                            // don't recommend to download anime that the user has files for
                            if (ser.LatestLocalEpisodeNumber > 0 && recommendationType == 2) continue;
                        }

                        var rec = new Contract_Recommendation();
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
                            rec.Recommended_AniDB_Anime = animeLink.ToContract();

                        rec.BasedOn_AniDB_Anime = anime.ToContract();

                        rec.Recommended_AnimeSeries = null;
                        if (ser != null)
                            rec.Recommended_AnimeSeries = ser.ToContract(ser.GetUserRecord(userID));

                        var serBasedOn = repSeries.GetByAnimeID(anime.AnimeID);
                        if (serBasedOn == null) continue;

                        rec.BasedOn_AnimeSeries = serBasedOn.ToContract(serBasedOn.GetUserRecord(userID));

                        dictRecs[rec.RecommendedAnimeID] = rec;
                    }
                }

                var tempRecs = new List<Contract_Recommendation>();
                foreach (var rec in dictRecs.Values)
                    tempRecs.Add(rec);

                // sort by the highest score
                sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("Score", true, SortType.eDoubleOrFloat));
                tempRecs = Sorting.MultiSort(tempRecs, sortCriteria);

                var numRecs = 0;
                foreach (var rec in tempRecs)
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
                logger.ErrorException(ex.ToString(), ex);
                return recs;
            }
        }

        public List<Contract_AniDBReleaseGroup> GetReleaseGroupsForAnime(int animeID)
        {
            var relGroups = new List<Contract_AniDBReleaseGroup>();

            try
            {
                var repSeries = new AnimeSeriesRepository();
                var series = repSeries.GetByAnimeID(animeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                //List<int> userReleaseGroups = new List<int>();
                var userReleaseGroups = new Dictionary<int, int>();
                foreach (var ep in series.GetAnimeEpisodes())
                {
                    var vids = ep.GetVideoLocals();
                    foreach (var vid in vids)
                    {
                        var anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                                userReleaseGroups[anifile.GroupID] = 0;

                            userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                        }
                    }
                }

                // get all the release groups for this series
                var repGrpStatus = new AniDB_GroupStatusRepository();
                var grpStatuses = repGrpStatus.GetByAnimeID(animeID);
                foreach (var gs in grpStatuses)
                {
                    var contract = new Contract_AniDBReleaseGroup();
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return relGroups;
        }

        public List<Contract_AniDB_Character> GetCharactersForAnime(int animeID)
        {
            var chars = new List<Contract_AniDB_Character>();

            try
            {
                var repAnimeChar = new AniDB_Anime_CharacterRepository();
                var repChar = new AniDB_CharacterRepository();

                var animeChars = repAnimeChar.GetByAnimeID(animeID);
                if (animeChars == null || animeChars.Count == 0) return chars;

                foreach (var animeChar in animeChars)
                {
                    var chr = repChar.GetByCharID(animeChar.CharID);
                    if (chr != null)
                        chars.Add(chr.ToContract(animeChar));
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return chars;
        }

        public List<Contract_AniDB_Character> GetCharactersForSeiyuu(int aniDB_SeiyuuID)
        {
            var chars = new List<Contract_AniDB_Character>();

            try
            {
                var repSeiyuu = new AniDB_SeiyuuRepository();
                var seiyuu = repSeiyuu.GetByID(aniDB_SeiyuuID);
                if (seiyuu == null) return chars;

                var repCharSei = new AniDB_Character_SeiyuuRepository();
                var links = repCharSei.GetBySeiyuuID(seiyuu.SeiyuuID);

                var repAnimeChar = new AniDB_Anime_CharacterRepository();
                var repChar = new AniDB_CharacterRepository();
                var repAnime = new AniDB_AnimeRepository();

                foreach (var chrSei in links)
                {
                    var chr = repChar.GetByCharID(chrSei.CharID);
                    if (chr != null)
                    {
                        var aniChars = repAnimeChar.GetByCharID(chr.CharID);
                        if (aniChars.Count > 0)
                        {
                            var anime = repAnime.GetByAnimeID(aniChars[0].AnimeID);
                            if (anime != null)
                            {
                                var contract = chr.ToContract(aniChars[0]);
                                contract.Anime = anime.ToContract(true, null);
                                chars.Add(contract);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return chars;
        }

        public void ForceAddFileToMyList(string hash)
        {
            try
            {
                var cmdAddFile = new CommandRequest_AddFileToMyList(hash);
                cmdAddFile.Save();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public List<Contract_MissingFile> GetMyListFilesForRemoval(int userID)
        {
            var contracts = new List<Contract_MissingFile>();

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

            var repAniFile = new AniDB_FileRepository();
            var repFileEp = new CrossRef_File_EpisodeRepository();
            var repAnime = new AniDB_AnimeRepository();
            var repEpisodes = new AniDB_EpisodeRepository();
            var repVids = new VideoLocalRepository();
            var repSeries = new AnimeSeriesRepository();

            var animeCache = new Dictionary<int, AniDB_Anime>();
            var animeSeriesCache = new Dictionary<int, AnimeSeries>();

            try
            {
                var cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                var ev = cmd.Process();
                if (ev == enHelperActivityType.GotMyListHTTP)
                {
                    foreach (var myitem in cmd.MyListItems)
                    {
                        // let's check if the file on AniDB actually exists in the user's local collection
                        var hash = string.Empty;

                        var anifile = repAniFile.GetByFileID(myitem.FileID);
                        if (anifile != null)
                            hash = anifile.Hash;
                        else
                        {
                            // look for manually linked files
                            var xrefs = repFileEp.GetByEpisodeID(myitem.EpisodeID);
                            foreach (var xref in xrefs)
                            {
                                if (xref.CrossRefSource != (int)CrossRefSource.AniDB)
                                {
                                    hash = xref.Hash;
                                    break;
                                }
                            }
                        }

                        var fileMissing = false;
                        if (string.IsNullOrEmpty(hash))
                            fileMissing = true;
                        else
                        {
                            // now check if the file actually exists on disk
                            var vid = repVids.GetByHash(hash);

                            if (vid != null && !File.Exists(vid.FullServerPath))
                                fileMissing = true;
                        }

                        if (fileMissing)
                        {
                            // this means we can't find the file
                            AniDB_Anime anime = null;
                            if (animeCache.ContainsKey(myitem.AnimeID))
                                anime = animeCache[myitem.AnimeID];
                            else
                            {
                                anime = repAnime.GetByAnimeID(myitem.AnimeID);
                                animeCache[myitem.AnimeID] = anime;
                            }

                            AnimeSeries ser = null;
                            if (animeSeriesCache.ContainsKey(myitem.AnimeID))
                                ser = animeSeriesCache[myitem.AnimeID];
                            else
                            {
                                ser = repSeries.GetByAnimeID(myitem.AnimeID);
                                animeSeriesCache[myitem.AnimeID] = ser;
                            }


                            var missingFile = new Contract_MissingFile();
                            missingFile.AnimeID = myitem.AnimeID;
                            missingFile.AnimeTitle = "Data Missing";
                            if (anime != null) missingFile.AnimeTitle = anime.MainTitle;
                            missingFile.EpisodeID = myitem.EpisodeID;
                            var ep = repEpisodes.GetByEpisodeID(myitem.EpisodeID);
                            missingFile.EpisodeNumber = -1;
                            missingFile.EpisodeType = 1;
                            if (ep != null)
                            {
                                missingFile.EpisodeNumber = ep.EpisodeNumber;
                                missingFile.EpisodeType = ep.EpisodeType;
                            }
                            missingFile.FileID = myitem.FileID;

                            if (ser == null) missingFile.AnimeSeries = null;
                            else missingFile.AnimeSeries = ser.ToContract(ser.GetUserRecord(userID));

                            contracts.Add(missingFile);
                        }
                    }
                }

                if (contracts.Count > 0)
                {
                    var sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("AnimeTitle", false, SortType.eString));
                    sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeID", false, SortType.eInteger));
                    contracts = Sorting.MultiSort(contracts, sortCriteria);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public void RemoveMissingMyListFiles(List<Contract_MissingFile> myListFiles)
        {
            foreach (var missingFile in myListFiles)
            {
                var cmd = new CommandRequest_DeleteFileFromMyList(missingFile.FileID);
                cmd.Save();

                // For deletion of files from Trakt, we will rely on the Daily sync
                // lets also try removing from the users trakt collecion
            }
        }

        public List<Contract_AnimeSeries> GetSeriesWithoutAnyFiles(int userID)
        {
            var contracts = new List<Contract_AnimeSeries>();

            var repVids = new VideoLocalRepository();
            var repSeries = new AnimeSeriesRepository();

            try
            {
                foreach (var ser in repSeries.GetAll())
                {
                    if (repVids.GetByAniDBAnimeID(ser.AniDB_ID).Count == 0)
                    {
                        contracts.Add(ser.ToContract(ser.GetUserRecord(userID)));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }


        public void DeleteFileFromMyList(int fileID)
        {
            var cmd = new CommandRequest_DeleteFileFromMyList(fileID);
            cmd.Save();
        }

        public List<Contract_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly,
            int airingState)
        {
            var contracts = new List<Contract_MissingEpisode>();
            var repSeries = new AnimeSeriesRepository();

            var airState = (AiringState)airingState;

            var animeCache = new Dictionary<int, AniDB_Anime>();
            var gvqCache = new Dictionary<int, List<Contract_GroupVideoQuality>>();
            var gfqCache = new Dictionary<int, List<Contract_GroupFileSummary>>();

            try
            {
                var i = 0;
                var allSeries = repSeries.GetAll();
                foreach (var ser in allSeries)
                {
                    i++;
                    //string msg = string.Format("Updating series {0} of {1} ({2}) -  {3}", i, allSeries.Count, ser.Anime.MainTitle, DateTime.Now);
                    //logger.Debug(msg);

                    //if (ser.Anime.AnimeID != 69) continue;

                    var missingEps = ser.MissingEpisodeCount;
                    if (onlyMyGroups) missingEps = ser.MissingEpisodeCountGroups;

                    var finishedAiring = ser.GetAnime().FinishedAiring;

                    if (!finishedAiring && airState == AiringState.FinishedAiring) continue;
                    if (finishedAiring && airState == AiringState.StillAiring) continue;

                    var start = DateTime.Now;
                    var ts = DateTime.Now - start;

                    double totalTiming = 0;
                    double timingVids = 0;
                    double timingSeries = 0;
                    double timingAnime = 0;
                    double timingQuality = 0;
                    double timingEps = 0;
                    double timingAniEps = 0;
                    var epCount = 0;

                    var oStart = DateTime.Now;

                    if (missingEps > 0)
                    {
                        // find the missing episodes
                        start = DateTime.Now;
                        var eps = ser.GetAnimeEpisodes();
                        ts = DateTime.Now - start;
                        timingEps += ts.TotalMilliseconds;

                        epCount = eps.Count;
                        foreach (var aep in ser.GetAnimeEpisodes())
                        {
                            if (regularEpisodesOnly && aep.EpisodeTypeEnum != enEpisodeType.Episode) continue;

                            var aniep = aep.AniDB_Episode;
                            if (aniep.FutureDated) continue;

                            start = DateTime.Now;
                            var vids = aep.GetVideoLocals();
                            ts = DateTime.Now - start;
                            timingVids += ts.TotalMilliseconds;

                            if (vids.Count == 0)
                            {
                                var contract = new Contract_MissingEpisode();
                                contract.AnimeID = ser.AniDB_ID;
                                start = DateTime.Now;
                                contract.AnimeSeries = ser.ToContract(ser.GetUserRecord(userID));
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

                                foreach (var gvq in summ)
                                {
                                    if (contract.GroupFileSummary.Length > 0)
                                        contract.GroupFileSummary += " --- ";

                                    contract.GroupFileSummary += string.Format("{0} - {1}/{2}/{3}bit ({4})",
                                        gvq.GroupNameShort, gvq.Resolution, gvq.VideoSource, gvq.VideoBitDepth,
                                        gvq.NormalEpisodeNumberSummary);
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

                                foreach (var gfq in summFiles)
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

                        var msg2 =
                            string.Format("Timing for series {0} ({1}) : {2}/{3}/{4}/{5}/{6}/{7} - {8} eps (AID: {9})",
                                ser.GetAnime().MainTitle, totalTiming, timingVids, timingSeries,
                                timingAnime, timingQuality, timingEps, timingAniEps, epCount, ser.GetAnime().AnimeID);
                        //logger.Debug(msg2);
                    }
                }

                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("AnimeTitle", false, SortType.eString));
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                contracts = Sorting.MultiSort(contracts, sortCriteria);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public void IgnoreAnime(int animeID, int ignoreType, int userID)
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return;

                var repUser = new JMMUserRepository();
                var user = repUser.GetByID(userID);
                if (user == null) return;

                var repIgnore = new IgnoreAnimeRepository();
                var ignore = repIgnore.GetByAnimeUserType(animeID, userID, ignoreType);
                if (ignore != null) return; // record already exists

                ignore = new IgnoreAnime();
                ignore.AnimeID = animeID;
                ignore.IgnoreType = ignoreType;
                ignore.JMMUserID = userID;

                repIgnore.Save(ignore);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return false;
        }

        public List<Contract_CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs()
        {
            var contracts = new List<Contract_CrossRef_AniDB_TraktV2>();
            try
            {
                var repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                var allCrossRefs = repCrossRef.GetAll();

                foreach (var xref in allCrossRefs)
                {
                    contracts.Add(xref.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID)
        {
            var comments = new List<Contract_Trakt_CommentUser>();

            try
            {
                var repFriends = new Trakt_FriendRepository();

                var commentsTemp = TraktTVHelper.GetShowCommentsV2(animeID);
                if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                foreach (var sht in commentsTemp)
                {
                    var comment = new Contract_Trakt_CommentUser();

                    var traktFriend = repFriends.GetByUsername(sht.user.username);

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
                    comment.Comment.CommentType = (int)TraktActivityType.Show; // episode or show
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return comments;
        }

        public Contract_AniDBVote GetUserVote(int animeID)
        {
            try
            {
                var repVotes = new AniDB_VoteRepository();
                var dbVotes = repVotes.GetByEntity(animeID);
                if (dbVotes.Count == 0) return null;
                return dbVotes[0].ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType)
        {
            try
            {
                var repEpisodes = new AnimeEpisodeRepository();
                var ep = repEpisodes.GetByID(animeEpisodeID);
                if (ep == null) return;

                var repEpisodeUsers = new AnimeEpisode_UserRepository();
                var epUserRecord = ep.GetUserRecord(userID);

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

                switch ((StatCountType)statCountType)
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

                repEpisodeUsers.Save(epUserRecord);

                var ser = ep.GetAnimeSeries();
                if (ser == null) return;

                var repSeriesUser = new AnimeSeries_UserRepository();
                var userRecord = ser.GetUserRecord(userID);
                if (userRecord == null)
                    userRecord = new AnimeSeries_User(userID, ser.AnimeSeriesID);

                switch ((StatCountType)statCountType)
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

                repSeriesUser.Save(userRecord);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public List<Contract_IgnoreAnime> GetIgnoredAnime(int userID)
        {
            var retAnime = new List<Contract_IgnoreAnime>();
            try
            {
                var repUser = new JMMUserRepository();
                var user = repUser.GetByID(userID);
                if (user == null) return retAnime;

                var repIgnore = new IgnoreAnimeRepository();
                var ignoredAnime = repIgnore.GetByUser(userID);
                foreach (var ign in ignoredAnime)
                    retAnime.Add(ign.ToContract());
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return retAnime;
        }


        public void RemoveIgnoreAnime(int ignoreAnimeID)
        {
            try
            {
                var repIgnore = new IgnoreAnimeRepository();
                var ignore = repIgnore.GetByID(ignoreAnimeID);
                if (ignore == null) return;

                repIgnore.Delete(ignoreAnimeID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID)
        {
            try
            {
                var repGroups = new AnimeGroupRepository();
                var repSeries = new AnimeSeriesRepository();

                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return;

                var ser = repSeries.GetByID(animeSeriesID);
                if (ser == null) return;

                grp.DefaultAnimeSeriesID = animeSeriesID;
                repGroups.Save(grp);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void RemoveDefaultSeriesForGroup(int animeGroupID)
        {
            try
            {
                var repGroups = new AnimeGroupRepository();

                var grp = repGroups.GetByID(animeGroupID);
                if (grp == null) return;

                grp.DefaultAnimeSeriesID = null;
                repGroups.Save(grp);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public List<Contract_TvDBLanguage> GetTvDBLanguages()
        {
            var retLanguages = new List<Contract_TvDBLanguage>();

            try
            {
                foreach (var lan in JMMService.TvdbHelper.GetLanguages())
                {
                    retLanguages.Add(lan.ToContract());
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                var repGroups = new AnimeGroupRepository();
                var repSeries = new AnimeSeriesRepository();

                var ser = repSeries.GetByID(animeSeriesID);
                if (ser == null) return null;

                var grp = ser.TopLevelAnimeGroup;
                if (grp == null) return null;

                return grp.ToContract(grp.GetUserRecord(userID));
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return null;
        }

        public void RecreateAllGroups()
        {
            try
            {
                // pause queues
                JMMService.CmdProcessorGeneral.Paused = true;
                JMMService.CmdProcessorHasher.Paused = true;
                JMMService.CmdProcessorImages.Paused = true;

                var repGroups = new AnimeGroupRepository();
                var repGroupUser = new AnimeGroup_UserRepository();
                var repSeries = new AnimeSeriesRepository();

                // get all the old groups
                var oldGroups = repGroups.GetAll();
                var oldGroupUsers = repGroupUser.GetAll();

                // create a new group, where we will place all the series temporarily
                var tempGroup = new AnimeGroup();
                tempGroup.GroupName = "AAA Migrating Groups AAA";
                tempGroup.Description = "AAA Migrating Groups AAA";
                tempGroup.SortName = "AAA Migrating Groups AAA";
                tempGroup.DateTimeUpdated = DateTime.Now;
                tempGroup.DateTimeCreated = DateTime.Now;
                repGroups.Save(tempGroup);

                // move all series to the new group
                foreach (var ser in repSeries.GetAll())
                {
                    ser.AnimeGroupID = tempGroup.AnimeGroupID;
                    repSeries.Save(ser, false);
                }

                // delete all the old groups
                foreach (var grp in oldGroups)
                    repGroups.Delete(grp.AnimeGroupID);

                // delete all the old group user records
                foreach (var grpUser in oldGroupUsers)
                    repGroupUser.Delete(grpUser.AnimeGroupID);


                // recreate groups
                foreach (var ser in repSeries.GetAll())
                {
                    var createNewGroup = true;

                    if (ServerSettings.AutoGroupSeries)
                    {
                        var grps = AnimeGroup.GetRelatedGroupsFromAnimeID(ser.AniDB_ID);

                        // only use if there is just one result
                        if (grps != null && grps.Count > 0 && !grps[0].GroupName.Equals("AAA Migrating Groups AAA"))
                        {
                            ser.AnimeGroupID = grps[0].AnimeGroupID;
                            createNewGroup = false;
                        }
                    }

                    if (createNewGroup)
                    {
                        var anGroup = new AnimeGroup();
                        anGroup.Populate(ser);
                        repGroups.Save(anGroup);

                        ser.AnimeGroupID = anGroup.AnimeGroupID;
                    }

                    repSeries.Save(ser, false);
                }

                // delete the temp group
                if (tempGroup.GetAllSeries().Count == 0)
                    repGroups.Delete(tempGroup.AnimeGroupID);

                // create group user records and update group stats
                foreach (var grp in repGroups.GetAll())
                    grp.UpdateStatsFromTopLevel(true, true);


                // un-pause queues
                JMMService.CmdProcessorGeneral.Paused = false;
                JMMService.CmdProcessorHasher.Paused = false;
                JMMService.CmdProcessorImages.Paused = false;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public Contract_AppVersions GetAppVersions()
        {
            try
            {
                var appv = XMLService.GetAppVersions();
                if (appv == null) return null;

                return appv.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return null;
        }

        public Contract_AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID)
        {
            var repSeiyuu = new AniDB_SeiyuuRepository();

            try
            {
                var seiyuu = repSeiyuu.GetByID(seiyuuID);
                if (seiyuu == null) return null;

                return seiyuu.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public Contract_FileFfdshowPreset GetFFDPreset(int videoLocalID)
        {
            var repVids = new VideoLocalRepository();
            var repFFD = new FileFfdshowPresetRepository();

            try
            {
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null) return null;

                var ffd = repFFD.GetByHashAndSize(vid.Hash, vid.FileSize);
                if (ffd == null) return null;

                return ffd.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return null;
        }

        public void DeleteFFDPreset(int videoLocalID)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var repFFD = new FileFfdshowPresetRepository();

                var vid = repVids.GetByID(videoLocalID);
                if (vid == null) return;

                var ffd = repFFD.GetByHashAndSize(vid.Hash, vid.FileSize);
                if (ffd == null) return;

                repFFD.Delete(ffd.FileFfdshowPresetID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void SaveFFDPreset(Contract_FileFfdshowPreset preset)
        {
            try
            {
                var repVids = new VideoLocalRepository();
                var repFFD = new FileFfdshowPresetRepository();

                var vid = repVids.GetByHashAndSize(preset.Hash, preset.FileSize);
                if (vid == null) return;

                var ffd = repFFD.GetByHashAndSize(preset.Hash, preset.FileSize);
                if (ffd == null) ffd = new FileFfdshowPreset();

                ffd.FileSize = preset.FileSize;
                ffd.Hash = preset.Hash;
                ffd.Preset = preset.Preset;

                repFFD.Save(ffd);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public List<Contract_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID)
        {
            try
            {
                var vids = new List<Contract_VideoLocal>();

                var sType = (FileSearchCriteria)searchType;

                var repVids = new VideoLocalRepository();
                switch (sType)
                {
                    case FileSearchCriteria.Name:

                        var results1 = repVids.GetByName(searchCriteria.Trim());
                        foreach (var vid in results1)
                            vids.Add(vid.ToContract(userID));

                        break;

                    case FileSearchCriteria.ED2KHash:

                        var vidByHash = repVids.GetByHash(searchCriteria.Trim());
                        if (vidByHash != null)
                            vids.Add(vidByHash.ToContract(userID));

                        break;

                    case FileSearchCriteria.Size:

                        break;

                    case FileSearchCriteria.LastOneHundred:

                        var results2 = repVids.GetMostRecentlyAdded(100);
                        foreach (var vid in results2)
                            vids.Add(vid.ToContract(userID));

                        break;
                }

                return vids;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
				logger.ErrorException(ex.ToString(), ex);
				
			}
			return ret;
		}*/

        public List<Contract_VideoLocal> RandomFileRenamePreview(int maxResults, int userID)
        {
            var ret = new List<Contract_VideoLocal>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetRandomFiles(maxResults))
                    ret.Add(vid.ToContract(userID));
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return ret;
        }

        public Contract_VideoLocalRenamed RenameFilePreview(int videoLocalID, string renameRules)
        {
            var ret = new Contract_VideoLocalRenamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;

            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = "ERROR: Could not find file record";
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
                logger.ErrorException(ex.ToString(), ex);
                ret.VideoLocal = null;
                ret.NewFileName = string.Format("ERROR: {0}", ex.Message);
                ret.Success = false;
            }
            return ret;
        }

        public Contract_VideoLocalRenamed RenameFile(int videoLocalID, string renameRules)
        {
            var ret = new Contract_VideoLocalRenamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;
            try
            {
                var repVids = new VideoLocalRepository();
                var vid = repVids.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = "ERROR: Could not find file record";
                    ret.Success = false;
                }
                else
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);

                    if (!string.IsNullOrEmpty(ret.NewFileName))
                    {
                        // check if the file exists
                        var fullFileName = vid.FullServerPath;
                        if (!File.Exists(fullFileName))
                        {
                            ret.NewFileName = "Error could not find the original file";
                            ret.Success = false;
                            return ret;
                        }

                        // actually rename the file
                        var path = Path.GetDirectoryName(fullFileName);
                        var newFullName = Path.Combine(path, ret.NewFileName);

                        try
                        {
                            logger.Info(string.Format("Renaming file From ({0}) to ({1})....", fullFileName, newFullName));

                            if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                logger.Info(string.Format("Renaming file SKIPPED, no change From ({0}) to ({1})",
                                    fullFileName, newFullName));
                                ret.NewFileName = newFullName;
                            }
                            else
                            {
                                File.Move(fullFileName, newFullName);
                                logger.Info(string.Format("Renaming file SUCCESS From ({0}) to ({1})", fullFileName,
                                    newFullName));
                                ret.NewFileName = newFullName;

                                var newPartialPath = "";
                                var folderID = vid.ImportFolderID;
                                var repFolders = new ImportFolderRepository();

                                DataAccessHelper.GetShareAndPath(newFullName, repFolders.GetAll(), ref folderID,
                                    ref newPartialPath);

                                vid.FilePath = newPartialPath;
                                repVids.Save(vid);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Info(string.Format("Renaming file FAIL From ({0}) to ({1}) - {2}", fullFileName,
                                newFullName, ex.Message));
                            logger.ErrorException(ex.ToString(), ex);
                            ret.Success = false;
                            ret.NewFileName = ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                ret.VideoLocal = null;
                ret.NewFileName = string.Format("ERROR: {0}", ex.Message);
                ret.Success = false;
            }
            return ret;
        }

        public List<Contract_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID)
        {
            var contracts = new List<Contract_VideoLocal>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetByAniDBAnimeID(animeID))
                {
                    contracts.Add(vid.ToContract(userID));
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contracts;
        }

        public List<Contract_RenameScript> GetAllRenameScripts()
        {
            var ret = new List<Contract_RenameScript>();
            try
            {
                var repScripts = new RenameScriptRepository();

                var allScripts = repScripts.GetAll();
                foreach (var scr in allScripts)
                    ret.Add(scr.ToContract());
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return ret;
        }

        public Contract_RenameScript_SaveResponse SaveRenameScript(Contract_RenameScript contract)
        {
            var response = new Contract_RenameScript_SaveResponse();
            response.ErrorMessage = "";
            response.RenameScript = null;

            try
            {
                var repScripts = new RenameScriptRepository();
                RenameScript script = null;
                if (contract.RenameScriptID.HasValue)
                {
                    // update
                    script = repScripts.GetByID(contract.RenameScriptID.Value);
                    if (script == null)
                    {
                        response.ErrorMessage = "Could not find Rename Script ID: " + contract.RenameScriptID.Value;
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
                var allScripts = repScripts.GetAll();

                if (contract.IsEnabledOnImport == 1)
                {
                    foreach (var rs in allScripts)
                    {
                        if (rs.IsEnabledOnImport == 1 &&
                            (!contract.RenameScriptID.HasValue || (contract.RenameScriptID.Value != rs.RenameScriptID)))
                        {
                            rs.IsEnabledOnImport = 0;
                            repScripts.Save(rs);
                        }
                    }
                }

                script.IsEnabledOnImport = contract.IsEnabledOnImport;
                script.Script = contract.Script;
                script.ScriptName = contract.ScriptName;
                repScripts.Save(script);

                response.RenameScript = script.ToContract();

                return response;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string DeleteRenameScript(int renameScriptID)
        {
            try
            {
                var repScripts = new RenameScriptRepository();
                var df = repScripts.GetByID(renameScriptID);
                if (df == null) return "Database entry does not exist";

                repScripts.Delete(renameScriptID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public List<Contract_AniDB_Recommendation> GetAniDBRecommendations(int animeID)
        {
            var contracts = new List<Contract_AniDB_Recommendation>();
            try
            {
                var repBA = new AniDB_RecommendationRepository();

                foreach (var rec in repBA.GetByAnimeID(animeID))
                    contracts.Add(rec.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return contracts;
            }
        }

        public List<Contract_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery)
        {
            var retTitles = new List<Contract_AnimeSearch>();

            var repSeries = new AnimeSeriesRepository();

            try
            {
                // check if it is a title search or an ID search
                var aid = 0;
                if (int.TryParse(titleQuery, out aid))
                {
                    // user is direct entering the anime id

                    // try the local database first
                    // if not download the data from AniDB now
                    AniDB_Anime anime =
                        anime =
                            JMMService.AnidbProcessor.GetAnimeInfoHTTP(aid, false,
                                ServerSettings.AniDB_DownloadRelatedAnime);
                    if (anime != null)
                    {
                        var res = new Contract_AnimeSearch();
                        res.AnimeID = anime.AnimeID;
                        res.MainTitle = anime.MainTitle;
                        res.Titles = anime.AllTitles;

                        // check for existing series and group details
                        var ser = repSeries.GetByAnimeID(anime.AnimeID);
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
                    var titles = AzureWebAPI.Get_AnimeTitle(titleQuery);

                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        foreach (var tit in titles)
                        {
                            var res = new Contract_AnimeSearch();
                            res.AnimeID = tit.AnimeID;
                            res.MainTitle = tit.MainTitle;
                            res.Titles = tit.Titles;

                            // check for existing series and group details
                            var ser = repSeries.GetByAnimeID(tit.AnimeID);
                            if (ser != null)
                            {
                                res.SeriesExists = true;
                                res.AnimeSeriesID = ser.AnimeSeriesID;
                                res.AnimeSeriesName = ser.GetAnime(session).GetFormattedTitle(session);
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retTitles;
        }


        /// <summary>
        ///     Can only be used when the group only has one series
        /// </summary>
        /// <param name="animeGroupID"></param>
        /// <param name="allSeries"></param>
        /// <returns></returns>
        public static AnimeSeries GetSeriesForGroup(int animeGroupID, List<AnimeSeries> allSeries)
        {
            try
            {
                foreach (var ser in allSeries)
                {
                    if (ser.AnimeGroupID == animeGroupID) return ser;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        private int[] GetEpisodePercentages(int numEpisodes)
        {
            if (numEpisodes == 1) return new[] { 100 };
            if (numEpisodes == 2) return new[] { 50, 100 };
            if (numEpisodes == 3) return new[] { 33, 66, 100 };
            if (numEpisodes == 4) return new[] { 25, 50, 75, 100 };
            if (numEpisodes == 5) return new[] { 20, 40, 60, 80, 100 };

            return new[] { 100 };
        }

        /// <summary>
        ///     www is usually not used correctly
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
            return origSource;
        }

        public Contract_AnimeEpisode GetNextUnwatchedEpisode(ISession session, int animeSeriesID, int userID)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var repAnimeSer = new AnimeSeriesRepository();
                var repEpUser = new AnimeEpisode_UserRepository();

                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                var series = repAnimeSer.GetByID(session, animeSeriesID);
                if (series == null) return null;

                //List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);
                var epList = new List<AnimeEpisode>();
                var dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
                foreach (var userRecord in repEpUser.GetByUserIDAndSeriesID(session, userID, animeSeriesID))
                    dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                foreach (var animeep in repEps.GetBySeriesID(session, animeSeriesID))
                {
                    if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                    {
                        epList.Add(animeep);
                        continue;
                    }

                    var usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                    if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                        epList.Add(animeep);
                }

                var repAniEps = new AniDB_EpisodeRepository();
                var aniEpList = repAniEps.GetByAnimeID(session, series.AniDB_ID);
                var dictAniEps = new Dictionary<int, AniDB_Episode>();
                foreach (var aniep in aniEpList)
                    dictAniEps[aniep.EpisodeID] = aniep;

                var candidateEps = new List<Contract_AnimeEpisode>();
                foreach (var ep in epList)
                {
                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        var anidbep = dictAniEps[ep.AniDB_EpisodeID];
                        if (anidbep.EpisodeType == (int)enEpisodeType.Episode ||
                            anidbep.EpisodeType == (int)enEpisodeType.Special)
                        {
                            AnimeEpisode_User userRecord = null;
                            if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                userRecord = dictEpUsers[ep.AnimeEpisodeID];

                            var epContract = ep.ToContract(anidbep, new List<VideoLocal>(), userRecord,
                                series.GetUserRecord(session, userID));
                            candidateEps.Add(epContract);
                        }
                    }
                }

                if (candidateEps.Count == 0) return null;

                // sort by episode type and number to find the next episode
                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                candidateEps = Sorting.MultiSort(candidateEps, sortCriteria);

                // this will generate a lot of queries when the user doesn have files
                // for these episodes
                foreach (var canEp in candidateEps)
                {
                    // now refresh from the database to get file count
                    var epFresh = repEps.GetByID(canEp.AnimeEpisodeID);
                    if (epFresh.GetVideoLocals().Count > 0)
                        return epFresh.ToContract(true, userID, series.GetUserRecord(session, userID));
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        private double CalculateRecommendationScore(int userVoteValue, double approvalPercentage, decimal animeRating)
        {
            double score = userVoteValue;

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

        public List<Contract_VideoLocalRenamed> RenameFiles(List<int> videoLocalIDs, string renameRules)
        {
            var ret = new List<Contract_VideoLocalRenamed>();
            try
            {
                var repVids = new VideoLocalRepository();
                foreach (var vid in videoLocalIDs)
                {
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return ret;
        }

        #region Custom Tags

        public List<Contract_CustomTag> GetAllCustomTags()
        {
            try
            {
                var repCustomTags = new CustomTagRepository();

                var ret = new List<Contract_CustomTag>();
                foreach (var ctag in repCustomTags.GetAll())
                    ret.Add(ctag.ToContract());

                return ret;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public Contract_CrossRef_CustomTag_SaveResponse SaveCustomTagCrossRef(Contract_CrossRef_CustomTag contract)
        {
            var contractRet = new Contract_CrossRef_CustomTag_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                var repCustomTagsXRefs = new CrossRef_CustomTagRepository();

                // this is an update
                CrossRef_CustomTag xref = null;
                if (contract.CrossRef_CustomTagID.HasValue)
                {
                    contractRet.ErrorMessage = "Updates are not allowed";
                    return contractRet;
                }
                xref = new CrossRef_CustomTag();

                //TODO: Custom Tags - check if the CustomTagID is valid
                //TODO: Custom Tags - check if the CrossRefID is valid

                xref.CrossRefID = contract.CrossRefID;
                xref.CrossRefType = contract.CrossRefType;
                xref.CustomTagID = contract.CustomTagID;

                repCustomTagsXRefs.Save(xref);

                contractRet.CrossRef_CustomTag = xref.ToContract();

                StatsCache.Instance.UpdateAnimeContract(contract.CrossRefID);
                StatsCache.Instance.UpdateUsingAnime(contract.CrossRefID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteCustomTagCrossRefByID(int xrefID)
        {
            try
            {
                var repCustomTagsXrefs = new CrossRef_CustomTagRepository();

                var pl = repCustomTagsXrefs.GetByID(xrefID);
                if (pl == null)
                    return "Custom Tag not found";

                repCustomTagsXrefs.Delete(xrefID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID)
        {
            try
            {
                var repCustomTagsXrefs = new CrossRef_CustomTagRepository();

                var xrefs = repCustomTagsXrefs.GetByUniqueID(customTagID, crossRefType, crossRefID);

                if (xrefs == null || xrefs.Count == 0)
                    return "Custom Tag not found";

                repCustomTagsXrefs.Delete(xrefs[0].CrossRef_CustomTagID);
                StatsCache.Instance.UpdateAnimeContract(crossRefID);
                StatsCache.Instance.UpdateUsingAnime(crossRefID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public Contract_CustomTag_SaveResponse SaveCustomTag(Contract_CustomTag contract)
        {
            var contractRet = new Contract_CustomTag_SaveResponse();
            contractRet.ErrorMessage = "";

            try
            {
                var repCustomTags = new CustomTagRepository();

                // this is an update
                CustomTag ctag = null;
                if (contract.CustomTagID.HasValue)
                {
                    ctag = repCustomTags.GetByID(contract.CustomTagID.Value);
                    if (ctag == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing custom tag with ID: " +
                                                   contract.CustomTagID.Value;
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

                repCustomTags.Save(ctag);

                contractRet.CustomTag = ctag.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteCustomTag(int customTagID)
        {
            try
            {
                var repCustomTags = new CustomTagRepository();

                var pl = repCustomTags.GetByID(customTagID);
                if (pl == null)
                    return "Custom Tag not found";

                // first get a list of all the anime that referenced this tag
                var repCustomTagsXRefs = new CrossRef_CustomTagRepository();
                var xrefs = repCustomTagsXRefs.GetByCustomTagID(customTagID);

                repCustomTags.Delete(customTagID);

                // update cached data for any anime that were affected
                foreach (var xref in xrefs)
                {
                    StatsCache.Instance.UpdateAnimeContract(xref.CrossRefID);
                    StatsCache.Instance.UpdateUsingAnime(xref.CrossRefID);
                }


                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public Contract_CustomTag GetCustomTag(int customTagID)
        {
            try
            {
                var repCustomTags = new CustomTagRepository();

                var ctag = repCustomTags.GetByID(customTagID);
                if (ctag == null)
                    return null;

                return ctag.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        #endregion

        #region Web Cache Admin

        public bool IsWebCacheAdmin()
        {
            try
            {
                var res = AzureWebAPI.Admin_AuthUser();
                return string.IsNullOrEmpty(res);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return false;
            }
        }

        public Contract_Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType)
        {
            try
            {
                var lType = (AzureLinkType)linkType;
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
                    return link.ToContract();

                return null;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_AdminMessage> GetAdminMessages()
        {
            try
            {
                var msgs = new List<Contract_AdminMessage>();

                if (ServerInfo.Instance.AdminMessages != null)
                {
                    foreach (var msg in ServerInfo.Instance.AdminMessages)
                        msgs.Add(msg.ToContract());
                }

                return msgs;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
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
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        /// <summary>
        ///     Sends the current user's TvDB links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTvDBLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                var results = AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                var foundLinks = false;
                if (results != null)
                {
                    foreach (var xref in results)
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
                foreach (var xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                if (results != null)
                {
                    var linksToApprove = new List<CrossRef_AniDB_TvDB>();
                    foreach (var xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (var xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(xref.CrossRef_AniDB_TvDBId.Value);
                    }
                    return "Success";
                }
                return "Failure to send links to web cache";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        /// <summary>
        ///     Sends the current user's Trakt links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTraktLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                var repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                var results = AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                var foundLinks = false;
                if (results != null)
                {
                    foreach (var xref in results)
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
                foreach (var xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                if (results != null)
                {
                    var linksToApprove = new List<CrossRef_AniDB_Trakt>();
                    foreach (var xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (var xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(xref.CrossRef_AniDB_TraktId.Value);
                    }
                    return "Success";
                }
                return "Failure to send links to web cache";

                //return JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                var contracts = new List<Contract_Azure_CrossRef_AniDB_TvDB>();
                List<CrossRef_AniDB_TvDB> results = null;

                if (isAdmin)
                    results = AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                else
                    results = AzureWebAPI.Get_CrossRefAniDBTvDB(animeID);
                if (results == null || results.Count == 0) return contracts;

                foreach (var xref in results)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }


        public List<Contract_CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID)
        {
            try
            {
                var ret = new List<Contract_CrossRef_AniDB_TvDBV2>();

                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);
                if (xrefs == null) return ret;

                foreach (var xref in xrefs)
                    ret.Add(xref.ToContract());

                return ret;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_CrossRef_AniDB_TvDB_Episode> GetTVDBCrossRefEpisode(int animeID)
        {
            try
            {
                var contracts = new List<Contract_CrossRef_AniDB_TvDB_Episode>();

                var repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);

                foreach (var xref in xrefs)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }


        public List<Contract_TVDBSeriesSearchResult> SearchTheTvDB(string criteria)
        {
            var results = new List<Contract_TVDBSeriesSearchResult>();
            try
            {
                var tvResults = JMMService.TvdbHelper.SearchSeries(criteria);

                foreach (var res in tvResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return results;
            }
        }


        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            var seasonNumbers = new List<int>();
            try
            {
                // refresh data from TvDB
                JMMService.TvdbHelper.UpdateAllInfoAndImages(seriesID, true, false);

                var repEps = new TvDB_EpisodeRepository();
                seasonNumbers = repEps.GetSeasonNumbersForSeries(seriesID);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return seasonNumbers;
            }
        }

        public string LinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber, int? crossRef_AniDB_TvDBV2ID)
        {
            try
            {
                var repXref = new CrossRef_AniDB_TvDBV2Repository();

                if (crossRef_AniDB_TvDBV2ID.HasValue)
                {
                    var xrefTemp = repXref.GetByID(crossRef_AniDB_TvDBV2ID.Value);
                    // delete the existing one if we are updating
                    TvDBHelper.RemoveLinkAniDBTvDB(xrefTemp.AnimeID, (enEpisodeType)xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TvDBID, xrefTemp.TvDBSeasonNumber, xrefTemp.TvDBStartEpisodeNumber);
                }

                var xref = repXref.GetByTvDBID(tvDBID, tvSeasonNumber, tvEpNumber, animeID, aniEpType, aniEpNumber);
                if (xref != null)
                {
                    var msg = string.Format("You have already linked Anime ID {0} to this TvDB show/season/ep",
                        xref.AnimeID);
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this TvDB show/season/ep",
                            anime.MainTitle, xref.AnimeID);
                    }
                    return msg;
                }

                return TvDBHelper.LinkAniDBTvDB(animeID, (enEpisodeType)aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber, false);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        /// <summary>
        ///     Removes all tvdb links for one anime
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public string RemoveLinkAniDBTvDBForAnime(int animeID)
        {
            try
            {
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);
                if (xrefs == null) return "";

                var repDefaults = new AniDB_Anime_DefaultImageRepository();
                foreach (var xref in xrefs)
                {
                    // check if there are default images used associated
                    var images = repDefaults.GetByAnimeID(animeID);
                    foreach (var image in images)
                    {
                        if (image.ImageParentType == (int)JMMImageType.TvDB_Banner ||
                            image.ImageParentType == (int)JMMImageType.TvDB_Cover ||
                            image.ImageParentType == (int)JMMImageType.TvDB_FanArt)
                        {
                            if (image.ImageParentID == xref.TvDBID)
                                repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
                        }
                    }

                    TvDBHelper.RemoveLinkAniDBTvDB(xref.AnimeID, (enEpisodeType)xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDB(int animeID, int aniEpType, int aniEpNumber, int tvDBID, int tvSeasonNumber,
            int tvEpNumber)
        {
            try
            {
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                var repDefaults = new AniDB_Anime_DefaultImageRepository();
                var images = repDefaults.GetByAnimeID(animeID);
                foreach (var image in images)
                {
                    if (image.ImageParentType == (int)JMMImageType.TvDB_Banner ||
                        image.ImageParentType == (int)JMMImageType.TvDB_Cover ||
                        image.ImageParentType == (int)JMMImageType.TvDB_FanArt)
                    {
                        if (image.ImageParentID == tvDBID)
                            repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TvDBHelper.RemoveLinkAniDBTvDB(animeID, (enEpisodeType)aniEpType, aniEpNumber, tvDBID, tvSeasonNumber,
                    tvEpNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID)
        {
            try
            {
                var repXrefs = new CrossRef_AniDB_TvDB_EpisodeRepository();
                var repEps = new AniDB_EpisodeRepository();
                var ep = repEps.GetByEpisodeID(aniDBEpisodeID);

                if (ep == null) return "Could not find Episode";

                var xref = repXrefs.GetByAniDBEpisodeID(aniDBEpisodeID);
                if (xref == null) return "Could not find Link!";


                repXrefs.Delete(xref.CrossRef_AniDB_TvDB_EpisodeID);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public List<Contract_TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID)
        {
            var allImages = new List<Contract_TvDB_ImagePoster>();
            try
            {
                var repImages = new TvDB_ImagePosterRepository();
                List<TvDB_ImagePoster> allPosters = null;
                if (tvDBID.HasValue)
                    allPosters = repImages.GetBySeriesID(tvDBID.Value);
                else
                    allPosters = repImages.GetAll();

                foreach (var img in allPosters)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        public List<Contract_TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID)
        {
            var allImages = new List<Contract_TvDB_ImageWideBanner>();
            try
            {
                var repImages = new TvDB_ImageWideBannerRepository();
                List<TvDB_ImageWideBanner> allBanners = null;
                if (tvDBID.HasValue)
                    allBanners = repImages.GetBySeriesID(tvDBID.Value);
                else
                    allBanners = repImages.GetAll();

                foreach (var img in allBanners)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        public List<Contract_TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID)
        {
            var allImages = new List<Contract_TvDB_ImageFanart>();
            try
            {
                var repImages = new TvDB_ImageFanartRepository();
                List<TvDB_ImageFanart> allFanart = null;
                if (tvDBID.HasValue)
                    allFanart = repImages.GetBySeriesID(tvDBID.Value);
                else
                    allFanart = repImages.GetAll();

                foreach (var img in allFanart)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        public List<Contract_TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID)
        {
            var allImages = new List<Contract_TvDB_Episode>();
            try
            {
                var repImages = new TvDB_EpisodeRepository();
                List<TvDB_Episode> allEpisodes = null;
                if (tvDBID.HasValue)
                    allEpisodes = repImages.GetBySeriesID(tvDBID.Value);
                else
                    allEpisodes = repImages.GetAll();

                foreach (var img in allEpisodes)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        #endregion

        #region Trakt

        public List<Contract_Trakt_ImageFanart> GetAllTraktFanart(int? traktShowID)
        {
            var allImages = new List<Contract_Trakt_ImageFanart>();
            try
            {
                var repImages = new Trakt_ImageFanartRepository();
                List<Trakt_ImageFanart> allFanart = null;
                if (traktShowID.HasValue)
                    allFanart = repImages.GetByShowID(traktShowID.Value);
                else
                    allFanart = repImages.GetAll();

                foreach (var img in allFanart)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        public List<Contract_Trakt_ImagePoster> GetAllTraktPosters(int? traktShowID)
        {
            var allImages = new List<Contract_Trakt_ImagePoster>();
            try
            {
                var repImages = new Trakt_ImagePosterRepository();
                List<Trakt_ImagePoster> allPosters = null;
                if (traktShowID.HasValue)
                    allPosters = repImages.GetByShowID(traktShowID.Value);
                else
                    allPosters = repImages.GetAll();

                foreach (var img in allPosters)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        public List<Contract_Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
        {
            var allEps = new List<Contract_Trakt_Episode>();
            try
            {
                var repEpisodes = new Trakt_EpisodeRepository();
                List<Trakt_Episode> allEpisodes = null;
                if (traktShowID.HasValue)
                    allEpisodes = repEpisodes.GetByShowID(traktShowID.Value);
                else
                    allEpisodes = repEpisodes.GetAll();

                foreach (var ep in allEpisodes)
                    allEps.Add(ep.ToContract());

                return allEps;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allEps;
            }
        }

        public List<Contract_Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
        {
            var allEps = new List<Contract_Trakt_Episode>();
            try
            {
                var repShows = new Trakt_ShowRepository();
                var show = repShows.GetByTraktSlug(traktID);
                if (show != null)
                    allEps = GetAllTraktEpisodes(show.Trakt_ShowID);

                return allEps;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allEps;
            }
        }

        public List<Contract_Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                var contracts = new List<Contract_Azure_CrossRef_AniDB_Trakt>();
                List<CrossRef_AniDB_Trakt> results = null;

                if (isAdmin)
                    results = AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                else
                    results = AzureWebAPI.Get_CrossRefAniDBTrakt(animeID);

                if (results == null || results.Count == 0) return contracts;

                foreach (var xref in results)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
            int traktEpNumber, int? crossRef_AniDB_TraktV2ID)
        {
            try
            {
                var repXref = new CrossRef_AniDB_TraktV2Repository();

                if (crossRef_AniDB_TraktV2ID.HasValue)
                {
                    var xrefTemp = repXref.GetByID(crossRef_AniDB_TraktV2ID.Value);
                    // delete the existing one if we are updating
                    TraktTVHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (enEpisodeType)xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }

                var xref = repXref.GetByTraktID(traktID, seasonNumber, traktEpNumber, animeID, aniEpType, aniEpNumber);
                if (xref != null)
                {
                    var msg = string.Format("You have already linked Anime ID {0} to this Trakt show/season/ep",
                        xref.AnimeID);
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this Trakt show/season/ep",
                            anime.MainTitle, xref.AnimeID);
                    }
                    return msg;
                }

                return TraktTVHelper.LinkAniDBTrakt(animeID, (enEpisodeType)aniEpType, aniEpNumber, traktID,
                    seasonNumber, traktEpNumber, false);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }


        public List<Contract_CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
        {
            try
            {
                var contracts = new List<Contract_CrossRef_AniDB_TraktV2>();

                var repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);
                if (xrefs == null) return contracts;

                foreach (var xref in xrefs)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
        {
            try
            {
                var contracts = new List<Contract_CrossRef_AniDB_Trakt_Episode>();

                var repCrossRef = new CrossRef_AniDB_Trakt_EpisodeRepository();
                var xrefs = repCrossRef.GetByAnimeID(animeID);

                foreach (var xref in xrefs)
                    contracts.Add(xref.ToContract());

                return contracts;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_TraktTVShowResponse> SearchTrakt(string criteria)
        {
            var results = new List<Contract_TraktTVShowResponse>();
            try
            {
                var traktResults = TraktTVHelper.SearchShowV2(criteria);

                foreach (var res in traktResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return results;
            }
        }

        public string RemoveLinkAniDBTraktForAnime(int animeID)
        {
            try
            {
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                var repDefaults = new AniDB_Anime_DefaultImageRepository();
                var images = repDefaults.GetByAnimeID(animeID);
                foreach (var image in images)
                {
                    if (image.ImageParentType == (int)JMMImageType.Trakt_Fanart ||
                        image.ImageParentType == (int)JMMImageType.Trakt_Poster)
                    {
                        repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                var repXrefTrakt = new CrossRef_AniDB_TraktV2Repository();
                foreach (var xref in repXrefTrakt.GetByAnimeID(animeID))
                {
                    TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (enEpisodeType)xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber);
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID,
            int traktSeasonNumber, int traktEpNumber)
        {
            try
            {
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                var repDefaults = new AniDB_Anime_DefaultImageRepository();
                var images = repDefaults.GetByAnimeID(animeID);
                foreach (var image in images)
                {
                    if (image.ImageParentType == (int)JMMImageType.Trakt_Fanart ||
                        image.ImageParentType == (int)JMMImageType.Trakt_Poster)
                    {
                        repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (enEpisodeType)aniEpType, aniEpNumber,
                    traktID, traktSeasonNumber, traktEpNumber);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public List<int> GetSeasonNumbersForTrakt(string traktID)
        {
            var seasonNumbers = new List<int>();
            try
            {
                // refresh show info including season numbers from trakt
                var tvshow = TraktTVHelper.GetShowInfoV2(traktID);

                var repShows = new Trakt_ShowRepository();
                var show = repShows.GetByTraktSlug(traktID);
                if (show == null) return seasonNumbers;

                foreach (var season in show.Seasons)
                    seasonNumbers.Add(season.Season);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return seasonNumbers;
            }
        }

        #endregion

        #region MAL

        public Contract_CrossRef_AniDB_MALResult GetMALCrossRefWebCache(int animeID)
        {
            try
            {
                var result = AzureWebAPI.Get_CrossRefAniDBMAL(animeID);
                if (result == null) return null;

                return result.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_MALAnimeResponse> SearchMAL(string criteria)
        {
            var results = new List<Contract_MALAnimeResponse>();
            try
            {
                var malResults = MALHelper.SearchAnimesByTitle(criteria);

                foreach (var res in malResults.entry)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return results;
            }
        }


        public string LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber)
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_MALRepository();
                var xrefTemp = repCrossRef.GetByMALID(malID);
                if (xrefTemp != null)
                {
                    var animeName = "";
                    try
                    {
                        var repAnime = new AniDB_AnimeRepository();
                        var anime = repAnime.GetByAnimeID(xrefTemp.AnimeID);
                        if (anime != null) animeName = anime.MainTitle;
                    }
                    catch
                    {
                    }
                    return string.Format("Not using MAL link as this MAL ID ({0}) is already in use by {1} ({2})", malID,
                        xrefTemp.AnimeID, animeName);
                }

                xrefTemp = repCrossRef.GetByAnimeConstraint(animeID, epType, epNumber);
                if (xrefTemp != null)
                {
                    // delete the link first because we are over-writing it
                    repCrossRef.Delete(xrefTemp.CrossRef_AniDB_MALID);
                    //return string.Format("Not using MAL link as this Anime ID ({0}) is already in use by {1}/{2}/{3} ({4})", animeID, xrefTemp.MALID, epType, epNumber, xrefTemp.MALTitle);
                }

                MALHelper.LinkAniDBMAL(animeID, malID, malTitle, epType, epNumber, false);

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string LinkAniDBMALUpdated(int animeID, int malID, string malTitle, int oldEpType, int oldEpNumber,
            int newEpType, int newEpNumber)
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_MALRepository();
                var xrefTemp = repCrossRef.GetByAnimeConstraint(animeID, oldEpType, oldEpNumber);
                if (xrefTemp == null)
                    return string.Format("Could not find MAL link ({0}/{1}/{2})", animeID, oldEpType, oldEpNumber);

                repCrossRef.Delete(xrefTemp.CrossRef_AniDB_MALID);

                return LinkAniDBMAL(animeID, malID, malTitle, newEpType, newEpNumber);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        #endregion

        #region Other Cross Refs

        public Contract_CrossRef_AniDB_OtherResult GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
        {
            try
            {
                var result = AzureWebAPI.Get_CrossRefAniDBOther(animeID, (CrossRefType)crossRefType);
                if (result == null) return null;

                return result.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public Contract_CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_OtherRepository();
                var xref = repCrossRef.GetByAnimeIDAndType(animeID, (CrossRefType)crossRefType);
                if (xref == null) return null;

                return xref.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public string LinkAniDBOther(int animeID, int movieID, int crossRefType)
        {
            try
            {
                var xrefType = (CrossRefType)crossRefType;

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
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBOther(int animeID, int crossRefType)
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(animeID);

                if (anime == null) return "Could not find Anime!";

                var xrefType = (CrossRefType)crossRefType;
                switch (xrefType)
                {
                    case CrossRefType.MovieDB:

                        // check if there are default images used associated
                        var repDefaults = new AniDB_Anime_DefaultImageRepository();
                        var images = repDefaults.GetByAnimeID(animeID);
                        foreach (var image in images)
                        {
                            if (image.ImageParentType == (int)JMMImageType.MovieDB_FanArt ||
                                image.ImageParentType == (int)JMMImageType.MovieDB_Poster)
                            {
                                repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
                            }
                        }

                        MovieDBHelper.RemoveLinkAniDBMovieDB(animeID);
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        #endregion

        #region MovieDB

        public List<Contract_MovieDBMovieSearchResult> SearchTheMovieDB(string criteria)
        {
            var results = new List<Contract_MovieDBMovieSearchResult>();
            try
            {
                var movieResults = MovieDBHelper.Search(criteria);

                foreach (var res in movieResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return results;
            }
        }

        public List<Contract_MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
        {
            var allImages = new List<Contract_MovieDB_Poster>();
            try
            {
                var repImages = new MovieDB_PosterRepository();
                List<MovieDB_Poster> allPosters = null;
                if (movieID.HasValue)
                    allPosters = repImages.GetByMovieID(movieID.Value);
                else
                    allPosters = repImages.GetAllOriginal();

                foreach (var img in allPosters)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        public List<Contract_MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
        {
            var allImages = new List<Contract_MovieDB_Fanart>();
            try
            {
                var repImages = new MovieDB_FanartRepository();
                List<MovieDB_Fanart> allFanart = null;
                if (movieID.HasValue)
                    allFanart = repImages.GetByMovieID(movieID.Value);
                else
                    allFanart = repImages.GetAllOriginal();

                foreach (var img in allFanart)
                    allImages.Add(img.ToContract());

                return allImages;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return allImages;
            }
        }

        #endregion
    }
}