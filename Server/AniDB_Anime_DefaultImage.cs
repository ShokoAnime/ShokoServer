namespace Shoko.Models.Server
{
    public class AniDB_Anime_DefaultImage
    {
        #region Server DB columns

        public int AniDB_Anime_DefaultImageID { get; set; }
        public int AnimeID { get; set; }
        public int ImageParentID { get; set; }
        public int ImageParentType { get; set; }
        public int ImageType { get; set; }

        #endregion  

    }
}