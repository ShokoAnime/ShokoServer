using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_Vote")]
    public class Vote
    {
        [Key, Column("AniDB_VoteID")] public int Id { get; set; }
        public int EntityID { get; set; } //PK?
        [Column("VoteValue")] public int Value { get; set; }
        [Column("VoteType")] public int Type { get; set; }
    }
}
