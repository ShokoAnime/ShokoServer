
# nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Vote
{
    public int AniDB_VoteID { get; set; }

    public int EntityID { get; set; }

    public int VoteValue { get; set; }

    public int VoteType { get; set; }
}
