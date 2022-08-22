using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class ResponseVote
    {
        public int EntityID { get; set; }
        public decimal VoteValue { get; set; }
        public AniDBVoteType VoteType { get; set; }
    }
}
