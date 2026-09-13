using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.UDP;

public class UDPRateLimiter : AniDbRateLimiter
{
    public UDPRateLimiter(ILogger<UDPRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
        : base(logger, settingsProvider, s => s.AniDb.UDPRateLimit)
    {
    }
}
