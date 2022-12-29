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
    private readonly IHostApplicationLifetime? _appLifetime;
    private readonly StartServer _startServer;
    private readonly ShokoServer _shokoServer;
    
    public Worker() { }
    public Worker(IHostApplicationLifetime lifetime, StartServer startServer, ShokoServer shokoServer)
    {
        _appLifetime      = lifetime;
        _startServer = startServer;
        _shokoServer = shokoServer;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startServer.StartupServer(AddEventHandlers, ServerCouldStart);
        return Task.CompletedTask;
    }
    
    private bool ServerCouldStart() => _shokoServer.StartUpServer();

    private void AddEventHandlers()
    {
        Utils.ShokoServer.ServerShutdown                       += OnInstanceOnServerShutdown;
        Utils.YesNoRequired                                       += OnUtilsOnYesNoRequired;
        ServerState.Instance.PropertyChanged                      += OnInstanceOnPropertyChanged;
        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnCmdProcessorGeneralOnQueueStateChangedEvent;
        ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += OnCmdProcessorImagesOnQueueStateChangedEvent;
        ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += OnCmdProcessorHasherOnQueueStateChangedEvent;
    }

    private void OnCmdProcessorGeneralOnQueueStateChangedEvent(QueueStateEventArgs ev) 
        => Console.WriteLine($@"General Queue state change: {ev.QueueState.formatMessage()}");

    private void OnCmdProcessorImagesOnQueueStateChangedEvent(QueueStateEventArgs ev) 
        => Console.WriteLine($@"Images Queue state change: {ev.QueueState.formatMessage()}");

    private void OnCmdProcessorHasherOnQueueStateChangedEvent(QueueStateEventArgs ev) 
        => Console.WriteLine($@"Hasher Queue state change: {ev.QueueState.formatMessage()}");

    private void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine("Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
    }

    private void OnUtilsOnYesNoRequired(object? _, Utils.CancelReasonEventArgs e) => e.Cancel = true;

    private void OnInstanceOnServerShutdown(object? _, EventArgs eventArgs) => _appLifetime?.StopApplication();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        ShokoService.CancelAndWaitForQueues();
        await base.StopAsync(cancellationToken);
    }
}
