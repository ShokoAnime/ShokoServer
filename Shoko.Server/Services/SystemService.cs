using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Web;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Exceptions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Logging.Services;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Web.Services;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Filters;
using Shoko.QueueProcessor.Scheduling;
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
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Image;
using Shoko.Server.Server;
using Shoko.Server.Services.Abstraction;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Services.Connectivity;
using Shoko.Server.Services.ErrorHandling;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Trinet.Core.IO.Ntfs;
using ISettingsProvider = Shoko.Server.Settings.ISettingsProvider;

namespace Shoko.Server.Services;

public class SystemService : ISystemService
{
    private readonly ILogger<SystemService> _logger;

    private readonly PluginManager _pluginManager;

    private readonly LogService _logService;

    private readonly ConfigurationService _configurationService;

    private readonly SettingsProvider _settingsProvider;

    private IHost? _webHost;

    public SystemService()
    {
        var now = DateTime.UtcNow;
        var args = Environment.GetCommandLineArgs();

        ApplicationPaths.SetHome(args);

        LogService.InitLogger(ApplicationPaths.Instance);
        var loggerFactory = LoggerFactory.Create(o => o.AddNLog());

        var unknownLangLogger = loggerFactory.CreateLogger("LanguageExtensions");
        LanguageExtensions.OnUnknownLanguage += lang =>
            unknownLangLogger.LogError("Unrecognized language string '{Language}' — add a mapping to LanguageExtensions.GetTitleLanguage().", lang);

        Version = PluginManager.GetVersionInformation();

        _logger = loggerFactory.CreateLogger<SystemService>();
        _pluginManager = new(loggerFactory.CreateLogger<PluginManager>(), this, ApplicationPaths.Instance);
        _configurationService = new(loggerFactory, ApplicationPaths.Instance, _pluginManager);
        _settingsProvider = new(loggerFactory.CreateLogger<SettingsProvider>(), this, _configurationService.CreateProvider<ServerSettings>());
        _logService = new(loggerFactory.CreateLogger<LogService>(), ApplicationPaths.Instance, _settingsProvider);
        _databaseBlockingTasks.Add(_startupTaskSource.Task);

        CanShutdown = args.Contains("--shutdown-enabled");
        CanRestart = args.Contains("--restart-enabled");
        BootstrappedAt = now;

        // Set the singleton instance for the settings provider.
        ISettingsProvider.Instance = _settingsProvider;
    }

    #region General

    /// <inheritdoc/>
    public DateTime BootstrappedAt { get; private set; }

    /// <inheritdoc/>
    public TimeSpan Uptime => DateTime.UtcNow - BootstrappedAt;

    /// <inheritdoc/>
    public TimeSpan? StartupTime => StartedAt.HasValue ? StartedAt.Value - BootstrappedAt : null;

    /// <inheritdoc/>
    public VersionInformation Version { get; }

    /// <inheritdoc/>
    public string? MediaInfoVersion { get; private set; }

    /// <inheritdoc/>
    public string? RHashVersion { get; private set; }

    #endregion

    #region Startup

    private TaskCompletionSource? _startupTaskSource = new();

    public event EventHandler<StartupFailedEventArgs>? StartupFailed;

    public event EventHandler<StartupMessageChangedEventArgs>? StartupMessageChanged;

    public event EventHandler<ServerAboutToStartEventArgs>? AboutToStart;

    public event EventHandler? Started;

    /// <inheritdoc/>
    public bool IsStarted { get => StartedAt.HasValue; }

    /// <inheritdoc/>
    public DateTime? StartedAt { get; private set; }

    /// <inheritdoc/>
    public string? StartupMessage
    {
        get;
        internal set
        {
            // We only allow setting it during startup.
            if (field is null && StartedAt.HasValue)
                return;

            var changed = !string.Equals(field, value);
            field = value;
            if (value is { Length: > 0 } && changed)
            {
                _logger.LogInformation("Starting Server: {Message}", value);
                Task.Run(() => StartupMessageChanged?.Invoke(this, new()
                {
                    Message = value
                }));
            }
        }
    } = string.Empty;

    /// <inheritdoc/>
    public StartupFailedException? StartupFailedException
    {
        get;
        private set
        {
            if (value is null || field is not null) return;
            lock (_logger)
            {
                if (field is not null) return;
                field = value;
                InSetupMode = false;
                // Always allow shutdown if we failed to start.
                CanShutdown = true;
            }

            Task.Run(() => StartupFailed?.Invoke(this, new(value)));

            _logger.LogError(value, "Failed to Start Server: {Message}", value.Message);
            _startupTaskSource?.SetException(value);
            _startupTaskSource = null;
        }
    }

