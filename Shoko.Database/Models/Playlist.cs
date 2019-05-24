using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class Playlist
    {
        [Key, Column("PlaylistID")] public int Id { get; set; }
        [Column("PlaylistName")] public string Name { get; set; }
        [Column("PlaylistItems")] public string Items { get; set; }
        public int DefaultPlayOrder { get; set; }
        public bool PlayWatched { get; set; }
        public bool PlayUnwatched { get; set; }
    }
}
