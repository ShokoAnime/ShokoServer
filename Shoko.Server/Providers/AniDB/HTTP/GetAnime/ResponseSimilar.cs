namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime
{
    public class ResponseSimilar
    {
        public int AnimeID { get; set; }
        public int SimilarAnimeID { get; set; }
        public int Approval { get; set; }
        public int Total { get; set; }
    }
}
