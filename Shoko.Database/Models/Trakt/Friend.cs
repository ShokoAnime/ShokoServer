using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.Trakt
{
    [Table("Trakt_Friend")]
    public class Friend
    {
        [Key, Column("Trakt_FriendID")] public int Id { get; set; }
        [MaxLength(100)] public string Username { get; set; }
        [MaxLength(100)] public string FullName { get; set; }
        [MaxLength(100)] public string Gender { get; set; }
        [MaxLength(100)] public string Age { get; set; }
        [MaxLength(100)] public string Location { get; set; }
        public string About { get; set; }
        public int Joined { get; set; }
        public string Avatar { get; set; }
        public int Url { get; set; }
        public DateTime LastAvatarUpdate { get; set; }
    }
}
