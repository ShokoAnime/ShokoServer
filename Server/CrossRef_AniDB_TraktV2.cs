using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_TraktV2
    {
        public int CrossRef_AniDB_TraktV2ID { get; private set; }
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
    }
}
