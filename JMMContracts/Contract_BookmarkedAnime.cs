namespace Shoko.Models
{
    public class Contract_BookmarkedAnime
    {
        public int? BookmarkedAnimeID { get; set; }
        public int AnimeID { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }
        public int Downloading { get; set; }
        public Contract_AniDBAnime Anime { get; set; }
    }
}