using System;

namespace Shoko.Server.Models
{
    // Normally this would be in models, but clients don't need to know about it.
    public class AniDB_AnimeUpdate
    {
        public int AniDB_AnimeUpdateID { get; set; }
        public int AnimeID { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}