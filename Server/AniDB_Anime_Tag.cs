

namespace Shoko.Models.Server
{
    public class AniDB_Anime_Tag
    {
        #region Server DB columns

        public int AniDB_Anime_TagID { get; set; }
        public int AnimeID { get; set; }
        public int TagID { get; set; }
        public int Approval { get; set; }
        public int Weight { get; set; }

        #endregion

        public AniDB_Anime_Tag() //Empty Constructor for nhibernate
        {

        }
    }
}