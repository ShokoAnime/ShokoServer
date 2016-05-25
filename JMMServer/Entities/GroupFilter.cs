using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
	public class GroupFilter
	{
		public int GroupFilterID { get; set; }
		public string GroupFilterName { get; set; }
		public int ApplyToSeries { get; set; }
		public int BaseCondition { get; set; }
		public string SortingCriteria { get; set; }
		public int? Locked { get; set; }

        public int FilterType { get; set; }



        public int GroupsIdsVersion { get; set; }
        public string GroupsIdsString { get; set; }

        public const int GROUPFILTER_VERSION = 1;

	    private Contract_GroupFilter VirtualContract = null;

        internal Dictionary<int, HashSet<int>> _groupsId =new Dictionary<int, HashSet<int>>();

        public virtual Dictionary<int, HashSet<int>> GroupsIds
        {
            get
            {
                if (_groupsId.Count == 0 && GroupsIdsVersion == GROUPFILTER_VERSION)
                {
                    Dictionary<int, List<int>> vals =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(GroupsIdsString);
                    if (vals != null)
                        _groupsId = vals.ToDictionary(a => a.Key, a => new HashSet<int>(a.Value));
                }
                return _groupsId;
            }
            set
            {
                _groupsId = value;
            }
        }


        public override string ToString()
		{
			return string.Format("{0} - {1}", GroupFilterID, GroupFilterName);
		}

		public List<GroupFilterCondition> FilterConditions
		{
			get
			{
			    if (VirtualContract != null)
			        return VirtualContract.FilterConditions.Select(a => new GroupFilterCondition { ConditionOperator = a.ConditionOperator, ConditionType = a.ConditionType,ConditionParameter = a.ConditionParameter,GroupFilterID = a.GroupFilterID ?? 0}).ToList();
            	GroupFilterConditionRepository repConds = new GroupFilterConditionRepository();
				return repConds.GetByGroupFilterID(this.GroupFilterID);
			}
		}

		public List<GroupFilterSortingCriteria> SortCriteriaList
		{
			get
			{
				List<GroupFilterSortingCriteria> sortCriteriaList = new List<GroupFilterSortingCriteria>();

				if (!string.IsNullOrEmpty(SortingCriteria))
				{
					string[] scrit = SortingCriteria.Split('|');
					foreach (string sortpair in scrit)
					{
						string[] spair = sortpair.Split(';');
						if (spair.Length != 2) continue;

						int stype = 0;
						int sdir = 0;

						int.TryParse(spair[0], out stype);
						int.TryParse(spair[1], out sdir);

						if (stype > 0 && sdir > 0)
						{
							GroupFilterSortingCriteria gfsc = new GroupFilterSortingCriteria();
							gfsc.GroupFilterID = this.GroupFilterID;
							gfsc.SortType = (GroupFilterSorting)stype;
							gfsc.SortDirection = (GroupFilterSortDirection)sdir;
							sortCriteriaList.Add(gfsc);
						}
					}
				}

				return sortCriteriaList;
			}
		}

		public List<GroupFilterCondition> GetFilterConditions(ISession session)
		{
			GroupFilterConditionRepository repConds = new GroupFilterConditionRepository();
			return repConds.GetByGroupFilterID(session, this.GroupFilterID);
		}

		public Contract_GroupFilter ToContract()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return ToContract(session);
			}
		}

		public Contract_GroupFilter ToContract(ISession session)
		{
			Contract_GroupFilter contract = new Contract_GroupFilter();
			contract.GroupFilterID = this.GroupFilterID;
			contract.GroupFilterName = this.GroupFilterName;
			contract.ApplyToSeries = this.ApplyToSeries;
			contract.BaseCondition = this.BaseCondition;
			contract.SortingCriteria = this.SortingCriteria;
			contract.Locked = this.Locked;
            contract.FilterType = this.FilterType;

            contract.FilterConditions = new List<Contract_GroupFilterCondition>();
			foreach (GroupFilterCondition gfc in GetFilterConditions(session))
				contract.FilterConditions.Add(gfc.ToContract());
		    contract.Groups = this.GroupsIds.ToDictionary(a => a.Key, a => new HashSet<int>(a.Value.ToList()));
			return contract;
		}

		public Contract_GroupFilterExtended ToContractExtended(JMMUser user)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return ToContractExtended(session, user);
			}
		}

		public Contract_GroupFilterExtended ToContractExtended(ISession session, JMMUser user)
		{
			Contract_GroupFilterExtended contract = new Contract_GroupFilterExtended();
			contract.GroupFilter = this.ToContract();
			contract.GroupCount = 0;
			contract.SeriesCount = 0;

            
		    if (GroupsIds.ContainsKey(user.JMMUserID))
		    {
		        contract.GroupCount = GroupsIds[user.JMMUserID].Count;
		    }
/*
			// find all the groups for thise group filter
			AnimeGroupRepository repGroups = new AnimeGroupRepository();
			List<AnimeGroup> allGrps = repGroups.GetAll(session);

			if ((StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID)) && (StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(this.GroupFilterID)))
			{
				HashSet<int> groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][GroupFilterID];
				foreach (AnimeGroup grp in allGrps)
				{
					if (groups.Contains(grp.AnimeGroupID))
						contract.GroupCount++;
				}
			}*/
			return contract;
		}
        public void UpdateGroupFilterUser(JMMUser ruser)
        {
            AnimeGroupRepository repGroups = new AnimeGroupRepository();
            AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();
            JMMUserRepository repUser = new JMMUserRepository();
            GroupFilterRepository repGrpFilter = new GroupFilterRepository();
            List<JMMUser> users = new List<JMMUser>();
            if (ruser != null)
                users.Add(ruser);
            else
                users = repUser.GetAll();
            bool change = false;
            foreach (JMMUser user in users)
            {
                List<AnimeGroup> allGrps = repGroups.GetAllTopLevelGroups(); // No Need of subgroups

                foreach (AnimeGroup grp in allGrps)
                {
                    AnimeGroup_User userRec = repUserGroups.GetByUserAndGroupID(user.JMMUserID, grp.AnimeGroupID);
                    if (EvaluateGroupFilter(grp, user, userRec))
                    {
                        if (!GroupsIds.ContainsKey(user.JMMUserID))
                        {
                            GroupsIds[user.JMMUserID] = new HashSet<int>();
                        }
                        if (!GroupsIds[user.JMMUserID].Contains(grp.AnimeGroupID))
                        {
                            GroupsIds[user.JMMUserID].Add(grp.AnimeGroupID);
                            change = true;
                        }
                    }
                    else
                    {
                        if (GroupsIds.ContainsKey(user.JMMUserID))
                        {
                            if (GroupsIds[user.JMMUserID].Contains(grp.AnimeGroupID))
                            {
                                GroupsIds[user.JMMUserID].Remove(grp.AnimeGroupID);
                                change = true;
                            }
                        }
                    }
                }
            }
            if (change)
                repGrpFilter.Save(this, true, null);
        }

	    public static Contract_GroupFilter EvaluateVirtualContract(Contract_GroupFilter gfc)
	    {
            //Convert Contract_GroupFilter into a Virtual GroupFilter
	        GroupFilter gf = new GroupFilter {VirtualContract = gfc, GroupFilterName=gfc.GroupFilterName,ApplyToSeries = gfc.ApplyToSeries,SortingCriteria = gfc.SortingCriteria};
            AnimeGroupRepository grepo=new AnimeGroupRepository();
            AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();
            JMMUserRepository repUsers=new JMMUserRepository();
	        List<JMMUser> users = repUsers.GetAll();
            foreach (AnimeGroup grp in grepo.GetAllTopLevelGroups())
	        {
	            foreach (JMMUser user in users)
	            {
	                AnimeGroup_User userRec = repUserGroups.GetByUserAndGroupID(user.JMMUserID, grp.AnimeGroupID);
	                if (gf.EvaluateGroupFilter(grp, user, userRec))
	                {
	                    if (!gf.GroupsIds.ContainsKey(user.JMMUserID))
	                    {
	                        gf.GroupsIds[user.JMMUserID] = new HashSet<int>();
	                    }
	                    if (!gf.GroupsIds[user.JMMUserID].Contains(grp.AnimeGroupID))
	                    {
	                        gf.GroupsIds[user.JMMUserID].Add(grp.AnimeGroupID);
	                    }
	                }
	                else
	                {
	                    if (gf.GroupsIds.ContainsKey(user.JMMUserID))
	                    {
	                        if (gf.GroupsIds[user.JMMUserID].Contains(grp.AnimeGroupID))
	                        {
	                            gf.GroupsIds[user.JMMUserID].Remove(grp.AnimeGroupID);
	                        }
	                    }
	                }
	            }
	        }
	        return gf.ToContract();
	    }

        public bool EvaluateGroupFilter(AnimeGroup grp, JMMUser curUser, AnimeGroup_User userRec)
        {
            // sub groups don't count
            if (grp.AnimeGroupParentID.HasValue) return false;

            // make sure the user has not filtered this out
            if (!curUser.AllowedGroup(grp, curUser)) return false;

            // first check for anime groups which are included exluded every time
            foreach (GroupFilterCondition gfc in FilterConditions)
            {
                if (gfc.ConditionTypeEnum != GroupFilterConditionType.AnimeGroup) continue;

                int groupID = 0;
                int.TryParse(gfc.ConditionParameter, out groupID);
                if (groupID == 0) break;

                if (gfc.ConditionOperatorEnum == GroupFilterOperator.Equals)
                    if (groupID == grp.AnimeGroupID) return true;

                if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotEquals)
                    if (groupID == grp.AnimeGroupID) return false;
            }

            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.InvariantCulture;
            
            if (BaseCondition == (int)GroupFilterBaseCondition.Exclude) return false;

            Contract_AnimeGroup contractGroup = grp.GetUserContract(curUser.JMMUserID);

            // now check other conditions
            foreach (GroupFilterCondition gfc in FilterConditions)
            {
                switch (gfc.ConditionTypeEnum)
                {
                    case GroupFilterConditionType.Favourite:
                        if (userRec == null) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && userRec.IsFave == 0) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && userRec.IsFave == 1) return false;
                        break;

                    case GroupFilterConditionType.MissingEpisodes:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && grp.HasMissingEpisodesAny == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && grp.HasMissingEpisodesAny == true) return false;
                        break;

                    case GroupFilterConditionType.MissingEpisodesCollecting:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && grp.HasMissingEpisodesGroups == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && grp.HasMissingEpisodesGroups == true) return false;
                        break;

                    case GroupFilterConditionType.HasWatchedEpisodes:
                        if (userRec == null) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && userRec.AnyFilesWatched == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && userRec.AnyFilesWatched == true) return false;
                        break;

                    case GroupFilterConditionType.HasUnwatchedEpisodes:
                        if (userRec == null) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && userRec.HasUnwatchedFiles == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && userRec.HasUnwatchedFiles == true) return false;
                        break;

                    case GroupFilterConditionType.AssignedTvDBInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_HasTvDBLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_HasTvDBLink == true) return false;
                        break;

                    case GroupFilterConditionType.AssignedMALInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_HasMALLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_HasMALLink == true) return false;
                        break;

                    case GroupFilterConditionType.AssignedMovieDBInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_HasMovieDBLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_HasMovieDBLink == true) return false;
                        break;

                    case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_HasMovieDBOrTvDBLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_HasMovieDBOrTvDBLink == true) return false;
                        break;

                    case GroupFilterConditionType.CompletedSeries:

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_IsComplete == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_IsComplete == true) return false;
                        break;

                    case GroupFilterConditionType.FinishedAiring:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_HasFinishedAiring == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_IsCurrentlyAiring == false) return false;
                        break;

                    case GroupFilterConditionType.UserVoted:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_UserVotePermanent.HasValue == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_UserVotePermanent.HasValue == true) return false;
                        break;

                    case GroupFilterConditionType.UserVotedAny:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.Stat_UserVoteOverall.HasValue == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_UserVoteOverall.HasValue == true) return false;
                        break;

                    case GroupFilterConditionType.AirDate:
                        DateTime filterDate;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            int days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDate = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDate = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan || gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue) return false;
                            if (contractGroup.Stat_AirDate_Max.Value < filterDate) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue) return false;
                            if (contractGroup.Stat_AirDate_Min.Value > filterDate) return false;
                        }
                        break;

                    case GroupFilterConditionType.SeriesCreatedDate:
                        DateTime filterDateSeries;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            int days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateSeries = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateSeries = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan || gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!contractGroup.Stat_SeriesCreatedDate.HasValue) return false;
                            if (contractGroup.Stat_SeriesCreatedDate.Value < filterDateSeries) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!contractGroup.Stat_SeriesCreatedDate.HasValue) return false;
                            if (contractGroup.Stat_SeriesCreatedDate.Value > filterDateSeries) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeWatchedDate:
                        DateTime filterDateEpsiodeWatched;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            int days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateEpsiodeWatched = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpsiodeWatched = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan || gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (userRec == null) return false;
                            if (!userRec.WatchedDate.HasValue) return false;
                            if (userRec.WatchedDate.Value < filterDateEpsiodeWatched) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (userRec == null) return false;
                            if (!userRec.WatchedDate.HasValue) return false;
                            if (userRec.WatchedDate.Value > filterDateEpsiodeWatched) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeAddedDate:
                        DateTime filterDateEpisodeAdded;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            int days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateEpisodeAdded = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpisodeAdded = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan || gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!grp.EpisodeAddedDate.HasValue) return false;
                            if (grp.EpisodeAddedDate.Value < filterDateEpisodeAdded) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!grp.EpisodeAddedDate.HasValue) return false;
                            if (grp.EpisodeAddedDate.Value > filterDateEpisodeAdded) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeCount:

                        int epCount = -1;
                        int.TryParse(gfc.ConditionParameter, out epCount);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && contractGroup.Stat_EpisodeCount < epCount) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && contractGroup.Stat_EpisodeCount > epCount) return false;
                        break;

                    case GroupFilterConditionType.AniDBRating:

                        decimal dRating = -1;
                        decimal.TryParse(gfc.ConditionParameter, style, culture, out dRating);

                        decimal thisRating = contractGroup.Stat_AniDBRating / (decimal)100;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && thisRating < dRating) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && thisRating > dRating) return false;
                        break;

                    case GroupFilterConditionType.UserRating:

                        if (!contractGroup.Stat_UserVoteOverall.HasValue) return false;

                        decimal dUserRating = -1;
                        decimal.TryParse(gfc.ConditionParameter, style, culture, out dUserRating);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && contractGroup.Stat_UserVoteOverall.Value < dUserRating) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && contractGroup.Stat_UserVoteOverall.Value > dUserRating) return false;
                        break;

                    case GroupFilterConditionType.Category:

                        string filterParm = gfc.ConditionParameter.Trim();

                        string[] cats = filterParm.Split(',');
                        bool foundCat = false;
                        int index = 0;
                        foreach (string cat in cats)
                        {
                            if (cat.Trim().Length == 0) continue;
                            if (cat.Trim() == ",") continue;

                            index = contractGroup.Stat_AllTags.IndexOf(cat.Trim(), 0, StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1)
                            {
                                foundCat = true;
                                break;
                            }
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundCat) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundCat) return false;
                        break;

                    case GroupFilterConditionType.CustomTags:

                        filterParm = gfc.ConditionParameter.Trim();

                        string[] tags = filterParm.Split(',');
                        bool foundTag = false;
                        index = 0;
                        foreach (string tag in tags)
                        {
                            if (tag.Trim().Length == 0) continue;
                            if (tag.Trim() == ",") continue;

                            index = contractGroup.Stat_AllCustomTags.IndexOf(tag.Trim(), 0, StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1)
                            {
                                foundTag = true;
                                break;
                            }
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundTag) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundTag) return false;
                        break;

                    case GroupFilterConditionType.AnimeType:

                        filterParm = gfc.ConditionParameter.Trim();
                        List<string> grpTypeList = grp.AnimeTypesList;

                        string[] atypes = filterParm.Split(',');
                        bool foundAnimeType = false;
                        index = 0;
                        foreach (string atype in atypes)
                        {
                            if (atype.Trim().Length == 0) continue;
                            if (atype.Trim() == ",") continue;

                            foreach (string thisAType in grpTypeList)
                            {
                                if (string.Equals(thisAType, atype, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    foundAnimeType = true;
                                    break;
                                }
                            }
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundAnimeType) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundAnimeType) return false;
                        break;



                    case GroupFilterConditionType.VideoQuality:

                        filterParm = gfc.ConditionParameter.Trim();

                        string[] vidQuals = filterParm.Split(',');
                        bool foundVid = false;
                        bool foundVidAllEps = false;
                        index = 0;
                        foreach (string vidq in vidQuals)
                        {
                            if (vidq.Trim().Length == 0) continue;
                            if (vidq.Trim() == ",") continue;

                            index = contractGroup.Stat_AllVideoQuality.IndexOf(vidq, 0, StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1) foundVid = true;

                            index = contractGroup.Stat_AllVideoQuality_Episodes.IndexOf(vidq, 0, StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1) foundVidAllEps = true;

                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundVid) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundVid) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.InAllEpisodes)
                            if (!foundVidAllEps) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotInAllEpisodes)
                            if (foundVidAllEps) return false;

                        break;

                    case GroupFilterConditionType.AudioLanguage:
                    case GroupFilterConditionType.SubtitleLanguage:

                        filterParm = gfc.ConditionParameter.Trim();

                        string[] languages = filterParm.Split(',');
                        bool foundLan = false;
                        index = 0;
                        foreach (string lanName in languages)
                        {
                            if (lanName.Trim().Length == 0) continue;
                            if (lanName.Trim() == ",") continue;

                            if (gfc.ConditionTypeEnum == GroupFilterConditionType.AudioLanguage)
                                index = contractGroup.Stat_AudioLanguages.IndexOf(lanName, 0, StringComparison.InvariantCultureIgnoreCase);

                            if (gfc.ConditionTypeEnum == GroupFilterConditionType.SubtitleLanguage)
                                index = contractGroup.Stat_SubtitleLanguages.IndexOf(lanName, 0, StringComparison.InvariantCultureIgnoreCase);

                            if (index > -1) foundLan = true;

                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundLan) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundLan) return false;

                        break;
                }
            }

            return true;
        }

        public static DateTime GetDateFromString(string sDate)
        {
            try
            {
                int year = int.Parse(sDate.Substring(0, 4));
                int month = int.Parse(sDate.Substring(4, 2));
                int day = int.Parse(sDate.Substring(6, 2));

                return new DateTime(year, month, day);
            }
            catch (Exception ex)
            {
                return DateTime.Today;
            }
        }
    }
}
