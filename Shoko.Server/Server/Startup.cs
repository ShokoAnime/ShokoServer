using System;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Plugin;
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

#nullable enable
namespace Shoko.Server.Server;

public class Startup
{
    private readonly ILogger<Startup> _logger;

    private readonly IPluginManager _pluginManager;

    private readonly ConfigurationService _configurationService;

    private readonly SettingsProvider _settingsProvider;

    private IWebHost? _webHost;

    public event EventHandler<ServerAboutToStartEventArgs>? AboutToStart;

    public Startup(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Startup>();
        _pluginManager = new PluginManager();
        _configurationService = new ConfigurationService(loggerFactory.CreateLogger<ConfigurationService>(), ApplicationPaths.Instance, _pluginManager);
        _settingsProvider = new SettingsProvider(loggerFactory.CreateLogger<SettingsProvider>(), _configurationService.CreateProvider<ServerSettings>());
    }

    // tried doing it without UseStartup<ServerStartup>(), but the documentation is lacking, and I couldn't get Configure() to work otherwise
    private class ServerStartup(IConfigurationService configurationService, ISettingsProvider settingsProvider, IPluginManager pluginManager)
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(configurationService);
            services.AddSingleton(settingsProvider);
            services.AddSingleton(pluginManager);

            services.AddSingleton<IRelocationService, RelocationService>();
            services.AddSingleton<RenameFileService>();
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
            services.AddSingleton<CssThemeService>();
            services.AddSingleton<WebUIUpdateService>();
            services.AddSingleton<IShokoEventHandler>(ShokoEventHandler.Instance);
            services.AddSingleton<IApplicationPaths>(ApplicationPaths.Instance);
            services.AddSingleton<IMetadataService, AbstractMetadataService>();
            services.AddSingleton<IVideoService, AbstractVideoService>();
            services.AddSingleton<IVideoReleaseService, AbstractVideoReleaseService>();
            services.AddSingleton<IVideoHashingService, AbstractVideoHashingService>();
            services.AddSingleton(typeof(ConfigurationProvider<>));
            services.AddSingleton<IUserService, AbstractUserService>();
            services.AddSingleton<IUserDataService, AbstractUserDataService>();
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
            services.AddSingleton<IAniDBService, AbstractAnidbService>();
            services.AddPlugins(settingsProvider);
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
            Utils.SettingsProvider = _settingsProvider;

            // Set default options for MessagePack
            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
            MessagePackSerializer.Typeless.DefaultOptions = MessagePackSerializer.Typeless.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            if (!await StartWebHost()) return;

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

    private async Task<bool> StartWebHost()
    {
        try
        {
            _webHost ??= InitWebHost();
            AboutToStart?.Invoke(null, new ServerAboutToStartEventArgs
            {
                ServiceProvider = Utils.ServiceContainer
            });

            _logger.LogInformation("Starting Web Hosts.");
            ServerState.Instance.ServerStartingStatus = "Starting Web Hosts.";
            await _webHost.StartAsync();
            _logger.LogInformation("Web Hosts started.");
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

    private IWebHost InitWebHost()
    {
        if (_webHost != null) return _webHost;

        _logger.LogInformation("Initializing Web Hosts.");
        ServerState.Instance.ServerStartingStatus = "Initializing Web Hosts.";
        var settings = _settingsProvider.GetSettings();
        var builder = new WebHostBuilder()
            .UseKestrel(options =>
            {
                options.ListenAnyIP(settings.Web.Port);
            })
            .ConfigureApp()
            .ConfigureServiceProvider()
            .UseStartup(_ => new ServerStartup(_configurationService, _settingsProvider, _pluginManager))
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
#if !LOGWEB
                logging.AddFilter("Microsoft", LogLevel.Warning);
                logging.AddFilter("System", LogLevel.Warning);
                logging.AddFilter("Shoko.Server.API", LogLevel.Warning);
#endif
            })
            .UseNLog()
            .UseSentryConfig();

        var result = builder.Build();

        Utils.ServiceContainer = result.Services;

        // Init. plugins before starting the IHostedService services.
        Loader.InitPlugins(result.Services);

        _logger.LogInformation("Web Hosts initialized.");

        return result;
    }

    public Task WaitForShutdown()
        => _webHost?.WaitForShutdownAsync() ?? Task.CompletedTask;

    private async Task StopHost()
    {
        if (_webHost is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
        else
            _webHost?.Dispose();
        _webHost = null;
    }
}
