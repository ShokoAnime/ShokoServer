using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.User
{
    [Table("AnimeGroup_User")]
    public class AnimeGroup
    {
        [Key, Column("AnimeGroup_UserID")] public int Id { get; set; }
        [Column("JMMUserID"), ForeignKey(nameof(User))] public int UserId { get; set; }
        [Column("AnimeGroupID"), ForeignKey(nameof(Group))] public int GroupID { get; set; }
        public bool IsFave { get; set; }
        public int UnwatchedEpisodeCount { get; set; }
        public int WatchedEpisodeCount { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }

        [Obsolete("To be removed")] public int PlexContractVersion { get; set; } = 0;
        [Obsolete("To be removed")] public byte[] PlexContractBlob { get; set; } = null;
        [Obsolete("To be removed")] public int PlexContractSize { get; set; } = 0;
        public virtual ShokoUser User { get; set; }
        public virtual Models.AnimeGroup Group { get; set; }
    }
}
