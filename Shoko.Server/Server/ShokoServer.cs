using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Properties;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Plex;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Services.ErrorHandling;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.FileSystemWatcher;
using Trinet.Core.IO.Ntfs;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Server;

public class ShokoServer
{
    //private static bool doneFirstTrakTinfo = false;
    private readonly ILogger<ShokoServer> logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly SentryInit _sentryInit;

    private static DateTime? StartTime;

    public static TimeSpan? UpTime => StartTime == null ? null : DateTime.Now - StartTime;

    private readonly BackgroundWorker _workerSetupDB = new();

    // TODO Move all of these to Quartz
    private static Timer autoUpdateTimer;
    private static Timer autoUpdateTimerShort;

    private DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
    private List<RecoveringFileSystemWatcher> _fileWatchers;

    private BackgroundWorker downloadImagesWorker = new();


    public ShokoServer(ILogger<ShokoServer> logger, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, SentryInit sentryInit)
    {
        this.logger = logger;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _sentryInit = sentryInit;

        var culture = CultureInfo.GetCultureInfo(settingsProvider.GetSettings().Culture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        ShokoEventHandler.Instance.Shutdown += ShutDown;
    }

    private void ShutDown(object sender, CancelEventArgs e)
    {
        ShutDown();
    }

    ~ShokoServer()
    {
        ShokoEventHandler.Instance.Shutdown -= ShutDown;
    }

    public bool StartUpServer()
    {
        _sentryInit.Init(); 


        // Check if any of the DLL are blocked, common issue with daily builds
        if (!CheckBlockedFiles())
        {
            Utils.ShowErrorMessage(Resources.ErrorBlockedDll);
            Environment.Exit(1);
        }

        //HibernatingRhinos.Profiler.Appender.NHibernate.NHibernateProfiler.Initialize();
        //CommandHelper.LoadCommands(Utils.ServiceContainer);

        Loader.InitPlugins(Utils.ServiceContainer);

        _settingsProvider.DebugSettingsToLog();

        ServerState.Instance.DatabaseAvailable = false;
        ServerState.Instance.ServerOnline = false;
        ServerState.Instance.ServerStarting = false;
        ServerState.Instance.StartupFailed = false;
        ServerState.Instance.StartupFailedMessage = string.Empty;

        downloadImagesWorker.DoWork += DownloadImagesWorker_DoWork;
        downloadImagesWorker.WorkerSupportsCancellation = true;
        
        _workerSetupDB.WorkerReportsProgress = true;
        _workerSetupDB.ProgressChanged += (_, _) => WorkerSetupDB_ReportProgress();
        _workerSetupDB.DoWork += WorkerSetupDB_DoWork;
        _workerSetupDB.RunWorkerCompleted += WorkerSetupDB_RunWorkerCompleted;

        // run rotator once and set 24h delay
        Utils.ServiceContainer.GetRequiredService<LogRotator>().Start();

        ShokoEventHandler.Instance.OnStarting();

        // for log readability, this will simply init the singleton
        Task.Run(async () => await Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>().Init());
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
            logger.LogError("Found blocked DLL file: " + dllFile);
            result = false;
        }


        return result;
    }

    #region Database settings and initial start up

    public event EventHandler ServerStarting;
    public event EventHandler LoginFormNeeded;
    public event EventHandler DatabaseSetup;
    public event EventHandler DBSetupCompleted;

