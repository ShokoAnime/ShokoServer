#region
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    private static          TaskbarIcon? _icon;
    private static readonly Logger       Logger = LogManager.GetCurrentClassLogger();

    private void OnStartup(object a, StartupEventArgs e)
    {
        Console.CancelKeyPress += OnConsoleOnCancelKeyPress;
        InitialiseTaskbarIcon();
        StartServer.StartupServer(AddEventHandlers, ServerStart);
    }

    private static bool ServerStart() 
        => ShokoServer.Instance.StartUpServer();

    private void AddEventHandlers()
    {
        ShokoServer.Instance.ServerShutdown                       += OnInstanceOnServerShutdown;
        Utils.YesNoRequired                                       += OnUtilsOnYesNoRequired;
        ServerState.Instance.PropertyChanged                      += OnInstanceOnPropertyChanged;
        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnCmdProcessorGeneralOnOnQueueStateChangedEvent;
    }
    
    private void OnCmdProcessorGeneralOnOnQueueStateChangedEvent(QueueStateEventArgs ev) 
        => Console.WriteLine($"General Queue state change: {ev.QueueState.formatMessage()}");
    
    private static void OnUtilsOnYesNoRequired(object? _, Utils.CancelReasonEventArgs e) 
        => e.Cancel = true;
    
    private void OnInstanceOnServerShutdown(object? o, EventArgs eventArgs) 
        => Shutdown();

    private static void OnInstanceOnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StartupFailedMessage" && ServerState.Instance.StartupFailed)
            Console.WriteLine("Startup failed! Error message: " + ServerState.Instance.StartupFailedMessage);
    }

    private void InitialiseTaskbarIcon()
    {
        _icon = new TaskbarIcon{
                                   ToolTipText = "Shoko Server", ContextMenu = CreateContextMenu(), MenuActivation = PopupActivationMode.All, Visibility = Visibility.Visible,
                               };
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/ShokoServer;component/db.ico"))?.Stream;
        if (iconStream is not null)
            _icon.Icon = new Icon(iconStream);
    }
    
    private ContextMenu CreateContextMenu()
    {
        var menu  = new ContextMenu();
        var webui = new MenuItem{Header = "Open WebUI"};
        webui.Click += OnWebuiOpenWebUIClick;
        menu.Items.Add(webui);
        webui       =  new MenuItem{Header = "Exit"};
        webui.Click += OnWebuiExit;
        menu.Items.Add(webui);
        return menu;
    }
    
    private void OnWebuiExit(object? sender, RoutedEventArgs args) 
        => Dispatcher.Invoke(Shutdown);

    private static void OnWebuiOpenWebUIClick(object? sender, RoutedEventArgs args)
    {
        string? ip = null;
        try
        {
            ip = GetLocalIPv6();
        }
        catch (Exception) { /*ok no IPv6 available.*/ }

        if (string.IsNullOrWhiteSpace(ip))
            ip = GetLocalIpv4OrFallback();
        else
            ip = $"[{ip}]";
        
        var url = $"http://{ip}:{ServerSettings.Instance.ServerPort}";
        try
        {
            OpenUrl(url);
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    private static string GetLocalIpv4OrFallback()
    {
        string? ip;
        try
        {
            ip = GetLocalIPv4();
        }
        catch (Exception e)
        {
            ip = null;
            Logger.Error(e);
        }
        if (string.IsNullOrWhiteSpace(ip))
            ip = "127.0.0.1";
        return ip;
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

    private static string? GetLocalIPv4()                          
        => GetLocalIP(AddressFamily.InterNetwork);
    
    private static string? GetLocalIPv6()                          
        => GetLocalIP(AddressFamily.InterNetworkV6);
    
    private static string? GetLocalIP(AddressFamily addressFamily) 
        => Dns.GetHostEntry(Dns.GetHostName(), addressFamily).AddressList.FirstOrDefault()?.ToString();
}