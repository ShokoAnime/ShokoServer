using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_IgnoreAnime : IgnoreAnime
    {
        public CL_AniDB_Anime Anime { get; set; }
    }
}