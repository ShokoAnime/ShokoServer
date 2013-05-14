using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
	public class GroupFilter
	{
		public int GroupFilterID { get; private set; }
		public string GroupFilterName { get; set; }
		public int ApplyToSeries { get; set; }
		public int BaseCondition { get; set; }
		public string SortingCriteria { get; set; }
		public int? Locked { get; set; }

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

			contract.FilterConditions = new List<Contract_GroupFilterCondition>();
			foreach (GroupFilterCondition gfc in GetFilterConditions(session))
				contract.FilterConditions.Add(gfc.ToContract());

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
			}
			return contract;
		}
	}
}
