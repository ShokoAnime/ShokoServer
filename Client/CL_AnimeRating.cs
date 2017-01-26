namespace Shoko.Models.Client
{
    public class CL_AnimeRating
    {
        public int AnimeID { get; set; }
        public CL_AniDB_AnimeDetailed AnimeDetailed { get; set; }
        public CL_AnimeSeries_User AnimeSeries { get; set; }
    }
}