    /// <inheritdoc/>
    public async Task<IHost?> StartAsync()
    {
        try
        {
            // Check if any of the DLL are blocked, common issue with daily builds.
            if (!CheckBlockedFiles())
            {
                StartupMessage = "Failed to start. Check your logs for more information.";
                StartupFailedException = new("Blocked DLL files found in server directory!");
                return null;
            }

            var settings = _settingsProvider.GetSettings();

            LogService.ApplyLoggingSettings(settings.Logging);

            // Set the setup mode flag before proceeding.
            InSetupMode = settings.FirstRun;

            // Set default culture.
            var culture = CultureInfo.GetCultureInfo(settings.Culture);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Raise the thread pool's minimum thread count so bursts of concurrent
            // work don't stall behind the runtime's gradual hill-climbing thread
            // injection. 0 leaves the runtime default untouched; negative values
            // are a multiplier against the CPU count, offset by one (e.g. -1 =>
            // CPU count x 2, -2 => CPU count x 3).
            if (settings.ThreadPoolMinThreads is not 0)
            {
                var minThreads = settings.ThreadPoolMinThreads > 0
                    ? settings.ThreadPoolMinThreads
                    : Environment.ProcessorCount * (Math.Abs(settings.ThreadPoolMinThreads) + 1);
                if (ThreadPool.SetMinThreads(minThreads, minThreads))
                    _logger.LogInformation("Thread pool minimum threads set to {MinThreads}.", minThreads);
                else
                    _logger.LogWarning("Failed to set thread pool minimum threads to {MinThreads}; leaving runtime default in place.", minThreads);
            }

            // Set default options for MessagePack.
            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
            MessagePackSerializer.Typeless.DefaultOptions = MessagePackSerializer.Typeless.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            MediaInfoVersion = MediaInfoUtility.GetVersion();
            RHashVersion = CoreHashProvider.GetRhashVersion();

            // Log some basic information about the server before we start.
            _logger.LogInformation("Shoko Server: {Version}", Version);
            _logger.LogInformation("Operating System: {OSInfo}", RuntimeInformation.OSDescription);
            _logger.LogInformation("MediaInfo: {Version}", MediaInfoVersion ?? "Program NOT found");
            _logger.LogInformation("RHash: {Version}", RHashVersion ?? "Library NOT found");

            StartupMessage = "Starting Log Service.";

            _logService.StartMaintenance();

            StartupMessage = "Log Service initialized.";

            StartupMessage = "Scanning for Plugins...";

            _pluginManager.ScanForPlugins();

            StartupMessage = "Scan for plugins completed.";

            StartupMessage = "Initializing Web Host & Services.";

            _webHost = InitWebHost(settings);

#pragma warning disable CS0618 // Type or member is obsolete
            ISystemService.StaticServices = _webHost.Services;
#pragma warning restore CS0618 // Type or member is obsolete

            StartupMessage = "Web Host & Services initialized.";

            StartupMessage = "Initializing Plugins.";

            // Init. plugins before starting the IHostedService services.
            _pluginManager.InitPlugins();

            StartupMessage = "Plugins initialized.";

            StartupMessage = "Starting Web Hosts.";

            // Start the web server and all IHostedService services.
            await _webHost.StartAsync();

            StartupMessage = "Web Host started.";

            if (settings.DumpSettingsOnStart)
                _settingsProvider.DebugSettingsToLog();

            // Start the database unblock loop.
            _ = Task.Factory.StartNew(DatabaseUnblockLoop, TaskCreationOptions.LongRunning);

            if (InSetupMode)
            {
                // In case the server is not fully started we need to check the
                // connectivity manually once, since Quartz is not up and
                // running yet, and the AniDB login test requires us to have
                // internet access.
                _ = Task.Run(_webHost.Services.GetRequiredService<IConnectivityService>().CheckAvailability);

                _logger.LogWarning("The server is in Setup Mode and is NOT STARTED. It needs to be configured via the Web UI or the server-settings.json before use!");

                _ = Task.Run(() => SetupRequired?.Invoke(this, EventArgs.Empty));
            }
            else
            {
                _ = Task.Factory.StartNew(LateStart, TaskCreationOptions.LongRunning);
            }

            return _webHost;
        }
        catch (Exception ex)
        {
            StartupMessage = "Failed to start. Check your logs for more information.";
            StartupFailedException = new(innerException: ex);
            return null;
        }
    }

