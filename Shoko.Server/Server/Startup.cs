using System;
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
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
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
            services.AddSingleton<ISettingsProvider, SettingsProvider>();
            services.AddScoped<AnimeGroupCreator>();
            services.AddSingleton<ShokoServer>();
            services.AddSingleton<LogRotator>();
            services.AddSingleton<TraktTVHelper>();
            services.AddSingleton<TvDBApiHelper>();
            services.AddSingleton<MovieDBHelper>();
            services.AddSingleton<FilterEvaluator>();
            services.AddSingleton<LegacyFilterConverter>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<VideoLocal_PlaceService>();
            services.AddSingleton<IShokoEventHandler>(ShokoEventHandler.Instance);
            services.AddSingleton<IConnectivityMonitor, CloudFlareConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, MicrosoftConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, MozillaConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, WeChatConnectivityMonitor>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddSingleton<SentryInit>();

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

    public void Start()
    {
        try
        {
            _logger.LogInformation("Initializing Web Hosts...");
            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingHosts;
            if (!StartWebHost(_settingsProvider)) return;

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

    private bool StartWebHost(ISettingsProvider settingsProvider)
    {
        try
        {
            _webHost ??= InitWebHost(settingsProvider);
            AboutToStart?.Invoke(null, new ServerAboutToStartEventArgs
            {
                ServiceProvider = Utils.ServiceContainer
            });
            _webHost.Start();
            return true;
        }
        catch (Exception e)
        {
            Utils.ShowErrorMessage(e, "Unable to start hosting. Check the logs");
            StopHost();
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
            }).UseNLog();
        

        var result = builder.Build();
        
        // Init Sentry
        result.Services.GetRequiredService<SentryInit>().Init();
        
        Utils.SettingsProvider = result.Services.GetRequiredService<ISettingsProvider>();
        Utils.ServiceContainer = result.Services;
        return result;
    }

    public void WaitForShutdown()
    {
        _webHost?.WaitForShutdown();
    }

    private void StopHost()
    {
        _webHost?.Dispose();
        _webHost = null;
    }
}
