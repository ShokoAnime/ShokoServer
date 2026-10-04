using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
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
using Shoko.Abstractions.Actions.Services;
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
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Services;
using Shoko.Abstractions.Web.Services;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Filters;
using Shoko.Server.API;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Hashing;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Plugin.Databases;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Watchdog;
using Shoko.Server.Server;
using Shoko.Server.Services.Airing;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Services.Connectivity;
using Shoko.Server.Services.ErrorHandling;
using Shoko.Server.Services.Mylist;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;
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

    private readonly RestartReasonTracker _restartReasons;

    private IHost? _webHost;

    public SystemService()
    {
        var now = DateTime.UtcNow;
        var args = Environment.GetCommandLineArgs();

        ApplicationPaths.SetHome(args);

        LogService.InitLogger(ApplicationPaths.Instance);
        var loggerFactory = LoggerFactory.Create(o => o.AddNLog());

        // Source numbers must be known before any plugin registers a source.
        MetadataNumberRegistry.Load(ApplicationPaths.Instance.DataPath);

        var unknownLangLogger = loggerFactory.CreateLogger("LanguageExtensions");
        LanguageExtensions.OnUnknownLanguage += lang =>
            unknownLangLogger.LogError("Unrecognized language string '{Language}' from {Caller}; add a mapping to LanguageExtensions.GetTitleLanguage().", lang, DescribeLanguageCaller());

        Version = PluginManager.GetVersionInformation();

        _logger = loggerFactory.CreateLogger<SystemService>();
        _pluginManager = new(loggerFactory.CreateLogger<PluginManager>(), this, ApplicationPaths.Instance);
        _configurationService = new(loggerFactory, ApplicationPaths.Instance, _pluginManager);
        _settingsProvider = new(loggerFactory.CreateLogger<SettingsProvider>(), this, _configurationService.CreateProvider<ServerSettings>());
        _restartReasons = new(loggerFactory.CreateLogger<RestartReasonTracker>(), _configurationService, _pluginManager) { Sender = this };
        _pluginManager.StateChanged += (_, _) => _restartReasons.RefreshPluginState();
        _logService = new(loggerFactory.CreateLogger<LogService>(), ApplicationPaths.Instance, _settingsProvider);
        _databaseBlockingTasks.Add(_startupTaskSource!.Task);

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

            // The queue's hosted services are already up by the time most startup failures happen, and
            // nothing in this process will ever be able to serve a job now, so stop dispatching for good.
            try
            {
                _webHost?.Services.GetService<IQueueScheduler>()?.Halt("the server failed to start").GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to halt the queue after the failed startup");
            }
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

            // Only now is a plugin active, so only now can a plugin's state differ from what loads next.
            _restartReasons.StartTrackingPluginState();

            StartupMessage = "Plugins initialized.";

            // Before the database, so plugins set up first. A failure is recorded, not thrown: throwing would skip
            // the web host and restart-loop a container silently, where the Web UI can say which plugin failed.
            try
            {
                _pluginManager.StartPlugins(_webHost.Services, message => StartupMessage = message);
            }
            catch (Exception ex)
            {
                (StartupMessage, StartupFailedException) = DescribePluginStartupFailure(ex);
                _logger.LogError(ex, "The plugins failed to start; the server will not continue starting");
            }

            // A failed start keeps its message, so the Web UI can show what stopped it.
            if (StartupFailedException is null)
                StartupMessage = "Starting Web Hosts.";

            // Start the web server and all IHostedService services.
            await _webHost.StartAsync();

            if (StartupFailedException is null)
                StartupMessage = "Web Host started.";

            if (settings.DumpSettingsOnStart)
                _settingsProvider.DebugSettingsToLog();

            // From an empty context, as the loop runs for the life of the server.
            using (DetachedFlow.Suppress())
                _ = Task.Factory.StartNew(DatabaseUnblockLoop, TaskCreationOptions.LongRunning);

            if (StartupFailedException is not null)
            {
                _logger.LogError("The server is reachable but will not finish starting. {Message}", StartupFailedException.Message);
                return _webHost;
            }

            // Checked once now, as the scheduled check only starts with the server and until
            // then network jobs wait and the first-run AniDB login test fails.
            _ = Task.Run(_webHost.Services.GetRequiredService<IConnectivityService>().CheckAvailability);

            if (InSetupMode)
            {
                _logger.LogWarning("The server is in Setup Mode and is NOT STARTED. It needs to be configured via the Web UI or the server-settings.json before use!");

                _ = Task.Run(() => SetupRequired?.Invoke(this, EventArgs.Empty));
            }
            else
            {
                using (DetachedFlow.Suppress())
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
        finally
        {
            // Every way out closes registration, including those that never reach the plugin setup.
            PluginManager.CloseMetadataRegistration(_logger);
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

    /// <summary>
    /// What to tell the user when the plugins fail to start or their databases fail to migrate: a
    /// failed plugin database migration says which plugin, context and migration failed and how to
    /// recover, anything else points at the logs.
    /// </summary>
    /// <param name="exception">What the plugins threw.</param>
    /// <returns>The start-up message and the exception to record.</returns>
    internal static (string Message, StartupFailedException Exception) DescribePluginStartupFailure(Exception exception)
        => exception is PluginDatabaseMigrationException migrationException
            ? ($"Failed to start. {migrationException.Message}", new(migrationException.Message, migrationException))
            : ("Failed to start. Check your logs for more information.", new(innerException: exception));

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
            services.AddSingleton(typeof(PluginPaths<>));
            services.AddSingleton<PluginDatabaseServer>();
            services.AddSingleton<PluginDatabaseGate>();
            services.AddSingleton<PluginDatabaseMigrator>();

            services.AddSingleton<IPluginPackageManager, PluginPackageManager>();
            services.AddSingleton<IPluginDependencyResolver, PluginDependencyResolver>();
            services.AddSingleton<FileSystemHelpers>();
            services.AddSingleton<IFileSystemHelpers>(sp => sp.GetRequiredService<FileSystemHelpers>());
            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<IFilteringEngine, FilteringEngine>();
            services.AddSingleton<IMetadataFilteringService, MetadataFilteringService>();
            services.AddSingleton<IFilterPresetManager, FilterPresetManager>();
            services.AddSingleton<IFuzzySearchService, FuzzySearchService>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<IActionService>(sp => sp.GetRequiredService<ActionService>());
            // Every recurring job of the core runs as a scheduled action, whose
            // triggers the admin sets.
            services.AddSingleton<ScheduledActionRegistry>();
            services.AddSingleton<IScheduledActionSource>(sp => sp.GetRequiredService<ScheduledActionRegistry>());
            services.AddSingleton<ScheduledActionService>();
            services.AddSingleton<IScheduledActionService>(sp => sp.GetRequiredService<ScheduledActionService>());
            services.AddHostedService(sp => sp.GetRequiredService<ScheduledActionService>());
            services.AddSingleton<AnimeSeriesService>();
            services.AddSingleton<AnimeGroupService>();
            services.AddSingleton<ShokoGroupManager>();
            services.AddSingleton<IShokoGroupManager>(sp => sp.GetRequiredService<ShokoGroupManager>());
            services.AddSingleton<IWebThemeService, WebThemeService>();
            services.AddSingleton<ISystemUpdateService, SystemUpdateService>();
            services.AddSingleton<MetadataProviderManager>();
            services.AddSingleton<IMetadataProviderManager>(sp => sp.GetRequiredService<MetadataProviderManager>());
            services.AddSingleton<MetadataProviderScheduler>();
            services.AddSingleton<MetadataEntityRefreshScheduler>();
            services.AddSingleton<MetadataImageContributorManager>();
            services.AddSingleton<IMetadataImageContributorManager>(sp => sp.GetRequiredService<MetadataImageContributorManager>());
            services.AddSingleton<MetadataImageContributorScheduler>();
            services.AddSingleton<IMetadataRefreshService, MetadataRefreshService>();
            services.AddSingleton<MetadataPurgeService>();
            services.AddSingleton<IMetadataPurgeService>(provider => provider.GetRequiredService<MetadataPurgeService>());
            services.AddSingleton<IMetadataCrossReferenceTransferService, MetadataCrossReferenceTransferService>();
            services.AddSingleton<MetadataImageReconciler>();
            services.AddSingleton<IMetadataRefreshState, MetadataRefreshState>();
            services.AddSingleton<MetadataEntryLocks>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton<MetadataLinkChangeTracker>();
            services.AddSingleton<MetadataCrossReferenceStore>();
            services.AddSingleton<IMetadataCrossReferenceStore>(provider => provider.GetRequiredService<MetadataCrossReferenceStore>());
            services.AddSingleton<MetadataTextStore>();
            services.AddSingleton<AnidbTitleSearch>();
            services.AddSingleton<IMetadataTextManager>(provider =>
            {
                // The models and repositories reach the manager through
                // TextAccess, so it is put there as soon as it is built.
                var manager = ActivatorUtilities.CreateInstance<MetadataTextManager>(provider);
                TextAccess.Use(manager);
                return manager;
            });
            services.AddSingleton<MetadataPeopleStore>();
            services.AddSingleton<IMetadataPeopleStore>(provider => provider.GetRequiredService<MetadataPeopleStore>());
            services.AddSingleton<MetadataTagStore>();
            services.AddSingleton<IMetadataTagStore>(provider => provider.GetRequiredService<MetadataTagStore>());
            services.AddSingleton<MetadataStudioStore>();
            services.AddSingleton<IMetadataStudioStore>(provider => provider.GetRequiredService<MetadataStudioStore>());
            services.AddSingleton<IMetadataRelationStore, MetadataRelationStore>();
            services.AddSingleton<IMetadataSuggestionStore, MetadataSuggestionStore>();
            services.AddSingleton<MetadataEntityCleanup>();
            services.AddSingleton<MetadataSeriesStore>();
            services.AddSingleton<IMetadataSeriesStore>(provider => provider.GetRequiredService<MetadataSeriesStore>());
            services.AddSingleton<MetadataMovieStore>();
            services.AddSingleton<IMetadataMovieStore>(provider => provider.GetRequiredService<MetadataMovieStore>());
            services.AddSingleton<MetadataCollectionStore>();
            services.AddSingleton<IMetadataCollectionStore>(provider => provider.GetRequiredService<MetadataCollectionStore>());
            services.AddSingleton<MetadataLinkingService>();
            services.AddSingleton<IMetadataLinkingService>(provider => provider.GetRequiredService<MetadataLinkingService>());
            services.AddSingleton<IOrderingRowState, OrderingRowState>();
            services.AddSingleton<MetadataOrderingService>();
            services.AddSingleton<IMetadataOrderingService>(provider => provider.GetRequiredService<MetadataOrderingService>());
            services.AddSingleton<IMetadataOrderingTransferService, MetadataOrderingTransferService>();
            services.AddSingleton<IMetadataMatchingEngine, MetadataMatchingEngine>();
            services.AddSingleton<IVideoService, VideoService>();
            services.AddSingleton<IVideoReleaseService, VideoReleaseService>();
            services.AddSingleton<IVideoStreamPipelineService, VideoStreamPipelineService>();
            services.AddSingleton<VideoStreamSessionManager>();
            // Sweeps idle sessions on its own clock, so a paused or busy queue never leaves them open.
            services.AddHostedService<VideoStreamSessionSweeper>();
            services.AddSingleton<VideoReleaseGroupingService>();
            services.AddSingleton<ReleaseComparisonService>();
            services.AddSingleton<ReleaseAutoManagementService>();
            services.AddSingleton<IReleaseManagementService, ReleaseManagementService>();
            services.AddSingleton<IVideoHashingService, VideoHashingService>();
            services.AddSingleton<VideoRelocationService>();
            services.AddSingleton<IVideoRelocationService>(sp => sp.GetRequiredService<VideoRelocationService>());
            services.AddSingleton<IRelocationPresetManager>(sp => sp.GetRequiredService<VideoRelocationService>());
            services.AddTransient<RelocationPresetMigrationService>();
            services.AddSingleton(typeof(ConfigurationProvider<>));
            services.AddSingleton<AuthenticationThrottleService>();
            services.AddSingleton<IAuthenticationThrottleService>(sp => sp.GetRequiredService<AuthenticationThrottleService>());
            services.AddSingleton<IUserService, UserService>();
            // Who the current request, hub call or queued job runs for.
            services.AddSingleton<ActorContext>();
            services.AddSingleton<IActorContext>(sp => sp.GetRequiredService<ActorContext>());
            // lets a service in a dependency cycle take a Lazy<T> rather than
            // injecting IServiceProvider and resolving by hand on first use
            services.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
            services.AddSingleton<IUserDataService, UserDataService>();
            // Registered concretely and forwarded, so the image manager and its
            // file store resolve to one instance.
            services.AddSingleton<ImageManager>();
            services.AddSingleton<IImageManager>(provider => provider.GetRequiredService<ImageManager>());
            services.AddSingleton<IImageFileStore>(provider => provider.GetRequiredService<ImageManager>());
            // Registered concretely as well, and forwarded, so the two resolve to one
            // instance. The sweep watchdog threshold needs the concrete type for the
            // internal GetSweepBudget(), and a cast off the interface would only fail
            // at runtime.
            services.AddSingleton<AiringScheduleService>();
            services.AddSingleton<IAiringScheduleService>(provider => provider.GetRequiredService<AiringScheduleService>());
            // Minute-level precision, so it runs on its own clock rather than
            // through the queue, where it would wait behind every other job.
            services.AddHostedService<EpisodeAiringNotificationService>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddScoped<AnimeGroupCreator>();

            services.AddRepositories();
            services.AddSentryConfig(settingsProvider);
            // Wire the new queue processor
            var queueSettings = ISettingsProvider.Instance.GetSettings().Queue;
            services.AddQueueProcessor(opts =>
            {
                opts.Provider = queueSettings.Provider;
                opts.ConnectionString = GetQueueConnectionString(queueSettings);
                opts.MaxTotalWorkers = queueSettings.GetEffectiveMaxTotalWorkers();
                opts.DefaultPoolMaxWorkers = queueSettings.GetEffectiveDefaultPoolMaxWorkers();
                opts.FlushIntervalMs = queueSettings.FlushIntervalMs;
                opts.MaxFlushBatch = queueSettings.MaxFlushBatch;
                opts.LimitedConcurrencyOverrides = queueSettings.LimitedConcurrencyOverrides;
            }, typeof(SystemService).Assembly);

            // Carries the actor across the queue, by user and device.
            services.AddSingleton<IJobActorAccessor, JobActorAccessor>();

            // Register acquisition filters
            services.AddSingleton<IAcquisitionFilter, AniDBUdpRateLimitedAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, AniDBHttpRateLimitedAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, DatabaseRequiredAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, NetworkRequiredAcquisitionFilter>();
            services.AddSingleton<IAcquisitionFilter, MetadataProviderPausedAcquisitionFilter>();

            // Each metadata provider's job types follow the limit it declared.
            services.AddSingleton<IJobConcurrencyProvider, MetadataProviderJobConcurrency>();
            services.AddSingleton<IJobConcurrencyProvider, MetadataImageContributorJobConcurrency>();

            // Register per-job watchdog thresholds
            services.AddSingleton<IJobWatchdogThreshold, AiringScheduleSweepWatchdogThreshold>();

            // Names the clients whose expired handlers are still referenced and so never disposed.
            services.AddHostedService<ExpiredHttpHandlerMonitor>();
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
            services.AddSingleton<AnidbAnimeCatalog>();
            services.AddSingleton<IAnidbService>(sp => sp.GetRequiredService<AnidbService>());
            services.AddSingleton<IAnidbAvdumpService>(sp => sp.GetRequiredService<AnidbService>());
            services.AddSingleton<MylistCache>();
            services.AddSingleton<MylistGenericsCache>();
            services.AddSingleton<IMylistService, MylistService>();
            services.AddSingleton<SupplementaryMetadataScheduler>();
            services.AddSingleton<AnimeMetadataOrchestrator>();

            // Registering the plugins' services is a host step and not on the interface, so it is
            // reached through the implementation that does it.
            if (pluginManager is PluginManager manager)
                manager.RegisterPlugins(services);

            services.AddAPI(pluginManager);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAPI(pluginManager);
            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(systemService.OnShutdown);
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

        // Started from the setup request, but it starts the server's loops, timers and watchers,
        // so it must not take the request's context along.
        using (DetachedFlow.Suppress())
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
            var databaseFactory = _webHost!.Services.GetRequiredService<DatabaseFactory>();
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

            // Right after the core's database, before anything that may reach a plugin's.
            if (MigratePluginDatabases(_webHost.Services.GetRequiredService<PluginDatabaseMigrator>(), message => StartupMessage = message) is { } failure)
            {
                (StartupMessage, StartupFailedException) = failure;
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            if (ProcessPasswordResetFile())
                return;

            StartupMessage = "Migrating failed relocation presets...";
            _webHost!.Services.GetRequiredService<RelocationPresetMigrationService>().MigrateFailedPresets();

            StartupMessage = "Initializing UDP Connection Handler...";
            var udpConnectionHandler = _webHost.Services.GetRequiredService<AniDBUDPConnectionHandler>();
            try
            {
                udpConnectionHandler.InitAsync(cancellationToken).GetAwaiter().GetResult();
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

            // The caches and an upgrade's data fixes fragment the heap by gigabytes on a large library;
            // compacting once and returning the C allocator's free memory keeps that off the resident size.
            StartupMessage = "Compacting memory...";
            // Start-up is done with the parsed XML docs; a later read loads them again.
            TypeReflectionExtensions.ReleaseXmlDocs();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            NativeAllocator.Trim();

            StartedAt = DateTime.UtcNow;

            Task.Run(() => Started?.Invoke(this, EventArgs.Empty));

            StartupMessage = "Startup Complete!";
            StartupMessage = null;

            _startupTaskSource?.SetResult();
            _startupTaskSource = null;
        }
        catch (Exception ex)
        {
            StartupMessage = "Failed to start. Check your logs for more information.";
            StartupFailedException = new(innerException: ex);
        }
    }

    /// <summary>
    /// Brings the plugin databases up to date and opens them, after the core's own database is,
    /// so the first-run setup has picked it and a plugin migration never runs before the core's.
    /// </summary>
    /// <param name="migrator">The plugin database migrator.</param>
    /// <param name="reportProgress">Told of each copy and each migration, one line apiece.</param>
    /// <returns>
    /// The start-up message and failure to record when a migration failed, which stops the start-up;
    /// otherwise <c>null</c>.
    /// </returns>
    internal static (string Message, StartupFailedException Exception)? MigratePluginDatabases(PluginDatabaseMigrator migrator, Action<string> reportProgress)
    {
        try
        {
            migrator.MigrateAll(reportProgress);
            return null;
        }
        catch (PluginDatabaseMigrationException ex)
        {
            return DescribePluginStartupFailure(ex);
        }
    }

    /// <summary>
    /// Checks for a <c>password-reset.json</c> file in the data folder and, if found, resets the
    /// password of the user it refers to and invalidates all of that user's API tokens. The file
    /// is expected to contain an object like
    /// <code>
    /// { "username": "Default", "password": "NewPassword12" }
    /// </code>
    /// A valid file is always processed, whether the user was found or not, and the server exits
    /// afterwards either way so it can be restarted with the new credentials (or with a corrected
    /// file). The file is deleted on success and kept when the user was not found, so it can be
    /// corrected and retried. An unparseable or empty file is only reported and ignored, so a typo
    /// in the file can never take the server down.
    /// </summary>
    /// <returns><c>true</c> if the startup sequence should stop here; otherwise, <c>false</c>.</returns>
    private bool ProcessPasswordResetFile()
    {
        var filePath = Path.Combine(ApplicationPaths.StaticDataPath, "password-reset.json");
        if (!File.Exists(filePath))
            return false;

        PasswordResetRequest? resetRequest;
        try
        {
            resetRequest = JsonSerializer.Deserialize<PasswordResetRequest>(File.ReadAllText(filePath), _passwordResetJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse the password reset file {FilePath}. Continuing startup without resetting any password. Fix or remove the file, then restart the server to try again.", filePath);
            return false;
        }

        if (resetRequest is null || string.IsNullOrWhiteSpace(resetRequest.Username) || string.IsNullOrEmpty(resetRequest.Password))
        {
            _logger.LogError("The password reset file {FilePath} is missing a username or a password. Continuing startup without resetting any password. Fix or remove the file, then restart the server to try again.", filePath);
            return false;
        }

        var user = RepoFactory.JMMUser.GetByUsername(resetRequest.Username);
        if (user is null)
        {
            StartupMessage = $"Password reset failed: no user named {resetRequest.Username}.";
            _logger.LogError("Password reset requested for username '{Username}', but no such user was found. The reset file has been kept, so it can be corrected and the server restarted to try again.", resetRequest.Username);
        }
        else
        {
            user.Password = Digest.Hash(resetRequest.Password);
            RepoFactory.JMMUser.Save(user);
            RepoFactory.AuthTokens.DeleteAllWithUserID(user.JMMUserID);
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The password for user '{Username}' was reset, but the reset file could not be deleted. Remove it manually to avoid it being processed again on the next startup.", resetRequest.Username);
            }

            StartupMessage = $"Password reset for user {resetRequest.Username} completed.";
            _logger.LogInformation("Password for user '{Username}' has been reset. The server will now exit; restart it to continue normal operation.", resetRequest.Username);
        }

        // Complete the startup task so the database unblock loop (and anything else waiting on
        // startup) finishes while the host shuts down.
        _startupTaskSource?.TrySetResult();
        _startupTaskSource = null;

        _webHost?.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        return true;
    }

    private static readonly JsonSerializerOptions _passwordResetJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private class PasswordResetRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    #endregion

    #region Startup | Database

    /// <summary>
    /// Initialize the database and repositories.
    /// </summary>
    /// <param name="databaseFactory">The database factory.</param>
    /// <param name="repositoryFactory">The repository factory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the database and repositories were initialized successfully; otherwise, <c>false</c>.</returns>
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

            // A release may only add revisions to a version it already
            // opened, so any step still to run warrants a backup.
            var upgrading = version is not 0 && (version < instance.RequiredVersion || instance.HasPendingSchemaSteps());
            if (upgrading)
            {
                StartupMessage = "Database changes detected. Database backup in progress...";
                var backupName = instance.GetDatabaseBackupName(version);
                instance.BackupDatabase(backupName);
                MetadataNumberRegistry.CopyWithBackup(backupName);
                PluginDatabaseBackups.CopyWithBackup(ApplicationPaths.Instance, backupName, _logger);
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

                StartupMessage = "Database - Checking stored metadata sources and entity types...";
                DatabaseFixes.CheckStoredMetadataNumbers(databaseFactory, message => StartupMessage = $"Database - {message}");

                // Nothing else writes yet, so the file is rebuilt without
                // blocking the API or draining the queue.
                StartupCompaction.Run(instance, upgrading, _logger, message => StartupMessage = message);
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

    private static readonly TimeSpan _anidbShutdownTimeout = TimeSpan.FromSeconds(10);

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

            // Bounded: this runs inside the host's stopping callbacks, and the host cannot stop the
            // queue or anything else until they return. The queue is paused first so it starts
            // nothing new meanwhile; its own stop pauses it too, so this changes nothing after.
            try
            {
                _webHost.Services.GetRequiredService<QueueHandler>().Pause().GetAwaiter().GetResult();

                var udpConnectionHandler = _webHost.Services.GetRequiredService<AniDBUDPConnectionHandler>();
                udpConnectionHandler.ShutdownAsync(_anidbShutdownTimeout).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending the AniDB UDP session");
            }
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

    #region Shutdown | Restart Required

    /// <inheritdoc/>
    /// <remarks>
    ///   Handlers are added straight to the tracker, which calls each on its
    ///   own, so one that throws does not keep the others from being told.
    /// </remarks>
    public event EventHandler<RestartReasonsChangedEventArgs>? RestartReasonsChanged
    {
        add => _restartReasons.Changed += value;
        remove => _restartReasons.Changed -= value;
    }

    /// <inheritdoc/>
    public IReadOnlyList<RestartReason> RestartReasons => _restartReasons.Reasons;

    /// <inheritdoc/>
    public bool RestartRequired => RestartReasons.Count > 0;

    /// <inheritdoc/>
    public IRestartRequirement RequireRestart<TPlugin>(string description) where TPlugin : class, IPlugin
        => RequireRestart(
            _pluginManager.GetPluginInfo<TPlugin>() is { IsActive: true } plugin
                ? plugin
                : throw new InvalidOperationException($"Could not raise a restart reason for {typeof(TPlugin).FullName}: it is not an active plugin."),
            description
        );

    /// <summary>
    ///   Raises a restart reason for a known plugin.
    /// </summary>
    /// <param name="plugin">The plugin raising the reason.</param>
    /// <param name="description">A short, human-readable description of the change.</param>
    /// <returns>The hold on the reason.</returns>
    /// <exception cref="ArgumentException"><paramref name="plugin"/> is not active, or <paramref name="description"/> is empty.</exception>
    internal IRestartRequirement RequireRestart(LocalPluginInfo plugin, string description)
        => _restartReasons.Require(plugin, description);

    #endregion

    #endregion

    #region Unknown Languages

    /// <summary>
    ///   Names the first few methods outside the language lookup and the
    ///   framework on the current call stack, so a report of an unknown
    ///   language string says where the string came from. Each string is
    ///   reported once, so walking the stack costs little.
    /// </summary>
    /// <returns>The calling methods, innermost first.</returns>
    private static string DescribeLanguageCaller()
    {
        var callers = new System.Diagnostics.StackTrace(false).GetFrames()
            .Select(frame => frame.GetMethod())
            .Where(method => method?.DeclaringType is { } type
                && type.Namespace?.StartsWith("Shoko.", StringComparison.Ordinal) is true
                && type != typeof(LanguageExtensions)
                && type != typeof(SystemService))
            .Take(3)
            .Select(method => $"{method!.DeclaringType!.Name}.{method.Name}")
            .ToList();
        return callers.Count > 0 ? string.Join(" < ", callers) : "an unknown caller";
    }

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
                using (DetachedFlow.Suppress())
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

            // A blocking task that faulted (a failed startup, for one) means the database never became
            // usable, so keep the block up and fail the waiters rather than unblock them. The block then
            // stays until the process restarts, which is the only way out of a failed startup anyway.
            var finished = tasks[task];
            if (finished.IsFaulted || finished.IsCanceled)
            {
                lock (_databaseBlockingTasks)
                {
                    _databaseBlockingTasks.Remove(finished);
                    _databaseTasksChangedCTS = null;
                }

                if (finished.Exception is { } exception)
                    taskSource.TrySetException(exception.InnerExceptions);
                else
                    taskSource.TrySetCanceled();
                _logger.LogError(finished.Exception, "A database blocking task failed; the database stays blocked until the server is restarted.");
                return;
            }

            lock (_databaseBlockingTasks)
            {
                _databaseBlockingTasks.Remove(finished);
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
