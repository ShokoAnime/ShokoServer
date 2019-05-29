using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Core.Models
{
    public class ShokoUser
    {
        [Key] public Guid Id { get; set; }
        [Required, MaxLength(150)] public string Username { get; set; }
        public string Password { get; set; }
        public bool IsAdmin { get; set; }
        public string Token { get; set; }
    }
}
