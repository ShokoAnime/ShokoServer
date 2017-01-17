
namespace Shoko.Models.Server
{
    public class BookmarkedAnime
    {
        public int BookmarkedAnimeID { get; set; }
        public int AnimeID { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }
        public int Downloading { get; set; }
        public BookmarkedAnime()
        {
        }
    }
}