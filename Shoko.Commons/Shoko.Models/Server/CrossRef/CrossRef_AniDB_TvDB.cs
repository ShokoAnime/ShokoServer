using Shoko.Models.Enums;

namespace Shoko.Models.Server
{
    // DESPITE LACKING V1 OR V3, THIS IS THE NEW ONE
    public class CrossRef_AniDB_TvDB
    {
        public int CrossRef_AniDB_TvDBID { get; set; }
        public int AniDBID { get; set; }
        public int TvDBID { get; set; }

        public CrossRefSource CrossRefSource { get; set; }
    }
}
