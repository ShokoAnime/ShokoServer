using System;

namespace Shoko.Server.Providers.AniDB
{
    public class AniDBStateUpdate : EventArgs
    {
        /// <summary>
        /// The value of the UpdateType, is the ban active, is it waiting on a response, etc
        /// </summary>
        public bool Value { get; set; }
        /// <summary>
        /// Auxiliary Message for some states
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Update type, Ban, Invalid Session, Waiting on Response, etc
        /// </summary>
        public UpdateType UpdateType { get; set; }
        /// <summary>
        /// When was it updated, usually Now, but may not be
        /// </summary>
        public DateTime UpdateTime { get; set; }
        /// <summary>
        /// If we are pausing the queue, then for how long(er)
        /// </summary>
        public int PauseTimeSecs { get; set; }

        protected bool Equals(AniDBStateUpdate other) => Value == other.Value && Message == other.Message && UpdateType == other.UpdateType && UpdateTime.Equals(other.UpdateTime) && PauseTimeSecs == other.PauseTimeSecs;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AniDBStateUpdate) obj);
        }

        public override int GetHashCode() => HashCode.Combine(Value, Message, (int) UpdateType, UpdateTime, PauseTimeSecs);

        public static bool operator ==(AniDBStateUpdate left, AniDBStateUpdate right) => Equals(left, right);

        public static bool operator !=(AniDBStateUpdate left, AniDBStateUpdate right) => !Equals(left, right);
    }
}