using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Quartz;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.TrayService;

public partial class App : Application
{
    private ILogger _logger = null!;

    private TrayIcon? _trayIcon;

    private SystemService? _systemService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        Console.CancelKeyPress += OnConsoleOnCancelKeyPress;
        InitialiseTrayIcon();

        try
        {
            UnhandledExceptionManager.AddHandler();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        Utils.SetInstance();
        Utils.InitLogger();
        var logFactory = LoggerFactory.Create(o => o.AddNLog());
        _logger = logFactory.CreateLogger("Main");

        try
        {
            _systemService = new SystemService(logFactory, false);
            _systemService.Shutdown += (_, _) => DispatchShutdown();
            if (!_systemService.StartAsync().ConfigureAwait(true).GetAwaiter().GetResult())
                DispatchShutdown();
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "The server has failed to start");
            DispatchShutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitialiseTrayIcon()
    {
        var menu = new NativeMenu();

        var openWebUI = new NativeMenuItem("Open WebUI");
        openWebUI.Click += OnTrayOpenWebUIClick;
        menu.Add(openWebUI);

        var exit = new NativeMenuItem("Exit");
        exit.Click += OnTrayExit;
        menu.Add(exit);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Shoko Server",
            Menu = menu,
            IsVisible = true
        };

        try
        {
            var uri = new Uri("avares://ShokoServer/db.ico");
            using var assetStream = AssetLoader.Open(uri);
            _trayIcon.Icon = new WindowIcon(assetStream);
        }
        catch (Exception)
        {
            // Icon loading may fail on some platforms; continue without icon
        }
    }

    private void OnTrayOpenWebUIClick(object? sender, EventArgs args)
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

    private void OnTrayExit(object? sender, EventArgs args)
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
            Dispatcher.UIThread.Invoke(() =>
            {
                _trayIcon?.IsVisible = false;

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown(_systemService?.RestartPending ?? false ? 140 /* Custom restart exit code */ : 0);
            });
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
