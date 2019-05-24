using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_MylistStats")]
    public class MyListStatus
    {
        [Key, Column("AniDB_MylistStatsID")] public int Id { get; set; }
        public int Animes { get; set; }
        public int Episodes { get; set; }
        public int Files { get; set; }
        public long SizeOfFiles { get; set; }
        public int AddedAnimes { get; set; }
        public int AddedEpisodes { get; set; }
        public int AddedFiles { get; set; }
        public int AddedGroups { get; set; }
        public int LeechPct { get; set; }
        public int GloryPct { get; set; }
        public int ViewedPct { get; set; }
        public int MylistPct { get; set; }
        public int EpisodesViewed { get; set; }
        public int Votes { get; set; }
        public int Reviews { get; set; }
        public int ViewiedLength { get; set; }
    }
}
