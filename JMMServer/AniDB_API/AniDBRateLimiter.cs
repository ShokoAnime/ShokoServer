using System;
using System.Threading;
using NLog;

namespace JMMServer.AniDB_API
{
    public class AniDBRateLimiter
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        // Short Term:
        // A Client MUST NOT send more than 0.5 packets per second(that's one packet every two seconds, not two packets a second!)
        // The server will start to enforce the limit after the first 5 packets have been received.
        private static int ShortDelay = 2500;

        // Long Term:
        // A Client MUST NOT send more than one packet every four seconds over an extended amount of time.
        // An extended amount of time is not defined. Use common sense.
        private static int LongDelay = 4500;

        // Switch to longer delay after 1 hour
        private static TimeSpan shortPeriod = new TimeSpan(1,0,0);

        // Switch to shorter delay after 30 minutes of inactivity
        private static TimeSpan resetPeriod = new TimeSpan(0, 30, 0);

        private static DateTime firstRequest;

        private static DateTime lastRequest;

        private static AniDBRateLimiter instance = null;

        public static AniDBRateLimiter GetInstance()
        {
            if (instance != null) return instance;
            instance = new AniDBRateLimiter();

            return instance;
        }

        private AniDBRateLimiter()
        {
            resetRate();
        }

        public void resetRate()
        {
            firstRequest = lastRequest = DateTime.Now;
            logger.Trace("Rate is reset.");
        }

        public void ensureRate()
        {
            TimeSpan delay = DateTime.Now - lastRequest;
            TimeSpan activeTime = DateTime.Now - firstRequest;
            lastRequest = DateTime.Now;

            if (delay > resetPeriod) {
                resetRate();
                activeTime = DateTime.Now - firstRequest;
            }

            int currentDelay = activeTime > shortPeriod ? LongDelay : ShortDelay;

            if (delay.TotalMilliseconds > currentDelay) {
                logger.Trace(String.Format("Time since last request is {0} ms, not throttling.", delay.TotalMilliseconds));
                return;
            }

            logger.Trace(String.Format("Time since last request is {0} ms, throttling for {1}.", delay.TotalMilliseconds, currentDelay));
            Thread.Sleep(currentDelay);

            logger.Trace("Sending AniDB command.");
        }
    }
}
