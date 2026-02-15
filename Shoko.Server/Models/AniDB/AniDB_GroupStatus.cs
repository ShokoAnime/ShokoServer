
# nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_GroupStatus
{
    public int AniDB_GroupStatusID { get; set; }

    public int AnimeID { get; set; }

    public int GroupID { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public int CompletionState { get; set; }

    public int LastEpisodeNumber { get; set; }

    public decimal Rating { get; set; }

    public int Votes { get; set; }

    public string EpisodeRange { get; set; } = string.Empty;
}
