#region
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NLog;
using Shoko.Server.Commands;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
#endregion
namespace Shoko.CLI;

public class Worker : BackgroundService
{
    private static readonly Logger                    Logger = LogManager.GetCurrentClassLogger();
    private readonly        IHostApplicationLifetime? _appLifetime;

    public Worker() { }
    public Worker(IHostApplicationLifetime lifetime) => _appLifetime = lifetime;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SetInstanceIfNeeded();
        InitInstanceLogger();
        LoadSettings();
        if (ServerCouldNotStart())
            return Task.CompletedTask;
        EnsureAniDBSocketInitialized();
        AddEventHandlers();
        return Task.CompletedTask;
    }

    /**
     * Ensure that the AniDB socket is initialized. Try to Login, then start the server if successful.
     */
    private static void EnsureAniDBSocketInitialized()
    {
        if (ServerSettings.Instance.FirstRun is false)
            ShokoServer.RunWorkSetupDB();
        else
            Logger.Warn("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");
    }

    private static bool ServerCouldNotStart() => ShokoServer.Instance.StartUpServer() is false;

    private static void InitInstanceLogger() => ShokoServer.Instance.InitLogger();
    
    private static string? GetInstanceFromCommandLineArguments()
    {
        const int notFound = -1;
        var       args     = Environment.GetCommandLineArgs();
        var       idx      = Array.FindIndex(args, x => string.Equals(x, "instance", StringComparison.InvariantCultureIgnoreCase));
        if (idx is notFound)
            return null;
        if (idx >= args.Length - 1)
            return null;
        return args[idx + 1];
    }

    private static void SetInstanceIfNeeded()
    {
        var instance = GetInstanceFromCommandLineArguments();
        if (string.IsNullOrWhiteSpace(instance) is false)
            ServerSettings.DefaultInstance = instance;
    }

    private static void LoadSettings()
    {
        ServerSettings.LoadSettings();
        ServerState.Instance.LoadSettings();
    }

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