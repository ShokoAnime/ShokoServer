using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_GroupFilter : GroupFilter
    {
        public List<GroupFilterCondition> FilterConditions { get; set; }
        public Dictionary<int, HashSet<int>> Groups { get; set; }
        public Dictionary<int, HashSet<int>> Series { get; set; }
        public HashSet<int> Childs { get; set; }
    }
}