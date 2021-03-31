using System;
using Shoko.Server.Providers.AniDB.UDP.User.Requests;

namespace Shoko.Server.Providers.AniDB.UDP.User.Responses
{
    public class ResponseVote
    {
        /// <summary>
        /// For anime, the AID, for episodes, the EID
        /// </summary>
        public int EntityID { get; set; }

        /// <summary>
        /// The type of Vote, in AniDB terms
        /// </summary>
        public VoteType Type { get; set; }

        /// <summary>
        /// The Vote value in AniDB terms
        /// </summary>
        public int AniDBValue { private get; set; }

        /// <summary>
        /// A proper vote value, between 0 and 10, rounded to one decimal
        /// </summary>
        public double Value
        {
            get => Math.Round(AniDBValue / 100D, 1, MidpointRounding.AwayFromZero);
            set => AniDBValue = (int) (Math.Round(value, 1, MidpointRounding.AwayFromZero) * 100D);
        }

        /// <summary>
        /// The name, Anime or Episode. Can be used for quick validation and logging
        /// </summary>
        public string EntityName { get; set; }
    }
}
