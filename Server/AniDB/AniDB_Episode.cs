using System;

namespace Shoko.Models.Server
{
    public class AniDB_Episode
    {
        #region DB columns

        public int AniDB_EpisodeID { get; set; }
        public int EpisodeID { get; set; }
        public int AnimeID { get; set; }
        public int LengthSeconds { get; set; }
        public string Rating { get; set; }
        public string Votes { get; set; }
        public int EpisodeNumber { get; set; }
        public int EpisodeType { get; set; }
        public string Description { get; set; }
        public int AirDate { get; set; }
        public DateTime DateTimeUpdated { get; set; }

        #endregion

        protected bool Equals(AniDB_Episode other)
        {
            return EpisodeID == other.EpisodeID && AnimeID == other.AnimeID && LengthSeconds == other.LengthSeconds &&
                   Rating == other.Rating && Votes == other.Votes && EpisodeNumber == other.EpisodeNumber &&
                   EpisodeType == other.EpisodeType && Description == other.Description && AirDate == other.AirDate;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((AniDB_Episode)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = EpisodeID;
                hashCode = (hashCode * 397) ^ AnimeID;
                hashCode = (hashCode * 397) ^ LengthSeconds;
                hashCode = (hashCode * 397) ^ (Rating != null ? Rating.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Votes != null ? Votes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ EpisodeNumber;
                hashCode = (hashCode * 397) ^ EpisodeType;
                hashCode = (hashCode * 397) ^ (Description != null ? Description.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ AirDate;
                return hashCode;
            }
        }
    }
}
