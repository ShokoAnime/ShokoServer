using System;
using System.Collections.Generic;
using Shoko.Models;
using Shoko.Models.PlexAndKodi;

using Shoko.Models.Enums;
using Stream = Shoko.Models.PlexAndKodi.Stream;

namespace Shoko.Models.Server
{
    public class AnimeEpisode
    {
        #region Server DB columns

        public int AnimeEpisodeID { get; private set; }
        public int AnimeSeriesID { get; set; }
        public int AniDB_EpisodeID { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }

        #endregion

    }
}