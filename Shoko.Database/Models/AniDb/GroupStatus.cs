using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_GroupStatus")]
    public class GroupStatus
    {
        [Key, Column("AniDB_GroupStatusID")] public int Id { get; set; }
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public int CompletionState { get; set; }
        public int LastEpisodeNumber { get; set; }
        public int Rating { get; set; }
        public int Votes { get; set; }
        [Required] public string EpisodeRange { get; set; }
    }
}
