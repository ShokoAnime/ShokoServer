using System;
using Microsoft.Extensions.Logging;
using Shoko.Server.Settings;

namespace Shoko.Server.Server;
#nullable enable
public class StartServer
{
    private readonly ILogger<StartServer> _logger;
    private readonly ISettingsProvider _settingsProvider;

    public StartServer(ILogger<StartServer> logger, ISettingsProvider settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
    }

    public void StartupServer(Action? addEventHandlers, Func<bool> serverStart)
    {
        if (serverStart?.Invoke() == false)
            return;
        EnsureAniDBSocketInitialized();
        addEventHandlers?.Invoke();
    }

    ///Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
    private void EnsureAniDBSocketInitialized()
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.FirstRun is false)
            ShokoServer.RunWorkSetupDB();
        else
            _logger.LogWarning("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");
    }
}
#nullable disable
