using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;
using System.IO;
using JMMFileHelper;
using JMMServer.Commands;
using System.Threading;
using AniDBAPI;
using System.ServiceModel;
using JMMServer.WebCache;
using JMMServer.Providers.TvDB;
using JMMServer.Providers.MovieDB;
using BinaryNorthwest;
using JMMServer.Providers.TraktTV;

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
				DateTime start = DateTime.Now;
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();

				List<AnimeGroup> allGrps = repGroups.GetAll();
				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAllGroups (Database) in {0} ms", ts.TotalMilliseconds);
				start = DateTime.Now;

				// user records
				AnimeGroup_UserRepository repGroupUser = new AnimeGroup_UserRepository();
				List<AnimeGroup_User> userRecordList = repGroupUser.GetByUserID(userID);
				Dictionary<int, AnimeGroup_User> dictUserRecords = new Dictionary<int, AnimeGroup_User>();
				foreach (AnimeGroup_User grpUser in userRecordList)
					dictUserRecords[grpUser.AnimeGroupID] = grpUser;

				foreach (AnimeGroup ag in allGrps)
				{
					AnimeGroup_User userRec = null;
					if (dictUserRecords.ContainsKey(ag.AnimeGroupID))
						userRec = dictUserRecords[ag.AnimeGroupID];

					// calculate stats
					Contract_AnimeGroup contract = ag.ToContract(userRec);
					grps.Add(contract);
				}

				


				grps.Sort();
				ts = DateTime.Now - start;
				logger.Info("GetAllGroups (Contracts) in {0} ms", ts.TotalMilliseconds);

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return grps;
		}

		public List<Contract_AnimeGroup> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
		{
			List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();

				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null)
					return grps;

				Contract_AnimeGroup contractGrp = grp.ToContract(grp.GetUserRecord(userID));
				grps.Add(contractGrp);

				int? groupID = grp.AnimeGroupParentID;
				while (groupID.HasValue)
				{
					AnimeGroup grpTemp = repGroups.GetByID(groupID.Value);
					if (grpTemp != null)
					{
						Contract_AnimeGroup contractGrpTemp = grpTemp.ToContract(grpTemp.GetUserRecord(userID));
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
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return grps;
		}

		public List<Contract_AnimeGroup> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
		{
			List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
			try
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries series = repSeries.GetByID(animeSeriesID);
				if (series == null)
					return grps;

				foreach (AnimeGroup grp in series.AllGroupsAbove)
				{
					Contract_AnimeGroup contractGrpTemp = grp.ToContract(grp.GetUserRecord(userID));
					grps.Add(contractGrpTemp);
				}

				return grps;
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
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null) return null;

				Contract_AnimeGroup contractGrpTemp = grp.ToContract(grp.GetUserRecord(userID));

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
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
				AnimeGroupRepository repGroups = new AnimeGroupRepository();

				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null) return "Group does not exist";

				int? parentGroupID = grp.AnimeGroupParentID;

				foreach (AnimeSeries ser in grp.AllSeries)
				{
					DeleteAnimeSeries(ser.AnimeSeriesID, deleteFiles);
				}

				// delete all sub groups
				foreach (AnimeGroup subGroup in grp.AllChildGroups)
				{
					DeleteAnimeGroup(subGroup.AnimeGroupID, deleteFiles);
				}

				GroupFilterConditionRepository repConditions = new GroupFilterConditionRepository();
				// delete any group filter conditions which reference these groups
				foreach (GroupFilterCondition gfc in repConditions.GetByConditionType(GroupFilterConditionType.AnimeGroup))
				{
					int thisGrpID = 0;
					int.TryParse(gfc.ConditionParameter, out thisGrpID);
					if (thisGrpID == animeGroupID)
						repConditions.Delete(gfc.GroupFilterConditionID);
				}
				

				repGroups.Delete(grp.AnimeGroupID);

				// finally update stats

				if (parentGroupID.HasValue)
				{
					AnimeGroup grpParent = repGroups.GetByID(parentGroupID.Value);

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

		public List<Contract_AnimeGroup> GetAnimeGroupsForFilter(int groupFilterID, int userID)
		{
			List<Contract_AnimeGroup> retGroups = new List<Contract_AnimeGroup>();
			try
			{

				DateTime start = DateTime.Now;
				GroupFilterRepository repGF = new GroupFilterRepository();

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser user = repUsers.GetByID(userID);
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
					gf = repGF.GetByID(groupFilterID);
					if (gf == null) return retGroups;	
				}

				Contract_GroupFilterExtended contract = gf.ToContractExtended(user);

				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				List<AnimeGroup> allGrps = repGroups.GetAll();

				AnimeGroup_UserRepository repUserRecords = new AnimeGroup_UserRepository();
				List<AnimeGroup_User> userRecords = repUserRecords.GetByUserID(userID);
				Dictionary<int, AnimeGroup_User> dictUserRecords = new Dictionary<int, AnimeGroup_User>();
				foreach (AnimeGroup_User userRec in userRecords)
					dictUserRecords[userRec.AnimeGroupID] = userRec;

				TimeSpan ts = DateTime.Now - start;
				string msg = string.Format("Got groups for filter DB: {0} - {1} in {2} ms", gf.GroupFilterName, allGrps.Count, ts.TotalMilliseconds);
				logger.Info(msg);
				start = DateTime.Now;

				foreach (AnimeGroup grp in allGrps)
				{
					AnimeGroup_User userRec = null;
					if (dictUserRecords.ContainsKey(grp.AnimeGroupID))
						userRec = dictUserRecords[grp.AnimeGroupID];

					if (StatsCache.Instance.EvaluateGroupFilter(gf, grp, user, userRec))
						retGroups.Add(grp.ToContract(userRec));
				}

				ts = DateTime.Now - start;
				msg = string.Format("Got groups for filter EVAL: {0} - {1} in {2} ms", gf.GroupFilterName, retGroups.Count, ts.TotalMilliseconds);
				logger.Info(msg);

				return retGroups;
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
				GroupFilterRepository repGF = new GroupFilterRepository();
				GroupFilter gf = repGF.GetByID(groupFilterID);
				if (gf == null) return null;

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser user = repUsers.GetByID(userID);
				if (user == null) return null;

				Contract_GroupFilterExtended contract = gf.ToContractExtended(user);

				return contract;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return null;
		}

		public List<Contract_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
		{
			List<Contract_GroupFilterExtended> gfs = new List<Contract_GroupFilterExtended>();
			try
			{
				DateTime start = DateTime.Now;
				GroupFilterRepository repGF = new GroupFilterRepository();

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser user = repUsers.GetByID(userID);
				if (user == null) return gfs;

				List<GroupFilter> allGfs = repGF.GetAll();
				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);
				start = DateTime.Now;

				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				List<AnimeGroup> allGrps = repGroups.GetAll();
				ts = DateTime.Now - start;
				logger.Info("GetAllGroups (Database) in {0} ms", ts.TotalMilliseconds);
				start = DateTime.Now;

				AnimeGroup_UserRepository repUserRecords = new AnimeGroup_UserRepository();
				List<AnimeGroup_User> userRecords = repUserRecords.GetByUserID(userID);
				Dictionary<int, AnimeGroup_User> dictUserRecords = new Dictionary<int, AnimeGroup_User>();
				foreach (AnimeGroup_User userRec in userRecords)
					dictUserRecords[userRec.AnimeGroupID] = userRec;

				start = DateTime.Now;
				foreach (GroupFilter gf in allGfs)
				{
					Contract_GroupFilter gfContract = gf.ToContract();
					Contract_GroupFilterExtended gfeContract = new Contract_GroupFilterExtended();
					gfeContract.GroupFilter = gfContract;
					gfeContract.GroupCount = 0;
					gfeContract.SeriesCount = 0;

					foreach (AnimeGroup grp in allGrps)
					{
						AnimeGroup_User userRec = null;
						if (dictUserRecords.ContainsKey(grp.AnimeGroupID))
							userRec = dictUserRecords[grp.AnimeGroupID];

						// calculate stats
						if (StatsCache.Instance.EvaluateGroupFilter(gf, grp, user, userRec))
							gfeContract.GroupCount++;
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
			List<Contract_GroupFilter> gfs = new List<Contract_GroupFilter>();
			try
			{
				DateTime start = DateTime.Now;
				GroupFilterRepository repGF = new GroupFilterRepository();


				List<GroupFilter> allGfs = repGF.GetAll();
				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

				start = DateTime.Now;
				foreach (GroupFilter gf in allGfs)
				{
					gfs.Add(gf.ToContract());
				}

				//gfs.Sort();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return gfs;
		}

		public Contract_GroupFilter_SaveResponse SaveGroupFilter(Contract_GroupFilter contract)
		{
			Contract_GroupFilter_SaveResponse response = new Contract_GroupFilter_SaveResponse();
			response.ErrorMessage = string.Empty;
			response.GroupFilter = null;

			GroupFilterRepository repGF = new GroupFilterRepository();
			GroupFilterConditionRepository repGFC = new GroupFilterConditionRepository();

			// Process the group
			GroupFilter gf = null;
			if (contract.GroupFilterID.HasValue)
			{
				gf = repGF.GetByID(contract.GroupFilterID.Value);
				if (gf == null)
				{
					response.ErrorMessage = "Could not find existing Group Filter with ID: " + contract.GroupFilterID.Value.ToString();
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
			foreach (GroupFilterCondition gfc in gf.FilterConditions)
			{
				bool gfcExists = false;
				foreach (Contract_GroupFilterCondition gfc_con in contract.FilterConditions)
				{
					if (gfc_con.GroupFilterConditionID.HasValue && gfc_con.GroupFilterConditionID.Value == gfc.GroupFilterConditionID)
					{
						gfcExists = true;
						break;
					}
				}
				if (!gfcExists)
					repGFC.Delete(gfc.GroupFilterConditionID);
			}

			// save newly added or modified ones
			foreach (Contract_GroupFilterCondition gfc_con in contract.FilterConditions)
			{
				GroupFilterCondition gfc = null;
				if (gfc_con.GroupFilterConditionID.HasValue)
				{
					gfc = repGFC.GetByID(gfc_con.GroupFilterConditionID.Value);
					if (gfc == null)
					{
						response.ErrorMessage = "Could not find existing Group Filter Condition with ID: " + gfc_con.GroupFilterConditionID.ToString();
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
				GroupFilterRepository repGF = new GroupFilterRepository();
				GroupFilterConditionRepository repGFC = new GroupFilterConditionRepository();

				GroupFilter gf = repGF.GetByID(groupFilterID);
				if (gf == null)
					return "Group Filter not found";

				// delete all the conditions first
				foreach (GroupFilterCondition gfc in gf.FilterConditions)
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
			Contract_AnimeGroup_SaveResponse contractout = new Contract_AnimeGroup_SaveResponse();
			contractout.ErrorMessage = "";
			contractout.AnimeGroup = null;
			try
			{
				AnimeGroupRepository repGroup = new AnimeGroupRepository();
				AnimeGroup grp = null;
				if (contract.AnimeGroupID.HasValue)
				{
					grp = repGroup.GetByID(contract.AnimeGroupID.Value);
					if (grp == null)
					{
						contractout.ErrorMessage = "Could not find existing group with ID: " + contract.AnimeGroupID.Value.ToString();
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

				AnimeGroup_User userRecord = grp.GetUserRecord(userID);
				if (userRecord == null) userRecord = new AnimeGroup_User(userID, grp.AnimeGroupID);
				userRecord.IsFave = contract.IsFave;
				AnimeGroup_UserRepository repUserRecords = new AnimeGroup_UserRepository();
				repUserRecords.Save(userRecord);

				Contract_AnimeGroup contractGrp = grp.ToContract(grp.GetUserRecord(userID));
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
			Contract_AnimeSeries_SaveResponse contractout = new Contract_AnimeSeries_SaveResponse();
			contractout.ErrorMessage = "";
			contractout.AnimeSeries = null;
			try
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = null;

				ser = repSeries.GetByID(animeSeriesID);
				if (ser == null)
				{
					contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID.ToString();
					return contractout;
				}

				// make sure the group exists
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup grpTemp = repGroups.GetByID(newAnimeGroupID);
				if (grpTemp == null)
				{
					contractout.ErrorMessage = "Could not find existing group with ID: " + newAnimeGroupID.ToString();
					return contractout;
				}

				int oldGroupID = ser.AnimeGroupID;
				ser.AnimeGroupID = newAnimeGroupID;
				ser.DateTimeUpdated = DateTime.Now;

				repSeries.Save(ser);

				// update stats for new groups
				//ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
				ser.UpdateStats(true, true, true);

				// update stats for old groups
				AnimeGroup grp = repGroups.GetByID(oldGroupID);
				if (grp != null)
				{
					grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
				}

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(ser.AniDB_ID);
				if (anime == null)
				{
					contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
					return contractout;
				}

				contractout.AnimeSeries = ser.ToContract(anime, ser.CrossRefTvDB, ser.CrossRefMovieDB, ser.GetUserRecord(userID));

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
			Contract_AnimeSeries_SaveResponse contractout = new Contract_AnimeSeries_SaveResponse();
			contractout.ErrorMessage = "";
			contractout.AnimeSeries = null;
			try
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = null;

				int? oldGroupID = null;
				if (contract.AnimeSeriesID.HasValue)
				{
					ser = repSeries.GetByID(contract.AnimeSeriesID.Value);
					if (ser == null)
					{
						contractout.ErrorMessage = "Could not find existing series with ID: " + contract.AnimeSeriesID.Value.ToString();
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
				}



				ser.AnimeGroupID = contract.AnimeGroupID;
				ser.AniDB_ID = contract.AniDB_ID;
				ser.DateTimeUpdated = DateTime.Now;

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(ser.AniDB_ID);
				if (anime == null)
				{
					contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
					return contractout;
				}

				repSeries.Save(ser);

				// update stats for groups
				//ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true ,true, true);
				ser.UpdateStats(true, true, true);

				if (oldGroupID.HasValue)
				{
					AnimeGroupRepository repGroups = new AnimeGroupRepository();
					AnimeGroup grp = repGroups.GetByID(oldGroupID.Value);
					if (grp != null)
					{
						grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
					}
				}

				contractout.AnimeSeries = ser.ToContract(anime, ser.CrossRefTvDB, ser.CrossRefMovieDB, ser.GetUserRecord(userID));

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
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEps.GetByID(animeEpisodeID);
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
				VideoLocalRepository repVids = new VideoLocalRepository();
				CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();

				VideoLocal vid = repVids.GetByID(videoLocalID);
				if (vid == null)
					return "Could not find video record";

				int? animeSeriesID = null;
				foreach (AnimeEpisode ep in vid.AnimeEpisodes)
				{
					if (ep.AniDB_EpisodeID != aniDBEpisodeID) continue;

					animeSeriesID = ep.AnimeSeriesID;
					CrossRef_File_Episode xref = repXRefs.GetByHashAndEpisodeID(vid.Hash, ep.AniDB_EpisodeID);
					if (xref != null)
					{
						if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
							return "Cannot remove associations created from AniDB data";

						// delete cross ref from web cache 
						CommandRequest_WebCacheDeleteXRefFileEpisode cr = new CommandRequest_WebCacheDeleteXRefFileEpisode(vid.Hash, ep.AniDB_EpisodeID);
						cr.Save();

						repXRefs.Delete(xref.CrossRef_File_EpisodeID);
					}
				}

				if (animeSeriesID.HasValue)
				{
					AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
					AnimeSeries ser = repSeries.GetByID(animeSeriesID.Value);
					if (ser != null)
						ser.UpdateStats(true, true, true);
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
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
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

		public string AssociateSingleFile(int videoLocalID, int animeEpisodeID)
		{

			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
				if (vid == null)
					return "Could not find video record";

				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEps.GetByID(animeEpisodeID);
				if (ep == null)
					return "Could not find episode record";

				CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();
				CrossRef_File_Episode xref = new CrossRef_File_Episode();
				xref.PopulateManually(vid, ep);
				repXRefs.Save(xref);

				vid.MoveFileIfRequired();

				CommandRequest_WebCacheSendXRefFileEpisode cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
				cr.Save();

				AnimeSeries ser = ep.AnimeSeries;
				ser.UpdateStats(true, true, true);

				// update epidsode added stats
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				ser.EpisodeAddedDate = DateTime.Now;
				repSeries.Save(ser);

				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				foreach (AnimeGroup grp in ser.AllGroupsAbove)
				{
					grp.EpisodeAddedDate = DateTime.Now;
					repGroups.Save(grp);
				}

				// lets also try adding to the users trakt collecion by sync'ing the series
				if (ser != null)
				{
					CommandRequest_TraktSyncCollectionSeries cmdTrakt = new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID, ser.Anime.MainTitle);
					cmdTrakt.Save();
				}

				return "";

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return "";
		}

		public string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startEpNum, int endEpNum)
		{

			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
				if (vid == null)
					return "Could not find video record";

				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
				CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();

				AnimeSeries ser = repSeries.GetByID(animeSeriesID);
				if (ser == null)
					return "Could not find anime series record";

				for (int i = startEpNum; i <= endEpNum; i++)
				{
					List<AniDB_Episode> anieps = repAniEps.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, i);
					if (anieps.Count == 0)
						return "Could not find the AniDB episode record";

					AniDB_Episode aniep = anieps[0];

					List<AnimeEpisode> eps = repEps.GetByAniEpisodeIDAndSeriesID(aniep.EpisodeID, ser.AnimeSeriesID);
					if (eps.Count == 0)
						return "Could not find episode record";

					AnimeEpisode ep = eps[0];

					CrossRef_File_Episode xref = new CrossRef_File_Episode();
					xref.PopulateManually(vid, ep);
					repXRefs.Save(xref);

					CommandRequest_WebCacheSendXRefFileEpisode cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
					cr.Save();
				}

				vid.MoveFileIfRequired();

				ser.UpdateStats(true, true, true);

				// update epidsode added stats
				ser.EpisodeAddedDate = DateTime.Now;
				repSeries.Save(ser);

				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				foreach (AnimeGroup grp in ser.AllGroupsAbove)
				{
					grp.EpisodeAddedDate = DateTime.Now;
					repGroups.Save(grp);
				}

				// lets also try adding to the users trakt collecion by sync'ing the series
				if (ser != null)
				{
					CommandRequest_TraktSyncCollectionSeries cmdTrakt = new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID, ser.Anime.MainTitle);
					cmdTrakt.Save();
				}

				return "";

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return "";
		}

		public string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber, bool singleEpisode)
		{
			try
			{
				CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				VideoLocalRepository repVids = new VideoLocalRepository();
				AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();

				AnimeSeries ser = repSeries.GetByID(animeSeriesID);
				if (ser == null)
					return "Could not find anime series record";

				int epNumber = startingEpisodeNumber;
				int count = 1;


				foreach (int videoLocalID in videoLocalIDs)
				{
					VideoLocal vid = repVids.GetByID(videoLocalID);
					if (vid == null)
						return "Could not find video local record";

					List<AniDB_Episode> anieps = repAniEps.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, epNumber);
					if (anieps.Count == 0)
						return "Could not find the AniDB episode record";

					AniDB_Episode aniep = anieps[0];

					List<AnimeEpisode> eps = repEps.GetByAniEpisodeIDAndSeriesID(aniep.EpisodeID, ser.AnimeSeriesID);
					if (eps.Count == 0)
						return "Could not find episode record";

					AnimeEpisode ep = eps[0];

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

					repXRefs.Save(xref);

					vid.MoveFileIfRequired();

					CommandRequest_WebCacheSendXRefFileEpisode cr = new CommandRequest_WebCacheSendXRefFileEpisode(xref.CrossRef_File_EpisodeID);
					cr.Save();

					count++;
					if (!singleEpisode) epNumber++;
				}

				ser.UpdateStats(true, true, true);

				// update epidsode added stats
				ser.EpisodeAddedDate = DateTime.Now;
				repSeries.Save(ser);

				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				foreach (AnimeGroup grp in ser.AllGroupsAbove)
				{
					grp.EpisodeAddedDate = DateTime.Now;
					repGroups.Save(grp);
				}

				// lets also try adding to the users trakt collecion by sync'ing the series
				if (ser != null)
				{
					CommandRequest_TraktSyncCollectionSeries cmdTrakt = new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID, ser.Anime.MainTitle);
					cmdTrakt.Save();
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return "";
		}

		private int[] GetEpisodePercentages(int numEpisodes)
		{
			if (numEpisodes == 1) return new int[] { 100 };
			if (numEpisodes == 2) return new int[] { 50, 100 };
			if (numEpisodes == 3) return new int[] { 33, 66, 100 };
			if (numEpisodes == 4) return new int[] { 25, 50, 75, 100 };
			if (numEpisodes == 5) return new int[] { 20, 40, 60, 80, 100 };

			return new int[] { 100 };
		}

		public Contract_AnimeSeries_SaveResponse CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID)
		{
			Contract_AnimeSeries_SaveResponse response = new Contract_AnimeSeries_SaveResponse();
			response.AnimeSeries = null;
			response.ErrorMessage = "";
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				if (animeGroupID.HasValue)
				{
					AnimeGroup grp = repGroups.GetByID(animeGroupID.Value);
					if (grp == null)
					{
						response.ErrorMessage = "Could not find the specified group";
						return response;
					}
				}

				// make sure a series doesn't already exists for this anime
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = repSeries.GetByAnimeID(animeID);
				if (ser != null)
				{
					response.ErrorMessage = "A series already exists for this anime";
					return response;
				}

				// make sure the anime exists first
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null)
					anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(animeID, false, false);

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
					ser = anime.CreateAnimeSeriesAndGroup();
				}

				ser.CreateAnimeEpisodes();

				// check if we have any group status data for this associated anime
				// if not we will download it now
				AniDB_GroupStatusRepository repStatus = new AniDB_GroupStatusRepository();
				if (repStatus.GetByAnimeID(anime.AnimeID).Count == 0)
				{
					CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
					cmdStatus.Save();
				}


				ser.UpdateStats(true, true, true);

				// check for TvDB associations
				CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(anime.AnimeID, false);
				cmd.Save();

				// check for Trakt associations
				CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(anime.AnimeID, false);
				cmd2.Save();

				response.AnimeSeries = ser.ToContract(anime, ser.CrossRefTvDB, ser.CrossRefMovieDB, ser.GetUserRecord(userID));
				return response;
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
				JMMService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true, false);

				// also find any files for this anime which don't have proper media info data
				// we can usually tell this if the Resolution == '0x0'
				VideoLocalRepository repVids = new VideoLocalRepository();
				AniDB_FileRepository repFiles = new AniDB_FileRepository();

				foreach (VideoLocal vid in repVids.GetByAniDBAnimeID(animeID))
				{
					AniDB_File aniFile = vid.AniDBFile;
					if (aniFile == null) continue;

					if (aniFile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase))
					{
						CommandRequest_GetFile cmd = new CommandRequest_GetFile(vid.VideoLocalID);
						cmd.Save();
					}
				}

				// update group status information
				CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID, true);
				cmdStatus.Save();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return "";
		}

		public string UpdateTvDBData(int seriesID)
		{

			try
			{
				JMMService.TvdbHelper.UpdateAllInfoAndImages(seriesID, false);
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

		public Contract_AniDBAnime GetAnime(int animeID)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

			try
			{
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return null;

				Contract_AniDBAnime contract = anime.ToContract();

				AniDB_Anime_DefaultImage defaultPoster = anime.DefaultPoster;
				if (defaultPoster == null)
					contract.DefaultImagePoster = null;
				else
					contract.DefaultImagePoster = defaultPoster.ToContract();

				AniDB_Anime_DefaultImage defaultFanart = anime.DefaultFanart;
				if (defaultFanart == null)
					contract.DefaultImageFanart = null;
				else
					contract.DefaultImageFanart = defaultFanart.ToContract();

				AniDB_Anime_DefaultImage defaultWideBanner = anime.DefaultWideBanner;
				if (defaultWideBanner == null)
					contract.DefaultImageWideBanner = null;
				else
					contract.DefaultImageWideBanner = defaultWideBanner.ToContract();


				return contract;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return null;
		}

		public List<Contract_AniDBAnime> GetAllAnime()
		{
			List<Contract_AniDBAnime> contracts = new List<Contract_AniDBAnime>();

			try
			{
				DateTime start = DateTime.Now;

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				List<AniDB_Anime> animes = repAnime.GetAll();

				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
				List<AniDB_Anime_DefaultImage> allDefaultImages = repDefaults.GetAll();

				Dictionary<int, AniDB_Anime_DefaultImage> dictDefaultsPosters = new Dictionary<int, AniDB_Anime_DefaultImage>();
				Dictionary<int, AniDB_Anime_DefaultImage> dictDefaultsFanart = new Dictionary<int, AniDB_Anime_DefaultImage>();
				Dictionary<int, AniDB_Anime_DefaultImage> dictDefaultsWideBanner = new Dictionary<int, AniDB_Anime_DefaultImage>();

				foreach (AniDB_Anime_DefaultImage defaultImage in allDefaultImages)
				{
					ImageSizeType sizeType = (ImageSizeType)defaultImage.ImageType;

					if (sizeType == ImageSizeType.Fanart)
						dictDefaultsFanart[defaultImage.AnimeID] = defaultImage;

					if (sizeType == ImageSizeType.Poster)
						dictDefaultsPosters[defaultImage.AnimeID] = defaultImage;

					if (sizeType == ImageSizeType.WideBanner)
						dictDefaultsWideBanner[defaultImage.AnimeID] = defaultImage;
				}

				foreach (AniDB_Anime anime in animes)
				{
					Contract_AniDBAnime contract = anime.ToContract();

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

				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAllAnimein {0} ms", ts.TotalMilliseconds);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return contracts;
		}

		public List<Contract_AniDB_AnimeDetailed> GetAllAnimeDetailed()
		{
			List<Contract_AniDB_AnimeDetailed> contracts = new List<Contract_AniDB_AnimeDetailed>();
			int countElements = 0;
			try
			{
				DateTime start = DateTime.Now;

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				

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

				List<AniDB_Anime> animes = repAnime.GetAll();

				// user votes
				AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
				List<AniDB_Vote> allVotes = repVotes.GetAll();

				int i = 0;

				
				
				foreach (AniDB_Anime anime in animes)
				{
					i++;
					//if (i >= 10) continue;

					countElements++;

					Contract_AniDB_AnimeDetailed contract = new Contract_AniDB_AnimeDetailed();

					contract.AnimeTitles = new List<Contract_AnimeTitle>();
					contract.Categories = new List<Contract_AnimeCategory>();
					contract.Tags = new List<Contract_AnimeTag>();
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
						AnimeVideoQualityStat stat = dictAnimeEpisodeVideoQualStats[anime.AnimeID];
						foreach (KeyValuePair<string, int> kvp in stat.VideoQualityEpisodeCount)
						{
							if (kvp.Value >= anime.EpisodeCountNormal)
							{
								if (contract.Stat_AllVideoQuality_Episodes.Length > 0) contract.Stat_AllVideoQuality_Episodes += ",";
								contract.Stat_AllVideoQuality_Episodes += kvp.Key;
							}
						}
					}

					List<string> audioLanguageList = new List<string>();
					List<string> subtitleLanguageList = new List<string>();

					// get audio languages
					if (dictAudioStats.ContainsKey(anime.AnimeID))
					{
						foreach (string lanName in dictAudioStats[anime.AnimeID].LanguageNames)
						{
							if (!audioLanguageList.Contains(lanName)) audioLanguageList.Add(lanName);
						}
					}

					// get subtitle languages
					if (dictSubtitleStats.ContainsKey(anime.AnimeID))
					{
						foreach (string lanName in dictSubtitleStats[anime.AnimeID].LanguageNames)
						{
							if (!subtitleLanguageList.Contains(lanName)) subtitleLanguageList.Add(lanName);
						}
					}

					contract.Stat_AudioLanguages = "";
					foreach (string audioLan in audioLanguageList)
					{
						if (contract.Stat_AudioLanguages.Length > 0) contract.Stat_AudioLanguages += ",";
						contract.Stat_AudioLanguages += audioLan;
					}

					contract.Stat_SubtitleLanguages = "";
					foreach (string subLan in subtitleLanguageList)
					{
						if (contract.Stat_SubtitleLanguages.Length > 0) contract.Stat_SubtitleLanguages += ",";
						contract.Stat_SubtitleLanguages += subLan;
					}

					
					if (allTitlesDict.ContainsKey(anime.AnimeID))
					{
						foreach (AniDB_Anime_Title title in allTitlesDict[anime.AnimeID])
						{
							Contract_AnimeTitle ctitle = new Contract_AnimeTitle();
							ctitle.AnimeID = title.AnimeID;
							ctitle.Language = title.Language;
							ctitle.Title = title.Title;
							ctitle.TitleType = title.TitleType;
							contract.AnimeTitles.Add(ctitle);
							countElements++;
						}
					}
					
					
					if (allAnimeCatgeoriesDict.ContainsKey(anime.AnimeID))
					{
						List<AniDB_Anime_Category> aniCats = allAnimeCatgeoriesDict[anime.AnimeID];
						foreach (AniDB_Anime_Category aniCat in aniCats)
						{
							if (allCatgeoriesDict.ContainsKey(aniCat.CategoryID))
							{
								AniDB_Category cat = allCatgeoriesDict[aniCat.CategoryID];

								Contract_AnimeCategory ccat = new Contract_AnimeCategory();
								ccat.CategoryDescription = cat.CategoryDescription;
								ccat.CategoryID = cat.CategoryID;
								ccat.CategoryName = cat.CategoryName;
								ccat.IsHentai = cat.IsHentai;
								ccat.ParentID = cat.ParentID;
								ccat.Weighting = aniCat.Weighting;
								contract.Categories.Add(ccat);
								countElements++;
							}
						}
					}
					
					if (allAnimeTagsDict.ContainsKey(anime.AnimeID))
					{
						List<AniDB_Anime_Tag> aniTags = allAnimeTagsDict[anime.AnimeID];
						foreach (AniDB_Anime_Tag aniTag in aniTags)
						{
							if (allTagsDict.ContainsKey(aniTag.TagID))
							{
								AniDB_Tag tag = allTagsDict[aniTag.TagID];

								Contract_AnimeTag ctag = new Contract_AnimeTag();
								ctag.Approval = aniTag.Approval;
								ctag.GlobalSpoiler = tag.GlobalSpoiler;
								ctag.LocalSpoiler = tag.LocalSpoiler;
								ctag.Spoiler = tag.Spoiler;
								ctag.TagCount = tag.TagCount;
								ctag.TagDescription = tag.TagDescription;
								ctag.TagID = tag.TagID;
								ctag.TagName = tag.TagName;
								contract.Tags.Add(ctag);
								countElements++;
							}
						}
					}

					// get user vote
					foreach (AniDB_Vote vote in allVotes)
					{
						if (vote.EntityID == anime.AnimeID && (vote.VoteType == (int)AniDBVoteType.Anime || vote.VoteType == (int)AniDBVoteType.AnimeTemp))
						{
							contract.UserVote = vote.ToContract();
							break;
						}
					}
					
					contracts.Add(contract);

				}

				
				TimeSpan ts = DateTime.Now - start;
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
			DateTime start = DateTime.Now;
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
			AniDB_Anime_CategoryRepository repAnimeCats = new AniDB_Anime_CategoryRepository();
			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			AniDB_TagRepository repTags = new AniDB_TagRepository();
			AniDB_Anime_TagRepository repAnimeTags = new AniDB_Anime_TagRepository();

			// get all the series
			List<Contract_AnimeSeries> seriesContractList = new List<Contract_AnimeSeries>();

			try
			{

				List<AnimeSeries> series = repSeries.GetAll();

				List<AniDB_Anime> animes = repAnime.GetAll();
				Dictionary<int, AniDB_Anime> dictAnimes = new Dictionary<int, AniDB_Anime>();
				foreach (AniDB_Anime anime in animes)
					dictAnimes[anime.AnimeID] = anime;

				// tvdb
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				List<CrossRef_AniDB_TvDB> allCrossRefs = repCrossRef.GetAll();
				Dictionary<int, CrossRef_AniDB_TvDB> dictCrossRefs = new Dictionary<int, CrossRef_AniDB_TvDB>();
				foreach (CrossRef_AniDB_TvDB xref in allCrossRefs)
					dictCrossRefs[xref.AnimeID] = xref;


				// moviedb
				CrossRef_AniDB_OtherRepository repOtherCrossRef = new CrossRef_AniDB_OtherRepository();
				List<CrossRef_AniDB_Other> allOtherCrossRefs = repOtherCrossRef.GetAll();
				Dictionary<int, CrossRef_AniDB_Other> dictMovieCrossRefs = new Dictionary<int, CrossRef_AniDB_Other>();
				foreach (CrossRef_AniDB_Other xref in allOtherCrossRefs)
				{
					if (xref.CrossRefType == (int)CrossRefType.MovieDB)
						dictMovieCrossRefs[xref.AnimeID] = xref;
				}

				// user records
				AnimeSeries_UserRepository repSeriesUser = new AnimeSeries_UserRepository();
				List<AnimeSeries_User> userRecordList = repSeriesUser.GetByUserID(userID);
				Dictionary<int, AnimeSeries_User> dictUserRecords = new Dictionary<int, AnimeSeries_User>();
				foreach (AnimeSeries_User serUser in userRecordList)
					dictUserRecords[serUser.AnimeSeriesID] = serUser;

				foreach (AnimeSeries aser in series)
				{
					if (!dictAnimes.ContainsKey(aser.AniDB_ID)) continue;

					CrossRef_AniDB_TvDB xref = null;
					if (dictCrossRefs.ContainsKey(aser.AniDB_ID)) xref = dictCrossRefs[aser.AniDB_ID];

					CrossRef_AniDB_Other xrefMovie = null;
					if (dictMovieCrossRefs.ContainsKey(aser.AniDB_ID)) xrefMovie = dictMovieCrossRefs[aser.AniDB_ID];

					AnimeSeries_User userRec = null;
					if (dictUserRecords.ContainsKey(aser.AnimeSeriesID))
						userRec = dictUserRecords[aser.AnimeSeriesID];

					seriesContractList.Add(aser.ToContract(dictAnimes[aser.AniDB_ID], xref, xrefMovie, userRec));
				}

				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAllSeries in {0} ms", ts.TotalMilliseconds);
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
				DateTime start = DateTime.Now;

				Contract_AniDB_AnimeDetailed contract = null;

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return null;

				contract = anime.ToContractDetailed();

				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAnimeDetailed  in {0} ms", ts.TotalMilliseconds);

				return contract;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public List<string> GetAllCategoryNames()
		{
			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			List<string> allCatNames = new List<string>();

			try
			{
				DateTime start = DateTime.Now;

				foreach (AniDB_Category cat in repCats.GetAll())
				{
					allCatNames.Add(cat.CategoryName);
				}
				allCatNames.Sort();


				TimeSpan ts = DateTime.Now - start;
				logger.Info("GetAllCategoryNames  in {0} ms", ts.TotalMilliseconds);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return allCatNames;
		}

		public List<Contract_AnimeGroup> GetSubGroupsForGroup(int animeGroupID, int userID)
		{
			List<Contract_AnimeGroup> retGroups = new List<Contract_AnimeGroup>();
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null) return retGroups;

				foreach (AnimeGroup grpChild in grp.ChildGroups)
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
			List<Contract_AnimeSeries> series = new List<Contract_AnimeSeries>();
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null) return series;

				foreach (AnimeSeries ser in grp.Series)
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
			List<Contract_AnimeSeries> series = new List<Contract_AnimeSeries>();
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null) return series;

				foreach (AnimeSeries ser in grp.AllSeries)
					series.Add(ser.ToContract(ser.GetUserRecord(userID)));

				return series;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return series;
			}
		}



		public List<Contract_AnimeEpisode> GetEpisodesForSeries(int animeSeriesID, int userID)
		{
			List<Contract_AnimeEpisode> eps = new List<Contract_AnimeEpisode>();
			try
			{
				

				DateTime start = DateTime.Now;
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode_UserRepository repEpUsers = new AnimeEpisode_UserRepository();
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
				logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.Anime.MainTitle, ts.TotalMilliseconds);


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
								foreach (VideoLocal vl in vids)
								{
									if (string.Equals(xref.Hash,vl.Hash, StringComparison.InvariantCultureIgnoreCase))
										epVids.Add(vl);
								}
							}
						}

						AnimeEpisode_User epuser = null;
						if (dictUserRecords.ContainsKey(ep.AnimeEpisodeID))
							epuser = dictUserRecords[ep.AnimeEpisodeID];

						eps.Add(ep.ToContract(dictAniEps[ep.AniDB_EpisodeID], epVids, epuser));
					}
				}

				ts = DateTime.Now - start;
				logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.Anime.MainTitle, ts.TotalMilliseconds);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return eps;
		}

		public List<Contract_AnimeEpisode> GetEpisodesForSeriesOld(int animeSeriesID)
		{
			List<Contract_AnimeEpisode> eps = new List<Contract_AnimeEpisode>();
			try
			{


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
				logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.Anime.MainTitle, ts.TotalMilliseconds);


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
				logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.Anime.MainTitle, ts.TotalMilliseconds);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return eps;
		}

		public Contract_AnimeSeries GetSeries(int animeSeriesID, int userID)
		{
			AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();

			try
			{
				AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
				if (series == null) return null;

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(series.AniDB_ID);
				if (anime == null) return null;

				return series.ToContract(anime, series.CrossRefTvDB, series.CrossRefMovieDB, series.GetUserRecord(userID));
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return null;
		}

		public Contract_AnimeSeries GetSeriesForAnime(int animeID, int userID)
		{
			AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();

			try
			{
				AnimeSeries series = repAnimeSer.GetByAnimeID(animeID);
				if (series == null) return null;

				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(series.AniDB_ID);
				if (anime == null) return null;

				return series.ToContract(anime, series.CrossRefTvDB, series.CrossRefMovieDB, series.GetUserRecord(userID));
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return null;
		}

		public bool GetSeriesExistingForAnime(int animeID)
		{
			AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();

			try
			{
				AnimeSeries series = repAnimeSer.GetByAnimeID(animeID);
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
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEps.GetByID(episodeID);
				if (ep != null)
					return ep.GetVideoDetailedContracts(userID);
				else
					return new List<Contract_VideoDetailed>();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return new List<Contract_VideoDetailed>();
		}

		public List<Contract_VideoLocal> GetIgnoredFiles(int userID)
		{
			List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetIgnoredVideos())
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
			List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetManuallyLinkedVideos())
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
			List<Contract_VideoLocal> contracts = new List<Contract_VideoLocal>();
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetVideosWithoutEpisode())
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
			Contract_ServerStatus contract = new Contract_ServerStatus();

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
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
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
						contract.ErrorMessage = "AniDB Client Port must be numeric and greater than 0" + Environment.NewLine;
					}
				}

				if (contractIn.AniDB_ServerPort != ServerSettings.AniDB_ServerPort)
				{
					anidbSettingsChanged = true;
					int sport = 0;
					int.TryParse(contractIn.AniDB_ServerPort, out sport);
					if (sport <= 0)
					{
						contract.ErrorMessage = "AniDB Server Port must be numeric and greater than 0" + Environment.NewLine;
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

				ServerSettings.AniDB_MyList_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.AniDB_MyList_UpdateFrequency;
				ServerSettings.AniDB_Calendar_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.AniDB_Calendar_UpdateFrequency;
				ServerSettings.AniDB_Anime_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.AniDB_Anime_UpdateFrequency;

				// Web Cache
				ServerSettings.WebCache_Address = contractIn.WebCache_Address;
				ServerSettings.WebCache_Anonymous = contractIn.WebCache_Anonymous;
				ServerSettings.WebCache_FileHashes_Get = contractIn.WebCache_FileHashes_Get;
				ServerSettings.WebCache_FileHashes_Send = contractIn.WebCache_FileHashes_Send;
				ServerSettings.WebCache_XRefFileEpisode_Get = contractIn.WebCache_XRefFileEpisode_Get;
				ServerSettings.WebCache_XRefFileEpisode_Send = contractIn.WebCache_XRefFileEpisode_Send;
				ServerSettings.WebCache_TvDB_Get = contractIn.WebCache_TvDB_Get;
				ServerSettings.WebCache_TvDB_Send = contractIn.WebCache_TvDB_Send;

				// TvDB
				ServerSettings.TvDB_AutoFanart = contractIn.TvDB_AutoFanart;
				ServerSettings.TvDB_AutoFanartAmount = contractIn.TvDB_AutoFanartAmount;
				ServerSettings.TvDB_AutoPosters = contractIn.TvDB_AutoPosters;
				ServerSettings.TvDB_AutoWideBanners = contractIn.TvDB_AutoWideBanners;
				ServerSettings.TvDB_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.TvDB_UpdateFrequency;

				// MovieDB
				ServerSettings.MovieDB_AutoFanart = contractIn.MovieDB_AutoFanart;
				ServerSettings.MovieDB_AutoFanartAmount = contractIn.MovieDB_AutoFanartAmount;
				ServerSettings.MovieDB_AutoPosters = contractIn.MovieDB_AutoPosters;

				// Import settings
				ServerSettings.VideoExtensions = contractIn.VideoExtensions;
				ServerSettings.Import_UseExistingFileWatchedStatus = contractIn.Import_UseExistingFileWatchedStatus;
				ServerSettings.WatchForNewFiles = contractIn.WatchForNewFiles;
				ServerSettings.AutoGroupSeries = contractIn.AutoGroupSeries;
				ServerSettings.RunImportOnStart = contractIn.RunImportOnStart;
				ServerSettings.Hash_CRC32 = contractIn.Hash_CRC32;
				ServerSettings.Hash_MD5 = contractIn.Hash_MD5;
				ServerSettings.Hash_SHA1 = contractIn.Hash_SHA1;

				// Language
				ServerSettings.LanguagePreference = contractIn.LanguagePreference;
				ServerSettings.LanguageUseSynonyms = contractIn.LanguageUseSynonyms;

				// Trakt
				ServerSettings.Trakt_Username = contractIn.Trakt_Username;
				ServerSettings.Trakt_Password = contractIn.Trakt_Password;
				ServerSettings.Trakt_UpdateFrequency = (ScheduledUpdateFrequency)contractIn.Trakt_UpdateFrequency;
				ServerSettings.Trakt_SyncFrequency = (ScheduledUpdateFrequency)contractIn.Trakt_SyncFrequency;

				if (anidbSettingsChanged)
				{
					JMMService.AnidbProcessor.ForceLogout();
					JMMService.AnidbProcessor.CloseConnections();

					Thread.Sleep(1000);
					JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password, ServerSettings.AniDB_ServerAddress,
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
			Contract_ServerSettings contract = new Contract_ServerSettings();

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
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
				if (vid == null)
					return "Could not find video local record";

				vid.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, true, userID);

				return "";
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return ex.Message;
			}
		}

		public Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID, bool watchedStatus, int userID)
		{
			Contract_ToggleWatchedStatusOnEpisode_Response response = new Contract_ToggleWatchedStatusOnEpisode_Response();
			response.ErrorMessage = "";
			response.AnimeEpisode = null;

			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEps.GetByID(animeEpisodeID);
				if (ep == null)
				{
					response.ErrorMessage = "Could not find anime episode record";
					return response;
				}

				ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, userID);

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
		/// Set watched status on all normal episodes
		/// </summary>
		/// <param name="animeSeriesID"></param>
		/// <param name="watchedStatus"></param>
		/// <param name="maxEpisodeNumber">Use this to specify a max episode number to apply to</param>
		/// <returns></returns>
		public string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber, int episodeType, int userID)
		{
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				List<AnimeEpisode> eps = repEps.GetBySeriesID(animeSeriesID);

				AnimeSeries ser = null;
				foreach (AnimeEpisode ep in eps)
				{
					if (ep.EpisodeTypeEnum == (enEpisodeType)episodeType && ep.AniDB_Episode.EpisodeNumber <= maxEpisodeNumber)
					{
						ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, userID);
						logger.Info("Updating episode: {0} to {1}", ep.AniDB_Episode.EpisodeNumber, watchedStatus);
					}
					

					ser = ep.AnimeSeries;
				}

				// now update the stats
				if (ser != null)
					ser.UpdateStats(true, true, true);
				return "";
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return ex.Message;
			}
		}

		public Contract_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
		{
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
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
			List<Contract_AnimeEpisode> contracts = new List<Contract_AnimeEpisode>();
			try
			{


				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
				if (vid == null)
					return contracts;

				foreach (AnimeEpisode ep in vid.AnimeEpisodes)
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
				AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
				AniDB_Episode aniEp = repAniEps.GetByEpisodeID(aniDBEpisodeID);
				if (aniEp == null) return relGroups;
				if (aniEp.EpisodeTypeEnum != AniDBAPI.enEpisodeType.Episode) return relGroups;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries series = repSeries.GetByAnimeID(aniEp.AnimeID);
				if (series == null) return relGroups;

				// get a list of all the release groups the user is collecting
				Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
				foreach (AnimeEpisode ep in series.AnimeEpisodes)
				{
					List<VideoLocal> vids = ep.VideoLocals;
					foreach (VideoLocal vid in vids)
					{
						AniDB_File anifile = vid.AniDBFile;
						if (anifile != null)
						{
							if (!userReleaseGroups.ContainsKey(anifile.GroupID))
								userReleaseGroups[anifile.GroupID] = 0;

							userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
						}
					}
				}

				// get all the release groups for this series
				AniDB_GroupStatusRepository repGrpStatus = new AniDB_GroupStatusRepository();
				List<AniDB_GroupStatus> grpStatuses = repGrpStatus.GetByAnimeID(aniEp.AnimeID);
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
				logger.ErrorException(ex.ToString(), ex);
			}
			return relGroups;
		}

		public List<Contract_ImportFolder> GetImportFolders()
		{
			List<Contract_ImportFolder> ifolders = new List<Contract_ImportFolder>();
			try
			{
				ImportFolderRepository repNS = new ImportFolderRepository();
				foreach (ImportFolder ns in repNS.GetAll())
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
			Contract_ImportFolder_SaveResponse response = new Contract_ImportFolder_SaveResponse();
			response.ErrorMessage = "";
			response.ImportFolder = null;

			try
			{


				ImportFolderRepository repNS = new ImportFolderRepository();
				ImportFolder ns = null;
				if (contract.ImportFolderID.HasValue)
				{
					// update
					ns = repNS.GetByID(contract.ImportFolderID.Value);
					if (ns == null)
					{
						response.ErrorMessage = "Could not find Import Folder ID: " + contract.ImportFolderID.Value.ToString();
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
					ImportFolder nsTemp = repNS.GetByImportLocation(contract.ImportFolderLocation);
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
				List<ImportFolder> allFolders = repNS.GetAll();

				if (contract.IsDropDestination == 1)
				{
					foreach (ImportFolder imf in allFolders)
					{
						if (imf.IsDropDestination == 1 && (!contract.ImportFolderID.HasValue || (contract.ImportFolderID.Value != imf.ImportFolderID)))
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
			try
			{
				ImportFolderRepository repNS = new ImportFolderRepository();
				ImportFolder ns = null;

				ns = repNS.GetByID(importFolderID);
				if (ns == null) return "Could not find Import Folder ID: " + importFolderID;

				repNS.Delete(importFolderID);

				return "";
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return ex.Message;
			}
		}

		public void RunImport()
		{
			MainWindow.RunImport();
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

		public void RescanUnlinkedFiles()
		{
			try
			{
				// files which have been hashed, but don't have an associated episode
				VideoLocalRepository repVidLocals = new VideoLocalRepository();
				List<VideoLocal> filesWithoutEpisode = repVidLocals.GetVideosWithoutEpisode();

				foreach (VideoLocal vl in filesWithoutEpisode)
				{
					CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID);
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

		public void RehashFile(int videoLocalID)
		{
			VideoLocalRepository repVidLocals = new VideoLocalRepository();
			VideoLocal vl = repVidLocals.GetByID(videoLocalID);

			if (vl != null)
			{
				CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(vl.FullServerPath, true);
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
				JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password, ServerSettings.AniDB_ServerAddress,
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

		public string TestTraktLogin()
		{
			try
			{
				return TraktTVHelper.TestUserLogin();
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TestTraktLogin: " + ex.ToString(), ex);
				return ex.Message;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="animeID"></param>
		/// <param name="voteValue">Must be 1 or 2 (Anime or Anime Temp(</param>
		/// <param name="voteType"></param>
		public void VoteAnime(int animeID, decimal voteValue, int voteType)
		{
			// lets save to the database and assume it will work
			AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
			List<AniDB_Vote> dbVotes = repVotes.GetByEntity(animeID);
			AniDB_Vote thisVote = null;
			foreach (AniDB_Vote dbVote in dbVotes)
			{
				// we can only have anime permanent or anime temp but not both
				if (voteType == (int)enAniDBVoteType.Anime || voteType == (int)enAniDBVoteType.AnimeTemp)
				{
					if (dbVote.VoteType == (int)enAniDBVoteType.Anime || dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
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
				iVoteValue = (int)(voteValue * 100);
			else
				iVoteValue = (int)voteValue;


			thisVote.VoteValue = iVoteValue;
			repVotes.Save(thisVote);

			CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, voteType, voteValue);
			cmdVote.Save();
		}

		public void VoteAnimeRevoke(int animeID)
		{
			// lets save to the database and assume it will work
			AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
			List<AniDB_Vote> dbVotes = repVotes.GetByEntity(animeID);
			AniDB_Vote thisVote = null;
			foreach (AniDB_Vote dbVote in dbVotes)
			{
				// we can only have anime permanent or anime temp but not both
				if (dbVote.VoteType == (int)enAniDBVoteType.Anime || dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
				{
					thisVote = dbVote;
				}
			}

			if (thisVote == null) return;

			CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, thisVote.VoteType, -1);
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
				AdhocRepository rep = new AdhocRepository();
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
				AdhocRepository rep = new AdhocRepository();
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
				AdhocRepository rep = new AdhocRepository();
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
			List<Contract_DuplicateFile> dupFiles = new List<Contract_DuplicateFile>();
			try
			{
				DuplicateFileRepository repDupFiles = new DuplicateFileRepository();
				foreach (DuplicateFile df in repDupFiles.GetAll())
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
		/// Delete a duplicate file entry, and also one of the physical files
		/// </summary>
		/// <param name="duplicateFileID"></param>
		/// <param name="fileNumber">0 = Don't delete any physical files, 1 = Delete file 1, 2 = Deleet file 2</param>
		/// <returns></returns>
		public string DeleteDuplicateFile(int duplicateFileID, int fileNumber)
		{
			try
			{
				DuplicateFileRepository repDupFiles = new DuplicateFileRepository();
				DuplicateFile df = repDupFiles.GetByID(duplicateFileID);
				if (df == null) return "Database entry does not exist";

				if (fileNumber == 1 || fileNumber == 2)
				{
					string fileName = "";
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
		/// Delets the VideoLocal record and the associated physical file
		/// </summary>
		/// <param name="videoLocalID"></param>
		/// <returns></returns>
		public string DeleteVideoLocalAndFile(int videoLocalID)
		{
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vid = repVids.GetByID(videoLocalID);
				if (vid == null) return "Database entry does not exist";

				logger.Info("Deleting video local record and file: {0}", vid.FullServerPath);
				if (!File.Exists(vid.FullServerPath)) return "File could not be found";
				File.Delete(vid.FullServerPath);

				AnimeSeries ser = null;
				if (vid.AnimeEpisodes.Count > 0)
					ser = vid.AnimeEpisodes[0].AnimeSeries;

				CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
				cmdDel.Save();

				repVids.Delete(videoLocalID);

				if (ser != null)
				{
					ser.UpdateStats(true, true, true);
					StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
				}
				

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
			List<Contract_VideoLocal> manualFiles = new List<Contract_VideoLocal>();
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetManuallyLinkedVideos())
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

		public List<Contract_AnimeEpisode> GetAllEpisodesWithMultipleFiles(int userID)
		{
			List<Contract_AnimeEpisode> eps = new List<Contract_AnimeEpisode>();
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();

				foreach (AnimeEpisode ep in repEps.GetEpisodesWithMultipleFiles())
					eps.Add(ep.ToContract(true, userID));

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
				DuplicateFileRepository repDupFiles = new DuplicateFileRepository();
				foreach (DuplicateFile df in repDupFiles.GetAll())
				{
					// check if both files still exist
					if (!File.Exists(df.FullServerPath1) || !File.Exists(df.FullServerPath2))
					{
						string msg = string.Format("Deleting duplicate file record as one of the files can't be found: {0} --- {1}", df.FullServerPath1, df.FullServerPath2);
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

		public List<Contract_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName, string resolution, string videoSource, int userID)
		{
			List<Contract_VideoDetailed> vids = new List<Contract_VideoDetailed>();

			List<Contract_GroupVideoQuality> vidQuals = new List<Contract_GroupVideoQuality>();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			VideoLocalRepository repVids = new VideoLocalRepository();
			AniDB_FileRepository repAniFile = new AniDB_FileRepository();

			try
			{
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return vids;

				foreach (VideoLocal vid in repVids.GetByAniDBAnimeID(animeID))
				{
					List<AnimeEpisode> eps = vid.AnimeEpisodes;
					if (eps.Count == 0) continue;
					AnimeEpisode animeEp = eps[0];
					if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode || animeEp.EpisodeTypeEnum == enEpisodeType.Special)
					{
						// get the anibd file info
						AniDB_File aniFile = vid.AniDBFile;
						if (aniFile != null)
						{
							// match based on group / video sorce / video res
							if (relGroupName.Equals(aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase) &&
								videoSource.Equals(aniFile.File_Source, StringComparison.InvariantCultureIgnoreCase) &&
								resolution.Equals(aniFile.File_VideoResolution, StringComparison.InvariantCultureIgnoreCase))
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
			List<Contract_GroupVideoQuality> vidQuals = new List<Contract_GroupVideoQuality>();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			VideoLocalRepository repVids = new VideoLocalRepository();
			AniDB_FileRepository repAniFile = new AniDB_FileRepository();

			try
			{
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return vidQuals;

				foreach (VideoLocal vid in repVids.GetByAniDBAnimeID(animeID))
				{
					List<AnimeEpisode> eps = vid.AnimeEpisodes;
					if (eps.Count == 0) continue;
					AnimeEpisode animeEp = eps[0];
					if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode || animeEp.EpisodeTypeEnum == enEpisodeType.Special)
					{
						// get the anibd file info
						AniDB_File aniFile = vid.AniDBFile;
						if (aniFile != null)
						{
							// match based on group / video sorce / video res
							bool foundSummaryRecord = false;
							foreach (Contract_GroupVideoQuality contract in vidQuals)
							{
								if (contract.GroupName.Equals(aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase) &&
									contract.VideoSource.Equals(aniFile.File_Source, StringComparison.InvariantCultureIgnoreCase) &&
									contract.Resolution.Equals(aniFile.File_VideoResolution, StringComparison.InvariantCultureIgnoreCase))
								{
									foundSummaryRecord = true;

									if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) contract.FileCountNormal++;
									if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) contract.FileCountSpecials++;
								}
							}
							if (!foundSummaryRecord)
							{
								Contract_GroupVideoQuality contract = new Contract_GroupVideoQuality();
								contract.FileCountNormal = 0;
								contract.FileCountSpecials = 0;
								if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) contract.FileCountNormal++;
								if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) contract.FileCountSpecials++;
								contract.GroupName = aniFile.Anime_GroupName;
								contract.GroupNameShort = aniFile.Anime_GroupNameShort;
								contract.Resolution = aniFile.File_VideoResolution;
								contract.VideoSource = aniFile.File_Source;
								contract.Ranking = Utils.GetOverallVideoSourceRanking(contract.Resolution, contract.VideoSource);
								vidQuals.Add(contract);
							}
						}
						else
						{
							// look at the Video Info record
							VideoInfo vinfo = vid.VideoInfo;
							if (vinfo != null)
							{
								bool foundSummaryRecord = false;
								foreach (Contract_GroupVideoQuality contract in vidQuals)
								{
									if (contract.GroupName.Equals("NO GROUP INFO", StringComparison.InvariantCultureIgnoreCase) &&
										contract.VideoSource.Equals("NO SOURCE INFO", StringComparison.InvariantCultureIgnoreCase) &&
										contract.Resolution.Equals(vinfo.VideoResolution, StringComparison.InvariantCultureIgnoreCase))
									{
										foundSummaryRecord = true;
										if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) contract.FileCountNormal++;
										if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) contract.FileCountSpecials++;
									}
								}
								if (!foundSummaryRecord)
								{
									Contract_GroupVideoQuality contract = new Contract_GroupVideoQuality();
									contract.FileCountNormal = 0;
									contract.FileCountSpecials = 0;
									if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) contract.FileCountNormal++;
									if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) contract.FileCountSpecials++;
									contract.GroupName = "NO GROUP INFO";
									contract.GroupNameShort = "NO GROUP INFO";
									contract.Resolution = vinfo.VideoResolution;
									contract.VideoSource = "NO SOURCE INFO";
									contract.Ranking = Utils.GetOverallVideoSourceRanking(contract.Resolution, contract.VideoSource);
								}
							}
						}
					}
				}

				foreach (Contract_GroupVideoQuality contract in vidQuals)
				{
					contract.NormalComplete = contract.FileCountNormal >= anime.EpisodeCountNormal;
					contract.SpecialsComplete = (contract.FileCountSpecials >= anime.EpisodeCountSpecial) && (anime.EpisodeCountSpecial > 0);
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
			Contract_AniDB_AnimeCrossRefs result = new Contract_AniDB_AnimeCrossRefs();
			result.AnimeID = animeID;

			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return result;

				// TvDB
				CrossRef_AniDB_TvDB xref = anime.CrossRefTvDB;
				if (xref == null) 
					result.CrossRef_AniDB_TvDB = null;
				else
					result.CrossRef_AniDB_TvDB = xref.ToContract();

				TvDB_Series tvseries = anime.TvDBSeries;
				if (tvseries == null) 
					result.TvDBSeries = null;
				else
					result.TvDBSeries = tvseries.ToContract();

				foreach (TvDB_Episode ep in anime.TvDBEpisodes)
					result.TvDBEpisodes.Add(ep.ToContract());

				foreach (TvDB_ImageFanart fanart in anime.TvDBImageFanarts)
					result.TvDBImageFanarts.Add(fanart.ToContract());

				foreach (TvDB_ImagePoster poster in anime.TvDBImagePosters)
					result.TvDBImagePosters.Add(poster.ToContract());

				foreach (TvDB_ImageWideBanner banner in anime.TvDBImageWideBanners)
					result.TvDBImageWideBanners.Add(banner.ToContract());

				// Trakt
				CrossRef_AniDB_Trakt xrefTrakt = anime.CrossRefTrakt;
				if (xrefTrakt == null)
					result.CrossRef_AniDB_Trakt = null;
				else
					result.CrossRef_AniDB_Trakt = xrefTrakt.ToContract();

				Trakt_Show show = anime.TraktShow;
				if (show == null)
					result.TraktShow = null;
				else
					result.TraktShow = show.ToContract();

				Trakt_ImageFanart traktFanart = anime.TraktImageFanart;
				if (traktFanart == null)
					result.TraktImageFanart = null;
				else
					result.TraktImageFanart = traktFanart.ToContract();

				Trakt_ImagePoster traktPoster = anime.TraktImagePoster;
				if (traktPoster == null)
					result.TraktImagePoster = null;
				else
					result.TraktImagePoster = traktPoster.ToContract();


				// MovieDB
				CrossRef_AniDB_Other xrefMovie = anime.CrossRefMovieDB;
				if (xrefMovie == null)
					result.CrossRef_AniDB_MovieDB = null;
				else
					result.CrossRef_AniDB_MovieDB = xrefMovie.ToContract();


				MovieDB_Movie movie = anime.MovieDBMovie;
				if (movie == null)
					result.MovieDBMovie = null;
				else
					result.MovieDBMovie = movie.ToContract();

				foreach (MovieDB_Fanart fanart in anime.MovieDBFanarts)
				{
					if (fanart.ImageSize.Equals(Constants.MovieDBImageSize.Original, StringComparison.InvariantCultureIgnoreCase))
						result.MovieDBFanarts.Add(fanart.ToContract());
				}

				foreach (MovieDB_Poster poster in anime.MovieDBPosters)
				{
					if (poster.ImageSize.Equals(Constants.MovieDBImageSize.Original, StringComparison.InvariantCultureIgnoreCase))
						result.MovieDBPosters.Add(poster.ToContract());
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
				JMMImageType imgType = (JMMImageType)imageType;

				switch (imgType)
				{
					case JMMImageType.AniDB_Cover:

						AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
						AniDB_Anime anime = repAnime.GetByAnimeID(imageID);
						if (anime == null) return "Could not find anime";

						anime.ImageEnabled = enabled ? 1 : 0;
						repAnime.Save(anime);

						break;

					case JMMImageType.TvDB_Banner:

						TvDB_ImageWideBannerRepository repBanners = new TvDB_ImageWideBannerRepository();
						TvDB_ImageWideBanner banner = repBanners.GetByID(imageID);

						if (banner == null) return "Could not find image";

						banner.Enabled = enabled ? 1 : 0;
						repBanners.Save(banner);

						break;

					case JMMImageType.TvDB_Cover:

						TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();
						TvDB_ImagePoster poster = repPosters.GetByID(imageID);

						if (poster == null) return "Could not find image";

						poster.Enabled = enabled ? 1 : 0;
						repPosters.Save(poster);

						break;

					case JMMImageType.TvDB_FanArt:

						TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
						TvDB_ImageFanart fanart = repFanart.GetByID(imageID);

						if (fanart == null) return "Could not find image";

						fanart.Enabled = enabled ? 1 : 0;
						repFanart.Save(fanart);

						break;

					case JMMImageType.MovieDB_Poster:

						MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
						MovieDB_Poster moviePoster = repMoviePosters.GetByID(imageID);

						if (moviePoster == null) return "Could not find image";

						moviePoster.Enabled = enabled ? 1 : 0;
						repMoviePosters.Save(moviePoster);

						break;

					case JMMImageType.MovieDB_FanArt:

						MovieDB_FanartRepository repMovieFanart = new MovieDB_FanartRepository();
						MovieDB_Fanart movieFanart = repMovieFanart.GetByID(imageID);

						if (movieFanart == null) return "Could not find image";

						movieFanart.Enabled = enabled ? 1 : 0;
						repMovieFanart.Save(movieFanart);

						break;

					case JMMImageType.Trakt_Poster:

						Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
						Trakt_ImagePoster traktPoster = repTraktPosters.GetByID(imageID);

						if (traktPoster == null) return "Could not find image";

						traktPoster.Enabled = enabled ? 1 : 0;
						repTraktPosters.Save(traktPoster);

						break;

					case JMMImageType.Trakt_Fanart:

						Trakt_ImageFanartRepository repTraktFanart = new Trakt_ImageFanartRepository();
						Trakt_ImageFanart traktFanart = repTraktFanart.GetByID(imageID);

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
				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();

				JMMImageType imgType = (JMMImageType)imageType;
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

					AniDB_Anime_DefaultImage img = repDefaults.GetByAnimeIDAndImagezSizeType(animeID, (int)sizeType);
					if (img != null)
						repDefaults.Delete(img.AniDB_Anime_DefaultImageID);
				}
				else
				{
					// making the image the default for it's type (poster, fanart etc)
					AniDB_Anime_DefaultImage img = repDefaults.GetByAnimeIDAndImagezSizeType(animeID, (int)sizeType);
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

		#region TvDB

		public Contract_CrossRef_AniDB_TvDBResult GetTVDBCrossRefWebCache(int animeID)
		{
			try
			{
				CrossRef_AniDB_TvDBResult result = XMLService.Get_CrossRef_AniDB_TvDB(animeID);
				if (result == null) return null;

				return result.ToContract();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public Contract_CrossRef_AniDB_TvDB GetTVDBCrossRef(int animeID)
		{
			try
			{
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				CrossRef_AniDB_TvDB xref = repCrossRef.GetByAnimeID(animeID);
				if (xref == null) return null;

				return xref.ToContract();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
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
				logger.ErrorException(ex.ToString(), ex);
				return results;
			}
		}

		public List<int> GetSeasonNumbersForSeries(int seriesID)
		{
			List<int> seasonNumbers = new List<int>();
			try
			{
				TvDB_EpisodeRepository repEps = new TvDB_EpisodeRepository();
				seasonNumbers = repEps.GetSeasonNumbersForSeries(seriesID);

				// if no results try refreshing from TvDB
				if (seasonNumbers.Count == 0)
					JMMService.TvdbHelper.UpdateAllInfoAndImages(seriesID, true);

				seasonNumbers = repEps.GetSeasonNumbersForSeries(seriesID);

				return seasonNumbers;
				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return seasonNumbers;
			}
		}

		public string LinkAniDBTvDB(int animeID, int tvDBID, int seasonNumber)
		{
			try
			{
				TvDBHelper.LinkAniDBTvDB(animeID, tvDBID, seasonNumber, false);

				return "";
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return ex.Message;
			}
		}

		public string RemoveLinkAniDBTvDB(int animeID)
		{
			try
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = repSeries.GetByAnimeID(animeID);

				if (ser == null) return "Could not find Series for Anime!";

				// check if there are default images used associated
				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
				List<AniDB_Anime_DefaultImage> images = repDefaults.GetByAnimeID(animeID);
				foreach (AniDB_Anime_DefaultImage image in images)
				{
					if (image.ImageParentType == (int)JMMImageType.TvDB_Banner ||
						image.ImageParentType == (int)JMMImageType.TvDB_Cover ||
						image.ImageParentType == (int)JMMImageType.TvDB_FanArt)
					{
						repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
					}
				}

				TvDBHelper.RemoveLinkAniDBTvDB(ser);

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
			List<Contract_TvDB_ImagePoster> allImages = new List<Contract_TvDB_ImagePoster>();
			try
			{
				TvDB_ImagePosterRepository repImages = new TvDB_ImagePosterRepository();
				List<TvDB_ImagePoster> allPosters = null;
				if (tvDBID.HasValue)
					allPosters = repImages.GetBySeriesID(tvDBID.Value);
				else
					allPosters = repImages.GetAll();

				foreach (TvDB_ImagePoster img in allPosters)
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
			List<Contract_TvDB_ImageWideBanner> allImages = new List<Contract_TvDB_ImageWideBanner>();
			try
			{
				TvDB_ImageWideBannerRepository repImages = new TvDB_ImageWideBannerRepository();
				List<TvDB_ImageWideBanner> allBanners = null;
				if (tvDBID.HasValue)
					allBanners = repImages.GetBySeriesID(tvDBID.Value);
				else
					allBanners = repImages.GetAll();

				foreach (TvDB_ImageWideBanner img in allBanners)
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
			List<Contract_TvDB_ImageFanart> allImages = new List<Contract_TvDB_ImageFanart>();
			try
			{
				TvDB_ImageFanartRepository repImages = new TvDB_ImageFanartRepository();
				List<TvDB_ImageFanart> allFanart = null;
				if (tvDBID.HasValue)
					allFanart = repImages.GetBySeriesID(tvDBID.Value);
				else
					allFanart = repImages.GetAll();

				foreach (TvDB_ImageFanart img in allFanart)
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
			List<Contract_TvDB_Episode> allImages = new List<Contract_TvDB_Episode>();
			try
			{
				TvDB_EpisodeRepository repImages = new TvDB_EpisodeRepository();
				List<TvDB_Episode> allEpisodes = null;
				if (tvDBID.HasValue)
					allEpisodes = repImages.GetBySeriesID(tvDBID.Value);
				else
					allEpisodes = repImages.GetAll();

				foreach (TvDB_Episode img in allEpisodes)
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
			List<Contract_Trakt_ImageFanart> allImages = new List<Contract_Trakt_ImageFanart>();
			try
			{
				Trakt_ImageFanartRepository repImages = new Trakt_ImageFanartRepository();
				List<Trakt_ImageFanart> allFanart = null;
				if (traktShowID.HasValue)
					allFanart = repImages.GetByShowID(traktShowID.Value);
				else
					allFanart = repImages.GetAll();

				foreach (Trakt_ImageFanart img in allFanart)
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
			List<Contract_Trakt_ImagePoster> allImages = new List<Contract_Trakt_ImagePoster>();
			try
			{
				Trakt_ImagePosterRepository repImages = new Trakt_ImagePosterRepository();
				List<Trakt_ImagePoster> allPosters = null;
				if (traktShowID.HasValue)
					allPosters = repImages.GetByShowID(traktShowID.Value);
				else
					allPosters = repImages.GetAll();

				foreach (Trakt_ImagePoster img in allPosters)
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
			List<Contract_Trakt_Episode> allImages = new List<Contract_Trakt_Episode>();
			try
			{
				Trakt_EpisodeRepository repImages = new Trakt_EpisodeRepository();
				List<Trakt_Episode> allEpisodes = null;
				if (traktShowID.HasValue)
					allEpisodes = repImages.GetByShowID(traktShowID.Value);
				else
					allEpisodes = repImages.GetAll();

				foreach (Trakt_Episode img in allEpisodes)
					allImages.Add(img.ToContract());

				return allImages;

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return allImages;
			}
		}

		public Contract_CrossRef_AniDB_TraktResult GetTraktCrossRefWebCache(int animeID)
		{
			try
			{
				CrossRef_AniDB_TraktResult result = XMLService.Get_CrossRef_AniDB_Trakt(animeID);
				if (result == null) return null;

				return result.ToContract();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public Contract_CrossRef_AniDB_Trakt GetTraktCrossRef(int animeID)
		{
			try
			{
				CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
				CrossRef_AniDB_Trakt xref = repCrossRef.GetByAnimeID(animeID);
				if (xref == null) return null;

				return xref.ToContract();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public List<Contract_TraktTVShowResponse> SearchTrakt(string criteria)
		{
			List<Contract_TraktTVShowResponse> results = new List<Contract_TraktTVShowResponse>();
			try
			{
				List<TraktTVShowResponse> traktResults = TraktTVHelper.SearchShow(criteria);

				foreach (TraktTVShowResponse res in traktResults)
					results.Add(res.ToContract());

				return results;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return results;
			}
		}

		public string LinkAniDBTrakt(int animeID, string traktID, int seasonNumber)
		{
			try
			{
				TraktTVHelper.LinkAniDBTrakt(animeID, traktID, seasonNumber, false);

				return "";
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return ex.Message;
			}
		}

		public string RemoveLinkAniDBTrakt(int animeID)
		{
			try
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = repSeries.GetByAnimeID(animeID);

				if (ser == null) return "Could not find Series for Anime!";

				// check if there are default images used associated
				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
				List<AniDB_Anime_DefaultImage> images = repDefaults.GetByAnimeID(animeID);
				foreach (AniDB_Anime_DefaultImage image in images)
				{
					if (image.ImageParentType == (int)JMMImageType.Trakt_Fanart ||
						image.ImageParentType == (int)JMMImageType.Trakt_Poster)
					{
						repDefaults.Delete(image.AniDB_Anime_DefaultImageID);
					}
				}

				TraktTVHelper.RemoveLinkAniDBTrakt(ser);

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
			List<int> seasonNumbers = new List<int>();
			try
			{
				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(traktID);
				if (show == null) return seasonNumbers;

				foreach (Trakt_Season season in show.Seasons)
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


		#region Other Cross Refs

		public Contract_CrossRef_AniDB_OtherResult GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
		{
			try
			{
				CrossRef_AniDB_OtherResult result = XMLService.Get_CrossRef_AniDB_Other(animeID, (CrossRefType)crossRefType);
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
				CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
				CrossRef_AniDB_Other xref = repCrossRef.GetByAnimeIDAndType(animeID, (CrossRefType)crossRefType);
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
				CrossRefType xrefType = (CrossRefType)crossRefType;

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
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);

				if (anime == null) return "Could not find Anime!";

				CrossRefType xrefType = (CrossRefType)crossRefType;
				switch (xrefType)
				{
					case CrossRefType.MovieDB:

						// check if there are default images used associated
						AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
						List<AniDB_Anime_DefaultImage> images = repDefaults.GetByAnimeID(animeID);
						foreach (AniDB_Anime_DefaultImage image in images)
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
				logger.ErrorException(ex.ToString(), ex);
				return results;
			}
		}

		public List<Contract_MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
		{
			List<Contract_MovieDB_Poster> allImages = new List<Contract_MovieDB_Poster>();
			try
			{
				MovieDB_PosterRepository repImages = new MovieDB_PosterRepository();
				List<MovieDB_Poster> allPosters = null;
				if (movieID.HasValue)
					allPosters = repImages.GetByMovieID(movieID.Value);
				else
					allPosters = repImages.GetAllOriginal();

				foreach (MovieDB_Poster img in allPosters)
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
			List<Contract_MovieDB_Fanart> allImages = new List<Contract_MovieDB_Fanart>();
			try
			{
				MovieDB_FanartRepository repImages = new MovieDB_FanartRepository();
				List<MovieDB_Fanart> allFanart = null;
				if (movieID.HasValue)
					allFanart = repImages.GetByMovieID(movieID.Value);
				else
					allFanart = repImages.GetAllOriginal();

				foreach (MovieDB_Fanart img in allFanart)
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

		public Contract_AnimeEpisode GetNextUnwatchedEpisode(int animeSeriesID, int userID)
		{
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();

				// get all the data first
				// we do this to reduce the amount of database calls, which makes it a lot faster
				AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
				if (series == null) return null;

				List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);

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
							Contract_AnimeEpisode epContract = ep.ToContract(anidbep, new List<VideoLocal>(), ep.GetUserRecord(userID));
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

				foreach (Contract_AnimeEpisode canEp in candidateEps)
				{
					// now refresh from the database to get file count
					AnimeEpisode epFresh = repEps.GetByID(canEp.AnimeEpisodeID);
					if (epFresh.VideoLocals.Count > 0)
						return epFresh.ToContract(true, userID);
				}
				
				return null;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public Contract_AnimeEpisode GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
		{
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();

				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp == null) return null;

				List<AnimeSeries> allSeries = grp.AllSeries;

				List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("AirDate", false, SortType.eDateTime));
				allSeries = Sorting.MultiSort<AnimeSeries>(allSeries, sortCriteria);

				foreach (AnimeSeries ser in allSeries)
				{
					Contract_AnimeEpisode contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
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
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
				AnimeSeries_UserRepository repSeriesUser = new AnimeSeries_UserRepository();
				JMMUserRepository repUsers = new JMMUserRepository();

				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return retEps;

				// get a list of series that is applicable
				List<AnimeSeries_User> allSeriesUser = repSeriesUser.GetMostRecentlyWatched(jmmuserID);
				foreach (AnimeSeries_User userRecord in allSeriesUser)
				{
					AnimeSeries series = repAnimeSer.GetByID(userRecord.AnimeSeriesID);
					if (series == null) continue;

					if (!user.AllowedSeries(series)) continue;

					Contract_AnimeEpisode ep = GetNextUnwatchedEpisode(userRecord.AnimeSeriesID, jmmuserID);
					if (ep != null)
					{
						retEps.Add(ep);

						// Lets only return the specified amount
						if (retEps.Count == maxRecords) return retEps;
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
		/// Delete a series, and everything underneath it (episodes, files)
		/// </summary>
		/// <param name="animeSeriesID"></param>
		/// <param name="deleteFiles">also delete the physical files</param>
		/// <returns></returns>
		public string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles)
		{
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
				VideoLocalRepository repVids = new VideoLocalRepository();
				AnimeGroupRepository repGroups = new AnimeGroupRepository();

				AnimeSeries ser = repAnimeSer.GetByID(animeSeriesID);
				if (ser == null) return "Series does not exist";

				int animeGroupID = ser.AnimeGroupID;

				foreach (AnimeEpisode ep in ser.AnimeEpisodes)
				{
					foreach (VideoLocal vid in ep.VideoLocals)
					{
						if (deleteFiles)
						{
							logger.Info("Deleting video local record and file: {0}", vid.FullServerPath);
							if (!File.Exists(vid.FullServerPath)) return "File could not be found";
							File.Delete(vid.FullServerPath);
						}
						CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(vid.Hash, vid.FileSize);
						cmdDel.Save();

						repVids.Delete(vid.VideoLocalID);
					}

					repEps.Delete(ep.AnimeEpisodeID);
				}
				repAnimeSer.Delete(ser.AnimeSeriesID);

				// finally update stats
				AnimeGroup grp = repGroups.GetByID(animeGroupID);
				if (grp != null)
				{
					grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
					StatsCache.Instance.UpdateUsingGroup(grp.TopLevelAnimeGroup.AnimeGroupID);
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
			DateTime start = DateTime.Now;
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			JMMUserRepository repUsers = new JMMUserRepository();

			

			// get all the series
			List<Contract_AnimeSeries> seriesContractList = new List<Contract_AnimeSeries>();

			try
			{
				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return seriesContractList;

				List<AnimeSeries> series = repSeries.GetWithMissingEpisodes();

				List<AniDB_Anime> animes = repAnime.GetAll();
				Dictionary<int, AniDB_Anime> dictAnimes = new Dictionary<int, AniDB_Anime>();
				foreach (AniDB_Anime anime in animes)
					dictAnimes[anime.AnimeID] = anime;

				// tvdb
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				List<CrossRef_AniDB_TvDB> allCrossRefs = repCrossRef.GetAll();
				Dictionary<int, CrossRef_AniDB_TvDB> dictCrossRefs = new Dictionary<int, CrossRef_AniDB_TvDB>();
				foreach (CrossRef_AniDB_TvDB xref in allCrossRefs)
					dictCrossRefs[xref.AnimeID] = xref;


				// moviedb
				CrossRef_AniDB_OtherRepository repOtherCrossRef = new CrossRef_AniDB_OtherRepository();
				List<CrossRef_AniDB_Other> allOtherCrossRefs = repOtherCrossRef.GetAll();
				Dictionary<int, CrossRef_AniDB_Other> dictMovieCrossRefs = new Dictionary<int, CrossRef_AniDB_Other>();
				foreach (CrossRef_AniDB_Other xref in allOtherCrossRefs)
				{
					if (xref.CrossRefType == (int)CrossRefType.MovieDB)
						dictMovieCrossRefs[xref.AnimeID] = xref;
				}

				// user records
				AnimeSeries_UserRepository repSeriesUser = new AnimeSeries_UserRepository();
				List<AnimeSeries_User> userRecordList = repSeriesUser.GetByUserID(jmmuserID);
				Dictionary<int, AnimeSeries_User> dictUserRecords = new Dictionary<int, AnimeSeries_User>();
				foreach (AnimeSeries_User serUser in userRecordList)
					dictUserRecords[serUser.AnimeSeriesID] = serUser;

				int i = 1;
				foreach (AnimeSeries aser in series)
				{
					if (!dictAnimes.ContainsKey(aser.AniDB_ID)) continue;

					AniDB_Anime anime = dictAnimes[aser.AniDB_ID];
					if (!user.AllowedAnime(anime)) continue;

					CrossRef_AniDB_TvDB xref = null;
					if (dictCrossRefs.ContainsKey(aser.AniDB_ID)) xref = dictCrossRefs[aser.AniDB_ID];

					CrossRef_AniDB_Other xrefMovie = null;
					if (dictMovieCrossRefs.ContainsKey(aser.AniDB_ID)) xrefMovie = dictMovieCrossRefs[aser.AniDB_ID];

					AnimeSeries_User userRec = null;
					if (dictUserRecords.ContainsKey(aser.AnimeSeriesID))
						userRec = dictUserRecords[aser.AnimeSeriesID];

					seriesContractList.Add(aser.ToContract(dictAnimes[aser.AniDB_ID], xref, xrefMovie, userRec));

					if (i == maxRecords) break;

					i++;
				}

				TimeSpan ts = DateTime.Now - start;
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
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			JMMUserRepository repUsers = new JMMUserRepository();

			// get all the series
			List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

			try
			{
				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return animeList;

				List<AniDB_Anime> animes = repAnime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays), DateTime.Today.AddDays(numberOfDays));
				foreach (AniDB_Anime anime in animes)
				{
					bool useAnime = true;

					string[] cats = user.HideCategories.ToLower().Split(',');
					string[] animeCats = anime.AllCategories.ToLower().Split('|');
					foreach (string cat in cats)
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
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			JMMUserRepository repUsers = new JMMUserRepository();

			// get all the series
			List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

			try
			{
				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return animeList;

				DateTime startDate = new DateTime(year, month, 1, 0, 0, 0);
				DateTime endDate = startDate.AddMonths(1);
				endDate = endDate.AddMinutes(-10);

				List<AniDB_Anime> animes = repAnime.GetForDate(startDate, endDate);
				foreach (AniDB_Anime anime in animes)
				{
					bool useAnime = true;

					string[] cats = user.HideCategories.ToLower().Split(',');
					string[] animeCats = anime.AllCategories.ToLower().Split('|');
					foreach (string cat in cats)
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
			JMMUserRepository repUsers = new JMMUserRepository();

			// get all the users
			List<Contract_JMMUser> userList = new List<Contract_JMMUser>();

			try
			{
				List<JMMUser> users = repUsers.GetAll();
				foreach (JMMUser user in users)
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
			JMMUserRepository repUsers = new JMMUserRepository();

			try
			{
				JMMUser user = repUsers.AuthenticateUser(username, password);
				if (user == null) return null;

				return user.ToContract();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public string SaveUser(Contract_JMMUser user)
		{
			JMMUserRepository repUsers = new JMMUserRepository();

			try
			{
				bool existingUser = false;
				bool updateStats = false;
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
				jmmUser.Password = user.Password;
				jmmUser.Username = user.Username;

				// make sure that at least one user is an admin
				if (jmmUser.IsAdmin == 0)
				{
					bool adminExists = false;
					List<JMMUser> users = repUsers.GetAll();
					foreach (JMMUser userOld in users)
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
					AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
					foreach (AnimeSeries ser in repSeries.GetAll())
						ser.UpdateStats(true, false, true);
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
			JMMUserRepository repUsers = new JMMUserRepository();

			try
			{
				JMMUser jmmUser = repUsers.GetByID(userID);
				if (jmmUser == null) return "User not found";

				// make sure that at least one user is an admin
				if (jmmUser.IsAdmin == 1)
				{
					bool adminExists = false;
					List<JMMUser> users = repUsers.GetAll();
					foreach (JMMUser userOld in users)
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
				AnimeSeries_UserRepository repSeries = new AnimeSeries_UserRepository();
				foreach (AnimeSeries_User ser in repSeries.GetByUserID(userID))
					repSeries.Delete(ser.AnimeSeries_UserID);

				AnimeGroup_UserRepository repGroup = new AnimeGroup_UserRepository();
				foreach (AnimeGroup_User grp in repGroup.GetByUserID(userID))
					repGroup.Delete(grp.AnimeGroup_UserID);

				AnimeEpisode_UserRepository repEpisode = new AnimeEpisode_UserRepository();
				foreach (AnimeEpisode_User ep in repEpisode.GetByUserID(userID))
					repEpisode.Delete(ep.AnimeEpisode_UserID);

				VideoLocal_UserRepository repVids = new VideoLocal_UserRepository();
				foreach (VideoLocal_User vid in repVids.GetByUserID(userID))
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
			List<Contract_AniDB_Anime_Similar> links = new List<Contract_AniDB_Anime_Similar>();
			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return links;

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser juser = repUsers.GetByID(userID);
				if (juser == null) return links;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

				foreach (AniDB_Anime_Similar link in anime.SimilarAnime)
				{
					AniDB_Anime animeLink = repAnime.GetByAnimeID(link.SimilarAnimeID);
					if (animeLink != null)
					{
						if (!juser.AllowedAnime(animeLink)) continue;
					}

					// check if this anime has a series
					AnimeSeries ser = repSeries.GetByAnimeID(link.SimilarAnimeID);

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
			List<Contract_AniDB_Anime_Relation> links = new List<Contract_AniDB_Anime_Relation>();
			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return links;

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser juser = repUsers.GetByID(userID);
				if (juser == null) return links;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

				foreach (AniDB_Anime_Relation link in anime.RelatedAnime)
				{
					AniDB_Anime animeLink = repAnime.GetByAnimeID(link.RelatedAnimeID);
					if (animeLink != null)
					{
						if (!juser.AllowedAnime(animeLink)) continue;
					}

					// check if this anime has a series
					AnimeSeries ser = repSeries.GetByAnimeID(link.RelatedAnimeID);

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
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser juser = repUsers.GetByID(userID);
				if (juser == null) return recs;

				// find all the series which the user has rated
				List<AniDB_Vote> allVotes = repVotes.GetAll();
				if (allVotes.Count == 0) return recs;

				// sort by the highest rated
				List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("VoteValue", true, SortType.eInteger));
				allVotes = Sorting.MultiSort<AniDB_Vote>(allVotes, sortCriteria);

				Dictionary<int, Contract_Recommendation> dictRecs = new Dictionary<int, Contract_Recommendation>();

				List<AniDB_Vote> animeVotes = new List<AniDB_Vote>();
				foreach (AniDB_Vote vote in allVotes)
				{
					if (vote.VoteType != (int)enAniDBVoteType.Anime && vote.VoteType != (int)enAniDBVoteType.AnimeTemp) continue;

					// check if the user has this anime
					AniDB_Anime anime = repAnime.GetByAnimeID(vote.EntityID);
					if (anime == null) continue;

					// get similar anime
					List<AniDB_Anime_Similar> simAnime = anime.SimilarAnime;
					// sort by the highest approval
					sortCriteria = new List<SortPropOrFieldAndDirection>();
					sortCriteria.Add(new SortPropOrFieldAndDirection("ApprovalPercentage", true, SortType.eDoubleOrFloat));
					simAnime = Sorting.MultiSort<AniDB_Anime_Similar>(simAnime, sortCriteria);

					foreach (AniDB_Anime_Similar link in simAnime)
					{
						AniDB_Anime animeLink = repAnime.GetByAnimeID(link.SimilarAnimeID);
						if (animeLink != null)
							if (!juser.AllowedAnime(animeLink)) continue;

						// don't recommend to watch anime that the user doesn't have
						if (animeLink == null && recommendationType == 1) continue;

						// don't recommend to watch series that the user doesn't have
						AnimeSeries ser = repSeries.GetByAnimeID(link.SimilarAnimeID);
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
							rec.Recommended_AniDB_Anime = animeLink.ToContract();

						rec.BasedOn_AniDB_Anime = anime.ToContract();

						rec.Recommended_AnimeSeries = null;
						if (ser != null)
							rec.Recommended_AnimeSeries = ser.ToContract(ser.GetUserRecord(userID));

						AnimeSeries serBasedOn = repSeries.GetByAnimeID(anime.AnimeID);
						if (serBasedOn == null) continue;

						rec.BasedOn_AnimeSeries = serBasedOn.ToContract(serBasedOn.GetUserRecord(userID));

						dictRecs[rec.RecommendedAnimeID] = rec;
					}
				}

				List<Contract_Recommendation> tempRecs = new List<Contract_Recommendation>();
				foreach (Contract_Recommendation rec in dictRecs.Values)
					tempRecs.Add(rec);

				// sort by the highest score
				sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("Score", true, SortType.eDoubleOrFloat));
				tempRecs = Sorting.MultiSort<Contract_Recommendation>(tempRecs, sortCriteria);

				int numRecs = 0;
				foreach (Contract_Recommendation rec in tempRecs)
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

		private double CalculateRecommendationScore(int userVoteValue, double approvalPercentage, decimal animeRating)
		{
			double score = (double)userVoteValue;

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
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries series = repSeries.GetByAnimeID(animeID);
				if (series == null) return relGroups;

				// get a list of all the release groups the user is collecting
				//List<int> userReleaseGroups = new List<int>();
				Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
				foreach (AnimeEpisode ep in series.AnimeEpisodes)
				{
					List<VideoLocal> vids = ep.VideoLocals;
					foreach (VideoLocal vid in vids)
					{
						AniDB_File anifile = vid.AniDBFile;
						if (anifile != null)
						{
							if (!userReleaseGroups.ContainsKey(anifile.GroupID))
								userReleaseGroups[anifile.GroupID] = 0;

							userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
						}
					}
				}

				// get all the release groups for this series
				AniDB_GroupStatusRepository repGrpStatus = new AniDB_GroupStatusRepository();
				List<AniDB_GroupStatus> grpStatuses = repGrpStatus.GetByAnimeID(animeID);
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
				logger.ErrorException(ex.ToString(), ex);
			}
			return relGroups;
		}

		public List<Contract_AniDB_Character> GetCharactersForAnime(int animeID)
		{
			List<Contract_AniDB_Character> chars = new List<Contract_AniDB_Character>();

			try
			{
				AniDB_Anime_CharacterRepository repAnimeChar = new AniDB_Anime_CharacterRepository();
				AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();

				List<AniDB_Anime_Character> animeChars = repAnimeChar.GetByAnimeID(animeID);
				if (animeChars == null || animeChars.Count == 0) return chars;

				foreach (AniDB_Anime_Character animeChar in animeChars)
				{
					AniDB_Character chr = repChar.GetByCharID(animeChar.CharID);
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
	}

	
}
