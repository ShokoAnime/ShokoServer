using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class GroupFilter
    {
        [Key, Column("GroupFilterID")] public int Id { get; set; }
        [Column("GroupFilterName"), MaxLength(500)] public string Name { get; set; }
        public bool ApplyToSeries { get; set; }
        public int BaseCondition { get; set; }
        public string SortingCriteria { get; set; }
        public bool? Locked { get; set; }
        public int FilterType { get; set; }

        public int GroupsIdsVersion { get; set; }
        public string GroupsIdsString { get; set; }

        public int GroupConditionsVersion { get; set; }
        public string GroupConditions { get; set; }

        public int SeriesIdsVersion { get; set; }


        [ForeignKey(nameof(ParentGroupFilter))] public int? ParentGroupFilterID { get; set; }
        public bool InvisibleInClients { get; set; }

        public GroupFilter ParentGroupFilter { get; set; }
    }
}
