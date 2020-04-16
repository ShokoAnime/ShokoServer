using System;

namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_MAL : ICloneable
    {
        public CrossRef_AniDB_MAL()
        {
        }
        public int CrossRef_AniDB_MALID { get; set; }
        public int AnimeID { get; set; }
        public int MALID { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }
        public int CrossRefSource { get; set; }
        public object Clone()
        {
            return new CrossRef_AniDB_MAL
            {
                CrossRef_AniDB_MALID = CrossRef_AniDB_MALID,
                AnimeID = AnimeID,
                MALID = MALID,
                MALTitle = MALTitle,
                StartEpisodeType = StartEpisodeType,
                StartEpisodeNumber = StartEpisodeNumber,
                CrossRefSource = CrossRefSource
            };
        }
    }
}
