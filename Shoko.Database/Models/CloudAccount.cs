using System.ComponentModel.DataAnnotations;

namespace Shoko.Database.Models
{
    public class CloudAccount
    {
        [Key] public int CloudID { get; set; }
        public int ConnectionString { get; set; }
        [Required, MaxLength(100)] public string Provider { get; set; }
        [Required, MaxLength(256)] public string Name { get; set; }
    }
}