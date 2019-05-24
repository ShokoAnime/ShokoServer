using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    [Table("JMMUser")]
    public class ShokoUser
    {
        [Key, Column("JMMUserID")] public int Id { get; set; }
        [MaxLength(150)] public string Username { get; set; }
        [MaxLength(150)] public string Password { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsAniDBUser { get; set; }
        public bool IsTraktUser { get; set; }
        public string HideCategories { get; set; }
        public bool? CanEditServerSettings { get; set; }
        public string PlexUsers { get; set; }
    }
}
