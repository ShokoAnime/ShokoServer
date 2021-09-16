namespace Shoko.Models.Server
{
    public class AniDB_Anime_Staff
    {
        #region Server DB columns

        public int AniDB_Anime_StaffID { get; set; }
        public int AnimeID { get; set; }
        public int CreatorID { get; set; }
        public string CreatorType { get; set; }

        #endregion
    }
}
