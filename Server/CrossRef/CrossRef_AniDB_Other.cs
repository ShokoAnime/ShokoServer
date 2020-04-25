using System;

namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_Other : ICloneable
    {
        public int CrossRef_AniDB_OtherID { get; set; }
        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public int CrossRefSource { get; set; }
        public int CrossRefType { get; set; }

        public object Clone()
        {
            return new CrossRef_AniDB_Other
            {
                CrossRef_AniDB_OtherID = CrossRef_AniDB_OtherID,
                AnimeID = AnimeID,
                CrossRefID = CrossRefID,
                CrossRefSource = CrossRefSource,
                CrossRefType = CrossRefType
            };
        }
    }
}
