using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.UDP;

public class UDPRateLimiter : AniDBRateLimiter
{
    public UDPRateLimiter(ILogger<UDPRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
        : base(logger, settingsProvider, s => s.AniDb.UDPRateLimit) { }
}
