#region
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Quartz;
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
    private DispatcherTimer? _tooltipTimer;
    private ILogger _logger = null!;

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

        if (Mutex.TryOpenExisting(Utils.DefaultInstance + "Mutex", out _)) Shutdown();

        Utils.SetInstance();
        Utils.InitLogger();
        var logFactory = LoggerFactory.Create(o => o.AddNLog());
        _logger = logFactory.CreateLogger("Main");

        try
        {
            var settingsProvider = new SettingsProvider(logFactory.CreateLogger<SettingsProvider>());
            settingsProvider.LoadSettings();
            Utils.SettingsProvider = settingsProvider;
            var startup = new Startup(logFactory.CreateLogger<Startup>(), settingsProvider);
            startup.AboutToStart += (_, _) => AddEventHandlers();
            startup.Start().ConfigureAwait(true).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "The server has failed to start");
            Shutdown();
        }
    }

    private void AddEventHandlers()
    {
        ShokoEventHandler.Instance.Shutdown += (_, _) => DispatchShutdown();
    }

    private void InitialiseTaskbarIcon()
    {
        ApplySystemTooltipTheme();
        _icon = new TaskbarIcon{
                                   ToolTipText = "Shoko Server",
                                   ContextMenu = CreateContextMenu(),
                                   MenuActivation = PopupActivationMode.RightClick,
                                   Visibility = Visibility.Visible
                               };
        _icon.TrayToolTipOpen += OnTrayToolTipOpen;
        _icon.TrayToolTipClose += OnTrayToolTipClose;
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/ShokoServer;component/db.ico"))?.Stream;
        if (iconStream is not null)
            _icon.Icon = new Icon(iconStream);
    }

    // Hardcodet fires NIN_POPUPOPEN after the hover delay even when the cursor has already
    // left, because NIN_POPUPCLOSE arrives first (while the popup wasn't open yet) and is
    // treated as a no-op. We mirror the system default tooltip duration so the popup
    // auto-closes if Hardcodet never receives a matching NIN_POPUPCLOSE.
    private void OnTrayToolTipOpen(object sender, RoutedEventArgs e)
    {
        _tooltipTimer?.Stop();
        _tooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _tooltipTimer.Tick += (_, _) =>
        {
            _tooltipTimer?.Stop();
            _tooltipTimer = null;
            if (_icon?.TrayToolTipResolved is { IsOpen: true } tooltip)
                tooltip.IsOpen = false;
        };
        _tooltipTimer.Start();
    }

    private void OnTrayToolTipClose(object sender, RoutedEventArgs e)
    {
        _tooltipTimer?.Stop();
        _tooltipTimer = null;
    }

    // Hardcodet shows a WPF ToolTip popup rather than the native Win32 tooltip, so it
    // ignores the system dark/light theme. Mirror the system tooltip colors so the popup
    // matches the rest of the taskbar.
    private void ApplySystemTooltipTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var isDark = key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0;
            if (!isDark)
                return;

            var style = new Style(typeof(ToolTip));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Colors.White)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x56, 0x56, 0x56))));
            Resources[typeof(ToolTip)] = style;
        }
        catch
        {
            // non-critical, fall back to default WPF tooltip appearance
        }
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        var webui = new MenuItem{Header = "Open WebUI"};
        webui.Click += OnTrayOpenWebUIClick;
        menu.Items.Add(webui);
        webui = new MenuItem{Header = "Exit"};
        webui.Click += OnTrayExit;
        menu.Items.Add(webui);
        return menu;
    }

    private void OnTrayOpenWebUIClick(object? sender, RoutedEventArgs args)
    {
        try
        {
            var settings = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>().GetSettings();
            OpenUrl($"http://localhost:{settings.Web.Port}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to Open WebUI");
        }
    }

    private void OnTrayExit(object? sender, RoutedEventArgs args)
    {
        var lifetime = Utils.ServiceContainer.GetRequiredService<IHostApplicationLifetime>();
        Task.Run(() => lifetime.StopApplication());
        // stupid
        Task.Run(async () => await ShutdownQuartz());
    }

    private async Task ShutdownQuartz()
    {
        var quartz = Utils.ServiceContainer.GetServices<IHostedService>().FirstOrDefault(a => a is QuartzHostedService);
        if (quartz == null)
        {
            _logger.LogError("Could not get QuartzHostedService");
            return;
        }

        await quartz.StopAsync(default);
    }

    private void OnConsoleOnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        DispatchShutdown();
    }

    private void DispatchShutdown()
    {
        try
        {
            Dispatcher.Invoke(Shutdown);
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

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
