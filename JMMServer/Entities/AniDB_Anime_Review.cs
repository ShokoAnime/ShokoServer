namespace JMMServer.Entities
{
    public class AniDB_Anime_Review
    {
        public int AniDB_Anime_ReviewID { get; private set; }
        public int AnimeID { get; set; }
        public int ReviewID { get; set; }
    }
}