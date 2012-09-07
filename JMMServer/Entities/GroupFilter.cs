using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
	public class GroupFilter
	{
		public int GroupFilterID { get; private set; }
		public string GroupFilterName { get; set; }
		public int ApplyToSeries { get; set; }
		public int BaseCondition { get; set; }
		public string SortingCriteria { get; set; }

		public override string ToString()
		{
			return string.Format("{0} - {1}", GroupFilterID, GroupFilterName);
		}

		public List<GroupFilterCondition> FilterConditions
		{
			get
			{
				GroupFilterConditionRepository repConds = new GroupFilterConditionRepository();
				return repConds.GetByGroupFilterID(this.GroupFilterID);
			}
		}

		public Contract_GroupFilter ToContract()
		{
			Contract_GroupFilter contract = new Contract_GroupFilter();
			contract.GroupFilterID = this.GroupFilterID;
			contract.GroupFilterName = this.GroupFilterName;
			contract.ApplyToSeries = this.ApplyToSeries;
			contract.BaseCondition = this.BaseCondition;
			contract.SortingCriteria = this.SortingCriteria;

			contract.FilterConditions = new List<Contract_GroupFilterCondition>();
			foreach (GroupFilterCondition gfc in FilterConditions)
				contract.FilterConditions.Add(gfc.ToContract());

			return contract;
		}

		public Contract_GroupFilterExtended ToContractExtended(JMMUser user)
		{
			Contract_GroupFilterExtended contract = new Contract_GroupFilterExtended();
			contract.GroupFilter = this.ToContract();
			contract.GroupCount = 0;
			contract.SeriesCount = 0;
			
			// find all the groups for thise group filter
			AnimeGroupRepository repGroups = new AnimeGroupRepository();
			List<AnimeGroup> allGrps = repGroups.GetAll();
			//TimeSpan ts = DateTime.Now - start;
			//logger.Info("GetAllGroups (Database) in {0} ms", ts.TotalMilliseconds);
			//start = DateTime.Now;

             if ((StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID)) && (StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(this.GroupFilterID)))
             {
                 HashSet<int> groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][GroupFilterID];
                 foreach (AnimeGroup grp in allGrps)
                 {
                     if (groups.Contains(grp.AnimeGroupID))
                         contract.GroupCount++;
                 }
             }
			return contract;
		}
	}
}
