using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;

using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Providers.AniDB.UDP;

public class UDPRateLimiter : AniDBRateLimiter
{
    public UDPRateLimiter(ILogger<UDPRateLimiter> logger, ISettingsProvider settingsProvider, IShokoEventHandler eventHandler)
        : base(logger, settingsProvider, eventHandler, s => s.AniDb.UDPRateLimit) { }
}
