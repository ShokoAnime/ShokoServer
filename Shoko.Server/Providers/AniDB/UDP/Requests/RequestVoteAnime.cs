using System;
using Shoko.Server.Providers.AniDB.UDP.Responses;
using Void = Shoko.Server.Providers.AniDB.UDP.Responses.Void;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{
    public enum AnimeVoteType
    {
        Permanent = 1,
        Temporary = 2
    }

    /// <summary>
    /// Vote for an anime
    /// </summary>
    public class RequestVoteAnime : UDPBaseRequest<Void>
    {
        /// <summary>
        /// AnimeID to vote on
        /// </summary>
        public int AnimeID { get; set; }

        /// <summary>
        /// Between 0 exclusive and 10 inclusive, will be rounded to nearest tenth
        /// </summary>
        public double Value { get; set; }

        private int AniDBValue => (int) (Math.Round(Value, 1, MidpointRounding.AwayFromZero) * 100D);

        /// <summary>
        /// Vote Type. If the anime is finished, use Permanent, otherwise Temporary
        /// </summary>
        public AnimeVoteType VoteType { get; set; }

        protected override string BaseCommand => $"VOTE type={(int) VoteType}&aid={AnimeID}&value={AniDBValue}";

        protected override UDPBaseResponse<Void> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            return new UDPBaseResponse<Void> {Code = code};
        }
    }
}
