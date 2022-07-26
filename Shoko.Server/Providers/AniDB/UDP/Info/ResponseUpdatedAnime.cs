using System;
using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class ResponseUpdatedAnime
    {
        public int Count { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<int> AnimeIDs { get; set; }
    }
}
