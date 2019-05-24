using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class AnimeStaff
    {
        [Key, Column("StaffID")] public int Id { get; set; }
        [Column("AniDBID")] public int AniDbId { get; set; }
        public string Name { get; set; }
        public string AlternateName { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
    }
}
