using System;
using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_AnimeSeries_User
{
    public int AnimeSeriesID { get; set; }
    public int UnwatchedEpisodeCount { get; set; }
    public int WatchedEpisodeCount { get; set; }
    public DateTime? WatchedDate { get; set; }
    public int PlayedCount { get; set; }
    public int WatchedCount { get; set; }
    public int StoppedCount { get; set; }
    public int AnimeGroupID { get; set; }
    public int AniDB_ID { get; set; }
    public DateTime DateTimeUpdated { get; set; }
    public DateTime DateTimeCreated { get; set; }
    public string DefaultAudioLanguage { get; set; }
    public string DefaultSubtitleLanguage { get; set; }
    public DateTime? EpisodeAddedDate { get; set; }
    public DateTime? LatestEpisodeAirDate { get; set; }
    public int LatestLocalEpisodeNumber { get; set; }
    public string SeriesNameOverride { get; set; }

    public string DefaultFolder { get; set; }

    public int MissingEpisodeCount { get; set; }
    public int MissingEpisodeCountGroups { get; set; }
    public DayOfWeek? AirsOn { get; set; }

    public CL_AniDB_AnimeDetailed AniDBAnime { get; set; }
    public List<object> CrossRefAniDBTvDBV2 { get; set; }
    public CL_CrossRef_AniDB_Other CrossRefAniDBMovieDB { get; set; }
    public List<CL_CrossRef_AniDB_MAL> CrossRefAniDBMAL { get; set; }
    public List<object> TvDB_Series { get; set; }
    public CL_MovieDB_Movie MovieDB_Movie { get; set; }

    public CL_AnimeSeries_User()
    {
    }
}
