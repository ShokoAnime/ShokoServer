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

        public AniDB_Episode() //Empty Constructor for nhibernate
        {

        }
    }
}
