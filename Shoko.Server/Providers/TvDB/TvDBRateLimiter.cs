using System;
using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.TvDB
{
    public sealed class TvDBRateLimiter
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly TvDBRateLimiter instance = new TvDBRateLimiter();
       
        private static int ShortDelay = 100;
        private static int LongDelay = 500;

        // Switch to longer delay after 1 hour
        private static long shortPeriod = 60 * 60 * 1000;

        // Switch to shorter delay after 30 minutes of inactivity
        private static long resetPeriod = 30 * 60 * 1000;

        private static Stopwatch _requestWatch = new Stopwatch();

        private static Stopwatch _activeTimeWatch = new Stopwatch();

        public Guid InstanceID { get; private set; }

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static TvDBRateLimiter()
        {
            _requestWatch.Start();
            _activeTimeWatch.Start();
        }

        public static TvDBRateLimiter Instance => instance;

        private TvDBRateLimiter()
        {
            InstanceID = Guid.NewGuid();
        }

        public void ResetRate()
        {
            long elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
            _activeTimeWatch.Restart();
            logger.Trace($"TvDBRateLimiter#{InstanceID}: Rate is reset. Active time was {elapsedTime} ms.");
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
                    logger.Trace($"TvDBRateLimiter#{InstanceID}: Time since last request is {delay} ms, not throttling.");
                    _requestWatch.Restart();
                    return;
                }

                logger.Trace($"TvDBRateLimiter#{InstanceID}: Time since last request is {delay} ms, throttling for {currentDelay} ms.");
                Thread.Sleep(currentDelay);

                logger.Trace($"TvDBRateLimiter#{InstanceID}: Sending TvDB command.");
                _requestWatch.Restart();
            }
        }
    }
}
