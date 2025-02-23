namespace Shoko.Models.Server
{
    public class AniDB_Anime_Relation
    {
        #region Server DB columns

        public int AniDB_Anime_RelationID { get; set; }
        public int AnimeID { get; set; }
        public string RelationType { get; set; }
        public int RelatedAnimeID { get; set; }

        #endregion
    }
}