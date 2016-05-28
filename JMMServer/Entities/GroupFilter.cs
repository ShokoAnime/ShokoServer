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

        public int? ParentGroupFilterID { get; set; }
        public int? IsVisibleInClients { get; set; }

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
			foreach (GroupFilterCondition gfc in FilterConditions)
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
	        GroupFilter gf = new GroupFilter
	        {
	            VirtualContract = gfc,
                GroupFilterName =gfc.GroupFilterName,
                ApplyToSeries = gfc.ApplyToSeries,
                SortingCriteria = gfc.SortingCriteria
	        };
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
            //Directories don't count
            if ((this.FilterType & (int) GroupFilterType.Directory) == (int)GroupFilterType.Directory)
                return false;
            // sub groups don't count
            if (grp.AnimeGroupParentID.HasValue) return false;

            // make sure the user has not filtered this out
            if (!curUser.AllowedGroup(grp)) return false;

            // first check for anime groups which are included exluded every time
            foreach (GroupFilterCondition gfc in FilterConditions)
            {
                if (gfc.ConditionTypeEnum != GroupFilterConditionType.AnimeGroup) continue;

                int groupID = 0;
                int.TryParse(gfc.ConditionParameter, out groupID);
                if (groupID == 0) break;


                if (gfc.ConditionOperatorEnum == GroupFilterOperator.Equals && groupID == grp.AnimeGroupID) return true;
                if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotEquals && groupID == grp.AnimeGroupID) return false;
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
                    case GroupFilterConditionType.Tag:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && !contractGroup.Stat_AllTags.Contains(gfc.ConditionParameter)) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_AllTags.Contains(gfc.ConditionParameter)) return false;
                        break;
                    case GroupFilterConditionType.Year:
                        if (!contractGroup.Stat_AirDate_Min.HasValue)
                            return false;
                        string year = contractGroup.Stat_AirDate_Min.Value.Year.ToString();
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && year!=gfc.ConditionParameter) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && year==gfc.ConditionParameter) return false;
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
                    case GroupFilterConditionType.LatestEpisodeAirDate:
                        DateTime filterDateEpisodeLastAired;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            int days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateEpisodeLastAired = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpisodeLastAired = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan || gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!grp.LatestEpisodeAirDate.HasValue) return false;
                            if (grp.LatestEpisodeAirDate.Value < filterDateEpisodeLastAired) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!grp.LatestEpisodeAirDate.HasValue) return false;
                            if (grp.LatestEpisodeAirDate.Value > filterDateEpisodeLastAired) return false;
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

                        List<string> cats = gfc.ConditionParameter.Trim().Split(new char[] { ','},StringSplitOptions.RemoveEmptyEntries).Select(a=>a.ToLowerInvariant()).ToList();
                        bool foundCat = cats.FindInEnumerable(contractGroup.Stat_AllTags);
                        if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && (!foundCat)) return false;
                        if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && (foundCat)) return false;
                        break;

                    case GroupFilterConditionType.CustomTags:

                        List<string> ctags = gfc.ConditionParameter.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.ToLowerInvariant()).ToList();
                        bool foundTag = ctags.FindInEnumerable(contractGroup.Stat_AllCustomTags);
                        if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && (!foundTag)) return false;
                        if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && (foundTag)) return false;
                        break;

                    case GroupFilterConditionType.AnimeType:

                        List<string> ctypes = gfc.ConditionParameter.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.ToLowerInvariant()).ToList();
                        bool foundAnimeType = ctypes.FindInEnumerable(contractGroup.Stat_AnimeTypes);
                        if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && (!foundAnimeType)) return false;
                        if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && (foundAnimeType)) return false;
                        break;

                    case GroupFilterConditionType.VideoQuality:

						List<string> vqs = gfc.ConditionParameter.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.ToLowerInvariant()).ToList();
						bool foundVid = vqs.FindInEnumerable(contractGroup.Stat_AllVideoQuality);
						bool foundVidAllEps = vqs.FindInEnumerable(contractGroup.Stat_AllVideoQuality_Episodes);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundVid) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundVid) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.InAllEpisodes && !foundVidAllEps) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotInAllEpisodes && foundVidAllEps) return false;
                        break;

                    case GroupFilterConditionType.AudioLanguage:
						List<string> als = gfc.ConditionParameter.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.ToLowerInvariant()).ToList();
						bool foundLang = als.FindInEnumerable(contractGroup.Stat_AudioLanguages);
						if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundLang) return false;
						if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundLang) return false;
		                break;

					case GroupFilterConditionType.SubtitleLanguage:
						List<string> ass = gfc.ConditionParameter.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.ToLowerInvariant()).ToList();
						bool foundSub = ass.FindInEnumerable(contractGroup.Stat_AudioLanguages);
						if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundSub) return false;
						if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundSub) return false;
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
