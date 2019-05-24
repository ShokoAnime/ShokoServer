using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class ScheduledUpdate
    {
        [Key, Column("ScheduledUpdateID")] public int Id { get; set; }
        public int UpdateType { get; set; } //PK? Has Unique constraint
        public DateTime LastUpdate { get; set; }
        public string UpdateDetails { get; set; }

    }
}
