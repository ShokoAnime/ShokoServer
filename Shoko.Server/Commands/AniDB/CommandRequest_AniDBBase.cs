namespace Shoko.Server.Commands
{
    public abstract class CommandRequest_AniDBBase : CommandRequest
    {
        public override CommandLimiterType CommandLimiterType => CommandLimiterType.AniDB;
    }
}