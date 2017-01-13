using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Rest.Annotations;
using Nancy.Rest.Annotations.Atributes;

namespace Shoko.Models.Server
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

        [Level(1)]
        public int? ParentGroupFilterID { get; set; }
        [Level(2)]
        public int InvisibleInClients { get; set; }
    }
}
