using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
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
using NLog.Extensions.Logging;
using NLog.Web;
using Quartz;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Services;
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

    private Timer? _autoUpdateTimer;

    private IHost? _webHost;

    public SystemService()
    {
        var now = DateTime.UtcNow;
        var args = Environment.GetCommandLineArgs();

        Utils.SetInstance();
        Utils.InitLogger();
        var loggerFactory = LoggerFactory.Create(o => o.AddNLog());

        Version = PluginManager.GetVersionInformation();

        _logger = loggerFactory.CreateLogger<SystemService>();
        _pluginManager = new(loggerFactory.CreateLogger<PluginManager>(), this, ApplicationPaths.Instance);
        _configurationService = new(loggerFactory, ApplicationPaths.Instance, _pluginManager);
        _settingsProvider = new(loggerFactory.CreateLogger<SettingsProvider>(), this, _configurationService.CreateProvider<ServerSettings>());
        _databaseBlockingTasks.Add(_startupTaskSource.Task);

        CanShutdown = args.Contains("--shutdown-enabled");
        CanRestart = args.Contains("--restart-enabled");
        BootstrappedAt = now;

        // Set the singleton instance for the settings provider.
        Utils.SettingsProvider = _settingsProvider;
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

    private string? _startupMessage = string.Empty;

    /// <inheritdoc/>
    public string? StartupMessage
    {
        get => _startupMessage;
        internal set
        {
            // We only allow setting it during startup.
            if (_startupMessage is null && StartedAt.HasValue)
                return;

            var changed = !string.Equals(_startupMessage, value);
            _startupMessage = value;
            if (value is { Length: > 0 } && changed)
            {
                _logger.LogInformation("Starting Server: {Message}", value);
                Task.Run(() => StartupMessageChanged?.Invoke(this, new() { Message = value }));
            }
        }
    }

    private StartupFailedException? _startupFailedException;

    /// <inheritdoc/>
    public StartupFailedException? StartupFailedException
    {
        get => _startupFailedException;
        private set
        {
            if (value is null || _startupFailedException is not null) return;
            lock (_logger)
            {
                if (_startupFailedException is not null) return;
                _startupFailedException = value;
                InSetupMode = false;
                // Always allow shutdown if we failed to start.
                CanShutdown = true;
            }

            Task.Run(() => StartupFailed?.Invoke(this, new(value)));

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

            // Set the setup mode flag before proceeding.
            InSetupMode = settings.FirstRun;

            // Set default culture.
            var culture = CultureInfo.GetCultureInfo(settings.Culture);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Set default options for MessagePack.
            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
            MessagePackSerializer.Typeless.DefaultOptions = MessagePackSerializer.Typeless.DefaultOptions.WithAllowAssemblyVersionMismatch(true)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            // Log some basic information about the server before we start.
            _logger.LogInformation("Shoko Server: {Version}", Version);
            _logger.LogInformation("Operating System: {OSInfo}", RuntimeInformation.OSDescription);

            try
            {
                var mediaInfoVersion = MediaInfoUtility.GetVersion();
                mediaInfoVersion ??= "Program NOT found";
                _logger.LogInformation("MediaInfo: {version}", mediaInfoVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to read MediaInfo version: {Message}", ex.Message);
            }

            try
            {
                var version = CoreHashProvider.GetRhashVersion();
                version ??= "Library NOT found";
                _logger.LogInformation("RHash: {version}", version);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to read RHash version: {Message}", ex.Message);
            }

            StartupMessage = "Scanning for Plugins...";

            _pluginManager.ScanForPlugins();

            StartupMessage = "Scan for plugins completed.";

            StartupMessage = "Initializing Web Host & Services.";

            _webHost = InitWebHost(settings);

            Utils.ServiceContainer = _webHost.Services;

            StartupMessage = "Web Host & Services initialized.";

            StartupMessage = "Starting Log Rotator.";

            _webHost.Services.GetRequiredService<LogRotator>().Start();

            StartupMessage = "Log Rotator initialized.";

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
    {
        throw new NotImplementedException();
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

    #region Startup | Services

    private IHost InitWebHost(IServerSettings settings)
        => new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
                webHostBuilder
                    .UseKestrel(options => options.ListenAnyIP(settings.Web.Port))
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
            )
            .Build();

    private class Startup(SystemService systemService, IConfigurationService configurationService, ISettingsProvider settingsProvider, IPluginManager pluginManager)
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(systemService);
            services.AddSingleton<ISystemService>(systemService);
            services.AddSingleton(configurationService);
            services.AddSingleton(settingsProvider);
            services.AddSingleton(pluginManager);
            services.AddSingleton(ApplicationPaths.Instance);

            services.AddSingleton<IPluginPackageManager, PluginPackageManager>();
            services.AddSingleton<FileWatcherService>();
            services.AddSingleton<LogRotator>();
            services.AddSingleton<TraktTVHelper>();
            services.AddSingleton<TmdbImageService>();
            services.AddSingleton<TmdbLinkingService>();
            services.AddSingleton<ITmdbLinkingService>(sp => sp.GetRequiredService<TmdbLinkingService>());
            services.AddSingleton<TmdbMetadataService>();
            services.AddSingleton<ITmdbMetadataService>(sp => sp.GetRequiredService<TmdbMetadataService>());
            services.AddSingleton<TmdbSearchService>();
            services.AddSingleton<ITmdbSearchService>(sp => sp.GetRequiredService<TmdbSearchService>());
            services.AddSingleton<IFilterEvaluator, FilterEvaluator>();
            services.AddSingleton<LegacyFilterConverter>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<AnimeSeriesService>();
            services.AddSingleton<AnimeGroupService>();
            services.AddSingleton<CssThemeService>();
            services.AddSingleton<ISystemUpdateService, SystemUpdateService>();
            services.AddSingleton<IMetadataService, AbstractMetadataService>();
            services.AddSingleton<IVideoService, VideoService>();
            services.AddSingleton<IVideoReleaseService, VideoReleaseService>();
            services.AddSingleton<IVideoHashingService, VideoHashingService>();
            services.AddSingleton<IVideoRelocationService, VideoRelocationService>();
            services.AddSingleton(typeof(ConfigurationProvider<>));
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IUserDataService, UserDataService>();
            services.AddSingleton<IImageManager, AbstractImageManager>();
            services.AddSingleton<IConnectivityService, ConnectivityService>();
            services.AddScoped<AnimeGroupCreator>();

            services.AddRepositories();
            services.AddSentryConfig();
            services.AddQuartz(systemService);

            services.AddHttpClient("GitHub", client =>
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                    client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{systemService.Version.Version.ToSemanticVersioningString()} (https://github.com/{settingsProvider.GetSettings().Web.ServerRepoName})");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
                    if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } githubToken)
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = DecompressionMethods.All,
                });
            services.AddHttpClient("PluginPackages", client =>
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain");
                    client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{systemService.Version.Version.ToSemanticVersioningString()} (https://github.com/{settingsProvider.GetSettings().Web.ServerRepoName})");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
                    client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = DecompressionMethods.All,
                });
            services.AddAniDB();
            services.AddSingleton<AnidbService>();
            services.AddSingleton<IAnidbService>(sp => sp.GetRequiredService<AnidbService>());
            services.AddSingleton<IAnidbAvdumpService>(sp => sp.GetRequiredService<AnidbService>());

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
            var schedulerFactory = _webHost!.Services.GetRequiredService<ISchedulerFactory>();
            var databaseFactory = _webHost.Services.GetRequiredService<DatabaseFactory>();
            var repoFactory = _webHost.Services.GetRequiredService<RepoFactory>();
            var fileWatcherService = _webHost.Services.GetRequiredService<FileWatcherService>();
            var lifetime = _webHost.Services.GetRequiredService<IHostApplicationLifetime>();
            var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, _shutdownTokenSource.Token).Token;
            if (cancellationToken.IsCancellationRequested)
                return;

            StartupMessage = "Setting up database...";
            if (!InitializeDatabase(databaseFactory, repoFactory, cancellationToken) && !cancellationToken.IsCancellationRequested)
                return;

            if (cancellationToken.IsCancellationRequested)
                return;

            StartupMessage = "Initializing Session Factory...";
            databaseFactory.CloseSessionFactory();
            _ = databaseFactory.SessionFactory;

            if (cancellationToken.IsCancellationRequested)
                return;

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

            // Start the timer for automatic updates now.
            _autoUpdateTimer = new Timer
            {
                AutoReset = true,
                Interval = TimeSpan.FromMinutes(5).TotalMilliseconds,
            };
            _autoUpdateTimer.Elapsed += AutoUpdateTimer_Elapsed;
            _autoUpdateTimer.Start();

            StartedAt = DateTime.UtcNow;

            Task.Run(() => Started?.Invoke(this, EventArgs.Empty));

            StartupMessage = "Startup Complete!";
            StartupMessage = null;

            _startupTaskSource?.SetResult();
            _startupTaskSource = null;

            var scheduler = schedulerFactory.GetScheduler().Result;
            if (settings.Import.ScanDropFoldersOnStart)
                scheduler.StartJob<ScanDropFoldersJob>().GetAwaiter().GetResult();
            if (settings.Import.RunOnStart)
                scheduler.StartJob<ImportJob>().GetAwaiter().GetResult();
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

    /// <inheritdoc/>
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
        actionService.CheckForPluginUpdates(false).GetAwaiter().GetResult();
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

    /// <inheritdoc/>
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
