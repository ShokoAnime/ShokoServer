namespace Shoko.Models.Server
{
    public class AniDB_Anime_Character
    {
        #region Server DB columns

        public int AniDB_Anime_CharacterID { get; set; }
        public int AnimeID { get; set; }
        public int CharID { get; set; }
        public string CharType { get; set; }
        public string EpisodeListRaw { get; set; }

        #endregion

        public AniDB_Anime_Character() //Empty Constructor for nhibernate
        {

        }
    }
}