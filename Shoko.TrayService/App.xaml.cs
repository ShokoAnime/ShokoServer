#region
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Shoko.Server;
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
    private ILogger _logger = null!;
    private static App s_instance = null!;

    private void OnStartup(object a, StartupEventArgs e)
    {
        s_instance = this;
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

        Mutex mutex;
        try
        {
            mutex = Mutex.OpenExisting(Utils.DefaultInstance + "Mutex");
            Shutdown();
        }
        catch (Exception ex)
        {
            // since we didn't find a mutex with that name, create one
            Debug.WriteLine("Exception thrown:" + ex.Message + " Creating a new mutex...");
            mutex = new Mutex(true, Utils.DefaultInstance + "Mutex");
        }

        Utils.SetInstance();
        Utils.InitLogger();
        var logFactory = new LoggerFactory().AddNLog();
        _logger = logFactory.CreateLogger("App.xaml");
        var settingsProvider = new SettingsProvider(logFactory.CreateLogger<SettingsProvider>());
        Utils.SettingsProvider = settingsProvider;
        var startup = new Startup(logFactory.CreateLogger<Startup>(), settingsProvider);
        startup.Start();
        AddEventHandlers();
    }

    private void AddEventHandlers()
    {
        ShokoEventHandler.Instance.Shutdown += OnInstanceOnServerShutdown;
        Utils.YesNoRequired += (_, args) => args.Cancel = true;
    }

    public static void OnInstanceOnServerShutdown(object? o, EventArgs eventArgs) 
        => s_instance.Shutdown();

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

    private void OnWebuiOpenWebUIClick(object? sender, RoutedEventArgs args)
    {
        try
        {
            var settings = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>().GetSettings();
            OpenUrl($"http://localhost:{settings.ServerPort}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to Open WebUI");
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
        _logger.LogInformation("Exit was Requested. Shutting Down");
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
