using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_BookmarkedAnime : BookmarkedAnime
    {
        public CL_AniDB_Anime Anime { get; set; }
    }
}