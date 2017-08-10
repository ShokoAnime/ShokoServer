using System;

namespace Shoko.Models.Server
{
    public class AnimeSeries
    {
        #region DB Columns

        public int AnimeSeriesID { get; set; }
        public int AnimeGroupID { get; set; }
        public int AniDB_ID { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public string DefaultAudioLanguage { get; set; }
        public string DefaultSubtitleLanguage { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public DateTime? LatestEpisodeAirDate { get; set; }
        public DayOfWeek? AirsOn { get; set; }
        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }
        public int LatestLocalEpisodeNumber { get; set; }
        public string SeriesNameOverride { get; set; }

        public string DefaultFolder { get; set; }

        #endregion
    }
}