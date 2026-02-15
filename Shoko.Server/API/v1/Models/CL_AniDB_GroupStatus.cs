namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_GroupStatus
{
    public int AniDB_GroupStatusID { get; set; }
    public int AnimeID { get; set; }
    public int GroupID { get; set; }
    public string GroupName { get; set; }
    public int CompletionState { get; set; }
    public int LastEpisodeNumber { get; set; }
    public decimal Rating { get; set; }
    public int Votes { get; set; }
    public string EpisodeRange { get; set; }
    public bool UserCollecting { get; set; }
    public int FileCount { get; set; }
}
