using System.Collections.Generic;

namespace JMMContracts
{
    public class Contract_GroupFilter
    {
        public int? GroupFilterID { get; set; }
        public string GroupFilterName { get; set; }
        public int ApplyToSeries { get; set; }
        public int BaseCondition { get; set; }
        public string SortingCriteria { get; set; }
        public int? Locked { get; set; }

        public int FilterType { get; set; }

        public List<Contract_GroupFilterCondition> FilterConditions { get; set; }
    }
}