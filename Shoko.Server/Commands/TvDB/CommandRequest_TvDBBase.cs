namespace Shoko.Server.Commands
{
    public abstract class CommandRequest_TvDBBase : CommandRequest
    {
        public override CommandLimiterType CommandLimiterType => CommandLimiterType.TvDB;
    }
}