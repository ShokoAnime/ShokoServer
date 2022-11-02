#region
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using NLog;
using Shoko.Server.Commands;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
#endregion
namespace Shoko.TrayService;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private static TaskbarIcon? _icon;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private void OnStartup(object a, StartupEventArgs e)
    {
        Console.CancelKeyPress += OnConsoleOnCancelKeyPress;
        InitialiseTaskbarIcon();
        try
        {
            UnhandledExceptionManager.AddHandler();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        new StartServer().StartupServer(AddEventHandlers, ServerStart);
    }

    private static bool ServerStart() 
        => ShokoServer.Instance.StartUpServer();

    private void AddEventHandlers()
    {
        ShokoServer.Instance.ServerShutdown += OnInstanceOnServerShutdown;
        Utils.YesNoRequired += OnUtilsOnYesNoRequired;
        ServerState.Instance.PropertyChanged += OnInstanceOnPropertyChanged;
        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnCmdProcessorGeneralOnOnQueueStateChangedEvent;
    }

    private void OnCmdProcessorGeneralOnOnQueueStateChangedEvent(QueueStateEventArgs ev) 
        => Console.WriteLine("General Queue state change: {0}", ev.QueueState.formatMessage());
    
    private static void OnUtilsOnYesNoRequired(object? _, Utils.CancelReasonEventArgs e) 
        => e.Cancel = true;
    
    private void OnInstanceOnServerShutdown(object? o, EventArgs eventArgs) 
        => Shutdown();

    private static void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine("Startup failed! Error message: {0}", ServerState.Instance.StartupFailedMessage);
    }

    private void InitialiseTaskbarIcon()
    {
#pragma warning disable CA1416
        _icon = new TaskbarIcon{
                                   ToolTipText = "Shoko Server",
                                   ContextMenu = CreateContextMenu(),
                                   MenuActivation = PopupActivationMode.All,
                                   Visibility = Visibility.Visible,
                               };
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/ShokoServer;component/db.ico"))?.Stream;
        if (iconStream is not null)
            _icon.Icon = new Icon(iconStream);
#pragma warning restore CA1416
    }
    
    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        var webui = new MenuItem{Header = "Open WebUI"};
        webui.Click += OnWebuiOpenWebUIClick;
        menu.Items.Add(webui);
        webui = new MenuItem{Header = "Exit"};
        webui.Click += OnWebuiExit;
        menu.Items.Add(webui);
        return menu;
    }
    
    private void OnWebuiExit(object? sender, RoutedEventArgs args) 
        => Dispatcher.Invoke(Shutdown);

    private static void OnWebuiOpenWebUIClick(object? sender, RoutedEventArgs args)
    {
        try
        {
            OpenUrl($"http://localhost:{ServerSettings.Instance.ServerPort}");
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    private void OnConsoleOnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
        => Dispatcher.Invoke(() =>
                             {
                                 args.Cancel = true;
                                 Shutdown();
                             }
                            );

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        Logger.Info("Exit was Requested. Shutting Down.");
        ShokoService.CancelAndWaitForQueues();
        if (_icon is not null)
            _icon.Visibility = Visibility.Hidden;
    }

    ///hack because of this: https://github.com/dotnet/corefx/issues/10361
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"){CreateNoWindow = true});
        }
    }
}
