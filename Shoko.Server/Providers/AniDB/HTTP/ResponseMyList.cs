using System;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class ResponseMyList
{
    public int? MyListID { get; set; }
    public int? AnimeID { get; set; }
    public int? EpisodeID { get; set; }
    public int? FileID { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public MyList_State State { get; set; }
}
