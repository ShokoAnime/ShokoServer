using Shoko.Models.Enums;

namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_TvDB_Episode
    {
        public int CrossRef_AniDB_TvDB_EpisodeID { get; set; }
        public int AniDBEpisodeID { get; set; }
        public int TvDBEpisodeID { get; set; }

        public MatchRating MatchRating { get; set; }

    }
}
