using System;

namespace Shoko.Server.API.v1.Models;

public class CL_AnimeEpisode
{
    public int AnimeEpisodeID { get; set; }
    public int AnimeSeriesID { get; set; }
    public int AniDB_EpisodeID { get; set; }
    public DateTime DateTimeCreated { get; set; }
    public DateTime DateTimeUpdated { get; set; }
}
