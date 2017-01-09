using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentNHibernate.MappingModel;
using Newtonsoft.Json;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
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
        public int InvisibleInClients { get; set; }

        public int GroupsIdsVersion { get; set; }
        public string GroupsIdsString { get; set; }

        public int GroupConditionsVersion { get; set; }
        public string GroupConditions { get; set; }

        public int SeriesIdsVersion { get; set; }
        public string SeriesIdsString { get; set; }

        public const int GROUPFILTER_VERSION = 3;
        public const int GROUPCONDITIONS_VERSION = 1;
        public const int SERIEFILTER_VERSION = 2;


        internal Dictionary<int, HashSet<int>> _groupsId = new Dictionary<int, HashSet<int>>();
        internal Dictionary<int, HashSet<int>> _seriesId = new Dictionary<int, HashSet<int>>();

        internal List<GroupFilterCondition> _conditions = new List<GroupFilterCondition>();


        public virtual HashSet<GroupFilterConditionType> Types
        {
            get
            {
                return
                    new HashSet<GroupFilterConditionType>(
                        Conditions.Select(a => a.ConditionType).Distinct().Cast<GroupFilterConditionType>());
            }
        }

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
            set { _groupsId = value; }
        }

        public virtual Dictionary<int, HashSet<int>> SeriesIds
        {
            get
            {
                if (_seriesId.Count == 0 && SeriesIdsVersion == SERIEFILTER_VERSION)
                {
                    Dictionary<int, List<int>> vals =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(SeriesIdsString);
                    if (vals != null)
                        _seriesId = vals.ToDictionary(a => a.Key, a => new HashSet<int>(a.Value));
                }
                return _seriesId;
            }
            set { _seriesId = value; }
        }

        public virtual List<GroupFilterCondition> Conditions
        {
            get
            {
                if (_conditions.Count == 0 && GroupConditionsVersion == GROUPCONDITIONS_VERSION)
                {
                    _conditions =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<List<GroupFilterCondition>>(GroupConditions);
                }
                return _conditions;
            }
            set
            {
                if (value != null)
                    _conditions = value;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", GroupFilterID, GroupFilterName);
        }

        /*
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
		*/

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
                            gfsc.SortType = (GroupFilterSorting) stype;
                            gfsc.SortDirection = (GroupFilterSortDirection) sdir;
                            sortCriteriaList.Add(gfsc);
                        }
                    }
                }

                return sortCriteriaList;
            }
        }


        public Contract_GroupFilter ToContract()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            contract.ParentGroupFilterID = this.ParentGroupFilterID;
            contract.InvisibleInClients = this.InvisibleInClients;
            contract.FilterConditions = new List<Contract_GroupFilterCondition>();
            foreach (GroupFilterCondition gfc in Conditions)
                contract.FilterConditions.Add(gfc.ToContract());
            contract.Groups = this.GroupsIds;
            contract.Series = this.SeriesIds;
            contract.Childs = GroupFilterID==0 ? new HashSet<int>() : RepoFactory.GroupFilter.GetByParentID(GroupFilterID).Select(a => a.GroupFilterID).ToHashSet();
            return contract;
        }

        public static GroupFilter FromContract(Contract_GroupFilter gfc)
        {
            GroupFilter gf = new GroupFilter();
            gf.GroupFilterID = gfc.GroupFilterID ?? 0;
            gf.GroupFilterName = gfc.GroupFilterName;
            gf.ApplyToSeries = gfc.ApplyToSeries;
            gf.BaseCondition = gfc.BaseCondition;
            gf.SortingCriteria = gfc.SortingCriteria;
            gf.Locked = gfc.Locked;
            gf.InvisibleInClients = gfc.InvisibleInClients;
            gf.ParentGroupFilterID = gfc.ParentGroupFilterID;
            gf.FilterType = gfc.FilterType;
            List<GroupFilterCondition> conds = new List<GroupFilterCondition>();
            foreach (Contract_GroupFilterCondition c in gfc.FilterConditions)
            {
                GroupFilterCondition cc = new GroupFilterCondition();
                cc.ConditionType = c.ConditionType;
                cc.ConditionOperator = c.ConditionOperator;
                cc.ConditionParameter = c.ConditionParameter;
                conds.Add(cc);
            }
            gf.Conditions = conds;
            gf.GroupsIds = gfc.Groups ?? new Dictionary<int, HashSet<int>>();
            gf.SeriesIds = gfc.Series ?? new Dictionary<int, HashSet<int>>();
            return gf;
        }

        public Contract_GroupFilterExtended ToContractExtended(JMMUser user)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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

        /*
        public void UpdateGroupFilterUser(JMMUser ruser)
        {
            AnimeGroupRepository repGroups = new AnimeGroupRepository();
            AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();
            JMMUserRepository repUser = new JMMUserRepository();
            GroupFilterRepository repGrpFilter = new GroupFilterRepository();
            List<JMMUser> users = new List<JMMUser>();
	        if ((this.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory)
		        return;
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
                    if (EvaluateGroupFilter(grp.GetUserContract(user.JMMUserID),user.Contract))
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
		*/


        public bool CalculateGroupFilterSeries(CL_AnimeSeries_User ser, Contract_JMMUser user, int id)
        {
            bool change = false;
            if (EvaluateGroupFilter(ser, user))
            {
                if (!SeriesIds.ContainsKey(id))
                {
                    SeriesIds[id] = new HashSet<int>();
                }
                if (!SeriesIds[id].Contains(ser.AnimeSeriesID))
                {
                    SeriesIds[id].Add(ser.AnimeSeriesID);
                    change = true;
                }
            }
            else
            {
                if (SeriesIds.ContainsKey(id))
                {
                    if (SeriesIds[id].Contains(ser.AnimeSeriesID))
                    {
                        SeriesIds[id].Remove(ser.AnimeSeriesID);
                        change = true;
                    }
                }
            }
            return change;
        }

        public bool CalculateGroupFilterGroups(CL_AnimeGroup_User grp, Contract_JMMUser user, int jmmUserId)
        {
            bool change = false;
            HashSet<int> groupIds;

            GroupsIds.TryGetValue(jmmUserId, out groupIds);

            if (EvaluateGroupFilter(grp, user))
            {
                if (groupIds == null)
                {
                    groupIds = new HashSet<int>();
                    GroupsIds[jmmUserId] = groupIds;
                }

                change = groupIds.Add(grp.AnimeGroupID);
            }
            else if (groupIds != null)
            {
                change = groupIds.Remove(grp.AnimeGroupID);
            }

            return change;
        }

        public void EvaluateAnimeGroups()
        {
            IReadOnlyList<JMMUser> users = RepoFactory.JMMUser.GetAll();
            foreach (SVR_AnimeGroup grp in RepoFactory.AnimeGroup.GetAllTopLevelGroups())
            {
                foreach (JMMUser user in users)
                {
                    CalculateGroupFilterGroups(grp.GetUserContract(user.JMMUserID), user.Contract, user.JMMUserID);
                }
            }
        }

        public void EvaluateAnimeSeries()
        {
            IReadOnlyList<JMMUser> users = RepoFactory.JMMUser.GetAll();
            foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
            {
                CalculateGroupFilterSeries(ser.Contract, null, 0); //Default no filter for JMM Client
                foreach (JMMUser user in users)
                {
                    CalculateGroupFilterSeries(ser.GetUserContract(user.JMMUserID), user.Contract, user.JMMUserID);
                }
            }
        }

        public static Contract_GroupFilter EvaluateContract(Contract_GroupFilter gfc)
        {
            GroupFilter gf = FromContract(gfc);
            gf.EvaluateAnimeGroups();
            gf.EvaluateAnimeSeries();
            return gf.ToContract();
        }


        public bool EvaluateGroupFilter(CL_AnimeGroup_User contractGroup, Contract_JMMUser curUser)
        {
            //Directories don't count
            if ((this.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory)
                return false;
            // sub groups don't count
            if (contractGroup.AnimeGroupParentID.HasValue) return false;


            // make sure the user has not filtered this out
            if ((curUser != null) && curUser.HideCategories.FindInEnumerable(contractGroup.Stat_AllTags))
                return false;

            // first check for anime groups which are included exluded every time
            foreach (GroupFilterCondition gfc in Conditions)
            {
                if (gfc.ConditionTypeEnum != GroupFilterConditionType.AnimeGroup) continue;

                int groupID = 0;
                int.TryParse(gfc.ConditionParameter, out groupID);
                if (groupID == 0) break;


                if (gfc.ConditionOperatorEnum == GroupFilterOperator.Equals && groupID == contractGroup.AnimeGroupID)
                    return true;
                if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotEquals && groupID != contractGroup.AnimeGroupID)
                    return true;
                return false;
            }

            bool exclude = BaseCondition == (int) GroupFilterBaseCondition.Exclude;

	        return exclude ^ EvaluateConditions(contractGroup, curUser);
        }

	    private bool EvaluateConditions(CL_AnimeGroup_User contractGroup, Contract_JMMUser curUser)
	    {
		    NumberStyles style = NumberStyles.Number;
		    CultureInfo culture = CultureInfo.InvariantCulture;
		    // now check other conditions

		    foreach (GroupFilterCondition gfc in Conditions)
		    {
			    switch (gfc.ConditionTypeEnum)
			    {
				    case GroupFilterConditionType.Favourite:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && contractGroup.IsFave == 0)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.IsFave == 1)
						    return false;
					    break;

				    case GroupFilterConditionType.MissingEpisodes:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        (contractGroup.MissingEpisodeCount > 0 || contractGroup.MissingEpisodeCountGroups > 0) ==
					        false) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        (contractGroup.MissingEpisodeCount > 0 || contractGroup.MissingEpisodeCountGroups > 0) ==
					        true) return false;
					    break;

				    case GroupFilterConditionType.MissingEpisodesCollecting:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.MissingEpisodeCountGroups > 0 == false) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.MissingEpisodeCountGroups > 0 == true) return false;
					    break;
				    case GroupFilterConditionType.Tag:
					    List<string> tags =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim()).Where(a => !string.IsNullOrWhiteSpace(a))
							    .ToList();
					    bool tagsFound =
						    tags.Any(a => contractGroup.Stat_AllTags.Contains(a, StringComparer.InvariantCultureIgnoreCase));
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In || gfc.ConditionOperatorEnum == GroupFilterOperator.Include) && !tagsFound) return false;
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn || gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude) && tagsFound) return false;
					    break;
				    case GroupFilterConditionType.Year:

					    int year = 0;
					    int.TryParse(gfc.ConditionParameter.Trim(), out year);
					    if (year == 0) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && !contractGroup.Stat_AllYears.Contains(year))
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_AllYears.Contains(year))
						    return false;
					    break;

				    case GroupFilterConditionType.HasWatchedEpisodes:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.WatchedEpisodeCount > 0 == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.WatchedEpisodeCount > 0 == true)
						    return false;
					    break;

				    case GroupFilterConditionType.HasUnwatchedEpisodes:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.UnwatchedEpisodeCount > 0 == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.UnwatchedEpisodeCount > 0 == true)
						    return false;
					    break;

				    case GroupFilterConditionType.AssignedTvDBInfo:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_HasTvDBLink == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_HasTvDBLink == true)
						    return false;
					    break;

				    case GroupFilterConditionType.AssignedMALInfo:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_HasMALLink == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_HasMALLink == true)
						    return false;
					    break;

				    case GroupFilterConditionType.AssignedMovieDBInfo:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_HasMovieDBLink == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_HasMovieDBLink == true)
						    return false;
					    break;

				    case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        !contractGroup.Stat_HasMovieDBOrTvDBLink)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_HasMovieDBOrTvDBLink)
						    return false;
					    break;

				    case GroupFilterConditionType.CompletedSeries:

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_IsComplete == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_IsComplete == true)
						    return false;
					    break;

				    case GroupFilterConditionType.FinishedAiring:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_HasFinishedAiring == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_IsCurrentlyAiring == false)
						    return false;
					    break;

				    case GroupFilterConditionType.UserVoted:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_UserVotePermanent.HasValue == false) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_UserVotePermanent.HasValue == true) return false;
					    break;

				    case GroupFilterConditionType.UserVotedAny:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractGroup.Stat_UserVoteOverall.HasValue == false) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractGroup.Stat_UserVoteOverall.HasValue == true) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue)
							    return false;
						    if (contractGroup.Stat_AirDate_Max.Value < filterDate) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue)
							    return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractGroup.LatestEpisodeAirDate.HasValue) return false;
						    if (contractGroup.LatestEpisodeAirDate.Value < filterDateEpisodeLastAired) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (!contractGroup.LatestEpisodeAirDate.HasValue) return false;
						    if (contractGroup.LatestEpisodeAirDate.Value > filterDateEpisodeLastAired) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractGroup.WatchedDate.HasValue) return false;
						    if (contractGroup.WatchedDate.Value < filterDateEpsiodeWatched) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (contractGroup == null) return false;
						    if (!contractGroup.WatchedDate.HasValue) return false;
						    if (contractGroup.WatchedDate.Value > filterDateEpsiodeWatched) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractGroup.EpisodeAddedDate.HasValue) return false;
						    if (contractGroup.EpisodeAddedDate.Value < filterDateEpisodeAdded) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (!contractGroup.EpisodeAddedDate.HasValue) return false;
						    if (contractGroup.EpisodeAddedDate.Value > filterDateEpisodeAdded) return false;
					    }
					    break;

				    case GroupFilterConditionType.EpisodeCount:

					    int epCount = -1;
					    int.TryParse(gfc.ConditionParameter, out epCount);

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan &&
					        contractGroup.Stat_EpisodeCount < epCount)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan &&
					        contractGroup.Stat_EpisodeCount > epCount)
						    return false;
					    break;

				    case GroupFilterConditionType.AniDBRating:

					    decimal dRating = -1;
					    decimal.TryParse(gfc.ConditionParameter, style, culture, out dRating);

					    decimal thisRating = contractGroup.Stat_AniDBRating/(decimal) 100;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && thisRating < dRating)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && thisRating > dRating)
						    return false;
					    break;

				    case GroupFilterConditionType.UserRating:

					    if (!contractGroup.Stat_UserVoteOverall.HasValue) return false;

					    decimal dUserRating = -1;
					    decimal.TryParse(gfc.ConditionParameter, style, culture, out dUserRating);

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan &&
					        contractGroup.Stat_UserVoteOverall.Value < dUserRating) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan &&
					        contractGroup.Stat_UserVoteOverall.Value > dUserRating) return false;
					    break;

				    case GroupFilterConditionType.CustomTags:

					    List<string> ctags =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundTag = ctags.FindInEnumerable(contractGroup.Stat_AllCustomTags);
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && !foundTag) return false;
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && foundTag) return false;
					    break;

				    case GroupFilterConditionType.AnimeType:

					    List<string> ctypes =
						    gfc.ConditionParameter
							    .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => ((int)AniDB_AnimeExtensions.RawToType(a)).ToString())
							    .ToList();
					    bool foundAnimeType = ctypes.FindInEnumerable(contractGroup.Stat_AnimeTypes);
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && !foundAnimeType) return false;
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && foundAnimeType) return false;
					    break;

				    case GroupFilterConditionType.VideoQuality:

					    List<string> vqs =
						    gfc.ConditionParameter
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundVid = vqs.FindInEnumerable(contractGroup.Stat_AllVideoQuality);
					    bool foundVidAllEps = vqs.FindInEnumerable(contractGroup.Stat_AllVideoQuality_Episodes);

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundVid) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundVid) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.InAllEpisodes && !foundVidAllEps)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotInAllEpisodes && foundVidAllEps)
						    return false;
					    break;

				    case GroupFilterConditionType.AudioLanguage:
					    List<string> als =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundLang = als.FindInEnumerable(contractGroup.Stat_AudioLanguages);
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundLang) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundLang) return false;
					    break;

				    case GroupFilterConditionType.SubtitleLanguage:
					    List<string> ass =
						    gfc.ConditionParameter
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundSub = ass.FindInEnumerable(contractGroup.Stat_SubtitleLanguages);
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundSub) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundSub) return false;
					    break;
			    }
		    }

		    return true;
	    }

        public bool EvaluateGroupFilter(CL_AnimeSeries_User contractSerie, Contract_JMMUser curUser)
        {
            //Directories don't count
            if ((this.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory)
                return false;


            // make sure the user has not filtered this out
            if ((curUser != null) &&
                curUser.HideCategories.FindInEnumerable(contractSerie.AniDBAnime.AniDBAnime.GetAllTags()))
                return false;

	        bool exclude = BaseCondition == (int) GroupFilterBaseCondition.Exclude;

	        return exclude ^ EvaluateConditions(contractSerie, curUser);
        }

	    private bool EvaluateConditions(CL_AnimeSeries_User contractSerie, Contract_JMMUser curUser)
	    {
		    NumberStyles style = NumberStyles.Number;
		    CultureInfo culture = CultureInfo.InvariantCulture;

		    // now check other conditions
		    foreach (GroupFilterCondition gfc in Conditions)
		    {
			    switch (gfc.ConditionTypeEnum)
			    {
				    case GroupFilterConditionType.MissingEpisodes:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        (contractSerie.MissingEpisodeCount > 0 || contractSerie.MissingEpisodeCountGroups > 0) ==
					        false) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        (contractSerie.MissingEpisodeCount > 0 || contractSerie.MissingEpisodeCountGroups > 0) ==
					        true) return false;
					    break;

				    case GroupFilterConditionType.MissingEpisodesCollecting:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractSerie.MissingEpisodeCountGroups > 0 == false) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractSerie.MissingEpisodeCountGroups > 0 == true) return false;
					    break;
				    case GroupFilterConditionType.Tag:
					    List<string> tags =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim()).Where(a => !string.IsNullOrWhiteSpace(a))
							    .ToList();
					    bool tagsFound =
						    tags.Any(a => contractSerie.AniDBAnime.AniDBAnime.GetAllTags().Contains(a,
							    StringComparer.InvariantCultureIgnoreCase));
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In || gfc.ConditionOperatorEnum == GroupFilterOperator.Include) && !tagsFound) return false;
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn || gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude) && tagsFound) return false;
					    break;
				    case GroupFilterConditionType.Year:
					    int BeginYear = contractSerie.AniDBAnime.AniDBAnime.BeginYear;
					    int EndYear = contractSerie.AniDBAnime.AniDBAnime.EndYear;
					    if (BeginYear == 0) return false;
					    if (EndYear == 0) EndYear = int.MaxValue;
					    int year = 0;
					    int.TryParse(gfc.ConditionParameter.Trim(), out year);
					    if (year == 0) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && (year < BeginYear || year > EndYear))
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && (year >= BeginYear && year <= EndYear))
						    return false;
					    break;

				    case GroupFilterConditionType.HasWatchedEpisodes:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractSerie.WatchedEpisodeCount > 0 == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractSerie.WatchedEpisodeCount > 0 == true)
						    return false;
					    break;

				    case GroupFilterConditionType.HasUnwatchedEpisodes:
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
					        contractSerie.UnwatchedEpisodeCount > 0 == false)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
					        contractSerie.UnwatchedEpisodeCount > 0 == true)
						    return false;
					    break;

				    case GroupFilterConditionType.AssignedTvDBInfo:
					    bool tvDBInfoMissing = contractSerie.CrossRefAniDBTvDBV2 == null ||
					                           contractSerie.CrossRefAniDBTvDBV2.Count == 0;
					    bool supposedToHaveTvDBLink = contractSerie.AniDBAnime.AniDBAnime.AnimeType != (int) enAnimeType.Movie &&
					                                  !(contractSerie.AniDBAnime.AniDBAnime.Restricted > 0);
					    tvDBInfoMissing &= supposedToHaveTvDBLink;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && tvDBInfoMissing) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && !tvDBInfoMissing) return false;
					    break;

				    case GroupFilterConditionType.AssignedMALInfo:
					    bool malMissing = contractSerie.CrossRefAniDBMAL == null ||
					                      contractSerie.CrossRefAniDBMAL.Count == 0;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && malMissing) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && !malMissing) return false;
					    break;

				    case GroupFilterConditionType.AssignedMovieDBInfo:
					    bool movieMissing = contractSerie.CrossRefAniDBMovieDB == null;
					    bool supposedToHaveMovieLink = contractSerie.AniDBAnime.AniDBAnime.AnimeType ==
					                                   (int) enAnimeType.Movie &&
					                                   !(contractSerie.AniDBAnime.AniDBAnime.Restricted > 0);
					    movieMissing &= supposedToHaveMovieLink;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && movieMissing) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && !movieMissing) return false;
					    break;

				    case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
					    bool restricted = (contractSerie.AniDBAnime.AniDBAnime.Restricted > 0);

					    bool movieLinkMissing = contractSerie.CrossRefAniDBMovieDB == null && !restricted;
					    bool tvlinkMissing = (contractSerie.CrossRefAniDBTvDBV2 == null ||
					                          contractSerie.CrossRefAniDBTvDBV2.Count == 0) && !restricted;
					    bool bothMissing = movieLinkMissing && tvlinkMissing;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && bothMissing) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && !bothMissing) return false;
					    break;

				    case GroupFilterConditionType.CompletedSeries:
					    bool completed = contractSerie.AniDBAnime.AniDBAnime.EndDate.HasValue &&
					                     contractSerie.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
					                     !(contractSerie.MissingEpisodeCount > 0 ||
					                       contractSerie.MissingEpisodeCountGroups > 0);
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && !completed) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && completed) return false;
					    break;

				    case GroupFilterConditionType.FinishedAiring:
					    bool finished = contractSerie.AniDBAnime.AniDBAnime.EndDate.HasValue &&
					                    contractSerie.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && !finished) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && finished) return false;
					    break;

				    case GroupFilterConditionType.UserVoted:

					    bool voted = (contractSerie.AniDBAnime.UserVote != null) &&
					                 (contractSerie.AniDBAnime.UserVote.VoteType == (int) AniDBVoteType.Anime);
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && !voted) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && voted) return false;
					    break;

				    case GroupFilterConditionType.UserVotedAny:
					    bool votedany = contractSerie.AniDBAnime.UserVote != null;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && !votedany) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && votedany) return false;
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
						    filterDate = GroupFilterHelper.GetDateFromString(gfc.ConditionParameter);


					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
						    if (!contractSerie.AniDBAnime.AniDBAnime.AirDate.HasValue ||
						        contractSerie.AniDBAnime.AniDBAnime.AirDate.Value < filterDate) return false;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
						    if (!contractSerie.AniDBAnime.AniDBAnime.AirDate.HasValue ||
						        contractSerie.AniDBAnime.AniDBAnime.AirDate.Value > filterDate) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractSerie.LatestEpisodeAirDate.HasValue) return false;
						    if (contractSerie.LatestEpisodeAirDate.Value < filterDateEpisodeLastAired) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (!contractSerie.LatestEpisodeAirDate.HasValue) return false;
						    if (contractSerie.LatestEpisodeAirDate.Value > filterDateEpisodeLastAired) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (contractSerie.DateTimeCreated < filterDateSeries) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (contractSerie.DateTimeCreated > filterDateSeries) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractSerie.WatchedDate.HasValue) return false;
						    if (contractSerie.WatchedDate.Value < filterDateEpsiodeWatched) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (contractSerie == null) return false;
						    if (!contractSerie.WatchedDate.HasValue) return false;
						    if (contractSerie.WatchedDate.Value > filterDateEpsiodeWatched) return false;
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

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
					        gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
					    {
						    if (!contractSerie.EpisodeAddedDate.HasValue) return false;
						    if (contractSerie.EpisodeAddedDate.Value < filterDateEpisodeAdded) return false;
					    }
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
					    {
						    if (!contractSerie.EpisodeAddedDate.HasValue) return false;
						    if (contractSerie.EpisodeAddedDate.Value > filterDateEpisodeAdded) return false;
					    }
					    break;

				    case GroupFilterConditionType.EpisodeCount:

					    int epCount = -1;
					    int.TryParse(gfc.ConditionParameter, out epCount);

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan &&
					        contractSerie.AniDBAnime.AniDBAnime.EpisodeCount < epCount) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan &&
					        contractSerie.AniDBAnime.AniDBAnime.EpisodeCount > epCount) return false;
					    break;

				    case GroupFilterConditionType.AniDBRating:

					    decimal dRating = -1;
					    decimal.TryParse(gfc.ConditionParameter, style, culture, out dRating);

					    int totalVotes = contractSerie.AniDBAnime.AniDBAnime.VoteCount +
					                     contractSerie.AniDBAnime.AniDBAnime.TempVoteCount;
					    decimal totalRating = contractSerie.AniDBAnime.AniDBAnime.Rating*
					                          contractSerie.AniDBAnime.AniDBAnime.VoteCount +
					                          contractSerie.AniDBAnime.AniDBAnime.TempRating*
					                          contractSerie.AniDBAnime.AniDBAnime.TempVoteCount;
					    decimal thisRating = totalVotes == 0 ? 0 : totalRating/totalVotes/100;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && thisRating < dRating)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && thisRating > dRating)
						    return false;
					    break;

				    case GroupFilterConditionType.UserRating:


					    decimal dUserRating = -1;
					    decimal.TryParse(gfc.ConditionParameter, style, culture, out dUserRating);
					    decimal val = contractSerie.AniDBAnime.UserVote?.VoteValue ?? 0;

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && val < dUserRating)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && val > dUserRating)
						    return false;
					    break;



				    case GroupFilterConditionType.CustomTags:

					    List<string> ctags =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundTag =
						    ctags.FindInEnumerable(contractSerie.AniDBAnime.CustomTags.Select(a => a.TagName));
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && !foundTag) return false;
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && foundTag) return false;
					    break;

				    case GroupFilterConditionType.AnimeType:

					    List<string> ctypes =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => ((int)AniDB_AnimeExtensions.RawToType(a.ToLowerInvariant())).ToString())
							    .ToList();
					    bool foundAnimeType = ctypes.Contains(contractSerie.AniDBAnime.AniDBAnime.AnimeType.ToString());
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.In) && !foundAnimeType) return false;
					    if ((gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn) && foundAnimeType) return false;
					    break;

				    case GroupFilterConditionType.VideoQuality:

					    List<string> vqs =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundVid = vqs.FindInEnumerable(contractSerie.AniDBAnime.Stat_AllVideoQuality);
					    bool foundVidAllEps =
						    vqs.FindInEnumerable(contractSerie.AniDBAnime.Stat_AllVideoQuality_Episodes);

					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundVid) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundVid) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.InAllEpisodes && !foundVidAllEps)
						    return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotInAllEpisodes && foundVidAllEps)
						    return false;
					    break;

				    case GroupFilterConditionType.AudioLanguage:
					    List<string> als =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundLang = als.FindInEnumerable(contractSerie.AniDBAnime.Stat_AudioLanguages);
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.In && !foundLang) return false;
					    if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn && foundLang) return false;
					    break;

				    case GroupFilterConditionType.SubtitleLanguage:
					    List<string> ass =
						    gfc.ConditionParameter.Trim()
							    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
							    .Select(a => a.ToLowerInvariant().Trim())
							    .ToList();
					    bool foundSub = ass.FindInEnumerable(contractSerie.AniDBAnime.Stat_AudioLanguages);
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

        /// <summary>
        /// Updates the <see cref="GroupIdsString"/> and/or <see cref="SeriesIdsString"/> properties
        /// based on the current contents of <see cref="GroupIds"/> and <see cref="SeriesIds"/>.
        /// </summary>
        /// <param name="updateGroups"><c>true</c> to update <see cref="GroupIdsString"/>; otherwise, <c>false</c>.</param>
        /// <param name="updateSeries"><c>true</c> to update <see cref="SeriesIds"/>; otherwise, <c>false</c>.</param>
        public void UpdateEntityReferenceStrings(bool updateGroups = true, bool updateSeries = true)
        {
            if (updateGroups)
            {
                GroupsIdsString = JsonConvert.SerializeObject(GroupsIds);
                GroupsIdsVersion = GROUPFILTER_VERSION;
            }
            if (updateSeries)
            {
                SeriesIdsString = JsonConvert.SerializeObject(SeriesIds);
                SeriesIdsVersion = SERIEFILTER_VERSION;
            }
        }

	    public void QueueUpdate()
	    {
		    CommandRequest_RefreshGroupFilter cmdRefreshGroupFilter = new CommandRequest_RefreshGroupFilter(this.GroupFilterID);
		    cmdRefreshGroupFilter.Save();
	    }

	    public override bool Equals(object obj)
	    {
		    if (!(obj is GroupFilter)) return false;
		    GroupFilter other = obj as GroupFilter;
		    if (other.ApplyToSeries != this.ApplyToSeries) return false;
		    if (other.BaseCondition != this.BaseCondition) return false;
		    if (other.FilterType != this.FilterType) return false;
		    if (other.InvisibleInClients != this.InvisibleInClients) return false;
		    if (other.Locked != this.Locked) return false;
		    if (other.ParentGroupFilterID != this.ParentGroupFilterID) return false;
		    if (other.GroupFilterName != this.GroupFilterName) return false;
		    if (other.SortingCriteria != this.SortingCriteria) return false;
		    if (this.Conditions == null || this.Conditions.Count == 0)
		    {
			    this.Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(this.GroupFilterID);
			    RepoFactory.GroupFilter.Save(this);
		    }
		    if (other.Conditions == null || other.Conditions.Count == 0)
		    {
			    other.Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(other.GroupFilterID);
			    RepoFactory.GroupFilter.Save(other);
		    }
		    if (this.Conditions != null && other.Conditions != null)
		    {
			    if (!this.Conditions.ContentEquals(other.Conditions)) return false;
		    }

		    return true;
	    }
    }
}