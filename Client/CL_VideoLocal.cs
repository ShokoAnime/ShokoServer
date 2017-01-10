using System;
using System.Collections.Generic;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_VideoLocal : VideoLocal
    {
        public int IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        public long ResumePosition { get; set; }
        public List<CL_VideoLocal_Place> Places { get; set; }
        public Media Media { get; set; }

    }
}