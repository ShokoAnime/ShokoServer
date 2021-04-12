using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.AniDB
{
    public abstract class AniDBRateLimiter
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object _lock = new();

        // Short Term:
        // A Client MUST NOT send more than 0.5 packets per second(that's one packet every two seconds, not two packets a second!)
        // The server will start to enforce the limit after the first 5 packets have been received.
        protected abstract int ShortDelay { get; init; }

        // Long Term:
        // A Client MUST NOT send more than one packet every four seconds over an extended amount of time.
        // An extended amount of time is not defined. Use common sense.
        protected abstract int LongDelay { get; init; }

        // Switch to longer delay after 1 hour
        protected abstract long shortPeriod { get; init; }

        // Switch to shorter delay after 30 minutes of inactivity
        protected abstract long resetPeriod { get; init; }

        private readonly Stopwatch _requestWatch = new();

        private readonly Stopwatch _activeTimeWatch = new();

        public AniDBRateLimiter()
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

                // add 50ms for good measure
                int waitTime = currentDelay - (int) delay + 50;

                Logger.Trace($"Time since last request is {delay} ms, throttling for {waitTime}.");
                Thread.Sleep(waitTime);

                Logger.Trace("Sending AniDB command.");
                _requestWatch.Restart();
            }
        }
    }
}
