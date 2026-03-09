using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using Quartz;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Server.API;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Hashing;
using Shoko.Server.MediaInfo;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Server;
using Shoko.Server.Services.Abstraction;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Services.Connectivity;
using Shoko.Server.Services.ErrorHandling;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;
using Trinet.Core.IO.Ntfs;

using AbstractReleaseChannel = Shoko.Abstractions.Core.ReleaseChannel;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;
using Timer = System.Timers.Timer;

#nullable enable
namespace Shoko.Server.Services;

public class SystemService : ISystemService
{
    private readonly ILogger<SystemService> _logger;

    private readonly PluginManager _pluginManager;

    private readonly ConfigurationService _configurationService;

    private readonly SettingsProvider _settingsProvider;

    private readonly CancellationTokenSource _shutdownTokenSource;

    private Timer? _autoUpdateTimer;

    private IHost? _webHost;

    public bool IsStarted => StartedAt.HasValue;

    public bool CanShutdown { get; private init; }

    public bool CanRestart { get; private init; }

    public bool ShutdownPending { get; private set; }

    public bool RestartPending { get; private set; }

    public VersionInformation Version { get; }

    public DateTime BootstrappedAt { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public event EventHandler<ServerAboutToStartEventArgs>? AboutToStart;

    public event EventHandler? Started;

    public event EventHandler<CancelEventArgs>? ShutdownOrRestartRequested;

    public event EventHandler? Shutdown;

    public SystemService(ILoggerFactory loggerFactory)
    {
        var now = DateTime.UtcNow;
        var args = Environment.GetCommandLineArgs();
        var extraVersionDict = Utils.GetApplicationExtraVersion();

        _logger = loggerFactory.CreateLogger<SystemService>();
        _pluginManager = new(loggerFactory.CreateLogger<PluginManager>(), ApplicationPaths.Instance);
        _configurationService = new(loggerFactory, ApplicationPaths.Instance, _pluginManager);
        _settingsProvider = new(loggerFactory.CreateLogger<SettingsProvider>(), this, _configurationService.CreateProvider<ServerSettings>());
        _shutdownTokenSource = new();

        CanShutdown = args.Contains("--shutdown-enabled");
        CanRestart = args.Contains("--restart-enabled");
        ShutdownPending = false;
        RestartPending = false;
        Version = new()
        {
            Version = Utils.GetApplicationVersion(),
            Tag = extraVersionDict.TryGetValue("tag", out var tag) && tag is { Length: > 0 }
                ? tag : null,
            SourceRevision = extraVersionDict.TryGetValue("commit", out var commit) && commit is { Length: > 0 }
                ? commit : null,
            Channel = extraVersionDict.TryGetValue("channel", out var rawChannel) &&
            Enum.TryParse<AbstractReleaseChannel>(rawChannel, true, out var channel)
                ? channel : AbstractReleaseChannel.Debug,
            ReleasedAt = extraVersionDict.TryGetValue("date", out var rawDate) && DateTime.TryParse(rawDate, out var date)
                ? date.ToUniversalTime() : null,
        };
        BootstrappedAt = now;
        StartedAt = null;
    }

    #region Startup

    public async Task<bool> StartAsync()
    {
        try
        {
            // Check if any of the DLL are blocked, common issue with daily builds.
            if (!CheckBlockedFiles())
            {
                Utils.ShowErrorMessage("Blocked DLL files found in server directory!");
                return false;
            }

            Utils.SettingsProvider = _settingsProvider;

            // Set default culture.
            var culture = CultureInfo.GetCultureInfo(_settingsProvider.GetSettings().Culture);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Set default options for MessagePack.
            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
            MessagePackSerializer.Typeless.DefaultOptions = MessagePackSerializer.Typeless.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            // Initialize the server state with new fields for the field tracking.
            ServerState.Instance.DatabaseAvailable = false;
            ServerState.Instance.ServerOnline = false;
            ServerState.Instance.StartupFailed = false;
            ServerState.Instance.StartupFailedMessage = string.Empty;
            ServerState.Instance.ServerStarting = false;

            // Log some basic information about the server before we start.
            _logger.LogInformation(
                "Shoko Server: {ApplicationVersion}, Channel: {Channel}, Tag: {Tag}, Commit: {Commit}",
                Version.Version,
                Version.Channel,
                Version.Tag ?? "null",
                Version.SourceRevision ?? "null"
            );
            _logger.LogInformation("Operating System: {OSInfo}", RuntimeInformation.OSDescription);

            try
            {
                var mediaInfoVersion = MediaInfoUtility.GetVersion();
                mediaInfoVersion ??= "MediaInfo program NOT found";
                _logger.LogInformation("MediaInfo: {version}", mediaInfoVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to read MediaInfo version: {Message}", ex.Message);
            }

            try
            {
                var version = CoreHashProvider.GetRhashVersion();
                version ??= "RHash library NOT found";
                _logger.LogInformation("RHash: {version}", version);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to read RHash version: {Message}", ex.Message);
            }

            try
            {
                _webHost = InitWebHost();

                _logger.LogInformation("Starting Web Host.");
                ServerState.Instance.ServerStartingStatus = "Starting Web Hosts.";
                await _webHost.StartAsync();
                _logger.LogInformation("Web Host started.");
            }
            catch (Exception e)
            {
                Utils.ShowErrorMessage(e, "Unable to start hosting. Check the logs");
                if (_webHost is IAsyncDisposable disposable)
                    await disposable.DisposeAsync();
                else
                    _webHost?.Dispose();
                _webHost = null;
                return false;
            }

            var settings = _settingsProvider.GetSettings();
            if (settings.DumpSettingsOnStart)
                _settingsProvider.DebugSettingsToLog();

            ServerState.Instance.ServerStartingStatus = "Initializing UDP Connection Handler...";
            var udpConnectionHandler = _webHost.Services.GetRequiredService<IUDPConnectionHandler>();
            try
            {
                udpConnectionHandler.Init();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UDP Connection Handler");
            }

            if (!settings.FirstRun)
            {
                LateStart();
            }
            else
            {
                // In case the server is not fully started we need to check the
                // connectivity manually once, since Quartz is not up and
                // running yet, and the AniDB login test requires us to have
                // internet access.
                _ = Task.Run(_webHost.Services.GetRequiredService<IConnectivityService>().CheckAvailability);

                _logger.LogWarning("The Server is NOT STARTED. It needs to be configured via webui or the server-settings.json");
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred starting the server");
            return false;
        }
    }

    private bool CheckBlockedFiles()
    {
        if (!PlatformUtility.IsWindows)
            return true;

        var result = true;
        var dllFiles = Directory.GetFiles(ApplicationPaths.Instance.ApplicationPath, "*.dll", SearchOption.AllDirectories);
        foreach (var dllFile in dllFiles)
        {
            if (FileSystem.AlternateDataStreamExists(dllFile, "Zone.Identifier"))
            {
                try
                {
                    FileSystem.DeleteAlternateDataStream(dllFile, "Zone.Identifier");
                }
                catch
                {
                    // ignored
                }
            }

            if (!FileSystem.AlternateDataStreamExists(dllFile, "Zone.Identifier"))
                continue;

            _logger.LogError("Found blocked DLL file: " + dllFile);
            result = false;
        }

        return result;
    }

    #region Startup | Init. Web Host & Services

    private IHost InitWebHost()
    {
        _logger.LogInformation("Initializing Web Host & Services.");
        ServerState.Instance.ServerStartingStatus = "Initializing Web Host & Services.";
        var settings = _settingsProvider.GetSettings();
        var builder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
                webHostBuilder
                    .UseKestrel(options =>
                    {
                        options.ListenAnyIP(settings.Web.Port);
                    })
                    .ConfigureApp()
                    .ConfigureServiceProvider()
                    .UseStartup(_ => new Startup(this, _configurationService, _settingsProvider, _pluginManager))
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
                    .UseSentryConfig()
            );

        var webHost = builder.Build();

        Utils.ServiceContainer = webHost.Services;

        // Initialize and start the log rotator.
        webHost.Services.GetRequiredService<LogRotator>()
            .Start();

        // Init. plugins before starting the IHostedService services.
        _pluginManager.InitPlugins();

        _logger.LogInformation("Web Host & Services initialized.");

        return webHost;
    }

    private class Startup(SystemService systemService, IConfigurationService configurationService, ISettingsProvider settingsProvider, IPluginManager pluginManager)
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISystemService>(systemService);
            services.AddSingleton(configurationService);
            services.AddSingleton(settingsProvider);
            services.AddSingleton(pluginManager);
            services.AddSingleton(ApplicationPaths.Instance);

            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<LogRotator>();
            services.AddSingleton<TraktTVHelper>();
            services.AddSingleton<TmdbImageService>();
            services.AddSingleton<TmdbLinkingService>();
            services.AddSingleton<TmdbMetadataService>();
            services.AddSingleton<TmdbSearchService>();
            services.AddSingleton<IFilterEvaluator, FilterEvaluator>();
            services.AddSingleton<LegacyFilterConverter>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<AnimeSeriesService>();
            services.AddSingleton<AnimeGroupService>();
            services.AddSingleton<CssThemeService>();
            services.AddSingleton<WebUIUpdateService>();
            services.AddSingleton<IMetadataService, AbstractMetadataService>();
            services.AddSingleton<IVideoService, VideoService>();
            services.AddSingleton<IVideoReleaseService, VideoReleaseService>();
            services.AddSingleton<IVideoHashingService, VideoHashingService>();
            services.AddSingleton<IRelocationService, RelocationService>();
            services.AddSingleton(typeof(ConfigurationProvider<>));
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IUserDataService, UserDataService>();
            services.AddSingleton<IImageManager, AbstractImageManager>();
            services.AddSingleton<IConnectivityMonitor, CloudFlareConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, MicrosoftConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, MozillaConnectivityMonitor>();
            services.AddSingleton<IConnectivityMonitor, WeChatConnectivityMonitor>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddScoped<AnimeGroupCreator>();

            services.AddRepositories();
            services.AddSentryConfig();
            services.AddQuartz(systemService);

            services.AddAniDB();
            services.AddSingleton<IAnidbService, AnidbService>();

            pluginManager.RegisterPlugins(services);

            services.AddAPI(pluginManager);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAPI(pluginManager);
            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(systemService.OnShutdown);
        }
    }

