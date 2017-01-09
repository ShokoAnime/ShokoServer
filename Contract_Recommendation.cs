using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public class Contract_Recommendation
    {
        public int RecommendedAnimeID { get; set; }
        public int BasedOnAnimeID { get; set; }
        public double Score { get; set; }
        public int BasedOnVoteValue { get; set; }
        public double RecommendedApproval { get; set; }

        public Client.CL_AniDB_Anime Recommended_AniDB_Anime { get; set; }
        public Client.CL_AnimeSeries_User Recommended_AnimeSeries { get; set; }

        public Client.CL_AniDB_Anime BasedOn_AniDB_Anime { get; set; }
        public Client.CL_AnimeSeries_User BasedOn_AnimeSeries { get; set; }
    }
}