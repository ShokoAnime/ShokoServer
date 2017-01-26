namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_Other
    {
        public int CrossRef_AniDB_OtherID { get; set; }
        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public int CrossRefSource { get; set; }
        public int CrossRefType { get; set; }

        public CrossRef_AniDB_Other()
        {
        }
    }
}
