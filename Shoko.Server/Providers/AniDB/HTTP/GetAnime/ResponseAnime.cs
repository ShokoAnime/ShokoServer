using System;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseAnime
{
    public int AnimeID { get; set; }
    public int EpisodeCount { get; set; }
    public DateTime? AirDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string URL { get; set; }
    public string Picname { get; set; }
    public int BeginYear { get; set; }
    public int EndYear { get; set; }
    public string MainTitle { get; set; }
    public string Description { get; set; }
    public int EpisodeCountNormal { get; set; }
    public int Rating { get; set; }
    public int VoteCount { get; set; }
    public int TempRating { get; set; }
    public int TempVoteCount { get; set; }
    public int AvgReviewRating { get; set; }
    public int ReviewCount { get; set; }
    public int Restricted { get; set; }
    public int AnimePlanetID { get; set; }
    public int ANNID { get; set; }
    public int AllCinemaID { get; set; }

    public AnimeType AnimeType { get; set; }
}
