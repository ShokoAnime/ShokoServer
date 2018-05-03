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
        private static int ShortDelay = 150;
        private static Stopwatch _requestWatch = new Stopwatch();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AniDbImageRateLimiter()
        {
            _requestWatch.Start();
        }

        public static AniDbImageRateLimiter Instance => instance;

        private AniDbImageRateLimiter()
        {
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

                if (delay > ShortDelay)
                {
                    Logger.Trace($"Time since last request is {delay} ms, not throttling.");
                    _requestWatch.Restart();
                    return;
                }

                Logger.Trace($"Time since last request is {delay} ms, throttling for {ShortDelay}.");
                Thread.Sleep(ShortDelay);

                Logger.Trace("Sending AniDB command.");
                _requestWatch.Restart();
            }
        }
    }
}
