using Microsoft.Extensions.Logging;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class HttpRateLimiter : AniDBRateLimiter
{
    protected override int ShortDelay { get; init; } = 2_000;
    protected override int LongDelay { get; init; } = 30_000;
    protected override long ShortPeriod { get; init; } = 10_000;
    protected override long ResetPeriod { get; init; } = 120_000;

    public HttpRateLimiter(ILogger<HttpRateLimiter> logger) : base(logger)
    {
    }
}
