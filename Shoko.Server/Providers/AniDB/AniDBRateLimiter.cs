using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.AniDB
{
    public sealed class AniDBRateLimiter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static AniDBRateLimiter _udp;
        private readonly object _lock = new();

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

        private readonly Stopwatch _requestWatch = new();

        private readonly Stopwatch _activeTimeWatch = new();

        public static AniDBRateLimiter UDP => _udp ??= new();

        private AniDBRateLimiter()
        {
            _requestWatch.Start();
            _activeTimeWatch.Start();
        }

        public void ResetRate()
        {
            long elapsedTime = _activeTimeWatch.ElapsedMilliseconds;
            _activeTimeWatch.Restart();
            Logger.Trace($"Rate is reset. Active time was {elapsedTime} ms.");
        }

        public void Reset()
        {
            _requestWatch.Restart();
        }

        public void EnsureRate()
        {
            lock (_lock)
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

                int waitTime = currentDelay - (int) delay + 25;

                Logger.Trace($"Time since last request is {delay} ms, throttling for {waitTime}.");
                Thread.Sleep(waitTime);

                Logger.Trace("Sending AniDB command.");
                _requestWatch.Restart();
            }
        }
    }
}
