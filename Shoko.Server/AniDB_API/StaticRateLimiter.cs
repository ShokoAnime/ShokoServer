using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.UDP;

namespace Shoko.Server.AniDB_API
{
    /// <summary>
    /// This is a band-aid class to allow static access to singleton rate limiters for legacy systems
    /// </summary>
    public static class StaticRateLimiter
    {
        public static UDPRateLimiter UDP { get; } = new();
        public static HttpRateLimiter HTTP { get; } = new();
    }
}
