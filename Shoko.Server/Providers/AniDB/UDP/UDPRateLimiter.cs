namespace Shoko.Server.Providers.AniDB.UDP
{
    public class UDPRateLimiter : AniDBRateLimiter
    {
        protected override int ShortDelay { get; init; } = 2000;
        protected override int LongDelay { get; init; } = 4000;
        protected override long shortPeriod { get; init; } = 3600000;
        protected override long resetPeriod { get; init; } = 1800000;
    }
}
