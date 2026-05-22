using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
using Quartz;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.TrayService;

public partial class App : Application
{
    private ILogger? _logger;

    private TrayIcon? _trayIcon;

    private SystemService? _systemService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Console.CancelKeyPress += OnConsoleOnCancelKeyPress;
        InitialiseTrayIcon();

        _systemService = new SystemService();
        _systemService.Shutdown += (_, _) => DispatchShutdown();
        var host = _systemService.StartAsync()
            .ConfigureAwait(true)
            .GetAwaiter()
            .GetResult();
        if (host is null)
        {
            // Exit after 1s if we failed to start properly.
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                DispatchShutdown();
            });
        }
        else
        {
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger("Main");
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
            _logger?.LogError(e, "Failed to Open WebUI");
        }
    }

    private void OnTrayExit(object? sender, EventArgs args)
        => DispatchShutdown();

    /// <summary>
    /// Signal to Quartz to shutdown, since it's not ran properly signalled by the application's lifetime
    /// when running in a desktop application.
    /// </summary>
    private async Task ShutdownQuartz()
    {
        var quartz = Utils.ServiceContainer.GetServices<IHostedService>().FirstOrDefault(a => a is QuartzHostedService);
        if (quartz == null)
        {
            _logger?.LogError("Could not get QuartzHostedService");
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
        // stupid
        Task.Run(ShutdownQuartz);

        try
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                    return;

                _trayIcon?.IsVisible = false;

                var exitCode = 0;
                if (_systemService?.RestartPending ?? false)
                    exitCode = 140; // Custom restart exit code
                else if (_systemService?.StartupFailedException is not null)
                    exitCode = 1;
                desktop.Shutdown(exitCode);
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