    #endregion

    #region Startup | Late Start

    internal bool LateStart()
    {
        if (StartedAt.HasValue || ServerState.Instance.ServerStarting || ServerState.Instance.StartupFailed)
            return true;
        ServerState.Instance.ServerStarting = true;

        var settings = _settingsProvider.GetSettings();
        try
        {
            ServerState.Instance.ServerOnline = false;
            ServerState.Instance.StartupFailed = false;
            ServerState.Instance.StartupFailedMessage = string.Empty;
            ServerState.Instance.ServerStartingStatus = "Cleaning up...";

            var schedulerFactory = _webHost!.Services.GetRequiredService<ISchedulerFactory>();
            var databaseFactory = _webHost.Services.GetRequiredService<DatabaseFactory>();
            var repoFactory = _webHost.Services.GetRequiredService<RepoFactory>();
            var fileWatcherService = _webHost.Services.GetRequiredService<FileWatcherService>();
            var lifetime = _webHost.Services.GetRequiredService<IHostApplicationLifetime>();
            var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, _shutdownTokenSource.Token).Token;
            if (cancellationToken.IsCancellationRequested)
                return false;

            _logger.LogInformation("Setting up database...");
            ServerState.Instance.ServerStartingStatus = "Setting up database...";
            if (!InitializeDatabase(databaseFactory, repoFactory, out var errorMessage, cancellationToken) && !cancellationToken.IsCancellationRequested)
            {
                ServerState.Instance.DatabaseAvailable = false;
                ServerState.Instance.StartupFailed = true;
                ServerState.Instance.StartupFailedMessage = errorMessage;
                return false;
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            //init session factory
            _logger.LogInformation("Initializing Session Factory...");
            ServerState.Instance.ServerStartingStatus = "Initializing Session Factory...";
            var _ = databaseFactory.SessionFactory;
            ServerState.Instance.DatabaseAvailable = true;

            if (cancellationToken.IsCancellationRequested)
                return false;

            // timer for automatic updates
            _autoUpdateTimer = new Timer
            {
                AutoReset = true,
                Interval = 5 * 60 * 1000, // 5 * 60 seconds (5 minutes)
            };
            _autoUpdateTimer.Elapsed += AutoUpdateTimer_Elapsed;
            _autoUpdateTimer.Start();

            if (cancellationToken.IsCancellationRequested)
                return false;

            ServerState.Instance.ServerStartingStatus = "Initializing File Watchers...";
            fileWatcherService.StartWatchingFiles();

            if (cancellationToken.IsCancellationRequested)
                return false;

            AboutToStart?.Invoke(this, new() { ServiceProvider = _webHost.Services });

            StartedAt = DateTime.UtcNow;

            var scheduler = schedulerFactory.GetScheduler().Result;
            if (settings.Import.ScanDropFoldersOnStart)
                scheduler.StartJob<ScanDropFoldersJob>().GetAwaiter().GetResult();
            if (settings.Import.RunOnStart)
                scheduler.StartJob<ImportJob>().GetAwaiter().GetResult();

            _logger.LogInformation("Starting Server: Complete!");
            ServerState.Instance.ServerStartingStatus = "Complete!";
            ServerState.Instance.ServerOnline = true;
            ServerState.Instance.ServerStarting = false;
            if (settings.FirstRun)
            {
                settings.FirstRun = false;
                _settingsProvider.SaveSettings(settings);
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            Started?.Invoke(this, new());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            ServerState.Instance.ServerStartingStatus = ex.Message;
            ServerState.Instance.StartupFailed = true;
            ServerState.Instance.StartupFailedMessage = $"Startup Failed: {ex}";
            return false;
        }
    }

    #endregion

    #region Startup | Database & Repositories

    private bool InitializeDatabase(DatabaseFactory databaseFactory, RepoFactory repoFactory, out string errorMessage, CancellationToken cancellationToken)
    {
        errorMessage = string.Empty;
        try
        {
            databaseFactory.Instance = null;
            var instance = databaseFactory.Instance;
            if (instance == null)
            {
                errorMessage = "Could not initialize database factory instance";
                return false;
            }

            for (var i = 0; i < 60; i++)
            {
                if (instance.TestConnection())
                {
                    _logger.LogInformation("Database Connection OK!");
                    break;
                }

                if (i == 59)
                {
                    _logger.LogError(errorMessage = "Unable to connect to database!");
                    return false;
                }

                _logger.LogInformation("Waiting for database connection...");
                Thread.Sleep(1000);
            }

            if (!instance.DatabaseAlreadyExists())
            {
                instance.CreateDatabase();
                Thread.Sleep(3000);
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            databaseFactory.CloseSessionFactory();

            var message = "Initializing Session Factory...";
            _logger.LogInformation("Starting Server: {Message}", message);
            ServerState.Instance.ServerStartingStatus = message;

            instance.Init();
            var version = instance.GetDatabaseVersion();
            if (version > instance.RequiredVersion)
            {
                message = "The Database Version is bigger than the supported version by Shoko Server. You should upgrade Shoko Server.";
                _logger.LogInformation("Starting Server: {Message}", message);
                ServerState.Instance.ServerStartingStatus = message;
                errorMessage = message;
                return false;
            }

            if (version != 0 && version < instance.RequiredVersion)
            {
                message = "New Version detected. Database Backup in progress...";
                _logger.LogInformation("Starting Server: {Message}", message);
                ServerState.Instance.ServerStartingStatus = message;
                instance.BackupDatabase(instance.GetDatabaseBackupName(version));
            }

            try
            {
                _logger.LogInformation("Starting Server: {Type} - CreateAndUpdateSchema()", instance.GetType());
                instance.CreateAndUpdateSchema();

                _logger.LogInformation("Starting Server: RepoFactory.Init()");
                repoFactory.Init(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return false;

                instance.ExecuteDatabaseFixes();
                instance.PopulateInitialData();
                repoFactory.PostInit();
            }
            catch (DatabaseCommandException ex)
            {
                _logger.LogError(ex, ex.ToString());
                Utils.ShowErrorMessage(ex, "Database Error :\n\r " + ex + "\n\rNotify developers about this error, it will be logged in your logs");
                ServerState.Instance.ServerStartingStatus = "Failed to start. Please review database settings.";
                errorMessage = "Database Error :\n\r " + ex +
                               "\n\rNotify developers about this error, it will be logged in your logs";
                return false;
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, $"Database Timeout: {ex}");
                ServerState.Instance.ServerStartingStatus = "Database timeout:";
                errorMessage = "Database timeout:\n\r" + ex;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Could not init database: {ex}";
            _logger.LogError(ex, errorMessage);
            ServerState.Instance.ServerStartingStatus = "Failed to start. Please review database settings.";
            return false;
        }
    }

    #endregion

    #endregion

    #region Shutdown

    public Task WaitForShutdown()
        => _webHost?.WaitForShutdownAsync() ?? Task.CompletedTask;

    internal void OnShutdown()
    {
        // Mark the server as shutting down.
        lock (_logger)
        {
            if (!RestartPending && !ShutdownPending)
                ShutdownPending = true;
        }

        _shutdownTokenSource.Cancel();
        if (_webHost is not null)
        {
            _autoUpdateTimer?.Stop();

            var fileWatcherService = _webHost.Services.GetRequiredService<FileWatcherService>();
            fileWatcherService.StopWatchingFiles();

            var udpConnectionHandler = _webHost.Services.GetRequiredService<IUDPConnectionHandler>();
            udpConnectionHandler.ForceLogout();
            udpConnectionHandler.CloseConnections();
        }

        try
        {
            Shutdown?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while invoking Shutdown");
        }
    }

    #region Shutdown | Request Handlers


    /// <inheritdoc/>
    public bool RequestShutdown()
    {
        if (!CanShutdown || RestartPending || ShutdownPending || _webHost is null)
            return false;

        lock (_logger)
        {
            if (!CanShutdown || RestartPending || ShutdownPending || _webHost is null)
                return false;

            _logger.LogTrace("Shutdown requested");
            var args = new CancelEventArgs();
            try
            {
                ShutdownOrRestartRequested?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while invoking ShutdownOrRestartRequested");
                return false;
            }
            if (args.Cancel || _webHost is null)
            {
                _logger.LogInformation("Shutdown request blocked");
                return false;
            }

            _logger.LogInformation("Shutdown request accepted");
            ShutdownPending = true;
            var lifetime = _webHost.Services.GetRequiredService<IHostApplicationLifetime>();
            Task.Run(lifetime.StopApplication);
            return true;
        }
    }

    /// <inheritdoc/>
    public bool RequestRestart()
    {
        if (!CanRestart || RestartPending || ShutdownPending || _webHost is null)
            return false;

        lock (_logger)
        {
            if (!CanRestart || RestartPending || ShutdownPending || _webHost is null)
                return false;

            _logger.LogTrace("Restart requested");
            var args = new CancelEventArgs();
            try
            {
                ShutdownOrRestartRequested?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while invoking ShutdownOrRestartRequested");
                return false;
            }
            if (args.Cancel || _webHost is null)
            {
                _logger.LogInformation("Restart request blocked");
                return false;
            }

            _logger.LogInformation("Restart request accepted");
            RestartPending = true;
            var lifetime = _webHost.Services.GetRequiredService<IHostApplicationLifetime>();
            Task.Run(lifetime.StopApplication);
            return true;
        }
    }

    #endregion

    #endregion

    #region Auto Update Timer

    private void AutoUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (RestartPending || ShutdownPending)
            return;

        var actionService = _webHost!.Services.GetRequiredService<ActionService>();

        // TODO: Move all of these to Quartz
        actionService.CheckForUnreadNotifications(false).GetAwaiter().GetResult();
        actionService.CheckForCalendarUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForAnimeUpdate().GetAwaiter().GetResult();
        actionService.CheckForMyListSyncUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForAniDBFileUpdate(false).GetAwaiter().GetResult();
    }

    #endregion
}
