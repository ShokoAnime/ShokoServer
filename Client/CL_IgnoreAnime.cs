using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_IgnoreAnime : IgnoreAnime
    {
        public Client.CL_AniDB_Anime Anime { get; set; }
    }
}