using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.AniDB
{
    public sealed class AniDbRateLimiter
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly AniDbRateLimiter instance = new AniDbRateLimiter();

        // Short Term:
        // A Client MUST NOT send more than 0.5 packets per second(that's one packet every two seconds, not two packets a second!)
        // The server will start to enforce the limit after the first 5 packets have been received.
        private static int ShortDelay = 2500;

        // Long Term:
        // A Client MUST NOT send more than one packet every four seconds over an extended amount of time.
        // An extended amount of time is not defined. Use common sense.
        private static int LongDelay = 4500;

        // Switch to longer delay after 1 hour
        private static long shortPeriod = 60 * 60 * 1000;

        // Switch to shorter delay after 30 minutes of inactivity
        private static long resetPeriod = 30 * 60 * 1000;

        private static Stopwatch _requestWatch = new Stopwatch();

        private static Stopwatch _activeTimeWatch = new Stopwatch();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AniDbRateLimiter()
        {
            _requestWatch.Start();
            _activeTimeWatch.Start();
        }

        public static AniDbRateLimiter Instance => instance;

        private AniDbRateLimiter()
        {
        }

        public void ResetRate()
        {
            long elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
            _activeTimeWatch.Restart();
            logger.Trace($"Rate is reset. Active time was {elapsedTime} ms.");
        }

        public void Reset()
        {
            _requestWatch.Restart();
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
                    logger.Trace($"Time since last request is {delay} ms, not throttling.");
                    _requestWatch.Restart();
                    return;
                }

                logger.Trace($"Time since last request is {delay} ms, throttling for {currentDelay}.");
                Thread.Sleep(currentDelay);

                logger.Trace("Sending AniDB command.");
                _requestWatch.Restart();
            }
        }
    }
}
