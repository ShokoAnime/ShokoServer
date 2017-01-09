using System;
using System.Collections.Generic;

namespace Shoko.Models
{
    public class Contract_AnimeSeries
    {
        public int AnimeSeriesID { get; set; }
        public int AnimeGroupID { get; set; }
        public int AniDB_ID { get; set; }
        public int UnwatchedEpisodeCount { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public int WatchedEpisodeCount { get; set; }
        public string DefaultAudioLanguage { get; set; }
        public string DefaultSubtitleLanguage { get; set; }
        public DateTime? WatchedDate { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public DateTime? LatestEpisodeAirDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }
        public int LatestLocalEpisodeNumber { get; set; }
        public string SeriesNameOverride { get; set; }

        public string DefaultFolder { get; set; }

        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }

        public Contract_AniDB_AnimeDetailed AniDBAnime { get; set; }
        public List<Contract_CrossRef_AniDB_TvDBV2> CrossRefAniDBTvDBV2 { get; set; }
        public Contract_CrossRef_AniDB_Other CrossRefAniDBMovieDB { get; set; }
        public List<Contract_CrossRef_AniDB_MAL> CrossRefAniDBMAL { get; set; }
        public List<Contract_TvDB_Series> TvDB_Series { get; set; }
        public Contract_MovieDB_Movie MovieDB_Movie { get; set; }
        public Contract_AnimeGroup TopLevelGroup { get; set; }
    }
}