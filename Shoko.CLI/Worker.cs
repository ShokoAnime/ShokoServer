#region
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Shoko.Server.Commands;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
#endregion
namespace Shoko.CLI;

public class Worker : BackgroundService
{
    private readonly        IHostApplicationLifetime? _appLifetime;
    public Worker() { }
    public Worker(IHostApplicationLifetime lifetime) => _appLifetime = lifetime;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartServer.StartupServer(AddEventHandlers, ServerCouldStart);
        return Task.CompletedTask;
    }
    
    private static bool ServerCouldStart() => ShokoServer.Instance.StartUpServer();

    private void AddEventHandlers()
    {
        ShokoServer.Instance.ServerShutdown                       += OnInstanceOnServerShutdown;
        Utils.YesNoRequired                                       += OnUtilsOnYesNoRequired;
        ServerState.Instance.PropertyChanged                      += OnInstanceOnPropertyChanged;
        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnCmdProcessorGeneralOnOnQueueStateChangedEvent;
    }

    private static void OnCmdProcessorGeneralOnOnQueueStateChangedEvent(QueueStateEventArgs ev) 
        => Console.WriteLine($"General Queue state change: {ev.QueueState.formatMessage()}");

    private static void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine("Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
    }

    private static void OnUtilsOnYesNoRequired(object? _, Utils.CancelReasonEventArgs e) => e.Cancel = true;

    private void OnInstanceOnServerShutdown(object? _, EventArgs eventArgs) => _appLifetime?.StopApplication();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        ShokoService.CancelAndWaitForQueues();
        await base.StopAsync(cancellationToken);
    }
}