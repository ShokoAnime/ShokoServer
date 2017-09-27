using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.AniDB_API
{
    public sealed class AniDbImageRateLimiter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly AniDbImageRateLimiter instance = new AniDbImageRateLimiter();

        // Short Term rate
        private static int ShortDelay = 500;

        // Long Term rate
        private static int LongDelay = 1000;

        // Switch to longer delay after 1 hour
        private static long shortPeriod = 60 * 60 * 1000;

        // Switch to shorter delay after 30 minutes of inactivity
        private static long resetPeriod = 30 * 60 * 1000;

        private static Stopwatch _requestWatch = new Stopwatch();

        private static Stopwatch _activeTimeWatch = new Stopwatch();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AniDbImageRateLimiter()
        {
            _requestWatch.Start();
            _activeTimeWatch.Start();
        }

        public static AniDbImageRateLimiter Instance => instance;

        private AniDbImageRateLimiter()
        {
        }

        public void ResetRate()
        {
            long elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
            _activeTimeWatch.Restart();
            Logger.Trace($"Rate is reset. Active time was {elapsedTime} ms.");
        }

        public void EnsureRate()
        {
            lock (instance)
            {
                long delay = _requestWatch.ElapsedMilliseconds;

                if (delay > resetPeriod)
                {
                    ResetRate();
                }

                int currentDelay = _activeTimeWatch.ElapsedMilliseconds > shortPeriod ? LongDelay : ShortDelay;

                if (delay > currentDelay)
                {
                    Logger.Trace($"Time since last request is {delay} ms, not throttling.");
                    _requestWatch.Restart();
                    return;
                }

                Logger.Trace($"Time since last request is {delay} ms, throttling for {currentDelay}.");
                Thread.Sleep(currentDelay);

                Logger.Trace("Sending AniDB command.");
                _requestWatch.Restart();
            }
        }
    }
}