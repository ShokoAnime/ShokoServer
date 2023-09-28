using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.TMDB;

public sealed class TmdbImageRateLimiter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Short Term rate
    private static int ShortDelay = 150;
    private static Stopwatch _requestWatch = new();

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static TmdbImageRateLimiter()
    {
        _requestWatch.Start();
    }

    public static readonly TmdbImageRateLimiter Instance = new();

    private TmdbImageRateLimiter()
    {
    }

    public void Reset()
    {
        _requestWatch.Restart();
    }

    public void EnsureRate()
    {
        lock (Instance)
        {
            var delay = _requestWatch.ElapsedMilliseconds;

            if (delay > ShortDelay)
            {
                Logger.Trace($"Time since last request is {delay} ms, not throttling.");
                _requestWatch.Restart();
                return;
            }

            Logger.Trace($"Time since last request is {delay} ms, throttling for {ShortDelay}.");
            Thread.Sleep(ShortDelay);

            Logger.Trace("Throttled.");
            _requestWatch.Restart();
        }
    }
}
