using System;
using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.TraktTV;

public sealed class TraktTVRateLimiter
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly TraktTVRateLimiter instance = new();

    private static int ShortDelay = 1500;
    private static int LongDelay = 1500;

    // Switch to longer delay after 1 hour
    private static long shortPeriod = 60 * 60 * 1000;

    // Switch to shorter delay after 30 minutes of inactivity
    private static long resetPeriod = 30 * 60 * 1000;

    private static Stopwatch _requestWatch = new();

    private static Stopwatch _activeTimeWatch = new();

    public Guid InstanceID { get; private set; }

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static TraktTVRateLimiter()
    {
        _requestWatch.Start();
        _activeTimeWatch.Start();
    }

    public static TraktTVRateLimiter Instance => instance;

    private TraktTVRateLimiter()
    {
        InstanceID = Guid.NewGuid();
    }

    public void ResetRate()
    {
        var elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
        _activeTimeWatch.Restart();
        logger.Trace($"TraktTVRateLimiter#{InstanceID}: Rate is reset. Active time was {elapsedTime} ms.");
    }

    public void EnsureRate()
    {
        lock (instance)
        {
            var delay = _requestWatch.ElapsedMilliseconds;

            if (delay > resetPeriod)
            {
                ResetRate();
            }

            var currentDelay = _activeTimeWatch.ElapsedMilliseconds > shortPeriod ? LongDelay : ShortDelay;

            if (delay > currentDelay)
            {
                logger.Trace($"TraktTVRateLimiter#{InstanceID}: Time since last request is {delay} ms, not throttling.");
                _requestWatch.Restart();
                return;
            }

            logger.Trace(
                $"TraktTVRateLimiter#{InstanceID}: Time since last request is {delay} ms, throttling for {currentDelay} ms.");
            Thread.Sleep(currentDelay);

            logger.Trace($"TraktTVRateLimiter#{InstanceID}: Sending TraktTV command.");
            _requestWatch.Restart();
        }
    }
}
