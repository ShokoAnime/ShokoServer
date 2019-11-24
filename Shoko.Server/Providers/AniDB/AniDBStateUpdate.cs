using System;
using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB
{
    public class AniDBStateUpdate : EventArgs
    {
        /// <summary>
        /// The value of the UpdateType, is the ban active, is it waiting on a response, etc
        /// </summary>
        public bool Value { get; set; }
        /// <summary>
        /// Update type, Ban, Invalid Session, Waiting on Response, etc
        /// </summary>
        public AniDBUpdateType UpdateType { get; set; }
        /// <summary>
        /// When was it updated, usually Now, but may not be
        /// </summary>
        public DateTime UpdateTime { get; set; }
        /// <summary>
        /// If we are pausing the queue, then for how long(er)
        /// </summary>
        public int PauseTimeSecs { get; set; }
    }
}