using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class HttpRateLimiter : AniDbRateLimiter
{
    public HttpRateLimiter(ILogger<HttpRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
        : base(logger, settingsProvider, s => s.AniDb.HTTPRateLimit)
    {
    }
}
