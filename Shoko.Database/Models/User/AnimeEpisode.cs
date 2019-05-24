using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.User
{
    [Table("AnimeEpisode_User")]
    public class AnimeEpisode
    {
        [Key, Column("AnimeEpisode_UserID")] public int Id { get; set; }
        [Column("JMMUserID"), ForeignKey(nameof(User))] public int UserId { get; set; }
        [ForeignKey(nameof(Episode))] public int AnimeEpisodeId { get; set; }
        [ForeignKey(nameof(Series))] public int AnimeSeriesId { get; set; }
        public DateTime WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }
        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; } = null;
        public int ContractSize { get; set; }

        public virtual Models.AnimeEpisode Episode { get; set; }
        public virtual Models.AnimeSeries Series { get; set; }

        public virtual ShokoUser User { get; set; }
    }
}
