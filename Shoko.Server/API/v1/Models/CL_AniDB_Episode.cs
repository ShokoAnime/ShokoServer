using System;
using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_Episode
{
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
    public Dictionary<string, string> Titles { get; set; }
}
