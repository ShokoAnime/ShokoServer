using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Databases;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Plex;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Trinet.Core.IO.Ntfs;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Server;

public class ShokoServer
{
    private readonly ILogger<ShokoServer> _logger;
    private readonly DatabaseFactory _databaseFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly RepoFactory _repoFactory;
    private readonly FileWatcherService _fileWatcherService;

    private static DateTime? _startTime;

    public static TimeSpan? UpTime => _startTime == null ? null : DateTime.Now - _startTime;

    private readonly BackgroundWorker _workerSetupDB = new();

    // TODO: Move all of these to Quartz

    private static Timer _autoUpdateTimer;

    private readonly BackgroundWorker _downloadImagesWorker = new();


    public ShokoServer(ILogger<ShokoServer> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, DatabaseFactory databaseFactory, RepoFactory repoFactory, FileWatcherService fileWatcherService)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _databaseFactory = databaseFactory;
        _repoFactory = repoFactory;
        _fileWatcherService = fileWatcherService;

        var culture = CultureInfo.GetCultureInfo(settingsProvider.GetSettings().Culture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        ShokoEventHandler.Instance.Shutdown += OnShutdown;
    }

    private void OnShutdown(object sender, EventArgs e)
    {
        ShutDown();
    }

    ~ShokoServer()
    {
        ShokoEventHandler.Instance.Shutdown -= OnShutdown;
    }

    public bool StartUpServer()
    {
        // TODO Nuke the BackgroundWorkers
        // Check if any of the DLL are blocked, common issue with daily builds
        if (!CheckBlockedFiles())
        {
            Utils.ShowErrorMessage("Blocked DLL files found in server directory!");
            Environment.Exit(1);
        }

        //HibernatingRhinos.Profiler.Appender.NHibernate.NHibernateProfiler.Initialize();
        //CommandHelper.LoadCommands(Utils.ServiceContainer);

        _settingsProvider.DebugSettingsToLog();

        ServerState.Instance.DatabaseAvailable = false;
        ServerState.Instance.ServerOnline = false;
        ServerState.Instance.ServerStarting = false;
        ServerState.Instance.StartupFailed = false;
        ServerState.Instance.StartupFailedMessage = string.Empty;

        _downloadImagesWorker.DoWork += DownloadImagesWorker_DoWork;
        _downloadImagesWorker.WorkerSupportsCancellation = true;

        _workerSetupDB.WorkerReportsProgress = true;
        _workerSetupDB.ProgressChanged += (_, _) => WorkerSetupDB_ReportProgress();
        _workerSetupDB.DoWork += WorkerSetupDB_DoWork;
        _workerSetupDB.RunWorkerCompleted += WorkerSetupDB_RunWorkerCompleted;

        // run rotator once and set 24h delay
        Utils.ServiceContainer.GetRequiredService<LogRotator>().Start();

        ShokoEventHandler.Instance.OnStarting();

        // for log readability, this will simply init the singleton
        Task.Run(() => Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>().Init());
        Task.Run(() => Utils.ServiceContainer.GetRequiredService<RenameFileService>().AllRenamers);
        return true;
    }

    private bool CheckBlockedFiles()
    {
        if (Utils.IsRunningOnLinuxOrMac()) return true;

        var programlocation =
            Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        var dllFiles = Directory.GetFiles(programlocation, "*.dll", SearchOption.AllDirectories);
        var result = true;

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
        }

        foreach (var dllFile in dllFiles)
        {
            if (!FileSystem.AlternateDataStreamExists(dllFile, "Zone.Identifier")) continue;
            _logger.LogError("Found blocked DLL file: " + dllFile);
            result = false;
        }


