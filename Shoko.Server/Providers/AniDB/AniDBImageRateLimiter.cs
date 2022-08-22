using System.Diagnostics;
using System.Threading;
using NLog;

namespace Shoko.Server.Providers.AniDB
{
    public sealed class AniDbImageRateLimiter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Short Term rate
        private static int ShortDelay = 150;
        private static Stopwatch _requestWatch = new Stopwatch();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AniDbImageRateLimiter()
        {
            _requestWatch.Start();
        }

        public static readonly AniDbImageRateLimiter Instance = new();

        private AniDbImageRateLimiter()
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
