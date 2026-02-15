
namespace Shoko.Server.Providers.AniDB.HTTP;

public class ResponseVote
{
    public int EntityID { get; set; }
    public double VoteValue { get; set; }
    public VoteType VoteType { get; set; }
}
