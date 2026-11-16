using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using NLog;
using NLog.Config;
using Shoko.Server.API.SignalR.NLog;

namespace Shoko.Server.API.SignalR;

public class LoggingEmitter : IDisposable
{
    private IHubContext<LoggingHub> Hub { get; set; }
    public SignalRTarget? Target { get; set; }

    public LoggingEmitter(IHubContext<LoggingHub> hub)
    {
        Hub = hub;
        Attach(LogManager.Configuration);
        // A replaced logging configuration brings its own target, so follow it rather than keep listening to the old one.
        LogManager.ConfigurationChanged += OnConfigurationChanged;
    }

    public void Dispose()
    {
        LogManager.ConfigurationChanged -= OnConfigurationChanged;
        Attach(null);
    }

    private void OnConfigurationChanged(object? sender, LoggingConfigurationChangedEventArgs e)
        => Attach(e.ActivatedConfiguration);

    private void Attach(LoggingConfiguration? configuration)
    {
        if (Target != null)
            Target.LogEventHandler -= OnLog;

        Target = configuration?.AllTargets?.Select(a => a as SignalRTarget).FirstOrDefault(a => a != null);
        if (Target != null)
            Target.LogEventHandler += OnLog;
    }

    public async void OnLog(LogEvent e)
    {
        await Hub.Clients.All.SendAsync(Target!.LogMethodName, e);
    }
}
