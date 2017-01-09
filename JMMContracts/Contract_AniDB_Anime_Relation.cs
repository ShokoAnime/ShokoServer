namespace Shoko.Models
{
    public class Contract_AniDB_Anime_Relation
    {
        public int AniDB_Anime_RelationID { get; set; }
        public int AnimeID { get; set; }
        public string RelationType { get; set; }
        public int RelatedAnimeID { get; set; }

        public Contract_AniDBAnime AniDB_Anime { get; set; }
        public Contract_AnimeSeries AnimeSeries { get; set; }
    }
}