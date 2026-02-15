using System;

namespace Shoko.Server.API.v1.Models;

public class CL_AnimeEpisode_User : CL_AnimeEpisode
{
    public DateTime? WatchedDate { get; set; }
    public int PlayedCount { get; set; }
    public int WatchedCount { get; set; }
    public int StoppedCount { get; set; }

    public int EpisodeNumber { get; set; }
    public string EpisodeNameRomaji { get; set; }
    public string EpisodeNameEnglish { get; set; }
    public string Description { get; set; }
    public int EpisodeType { get; set; }
    public int LocalFileCount { get; set; }
    public int UnwatchedEpCountSeries { get; set; }

    // from AniDB_Episode
    public int AniDB_LengthSeconds { get; set; }
    public string AniDB_Rating { get; set; }
    public string AniDB_Votes { get; set; }
    public string AniDB_RomajiName { get; set; }
    public string AniDB_EnglishName { get; set; }
    public DateTime? AniDB_AirDate { get; set; }
}
