using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Anime_Similar : AniDB_Anime_Similar
    {
        public CL_AniDB_Anime AniDB_Anime { get; set; }
        public CL_AnimeSeries_User AnimeSeries { get; set; }
    }
}
