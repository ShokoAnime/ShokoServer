using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class GroupFilterCondition
    {
        [Key, Column("GroupFilterConditionID")] public int Id { get; set; }
        [ForeignKey(nameof(Filter))] public int GroupFilterId { get; set; }
        [Column("ConditionType")] public int Type { get; set; }
        [Column("ConditionOperator")] public int Operator { get; set; }
        [Column("ConditionParameter")] public string Paramater { get; set; }

        public virtual GroupFilter Filter { get; set; }
    }
}
