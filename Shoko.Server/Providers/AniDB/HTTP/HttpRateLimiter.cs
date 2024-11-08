using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class HttpRateLimiter : AniDBRateLimiter
{
    public HttpRateLimiter(ILogger<HttpRateLimiter> logger, ISettingsProvider settingsProvider, IShokoEventHandler eventHandler)
        : base(logger, settingsProvider, eventHandler, s => s.AniDb.HTTPRateLimit) { }
}