    private void WorkerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        ServerState.Instance.ServerStarting = false;
        var setupComplete = bool.Parse(e.Result.ToString());
        if (!setupComplete)
        {
            var settings = _settingsProvider.GetSettings();
            ServerState.Instance.ServerOnline = false;
            if (!string.IsNullOrEmpty(settings.Database.Type))
            {
                return;
            }

            settings.Database.Type = Constants.DatabaseType.Sqlite;
            ShowDatabaseSetup();
        }
    }

    private void WorkerSetupDB_ReportProgress()
    {
        logger.LogInformation("Starting Server: Complete!");
        ServerState.Instance.ServerStartingStatus = Resources.Server_Complete;
        ServerState.Instance.ServerOnline = true;
        var settings = _settingsProvider.GetSettings();
        settings.FirstRun = false;
        _settingsProvider.SaveSettings();
        if (string.IsNullOrEmpty(settings.AniDb.Username) ||
            string.IsNullOrEmpty(settings.AniDb.Password))
        {
            LoginFormNeeded?.Invoke(this, EventArgs.Empty);
        }

        DBSetupCompleted?.Invoke(this, EventArgs.Empty);
        ShokoEventHandler.Instance.OnStarted();
    }

    private void ShowDatabaseSetup()
    {
        DatabaseSetup?.Invoke(this, EventArgs.Empty);
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
            ServerState.Instance.ServerStartingStatus = Resources.Server_Cleaning;

            StopWatchingFiles();

            if (autoUpdateTimer != null)
            {
                autoUpdateTimer.Enabled = false;
            }

            if (autoUpdateTimerShort != null)
            {
                autoUpdateTimerShort.Enabled = false;
            }

            DatabaseFactory.CloseSessionFactory();

            ServerState.Instance.ServerStartingStatus = Resources.Server_Initializing;
            Thread.Sleep(1000);

            ServerState.Instance.ServerStartingStatus = Resources.Server_DatabaseSetup;

            logger.LogInformation("Setting up database...");
            if (!DatabaseFactory.InitDB(out var errorMessage))
            {
                ServerState.Instance.DatabaseAvailable = false;

                if (string.IsNullOrEmpty(settings.Database.Type))
                {
                    ServerState.Instance.ServerStartingStatus =
                        Resources.Server_DatabaseConfig;
                }

                e.Result = false;
                ServerState.Instance.StartupFailed = true;
                ServerState.Instance.StartupFailedMessage = errorMessage;
                return;
            }

            logger.LogInformation("Initializing Session Factory...");
            //init session factory
            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingSession;
            var _ = DatabaseFactory.SessionFactory;
            ServerState.Instance.DatabaseAvailable = true;


            // timer for automatic updates
            autoUpdateTimer = new Timer
            {
                AutoReset = true, Interval = 5 * 60 * 1000 // 5 * 60 seconds (5 minutes)
            };
            autoUpdateTimer.Elapsed += AutoUpdateTimer_Elapsed;
            autoUpdateTimer.Start();

            // timer for automatic updates
            autoUpdateTimerShort = new Timer
            {
                AutoReset = true, Interval = 5 * 1000 // 5 seconds, later we set it to 30 seconds
            };
            autoUpdateTimerShort.Elapsed += AutoUpdateTimerShort_Elapsed;
            autoUpdateTimerShort.Start();

            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingFile;

            StartWatchingFiles();

            var scheduler = _schedulerFactory.GetScheduler().Result;
            if (settings.Import.ScanDropFoldersOnStart) scheduler.StartJob<ScanDropFoldersJob>().GetAwaiter().GetResult();
            if (settings.Import.RunOnStart) scheduler.StartJob<ImportJob>().GetAwaiter().GetResult();

            ServerState.Instance.ServerOnline = true;
            _workerSetupDB.ReportProgress(100);

            StartTime = DateTime.Now;

            e.Result = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            ServerState.Instance.ServerStartingStatus = ex.Message;
            ServerState.Instance.StartupFailed = true;
            ServerState.Instance.StartupFailedMessage = $"Startup Failed: {ex}";
            e.Result = false;
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
        if (!downloadImagesWorker.IsBusy)
        {
            downloadImagesWorker.RunWorkerAsync();
        }
    }

    private void DownloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        var actionService = Utils.ServiceContainer.GetRequiredService<ActionService>();
        actionService.RunImport_GetImages().GetAwaiter().GetResult();
    }

    private void AutoUpdateTimerShort_Elapsed(object sender, ElapsedEventArgs e)
    {
        autoUpdateTimerShort.Enabled = false;


        autoUpdateTimerShort.Interval = 30 * 1000; // 30 seconds
        autoUpdateTimerShort.Enabled = true;
    }

    #region Tray Minimize

    private void ShutDown()
    {
        StopWatchingFiles();
        AniDBDispose();
    }

    #endregion

    private static void AutoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        var actionService = Utils.ServiceContainer.GetRequiredService<ActionService>();
        actionService.CheckForCalendarUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForAnimeUpdate().GetAwaiter().GetResult();
        actionService.CheckForTvDBUpdates(false).GetAwaiter().GetResult();
        actionService.CheckForMyListSyncUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForTraktAllSeriesUpdate(false).GetAwaiter().GetResult();
        actionService.CheckForTraktTokenUpdate(false);
        actionService.CheckForAniDBFileUpdate(false).GetAwaiter().GetResult();
    }

    public void StartWatchingFiles()
    {
        _fileWatchers = new List<RecoveringFileSystemWatcher>();
        var settings = _settingsProvider.GetSettings();

        foreach (var share in RepoFactory.ImportFolder.GetAll())
        {
            try
            {
                if (share.FolderIsWatched)
                {
                    logger.LogInformation("Watching ImportFolder: {ImportFolderName} || {ImportFolderLocation}", share.ImportFolderName, share.ImportFolderLocation);
                }

                if (Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
                {
                    
                    logger.LogInformation("Parsed ImportFolderLocation: {ImportFolderLocation}", share.ImportFolderLocation);

                    var fsw = new RecoveringFileSystemWatcher(share.ImportFolderLocation,
                        filters: settings.Import.VideoExtensions.Select(a => "." + a.ToLowerInvariant().TrimStart('.')),
                        pathExclusions: settings.Import.Exclude);
                    fsw.Options = new FileSystemWatcherLockOptions
                    {
                        Enabled = settings.Import.FileLockChecking,
                        Aggressive = settings.Import.AggressiveFileLockChecking,
                        WaitTimeMilliseconds = settings.Import.FileLockWaitTimeMS,
                        FileAccessMode = share.IsDropSource == 1 ? FileAccess.ReadWrite : FileAccess.Read,
                        AggressiveWaitTimeSeconds = settings.Import.AggressiveFileLockWaitTimeSeconds
                    };
                    fsw.FileAdded += FileAdded;
                    fsw.Start();
                    _fileWatchers.Add(fsw);
                }
                else if (!share.FolderIsWatched)
                {
                    logger.LogInformation("ImportFolder found but not watching: {Name} || {Location}", share.ImportFolderName,
                        share.ImportFolderLocation);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred initializing the Filesystem Watchers: {Ex}", ex.ToString());
            }
        }
    }

    private void FileAdded(object sender, string path)
    {
        if (!File.Exists(path)) return;
        if (!FileHashHelper.IsVideo(path)) return;

        logger.LogInformation("Found file {Path}", path);
        var tup = VideoLocal_PlaceRepository.GetFromFullPath(path);
        ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(path));
        _schedulerFactory.GetScheduler().Result.StartJob<DiscoverFileJob>(a => a.FilePath = path).GetAwaiter().GetResult();
    }

    public void AddFileWatcherExclusion(string path)
    {
        if (_fileWatchers == null || !_fileWatchers.Any()) return;
        var watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        watcher?.AddExclusion(path);
        logger.LogTrace("Added {Path} to filesystem watcher exclusions", path);
    }

    public void RemoveFileWatcherExclusion(string path)
    {
        if (_fileWatchers == null || !_fileWatchers.Any()) return;
        var watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        watcher?.RemoveExclusion(path);
        logger.LogTrace("Removed {Path} from filesystem watcher exclusions", path);
    }

    public void StopWatchingFiles()
    {
        if (_fileWatchers == null || !_fileWatchers.Any())
        {
            return;
        }

        foreach (var fsw in _fileWatchers)
        {
            fsw.Stop();
            fsw.Dispose();
        }

        _fileWatchers.Clear();
    }

    public void RemoveMissingFiles(bool removeMyList = true)
    {
        var scheduler = _schedulerFactory.GetScheduler().Result;
        scheduler.StartJob<RemoveMissingFilesJob>(a => a.RemoveMyList = removeMyList).GetAwaiter().GetResult();
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
