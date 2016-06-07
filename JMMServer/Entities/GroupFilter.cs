using System.Collections.Generic;
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

        public List<GroupFilterCondition> FilterConditions
        {
            get
            {
                var repConds = new GroupFilterConditionRepository();
                return repConds.GetByGroupFilterID(GroupFilterID);
            }
        }

        public List<GroupFilterSortingCriteria> SortCriteriaList
        {
            get
            {
                var sortCriteriaList = new List<GroupFilterSortingCriteria>();

                if (!string.IsNullOrEmpty(SortingCriteria))
                {
                    var scrit = SortingCriteria.Split('|');
                    foreach (var sortpair in scrit)
                    {
                        var spair = sortpair.Split(';');
                        if (spair.Length != 2) continue;

                        var stype = 0;
                        var sdir = 0;

                        int.TryParse(spair[0], out stype);
                        int.TryParse(spair[1], out sdir);

                        if (stype > 0 && sdir > 0)
                        {
                            var gfsc = new GroupFilterSortingCriteria();
                            gfsc.GroupFilterID = GroupFilterID;
                            gfsc.SortType = (GroupFilterSorting)stype;
                            gfsc.SortDirection = (GroupFilterSortDirection)sdir;
                            sortCriteriaList.Add(gfsc);
                        }
                    }
                }

                return sortCriteriaList;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", GroupFilterID, GroupFilterName);
        }

        public List<GroupFilterCondition> GetFilterConditions(ISession session)
        {
            var repConds = new GroupFilterConditionRepository();
            return repConds.GetByGroupFilterID(session, GroupFilterID);
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
            var contract = new Contract_GroupFilter();
            contract.GroupFilterID = GroupFilterID;
            contract.GroupFilterName = GroupFilterName;
            contract.ApplyToSeries = ApplyToSeries;
            contract.BaseCondition = BaseCondition;
            contract.SortingCriteria = SortingCriteria;
            contract.Locked = Locked;
            contract.FilterType = FilterType;

            contract.FilterConditions = new List<Contract_GroupFilterCondition>();
            foreach (var gfc in GetFilterConditions(session))
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
            var contract = new Contract_GroupFilterExtended();
            contract.GroupFilter = ToContract();
            contract.GroupCount = 0;
            contract.SeriesCount = 0;

            // find all the groups for thise group filter
            var repGroups = new AnimeGroupRepository();
            var allGrps = repGroups.GetAll(session);

            if (StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID) &&
                StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(GroupFilterID))
            {
                var groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][GroupFilterID];
                foreach (var grp in allGrps)
                {
                    if (groups.Contains(grp.AnimeGroupID))
                        contract.GroupCount++;
                }
            }
            return contract;
        }
    }
}