using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public class Contract_IgnoreAnime
    {
        public int IgnoreAnimeID { get; set; }
        public int JMMUserID { get; set; }
        public int AnimeID { get; set; }
        public int IgnoreType { get; set; }

        public Client.CL_AniDB_Anime Anime { get; set; }
    }
}