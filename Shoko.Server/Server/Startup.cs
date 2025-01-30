using System;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using Shoko.Commons.Properties;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Services;
using Shoko.Server.Services.Connectivity;
using Shoko.Server.Services.ErrorHandling;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Server;

public class Startup
{
    private readonly ILogger<Startup> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private IWebHost _webHost;
    public event EventHandler<ServerAboutToStartEventArgs> AboutToStart;

    public Startup(ILogger<Startup> logger, ISettingsProvider settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
    }

    // tried doing it without UseStartup<ServerStartup>(), but the documentation is lacking, and I couldn't get Configure() to work otherwise
    private class ServerStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IRelocationService, RelocationService>();
            services.AddSingleton<RenameFileService>();
            services.AddSingleton<ISettingsProvider, SettingsProvider>();
            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<ShokoServer>();
            services.AddSingleton<LogRotator>();
            services.AddSingleton<TraktTVHelper>();
            services.AddSingleton<TmdbImageService>();
            services.AddSingleton<TmdbLinkingService>();
            services.AddSingleton<TmdbMetadataService>();
            services.AddSingleton<TmdbSearchService>();
            services.AddSingleton<FilterEvaluator>();
            services.AddSingleton<LegacyFilterConverter>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<AniDB_AnimeService>();
            services.AddSingleton<AnimeEpisodeService>();
            services.AddSingleton<AnimeSeriesService>();
            services.AddSingleton<AnimeGroupService>();
            services.AddSingleton<VideoLocalService>();
            services.AddSingleton<VideoLocal_PlaceService>();
            services.AddSingleton<WatchedStatusService>();
            services.AddSingleton<CssThemeService>();
            services.AddSingleton<WebUIUpdateService>();
            services.AddSingleton<IShokoEventHandler>(ShokoEventHandler.Instance);
            services.AddSingleton<IConnectivityMonitor, CloudFlareConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, MicrosoftConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, MozillaConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, WeChatConnectivityMonitor>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddScoped<AnimeGroupCreator>();

            services.AddRepositories();
            services.AddSentry();
            services.AddQuartz();

            services.AddAniDB();
            services.AddPlugins();
            services.AddAPI();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAPI();
            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() => ShokoEventHandler.Instance.OnShutdown());
        }
    }

    public async Task Start()
    {
        try
        {
            // Set default options for MessagePack
            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
            MessagePackSerializer.Typeless.DefaultOptions = MessagePackSerializer.Typeless.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            _logger.LogInformation("Initializing Web Hosts...");
            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingHosts;
            if (!await StartWebHost(_settingsProvider)) return;

            var shokoServer = Utils.ServiceContainer.GetRequiredService<ShokoServer>();
            Utils.ShokoServer = shokoServer;
            if (!shokoServer.StartUpServer())
                return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred starting the server");
        }

        var settings = _settingsProvider?.GetSettings();
        if (settings?.FirstRun is false)
            Utils.ShokoServer.RunWorkSetupDB();
        else
            _logger.LogWarning("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");
    }

    private async Task<bool> StartWebHost(ISettingsProvider settingsProvider)
    {
        try
        {
            _webHost ??= InitWebHost(settingsProvider);
            AboutToStart?.Invoke(null, new ServerAboutToStartEventArgs
            {
                ServiceProvider = Utils.ServiceContainer
            });
            await _webHost.StartAsync();
            return true;
        }
        catch (Exception e)
        {
            Utils.ShowErrorMessage(e, "Unable to start hosting. Check the logs");
            await StopHost();
            ShokoEventHandler.Instance.OnShutdown();
        }

        return false;
    }

    private IWebHost InitWebHost(ISettingsProvider settingsProvider)
    {
        if (_webHost != null) return _webHost;

        var settings = settingsProvider.GetSettings();
        var builder = new WebHostBuilder().UseKestrel(options =>
            {
                options.ListenAnyIP(settings.ServerPort);
            })
            .ConfigureApp()
            .ConfigureServiceProvider()
            .UseStartup<ServerStartup>()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
#if !LOGWEB
                logging.AddFilter("Microsoft", LogLevel.Warning);
                logging.AddFilter("System", LogLevel.Warning);
                logging.AddFilter("Shoko.Server.API", LogLevel.Warning);
#endif
            }).UseNLog()
            .UseSentryConfig();

        var result = builder.Build();

        Utils.SettingsProvider = result.Services.GetRequiredService<ISettingsProvider>();
        Utils.ServiceContainer = result.Services;
        return result;
    }

    public Task WaitForShutdown()
    {
        return _webHost?.WaitForShutdownAsync();
    }

    private async Task StopHost()
    {
        if (_webHost is IAsyncDisposable disp) await disp.DisposeAsync();
        else _webHost?.Dispose();
        _webHost = null;
    }
}
