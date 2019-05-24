using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    [Table("AuthTokens")]
    public class AuthToken
    {
        [Column("AuthID"), Key] public int Id { get; set; }
        [Column("UserID"), ForeignKey(nameof(User))] public int UserId { get; set; }
        public string DeviceName { get; set; }
        public Guid Token { get; set; } //This could be the PK

        public virtual ShokoUser User { get; set; }
    }
}
