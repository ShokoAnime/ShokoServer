using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.User
{
    [Table("AnimeSeries_User")]
    public class AnimeSeries
    {
        [Key, Column("AnimeSeries_UserID")] public int Id { get; set; }
        [Column("JMMUserID"), ForeignKey(nameof(User))] public int UserId { get; set; }
        [Column("AnimeSeriesID"), ForeignKey(nameof(Series))] public int SeriesId { get; set; }
        public int UnwatchedEpisodeCount { get; set; }
        public int WatchedEpisodeCount { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }

        [Obsolete("To be removed")] public int PlexContractVersion { get; set; } = 0;
        [Obsolete("To be removed")] public byte[] PlexContractBlob { get; set; } = null;
        [Obsolete("To be removed")] public int PlexContractSize { get; set; } = 0;

        public virtual Models.AnimeSeries Series { get; set; }
    }
}