        return result;
    }

    #region Database settings and initial start up

    public event EventHandler DBSetupCompleted;

    private void WorkerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        ServerState.Instance.ServerStarting = false;
        if (e.Result is not bool setupComplete) return;
        if (setupComplete) return;

        var settings = _settingsProvider.GetSettings();
        ServerState.Instance.ServerOnline = false;
        if (!string.IsNullOrEmpty(settings.Database.Type)) return;

        settings.Database.Type = Constants.DatabaseType.Sqlite;
    }

    private void WorkerSetupDB_ReportProgress()
    {
        _logger.LogInformation("Starting Server: Complete!");
        ServerState.Instance.ServerStartingStatus = "Complete!";
        ServerState.Instance.ServerOnline = true;
        var settings = _settingsProvider.GetSettings();
        if (settings.FirstRun)
        {
            settings.FirstRun = false;
            _settingsProvider.SaveSettings(settings);
        }

        DBSetupCompleted?.Invoke(this, EventArgs.Empty);
        ShokoEventHandler.Instance.OnStarted();
    }

    private void WorkerSetupDB_DoWork(object sender, DoWorkEventArgs e)
    {
        ServerState.Instance.DatabaseAvailable = false;
        var settings = _settingsProvider.GetSettings();

        try
        {
            ServerState.Instance.ServerOnline = false;
            ServerState.Instance.ServerStarting = true;
            ServerState.Instance.StartupFailed = false;
            ServerState.Instance.StartupFailedMessage = string.Empty;
            ServerState.Instance.ServerStartingStatus = "Cleaning up...";

            _fileWatcherService.StopWatchingFiles();

            if (_autoUpdateTimer != null)
            {
                _autoUpdateTimer.Enabled = false;
            }

            _databaseFactory.CloseSessionFactory();

            ServerState.Instance.ServerStartingStatus = "Initializing...";
            Thread.Sleep(1000);

            ServerState.Instance.ServerStartingStatus = "Setting up database...";

            _logger.LogInformation("Setting up database...");
            if (!InitDB(out var errorMessage))
            {
                ServerState.Instance.DatabaseAvailable = false;

                if (string.IsNullOrEmpty(settings.Database.Type))
                {
                    ServerState.Instance.ServerStartingStatus = "Please select and configure your database.";
                }

                e.Result = false;
                ServerState.Instance.StartupFailed = true;
                ServerState.Instance.StartupFailedMessage = errorMessage;
                return;
            }

            _logger.LogInformation("Initializing Session Factory...");
            //init session factory
            ServerState.Instance.ServerStartingStatus = "Initializing Session Factory...";
            var _ = _databaseFactory.SessionFactory;
            ServerState.Instance.DatabaseAvailable = true;


            // timer for automatic updates
            _autoUpdateTimer = new Timer
            {
                AutoReset = true,
                Interval = 5 * 60 * 1000 // 5 * 60 seconds (5 minutes)
            };
            _autoUpdateTimer.Elapsed += AutoUpdateTimer_Elapsed;
            _autoUpdateTimer.Start();

            ServerState.Instance.ServerStartingStatus = "Initializing File Watchers...";

            _fileWatcherService.StartWatchingFiles();

            var scheduler = _schedulerFactory.GetScheduler().Result;
            if (settings.Import.ScanDropFoldersOnStart) scheduler.StartJob<ScanDropFoldersJob>().GetAwaiter().GetResult();
            if (settings.Import.RunOnStart) scheduler.StartJob<ImportJob>().GetAwaiter().GetResult();

            ServerState.Instance.ServerOnline = true;
            _workerSetupDB.ReportProgress(100);

            _startTime = DateTime.Now;

            e.Result = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            ServerState.Instance.ServerStartingStatus = ex.Message;
            ServerState.Instance.StartupFailed = true;
            ServerState.Instance.StartupFailedMessage = $"Startup Failed: {ex}";
            e.Result = false;
        }
    }

    public bool InitDB(out string errorMessage)
    {
        try
        {
            _databaseFactory.Instance = null;
            var instance = _databaseFactory.Instance;
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

            _databaseFactory.CloseSessionFactory();

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
                _repoFactory.Init();
                instance.ExecuteDatabaseFixes();
                instance.PopulateInitialData();
                _repoFactory.PostInit();
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

            errorMessage = string.Empty;
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

    #region Update all media info

    public void RefreshAllMediaInfo()
    {
        var scheduler = _schedulerFactory.GetScheduler().Result;
        scheduler.StartJob<MediaInfoAllFilesJob>().GetAwaiter().GetResult();
    }

    #endregion

    public void DownloadAllImages()
    {
        if (!_downloadImagesWorker.IsBusy)
        {
            _downloadImagesWorker.RunWorkerAsync();
        }
    }

    private void DownloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        var actionService = Utils.ServiceContainer.GetRequiredService<ActionService>();
        actionService.RunImport_GetImages().GetAwaiter().GetResult();
    }

    #region Tray Minimize

    private void ShutDown()
    {
        _fileWatcherService.StopWatchingFiles();
        AniDBDispose();
    }

    #endregion

    private static void AutoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        var actionService = Utils.ServiceContainer.GetRequiredService<ActionService>();
        actionService.CheckForUnreadNotifications(false).GetAwaiter().GetResult();
        actionService.CheckForCalendarUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForAnimeUpdate().GetAwaiter().GetResult();
        actionService.CheckForMyListSyncUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForTraktAllSeriesUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForAniDBFileUpdate(false).GetAwaiter().GetResult();
    }

    private static void AniDBDispose()
    {
        var handler = Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
        handler.ForceLogout();
        handler.CloseConnections();
    }

    public static int OnHashProgress(string fileName, int percentComplete)
    {
        //string msg = Path.GetFileName(fileName);
        //if (msg.Length > 35) msg = msg.Substring(0, 35);
        //logger.LogInformation("{0}% Hashing ({1})", percentComplete, Path.GetFileName(fileName));
        return 1; //continue hashing (return 0 to abort)
    }

    /// <summary>
    /// Sync plex watch status.
    /// </summary>
    /// <returns>true if there was any commands added to the queue, flase otherwise</returns>
    public async Task<bool> SyncPlex()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var flag = false;
        foreach (var user in RepoFactory.JMMUser.GetAll())
        {
            if (string.IsNullOrEmpty(user.PlexToken)) continue;
            flag = true;
            await scheduler.StartJob<SyncPlexWatchedStatesJob>(c => c.User = user);
        }

        return flag;
    }

    public void RunWorkSetupDB()
    {
        _workerSetupDB.RunWorkerAsync();
    }
}
