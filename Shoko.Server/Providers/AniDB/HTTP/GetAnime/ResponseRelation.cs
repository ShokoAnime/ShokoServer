namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime
{
    public class ResponseRelation
    {
        public int AnimeID { get; set; }
        public int RelatedAnimeID { get; set; }
        public RelationType RelationType { get; set; }
    }
}