    public Task WaitForStartupAsync()
         => _startupTaskSource?.Task ?? Task.CompletedTask;

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

            _logger.LogError("Found blocked DLL file: {DllFile}", dllFile);
            result = false;
        }

        return result;
    }

    #region Startup | Services

    private IHost InitWebHost(IServerSettings settings)
        => new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
                webHostBuilder
                    .UseKestrel(options => options.ListenAnyIP(settings.Web.Port))
                    .ConfigureApp()
                    .ConfigureServiceProvider()
                    .UseStartup(_ => new Startup(this, _logService, _configurationService, _settingsProvider, _pluginManager))
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
                    .UseSentryConfig(_settingsProvider)
            )
            .Build();

    private class Startup(SystemService systemService, ILogService logService, IConfigurationService configurationService, ISettingsProvider settingsProvider, IPluginManager pluginManager)
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(systemService);
            services.AddSingleton<ISystemService>(systemService);
            services.AddSingleton(logService);
            services.AddSingleton(configurationService);
            services.AddSingleton(settingsProvider);
            services.AddSingleton(pluginManager);
            services.AddSingleton(ApplicationPaths.Instance);

            services.AddSingleton<IPluginPackageManager, PluginPackageManager>();
            services.AddSingleton<FileSystemHelpers>();
            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<TmdbRateLimiter>();
            services.AddSingleton<TmdbImageService>();
            services.AddSingleton<TmdbLinkingService>();
            services.AddSingleton<ITmdbLinkingService>(sp => sp.GetRequiredService<TmdbLinkingService>());
            services.AddSingleton<TmdbMetadataService>();
            services.AddSingleton<ITmdbMetadataService>(sp => sp.GetRequiredService<TmdbMetadataService>());
            services.AddSingleton<TmdbSearchService>();
            services.AddSingleton<ITmdbSearchService>(sp => sp.GetRequiredService<TmdbSearchService>());
            services.AddSingleton<IFilteringEngine, FilteringEngine>();
            services.AddSingleton<IMetadataFilteringService, MetadataFilteringService>();
            services.AddSingleton<IFilterPresetManager, FilterPresetManager>();
            services.AddSingleton<IFuzzySearchService, FuzzySearchService>();
            services.AddSingleton<LegacyFilterConverter>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<AnimeSeriesService>();
            services.AddSingleton<AnimeGroupService>();
            services.AddSingleton<ShokoGroupManager>();
            services.AddSingleton<IShokoGroupManager>(sp => sp.GetRequiredService<ShokoGroupManager>());
            services.AddSingleton<IWebThemeService, WebThemeService>();
            services.AddSingleton<ISystemUpdateService, SystemUpdateService>();
            services.AddSingleton<IMetadataService, AbstractMetadataService>();
            services.AddSingleton<IVideoService, VideoService>();
            services.AddSingleton<IVideoReleaseService, VideoReleaseService>();
            services.AddSingleton<VideoReleaseGroupingService>();
            services.AddSingleton<ReleaseComparisonService>();
            services.AddSingleton<ReleaseAutoManagementService>();
            services.AddSingleton<IVideoHashingService, VideoHashingService>();
            services.AddSingleton<VideoRelocationService>();
            services.AddSingleton<IVideoRelocationService>(sp => sp.GetRequiredService<VideoRelocationService>());
            services.AddSingleton<IRelocationPresetManager>(sp => sp.GetRequiredService<VideoRelocationService>());
            services.AddTransient<RelocationPresetMigrationService>();
            services.AddSingleton(typeof(ConfigurationProvider<>));
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IUserDataService, UserDataService>();
            services.AddSingleton<IImageManager, ImageManager>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddScoped<AnimeGroupCreator>();

            services.AddRepositories();
            services.AddSentryConfig(settingsProvider);
            // Wire the new queue processor
            var queueSettings = ISettingsProvider.Instance.GetSettings().Queue;
            var maxWorkers = queueSettings.MaxTotalWorkers > 0 ? queueSettings.MaxTotalWorkers : Environment.ProcessorCount + 4;
            services.AddQueueProcessor(opts =>
            {
                opts.Provider = queueSettings.Provider;
                opts.ConnectionString = GetQueueConnectionString(queueSettings);
                opts.MaxTotalWorkers = maxWorkers;
                opts.DefaultPoolMaxWorkers = maxWorkers;
                opts.FlushIntervalMs = queueSettings.FlushIntervalMs;
                opts.MaxFlushBatch = queueSettings.MaxFlushBatch;
                opts.LimitedConcurrencyOverrides = queueSettings.LimitedConcurrencyOverrides;
            }, typeof(SystemService).Assembly);

            // Register acquisition filters
            services.AddSingleton<IAcquisitionFilter, AniDBUdpRateLimitedAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, AniDBHttpRateLimitedAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, TmdbApiRateLimitedAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, DatabaseRequiredAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, NetworkRequiredAcquisitionFilter>();

            services.AddHttpClient("Default", client =>
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain");
                    client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{systemService.Version.Version.ToSemanticVersioningString()}");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
                })
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
                .UseSocketsHttpHandler((handler, _) =>
                {
                    handler.AllowAutoRedirect = true;
                    handler.AutomaticDecompression = DecompressionMethods.All;
                    handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
                });
            services.AddAniDB();
            services.AddSingleton<AnidbService>();
            services.AddSingleton<IAnidbService>(sp => sp.GetRequiredService<AnidbService>());
            services.AddSingleton<IAnidbAvdumpService>(sp => sp.GetRequiredService<AnidbService>());
            services.AddSingleton<SupplementaryMetadataService>();
            services.AddSingleton<ISupplementaryMetadataService>(sp => sp.GetRequiredService<SupplementaryMetadataService>());
            services.AddSingleton<AnimeMetadataOrchestrator>();
            services.AddSingleton<TmdbSupplementaryProvider>();

            pluginManager.RegisterPlugins(services);

            services.AddAPI(pluginManager);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAPI(pluginManager);
            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(systemService.OnShutdown);

            // Register core recurring jobs that don't need the main DB so they start running
            // with the queue (the network check probes connectivity — no DB touch). Registering
            // here, before the IHostedServices boot, ensures RecurringJobRegistry.StartAsync
            // picks them up. DB-dependent recurring jobs should carry [DatabaseRequired] and the
            // acquisition filter will hold them out of the pool until startup signals DB ready.
            var registry = app.ApplicationServices.GetRequiredService<RecurringJobRegistry>();
            registry.Register<CheckNetworkAvailabilityJob>(TimeSpan.FromMinutes(30), runImmediately: true);
            registry.Register<ScanForMissingReleaseInfoJob>(TimeSpan.FromHours(24), runImmediately: false);
            registry.Register<PeriodicImageMaintenanceJob>(TimeSpan.FromHours(24), runImmediately: false);
            registry.Register<CleanupExpiredTokensJob>(TimeSpan.FromHours(24), runImmediately: false);
            registry.Register<PurgeOrphanedTmdbDataJob>(TimeSpan.FromHours(24), runImmediately: false);

            // Register settings-driven recurring jobs. Jobs whose frequency is Never are skipped
            // entirely at startup; they are registered on-demand when settings change.
            var settingsProvider = app.ApplicationServices.GetRequiredService<ISettingsProvider>();
            var settings = settingsProvider.GetSettings();
            var anidb = settings.AniDb;
            var pluginUpdates = settings.Plugins.Updates;

            if (anidb.Notification_UpdateFrequency != ScheduledUpdateFrequency.Never)
                registry.Register<CheckAniDBNotificationsJob>(TimeSpan.FromHours(anidb.Notification_UpdateFrequency.Hours), runImmediately: false);
            if (anidb.Calendar_UpdateFrequency != ScheduledUpdateFrequency.Never)
                registry.Register<GetAniDBCalendarJob>(TimeSpan.FromHours(anidb.Calendar_UpdateFrequency.Hours), runImmediately: false);
            if (anidb.Anime_UpdateFrequency != ScheduledUpdateFrequency.Never)
                registry.Register<GetUpdatedAniDBAnimeJob>(TimeSpan.FromHours(anidb.Anime_UpdateFrequency.Hours), runImmediately: false);
            if (anidb.MyList_UpdateFrequency != ScheduledUpdateFrequency.Never)
                registry.Register<SyncAniDBMyListJob>(TimeSpan.FromHours(anidb.MyList_UpdateFrequency.Hours), runImmediately: false);
            if (anidb.File_UpdateFrequency != ScheduledUpdateFrequency.Never)
                registry.Register<CheckAniDBFileUpdatesJob>(TimeSpan.FromHours(anidb.File_UpdateFrequency.Hours), runImmediately: false);
            if (pluginUpdates.IsAutoSyncEnabled && pluginUpdates.AutoUpdateFrequency != ScheduledUpdateFrequency.Never)
                registry.Register<CheckPluginUpdatesJob>(TimeSpan.FromHours(pluginUpdates.AutoUpdateFrequency.Hours), runImmediately: false);

            // Reschedule recurring jobs when frequency settings change.
            var configProvider = app.ApplicationServices.GetRequiredService<ConfigurationProvider<ServerSettings>>();
            configProvider.Saved += (_, args) =>
            {
                var s = args.Configuration;
                RescheduleByFrequency<CheckAniDBNotificationsJob>(registry, s.AniDb.Notification_UpdateFrequency);
                RescheduleByFrequency<GetAniDBCalendarJob>(registry, s.AniDb.Calendar_UpdateFrequency);
                RescheduleByFrequency<GetUpdatedAniDBAnimeJob>(registry, s.AniDb.Anime_UpdateFrequency);
                RescheduleByFrequency<SyncAniDBMyListJob>(registry, s.AniDb.MyList_UpdateFrequency);
                RescheduleByFrequency<CheckAniDBFileUpdatesJob>(registry, s.AniDb.File_UpdateFrequency);

                var pu = s.Plugins.Updates;
                if (pu.IsAutoSyncEnabled && pu.AutoUpdateFrequency != ScheduledUpdateFrequency.Never)
                    registry.Reschedule<CheckPluginUpdatesJob>(TimeSpan.FromHours(pu.AutoUpdateFrequency.Hours));
                else
                    registry.Unschedule<CheckPluginUpdatesJob>();
            };
        }

        private static void RescheduleByFrequency<T>(RecurringJobRegistry registry, ScheduledUpdateFrequency freq)
            where T : class, IQueueJob
        {
            if (freq == ScheduledUpdateFrequency.Never)
                registry.Unschedule<T>();
            else
                registry.Reschedule<T>(TimeSpan.FromHours(freq.Hours));
        }

        private static string GetQueueConnectionString(QueueProcessorSettings q)
        {
            if (q.Provider != DatabaseProvider.SQLite)
                return q.ConnectionString;

            if (string.IsNullOrEmpty(q.SQLiteFilePath) && string.IsNullOrEmpty(q.ConnectionString))
                throw new ArgumentException("SQLiteFilePath or ConnectionString must be set when using SQLite.");

            var connectionString = string.Empty;
            if (!string.IsNullOrEmpty(q.SQLiteFilePath))
            {
                var filePath = Path.IsPathRooted(q.SQLiteFilePath)
                    ? q.SQLiteFilePath
                    : Path.GetFullPath(Path.Combine(ApplicationPaths.StaticDataPath, q.SQLiteFilePath));
                connectionString = $"Data Source={filePath};Mode=ReadWriteCreate;Pooling=True";
            }

            if (!string.IsNullOrEmpty(q.ConnectionString))
                connectionString += $";{q.ConnectionString}";

            return connectionString.TrimStart(';');
        }
    }

    #endregion

    #region Startup | Setup

    /// <inheritdoc/>
    public event EventHandler? SetupRequired;

    /// <inheritdoc/>
    public event EventHandler? SetupCompleted;

    public bool InSetupMode { get; private set; }

    /// <inheritdoc/>
    public bool CompleteSetup()
    {
        if (!InSetupMode)
            return false;

        lock (_logger)
        {
            if (!InSetupMode)
                return false;

            InSetupMode = false;
        }

        Task.Factory.StartNew(LateStart, TaskCreationOptions.LongRunning);
        return true;
    }

    #endregion

    #region Startup | Late Start

    /// <summary>
    ///   Responsible for the late start of the application after the initial
    ///   setup is complete.
    /// </summary>
    private void LateStart()
    {
        var settings = _settingsProvider.GetSettings();
        try
        {
            var scheduler = _webHost!.Services.GetRequiredService<IQueueScheduler>();
            var databaseFactory = _webHost.Services.GetRequiredService<DatabaseFactory>();
            var repoFactory = _webHost.Services.GetRequiredService<RepoFactory>();
            var fileWatcherService = _webHost.Services.GetRequiredService<FileWatcherService>();
            var lifetime = _webHost.Services.GetRequiredService<IHostApplicationLifetime>();
            var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, _shutdownTokenSource.Token).Token;
            if (cancellationToken.IsCancellationRequested)
                return;

            if (!InitializeDatabase(databaseFactory, repoFactory, cancellationToken) && !cancellationToken.IsCancellationRequested)
                return;

            if (cancellationToken.IsCancellationRequested)
                return;

            StartupMessage = "Initializing Session Factory...";
            databaseFactory.CloseSessionFactory();
            _ = databaseFactory.SessionFactory;

            if (cancellationToken.IsCancellationRequested)
                return;

            StartupMessage = "Migrating failed relocation presets...";
            _webHost!.Services.GetRequiredService<RelocationPresetMigrationService>().MigrateFailedPresets();

            StartupMessage = "Initializing UDP Connection Handler...";
            var udpConnectionHandler = _webHost.Services.GetRequiredService<IUDPConnectionHandler>();
            try
            {
                udpConnectionHandler.Init();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UDP Connection Handler");
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            StartupMessage = "Initializing File Watchers...";
            fileWatcherService.StartWatchingFiles();

            StartupMessage = "About to start...";
            AboutToStart?.Invoke(this, new() { ServiceProvider = _webHost.Services });

            if (cancellationToken.IsCancellationRequested)
                return;

            if (settings.FirstRun)
            {
                settings.FirstRun = false;
                _settingsProvider.SaveSettings(settings);

                Task.Run(() => SetupCompleted?.Invoke(this, EventArgs.Empty));
            }

            StartedAt = DateTime.UtcNow;

            Task.Run(() => Started?.Invoke(this, EventArgs.Empty));

            StartupMessage = "Startup Complete!";
            StartupMessage = null;

            _startupTaskSource?.SetResult();
            _startupTaskSource = null;

            if (settings.Import.ScanDropFoldersOnStart)
                scheduler.Enqueue<ScanDropFoldersJob>().GetAwaiter().GetResult();
            if (settings.Import.RunOnStart)
                scheduler.Enqueue<ImportJob>().GetAwaiter().GetResult();
            else
                _webHost.Services.GetRequiredService<ActionService>()
                    .ScheduleMissingAnidbAnimeForFiles().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StartupMessage = "Failed to start. Check your logs for more information.";
            StartupFailedException = new(innerException: ex);
        }
    }

    #endregion

    #region Startup | Database

    /// <summary>
    /// Initialize the database and repositories.
    /// </summary>
    /// <param name="databaseFactory">The database factory.</param>
    /// <param name="repositoryFactory">The repository factory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the database and repositories were initialized successfully; otherwise, <see langword="false"/>.</returns>
    private bool InitializeDatabase(DatabaseFactory databaseFactory, RepoFactory repositoryFactory, CancellationToken cancellationToken)
    {
        try
        {
            databaseFactory.Instance = null;
            var instance = databaseFactory.Instance;
            if (instance is null)
            {
                StartupMessage = "Failed to start. Could not initialize database factory instance!";
                StartupFailedException = new(StartupMessage);
                return false;
            }

            StartupMessage = $"Setting up database connection to {instance.GetType().Name}...";
            for (var attempt = 1; attempt <= 60; attempt++)
            {
                if (instance.TestConnection())
                {
                    StartupMessage = "Database Connection OK!";
                    break;
                }

                if (attempt is 60)
                {
                    StartupMessage = "Failed to start. Could not connect to database!";
                    StartupFailedException = new(StartupMessage);
                    return false;
                }

                if (cancellationToken.IsCancellationRequested)
                    return false;

                StartupMessage = $"Waiting for database connection... ({attempt}/60)";
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

            StartupMessage = "Initializing Session Factory...";

            instance.Init();
            var version = instance.GetDatabaseVersion();
            if (version > instance.RequiredVersion)
            {
                StartupMessage = "The database version is bigger than the supported version by Shoko Server. You should upgrade Shoko Server or manually restore your database from a backup.";
                StartupFailedException = new(StartupMessage);
                return false;
            }

            if (version is not 0 && version < instance.RequiredVersion)
            {
                StartupMessage = "New database version detected. Database backup in progress...";
                instance.BackupDatabase(instance.GetDatabaseBackupName(version));
            }

            try
            {
                StartupMessage = $"Creating and updating database schema for {instance.GetType().Name}...";
                instance.CreateAndUpdateSchema();

                StartupMessage = "RepoFactory.PostInit()";
                repositoryFactory.Init(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return false;

                instance.ExecuteDatabaseFixes();
                instance.PopulateInitialData();
                repositoryFactory.PostInit();
            }
            catch (DatabaseCommandException ex)
            {
                _logger.LogError(ex, ex.Message);
                StartupMessage = "Failed to start. Please review database settings. Notify developers about this error, it will be logged in your logs!";
                StartupFailedException = new($"{StartupMessage} Error Message: {ex.Message}", innerException: ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                StartupMessage = "Failed to start. Database timed out!";
                StartupFailedException = new($"{StartupMessage} Error Message: {ex.Message}", innerException: ex);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            StartupMessage = "Failed to start. Please review database settings.";
            StartupFailedException = new($"{StartupMessage} Error Message: {ex.Message}", innerException: ex);
            return false;
        }
    }

    #endregion

    #endregion

    #region Shutdown

    private readonly CancellationTokenSource _shutdownTokenSource = new();

    /// <inheritdoc/>
    public event EventHandler<CancelEventArgs>? ShutdownOrRestartRequested;

    /// <inheritdoc/>
    public event EventHandler? Shutdown;

    /// <inheritdoc/>
    public bool CanShutdown { get; private set; }

    /// <inheritdoc/>
    public bool ShutdownPending { get; private set; }

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
    public Task WaitForShutdownAsync()
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

    #region Shutdown | Restart

    public bool CanRestart { get; private init; }

    public bool RestartPending { get; private set; }

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

    #region Database

    private readonly List<Task> _databaseBlockingTasks = [];

    private CancellationTokenSource? _databaseTasksChangedCTS;

    private TaskCompletionSource? _databaseTaskSource = new();

    /// <inheritdoc/>
    public event EventHandler<DatabaseBlockedChangedEventArgs>? DatabaseBlockedChanged;

    /// <inheritdoc/>
    public bool IsDatabaseBlocked => _databaseTaskSource is not null;

    /// <inheritdoc/>
    public Task WaitForDatabaseUnblockedAsync()
        => _databaseTaskSource?.Task ?? Task.CompletedTask;

    public void AddDatabaseBlockingTask(Task task)
    {
        lock (_databaseBlockingTasks)
        {
            _databaseBlockingTasks.Add(task);

            // Signal to the loop that we have a new task to wait for.
            _databaseTasksChangedCTS?.Cancel();

            // Start the loop if it's not already running.
            if (_databaseTaskSource is null)
            {
                _databaseTaskSource = new TaskCompletionSource();
                Task.Factory.StartNew(DatabaseUnblockLoop, TaskCreationOptions.LongRunning);
            }
        }
    }

    /// <summary>
    /// Waits for all database blocking tasks to complete.
    /// </summary>
    private void DatabaseUnblockLoop()
    {
        Task[] tasks;
        TaskCompletionSource taskSource;
        CancellationTokenSource changedSignal;
        lock (_databaseBlockingTasks)
        {
            taskSource = _databaseTaskSource!;
            changedSignal = _databaseTasksChangedCTS = new();
            tasks = _databaseBlockingTasks.ToArray();
        }

        Task.Run(() => DatabaseBlockedChanged?.Invoke(this, new() { IsBlocked = true }));

        while (tasks.Length > 0)
        {
            int task;
            try
            {
                task = Task.WaitAny(tasks, changedSignal.Token);
            }
            // If the operation was cancelled, we need to get the new list of tasks since
            // the list of tasks may have changed.
            catch (OperationCanceledException)
            {
                lock (_databaseBlockingTasks)
                {
                    changedSignal = _databaseTasksChangedCTS = new();
                    tasks = _databaseBlockingTasks.ToArray();
                }
                continue;
            }

            lock (_databaseBlockingTasks)
            {
                _databaseBlockingTasks.Remove(tasks[task]);
                if (_databaseBlockingTasks.Count is 0)
                {
                    taskSource.TrySetResult();
                    _databaseTaskSource = null;
                    _databaseTasksChangedCTS = null;

                    Task.Run(() => DatabaseBlockedChanged?.Invoke(this, new() { IsBlocked = false }));
                    return;
                }

                changedSignal = _databaseTasksChangedCTS = new();
                tasks = _databaseBlockingTasks.ToArray();
            }
        }
    }

    #endregion
}
