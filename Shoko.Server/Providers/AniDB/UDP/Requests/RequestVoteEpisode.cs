using System;
using Shoko.Server.Providers.AniDB.Helpers;
using Shoko.Server.Providers.AniDB.UDP.Responses;
using Void = Shoko.Server.Providers.AniDB.UDP.Responses.Void;

namespace Shoko.Server.Providers.AniDB.UDP.Requests
{

    /// <summary>
    /// Vote for an anime
    /// </summary>
    public class RequestVoteEpisode : UDPBaseRequest<Void>
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
        /// Episode Number
        /// </summary>
        public AniDBEpisodeNumber EpisodeNumber { get; set; }

        protected override string BaseCommand => $"VOTE type=1&aid={AnimeID}&value={AniDBValue}&epno={EpisodeNumber}";

        protected override UDPBaseResponse<Void> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            return new UDPBaseResponse<Void> {Code = code};
        }
    }
}
