using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class GroupFilter
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public GroupFilterBaseCondition BaseCondition { get; set; }
        public List<GroupFilterSortCriteria> SortingCriteria { get; set; }
        public bool ApplyToSeries { get; set; }
        public bool Locked { get; set; }
        public List<GroupFilterCondition> Conditions { get; set; } 
    }
}
