using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using Shoko.Commons.Properties;
using Shoko.Plugin.Abstractions;
using Shoko.Server.API;
using Shoko.Server.Commands;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Server;

public class Startup
{
    private const string SentryDsn = "https://47df427564ab42f4be998e637b3ec45a@o330862.ingest.sentry.io/1851880";
    private readonly ILogger<Startup> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private IWebHost _webHost;

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
            services.AddSingleton<ShokoServer>();
            services.AddSingleton(_ => ShokoService.CmdProcessorGeneral);
            services.AddSingleton<LogRotator>();
            services.AddSingleton<TraktTVHelper>();
            services.AddSingleton<TvDBApiHelper>();
            services.AddSingleton<MovieDBHelper>();
            services.AddScoped<CommonImplementation>();
            services.AddSingleton<IShokoEventHandler>(ShokoEventHandler.Instance);
            services.AddAniDB();
            services.AddCommands();
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
            _logger.LogError(e, "An error occurred starting the server: {Ex}", e);
        }

        var settings = _settingsProvider?.GetSettings();
        if (settings?.FirstRun is false)
            ShokoServer.RunWorkSetupDB();
        else
            _logger.LogWarning("The Server is NOT STARTED. It needs to be configured via webui or the settings.json");
    }

    private bool StartWebHost(ISettingsProvider settingsProvider)
    {
        try
        {
            _webHost ??= InitWebHost(settingsProvider);
            _webHost.Start();
            return true;
        }
        catch (Exception e)
        {
            Utils.ShowErrorMessage("Unable to start hosting. Check the logs");
            _logger.LogError(e, "Error was: {Ex}", e);
            StopHost();
            ShokoEventHandler.Instance.OnShutdown();
        }

        return false;
    }

    private IWebHost InitWebHost(ISettingsProvider settingsProvider)
    {
        if (_webHost != null) return _webHost;

        var settings = settingsProvider.GetSettings();
        var result = new WebHostBuilder().UseKestrel(options =>
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
            .UseSentry(
                o =>
                {
                    o.Release = Utils.GetApplicationVersion();
                    o.Dsn = SentryDsn;
                })
            .Build();
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
