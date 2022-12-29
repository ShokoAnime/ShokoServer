using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Timers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using NLog.Web;
using Sentry;
using Shoko.Commons.Properties;
using Shoko.Server.API;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.JMMAutoUpdates;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;
using Shoko.Server.UI;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.FileSystemWatcher;
using Trinet.Core.IO.Ntfs;
using LogLevel = NLog.LogLevel;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Server;

public class ShokoServer
{
    //private static bool doneFirstTrakTinfo = false;
    private readonly ILogger<ShokoServer> logger;
    internal static LogRotator logrotator = new();
    private static DateTime lastTraktInfoUpdate = DateTime.Now;
    private static DateTime lastVersionCheck = DateTime.Now;

    public static DateTime? StartTime;

    public static TimeSpan? UpTime => StartTime == null ? null : DateTime.Now - StartTime;
    private static IDisposable _sentry;

    public static string PathAddressREST = "api/Image";
    public static string PathAddressPlex = "api/Plex";
    public static string PathAddressKodi = "Kodi";

    private static IWebHost webHost;

    private static BackgroundWorker workerImport = new();
    private static BackgroundWorker workerScanFolder = new();
    private static BackgroundWorker workerScanDropFolders = new();
    private static BackgroundWorker workerRemoveMissing = new();
    private static BackgroundWorker workerDeleteImportFolder = new();
    private static BackgroundWorker workerMediaInfo = new();

    internal static BackgroundWorker workerSetupDB = new();
    internal static BackgroundWorker LogRotatorWorker = new();

    private static Timer autoUpdateTimer;
    private static Timer autoUpdateTimerShort;
    internal static Timer LogRotatorTimer;

    private DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
    private List<RecoveringFileSystemWatcher> _fileWatchers;

    private BackgroundWorker downloadImagesWorker = new();

    public static List<UserCulture> userLanguages = new();

    private Mutex mutex;
    private const string SentryDsn = "https://47df427564ab42f4be998e637b3ec45a@o330862.ingest.sentry.io/1851880";

    public string[] GetSupportedDatabases()
    {
        return new[] { "SQLite", "Microsoft SQL Server 2014", "MySQL/MariaDB" };
    }

    private ShokoServer(ILogger<ShokoServer> logger)
    {
        this.logger = logger;
        SetupNetHosts();
    }

    ~ShokoServer()
    {
        _sentry.Dispose();
        ShutDown();
    }

    public static void SetTraceLogging(bool enabled)
    {
        var rule = LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Any(b => b is FileTarget));
        if (rule == null)
        {
            return;
        }

        if (enabled)
        {
            rule.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Debug);
        }
        else
        {
            rule.DisableLoggingForLevel(LogLevel.Trace);
        }

        LogManager.ReconfigExistingLoggers();
    }

    public bool StartUpServer()
    {
        _sentry = SentrySdk.Init(opts =>
        {
            opts.Dsn = SentryDsn;
            opts.Release = Utils.GetApplicationVersion();
        });


        Analytics.PostEvent("Server", "Startup");
        if (Utils.IsLinux)
        {
            Analytics.PostEvent("Server", "Linux Startup");
        }

        var settingsProvider = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>();
        var settings = settingsProvider.GetSettings();
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(settings.Culture);

        // Check if any of the DLL are blocked, common issue with daily builds
        if (!CheckBlockedFiles())
        {
            Utils.ShowErrorMessage(Resources.ErrorBlockedDll);
            Environment.Exit(1);
        }

        // Migrate programdata folder from JMMServer to ShokoServer
        // this needs to run before UnhandledExceptionManager.AddHandler(), because that will probably lock the log file
        if (!MigrateProgramDataLocation())
        {
            Utils.ShowErrorMessage(Resources.Migration_LoadError,
                Resources.ShokoServer);
            Environment.Exit(1);
        }

        //HibernatingRhinos.Profiler.Appender.NHibernate.NHibernateProfiler.Initialize();
        CommandHelper.LoadCommands(Utils.ServiceContainer);

        if (!Utils.IsLinux)
        {
            try
            {
                mutex = Mutex.OpenExisting(Utils.DefaultInstance + "Mutex");
                //since it hasn't thrown an exception, then we already have one copy of the app open.
                return false;
                //MessageBox.Show(Shoko.Commons.Properties.Resources.Server_Running,
                //    Shoko.Commons.Properties.Resources.ShokoServer, MessageBoxButton.OK, MessageBoxImage.Error);
                //Environment.Exit(0);
            }
            catch (Exception ex)
            {
                //since we didn't find a mutex with that name, create one
                Debug.WriteLine("Exception thrown:" + ex.Message + " Creating a new mutex...");
                mutex = new Mutex(true, Utils.DefaultInstance + "Mutex");
            }
        }

        // RenameFileHelper.InitialiseRenamers();
        // var services = new ServiceCollection();
        // ConfigureServices(services);
        // Plugin.Loader.Instance.Load(services);
        // Utils.ServiceContainer = services.BuildServiceProvider();
        // Plugin.Loader.Instance.InitPlugins(Utils.ServiceContainer);

        settingsProvider.DebugSettingsToLog();

        //logrotator worker setup
        LogRotatorWorker.WorkerReportsProgress = false;
        LogRotatorWorker.WorkerSupportsCancellation = false;
        LogRotatorWorker.DoWork += LogRotatorWorker_DoWork;
        LogRotatorWorker.RunWorkerCompleted +=
            LogRotatorWorker_RunWorkerCompleted;

        ServerState.Instance.DatabaseAvailable = false;
        ServerState.Instance.ServerOnline = false;
        ServerState.Instance.ServerStarting = false;
        ServerState.Instance.StartupFailed = false;
        ServerState.Instance.StartupFailedMessage = string.Empty;
        ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();

        downloadImagesWorker.DoWork += DownloadImagesWorker_DoWork;
        downloadImagesWorker.WorkerSupportsCancellation = true;

        workerMediaInfo.DoWork += WorkerMediaInfo_DoWork;

        workerImport.WorkerReportsProgress = true;
        workerImport.WorkerSupportsCancellation = true;
        workerImport.DoWork += WorkerImport_DoWork;

        workerScanFolder.WorkerReportsProgress = true;
        workerScanFolder.WorkerSupportsCancellation = true;
        workerScanFolder.DoWork += WorkerScanFolder_DoWork;


        workerScanDropFolders.WorkerReportsProgress = true;
        workerScanDropFolders.WorkerSupportsCancellation = true;
        workerScanDropFolders.DoWork += WorkerScanDropFolders_DoWork;

        workerRemoveMissing.WorkerReportsProgress = true;
        workerRemoveMissing.WorkerSupportsCancellation = true;
        workerRemoveMissing.DoWork += WorkerRemoveMissing_DoWork;

        workerDeleteImportFolder.WorkerReportsProgress = false;
        workerDeleteImportFolder.WorkerSupportsCancellation = true;
        workerDeleteImportFolder.DoWork += WorkerDeleteImportFolder_DoWork;

        workerSetupDB.WorkerReportsProgress = true;
        workerSetupDB.ProgressChanged += (sender, args) => WorkerSetupDB_ReportProgress();
        workerSetupDB.DoWork += WorkerSetupDB_DoWork;
        workerSetupDB.RunWorkerCompleted += WorkerSetupDB_RunWorkerCompleted;

        ServerState.Instance.LoadSettings(settings);

        InitCulture();
        Utils.ShokoServer = this;

        // run rotator once and set 24h delay
        logrotator.Start();
        StartLogRotatorTimer();

        Analytics.PostEvent("Server", "StartupFinished");
        // for log readability, this will simply init the singleton
        Utils.ServiceContainer.GetService<IUDPConnectionHandler>();
        return true;
    }

    private bool CheckBlockedFiles()
    {
        if (Utils.IsRunningOnLinuxOrMac())
        {
            return true;
        }

        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            // do stuff on windows only
            return true;
        }

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
            if (FileSystem.AlternateDataStreamExists(dllFile, "Zone.Identifier"))
            {
                logger.LogError("Found blocked DLL file: " + dllFile);
                result = false;
            }
        }


        return result;
    }

    public bool MigrateProgramDataLocation()
    {
        var oldApplicationPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "JMMServer");
        var newApplicationPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Assembly.GetEntryAssembly().GetName().Name);
        if (Directory.Exists(oldApplicationPath) && !Directory.Exists(newApplicationPath))
        {
            try
            {
                var migrationdirs = new List<MigrationDirectory>
                {
                    new() { From = oldApplicationPath, To = newApplicationPath }
                };

                foreach (var md in migrationdirs)
                {
                    if (!md.SafeMigrate())
                    {
                        break;
                    }
                }

                logger.LogInformation("Successfully migrated programdata folder");
            }
            catch (Exception e)
            {
                logger.LogError("Error occured during MigrateProgramDataLocation(): {Ex}", e);
                return false;
            }
        }

        return true;
    }

    public bool NetPermissionWrapper(Func<bool> action)
    {
        try
        {
            if (!action()) return false;
        }
        catch (Exception e)
        {
            if (Utils.IsAdministrator())
            {
                Utils.ShowMessage(null, "Settings the ports, after that JMMServer will quit, run again in normal mode");

                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Utils.ShowErrorMessage("Unable start hosting");
                    logger.LogError("Unable to run task: {MethodName}", action.Method.Name);
                    logger.LogError(exception, "Error was: {Ex}", exception);
                }
                finally
                {
                    ShutDown();
                }

                return false;
            }

            Utils.ShowErrorMessage("Unable to start hosting, please run Shoko Server as administrator once");
            logger.LogError(e, "Error was: {Ex}", e);
            ShutDown();
            return false;
        }

        return true;
    }

    private void LogRotatorWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        // for later use
    }

    private void LogRotatorWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        logrotator.Start();
    }

    private void InitCulture()
    {
    }


    #region Database settings and initial start up

    public event EventHandler LoginFormNeeded;
    public event EventHandler DatabaseSetup;
    public event EventHandler DBSetupCompleted;

    private void WorkerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        ServerState.Instance.ServerStarting = false;
        var setupComplete = bool.Parse(e.Result.ToString());
        if (!setupComplete)
        {
            var settings = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>().GetSettings();
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
        ServerInfo.Instance.RefreshImportFolders();
        ServerState.Instance.ServerStartingStatus = Resources.Server_Complete;
        ServerState.Instance.ServerOnline = true;
        var settingsProvider = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>();
        var settings = settingsProvider.GetSettings();
        settings.FirstRun = false;
        settingsProvider.SaveSettings();
        if (string.IsNullOrEmpty(settings.AniDb.Username) ||
            string.IsNullOrEmpty(settings.AniDb.Password))
        {
            LoginFormNeeded?.Invoke(this, null);
        }

        DBSetupCompleted?.Invoke(this, null);
        
        // Start queues
        ShokoService.CmdProcessorGeneral.Paused = false;
        ShokoService.CmdProcessorHasher.Paused = false;
        ShokoService.CmdProcessorImages.Paused = false;
    }

    private void ShowDatabaseSetup()
    {
        DatabaseSetup?.Invoke(this, null);
    }

    public static void StartLogRotatorTimer()
    {
        LogRotatorTimer = new Timer
        {
            AutoReset = true,
            // 86400000 = 24h
            Interval = 86400000
        };
        LogRotatorTimer.Elapsed += LogRotatorTimer_Elapsed;
        LogRotatorTimer.Start();
    }

    private static void LogRotatorTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        logrotator.Start();
    }

    public bool SetupNetHosts()
    {
        logger.LogInformation("Initializing Web Hosts...");
        ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingHosts;
        var started = true;
        started &= NetPermissionWrapper(StartWebHost);
        if (!started)
        {
            StopHost();
            return false;
        }

        return true;
    }

    public void RestartAniDBSocket()
    {
        AniDBDispose();
        SetupAniDBProcessor();
    }

    public void StartAniDBSocket()
    {
        SetupAniDBProcessor();
    }

    private void WorkerSetupDB_DoWork(object sender, DoWorkEventArgs e)
    {
        var settingsProvider = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>();
        var settings = settingsProvider.GetSettings();
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(settings.Culture);

        try
        {
            ServerState.Instance.ServerOnline = false;
            ServerState.Instance.ServerStarting = true;
            ServerState.Instance.StartupFailed = false;
            ServerState.Instance.StartupFailedMessage = string.Empty;
            ServerState.Instance.ServerStartingStatus = Resources.Server_Cleaning;

            StopWatchingFiles();

            ShokoService.CmdProcessorGeneral.Stop();
            ShokoService.CmdProcessorHasher.Stop();
            ShokoService.CmdProcessorImages.Stop();


            // wait until the queue count is 0
            // ie the cancel has actuall worked
            while (true)
            {
                if (ShokoService.CmdProcessorGeneral.QueueCount == 0 &&
                    ShokoService.CmdProcessorHasher.QueueCount == 0 &&
                    ShokoService.CmdProcessorImages.QueueCount == 0)
                {
                    break;
                }

                Thread.Sleep(250);
            }

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

            Scanner.Instance.Init();

            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingQueue;
            ShokoService.CmdProcessorGeneral.Init(Utils.ServiceContainer);
            ShokoService.CmdProcessorHasher.Init(Utils.ServiceContainer);
            ShokoService.CmdProcessorImages.Init(Utils.ServiceContainer);

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

            DownloadAllImages();

            var folders = RepoFactory.ImportFolder.GetAll();

            if (settings.Import.ScanDropFoldersOnStart)
            {
                ScanDropFolders();
            }

            if (settings.Import.RunOnStart && folders.Count > 0)
            {
                RunImport();
            }

            ServerState.Instance.ServerOnline = true;
            workerSetupDB.ReportProgress(100);

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

    private void WorkerMediaInfo_DoWork(object sender, DoWorkEventArgs e)
    {
        // first build a list of files that we already know about, as we don't want to process them again
        var filesAll = RepoFactory.VideoLocal.GetAll();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        foreach (var vl in filesAll)
        {
            var cr = commandFactory.Create<CommandRequest_ReadMediaInfo>(c => c.VideoLocalID = vl.VideoLocalID);
            cr.Save();
        }
    }

    public static void RefreshAllMediaInfo()
    {
        if (workerMediaInfo.IsBusy)
        {
            return;
        }

        workerMediaInfo.RunWorkerAsync();
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
        Importer.RunImport_GetImages();
    }

    public class UpdateEventArgs : EventArgs
    {
        public UpdateEventArgs(long newVersion, long oldVersion, bool force)
        {
            NewVersion = newVersion;
            OldVersion = oldVersion;
            Forced = force;
        }

        public bool Forced { get; }

        public long OldVersion { get; }
        public long NewVersion { get; }
    }

    public event EventHandler<UpdateEventArgs> UpdateAvailable;

    public void CheckForUpdatesNew(bool forceShowForm)
    {
        try
        {
            long verCurrent = 0;
            long verNew = 0;

            // get the latest version as according to the release

            // get the user's version
            var a = Assembly.GetEntryAssembly();
            if (a == null)
            {
                logger.LogError("Could not get current version");
                return;
            }

            var an = a.GetName();

            //verNew = verInfo.versions.ServerVersionAbs;

            var settingsProvider = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>();
            var settings = settingsProvider.GetSettings();
            verNew =
                JMMAutoUpdatesHelper.ConvertToAbsoluteVersion(
                    JMMAutoUpdatesHelper.GetLatestVersionNumber(settings.UpdateChannel))
                ;
            verCurrent = an.Version.Revision * 100 +
                         an.Version.Build * 100 * 100 +
                         an.Version.Minor * 100 * 100 * 100 +
                         an.Version.Major * 100 * 100 * 100 * 100;

            if (forceShowForm || verNew > verCurrent)
            {
                UpdateAvailable?.Invoke(this, new UpdateEventArgs(verNew, verCurrent, forceShowForm));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    #region UI events and methods

    internal static string GetLocalIPv4(NetworkInterfaceType _type)
    {
        var output = string.Empty;
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
            {
                var adapterProperties = item.GetIPProperties();

                if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                {
                    foreach (var ip in adapterProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
        }

        return output;
    }

    public event EventHandler<ReasonedEventArgs> ServerShutdown;
    public event EventHandler ServerRestart;

    private void RestartServer()
    {
        ServerRestart?.Invoke(this, null);
    }

    #endregion

    private void AutoUpdateTimerShort_Elapsed(object sender, ElapsedEventArgs e)
    {
        autoUpdateTimerShort.Enabled = false;

        CheckForAdminMesages();


        autoUpdateTimerShort.Interval = 30 * 1000; // 30 seconds
        autoUpdateTimerShort.Enabled = true;
    }

    private void CheckForAdminMesages()
    {
        try
        {
            var lastUpdate = DateTime.Now - lastAdminMessage;

            if (lastUpdate.TotalHours > 5)
            {
                lastAdminMessage = DateTime.Now;
                ServerInfo.Instance.RefreshAdminMessages();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    #region Tray Minimize

    private void ShutDown()
    {
        StopWatchingFiles();
        AniDBDispose();
        StopHost();
        ServerShutdown?.Invoke(this, null);
        Analytics.PostEvent("Server", "Shutdown");
    }

    #endregion

    private static void AutoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        Importer.CheckForDayFilters();
        Importer.CheckForCalendarUpdate(false);
        Importer.CheckForAnimeUpdate(false);
        Importer.CheckForTvDBUpdates(false);
        Importer.CheckForMyListSyncUpdate(false);
        Importer.CheckForTraktAllSeriesUpdate(false);
        Importer.CheckForTraktTokenUpdate(false);
        Importer.CheckForMyListStatsUpdate(false);
        Importer.CheckForAniDBFileUpdate(false);
    }

    public void StartWatchingFiles()
    {
        _fileWatchers = new List<RecoveringFileSystemWatcher>();
        var settingsProvider = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>();
        var settings = settingsProvider.GetSettings();

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
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        if (!File.Exists(path)) return;
        if (!FileHashHelper.IsVideo(path)) return;

        logger.LogInformation("Found file {0}", path);
        var tup = VideoLocal_PlaceRepository.GetFromFullPath(path);
        ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(path));
        var cmd = commandFactory.Create<CommandRequest_HashFile>(c => c.FileName = path);
        cmd.Save();
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

    public static void ScanDropFolders()
    {
        if (!workerScanDropFolders.IsBusy)
        {
            workerScanDropFolders.RunWorkerAsync();
        }
    }

    public static void ScanFolder(int importFolderID)
    {
        if (!workerScanFolder.IsBusy)
        {
            workerScanFolder.RunWorkerAsync(importFolderID);
        }
    }

    public static void RunImport()
    {
        Analytics.PostEvent("Importer", "Run");

        if (!workerImport.IsBusy)
        {
            workerImport.RunWorkerAsync();
        }
    }

    public static void RemoveMissingFiles(bool removeMyList = true)
    {
        Analytics.PostEvent("Importer", "RemoveMissing");

        if (!workerRemoveMissing.IsBusy)
        {
            workerRemoveMissing.RunWorkerAsync(removeMyList);
        }
    }

    public static void SyncMyList()
    {
        Importer.CheckForMyListSyncUpdate(true);
    }

    public static void DeleteImportFolder(int importFolderID)
    {
        if (!workerDeleteImportFolder.IsBusy)
        {
            workerDeleteImportFolder.RunWorkerAsync(importFolderID);
        }
    }

    private void WorkerRemoveMissing_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            Importer.RemoveRecordsWithoutPhysicalFiles(e.Argument as bool? ?? true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    private void WorkerDeleteImportFolder_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            var importFolderID = int.Parse(e.Argument.ToString());
            Importer.DeleteImportFolder(importFolderID);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    private void WorkerScanFolder_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            Importer.RunImport_ScanFolder(int.Parse(e.Argument.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    private void WorkerScanDropFolders_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            Importer.RunImport_DropFolders();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    private void WorkerImport_DoWork(object sender, DoWorkEventArgs e)
    {
        try
        {
            Importer.RunImport_NewFiles();
            Importer.RunImport_IntegrityCheck();

            // drop folder
            Importer.RunImport_DropFolders();

            // TvDB association checks
            Importer.RunImport_ScanTvDB();

            // Trakt association checks
            Importer.RunImport_ScanTrakt();

            // MovieDB association checks
            Importer.RunImport_ScanMovieDB();

            // Check for missing images
            Importer.RunImport_GetImages();

            // Check for previously ignored files
            Importer.CheckForPreviouslyIgnored();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
        }
    }

    private IWebHost InitWebHost(IServerSettings settings)
    {
        if (webHost != null)
        {
            return webHost;
        }

        var port = settings.ServerPort;
        var result = new WebHostBuilder().UseKestrel(options =>
            {
                options.ListenAnyIP(port);
            })
            .UseStartup<Startup>()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
#if !LOGWEB
                logging.AddFilter("Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning);
                logging.AddFilter("System", Microsoft.Extensions.Logging.LogLevel.Warning);
                logging.AddFilter("Shoko.Server.API", Microsoft.Extensions.Logging.LogLevel.Warning);
#endif
            }).UseNLog()
            .UseSentry(
                o =>
                {
                    o.Release = Utils.GetApplicationVersion();
                    o.Dsn = SentryDsn;
                })
            .Build();
        return result;
    }

    /// <summary>
    /// Running Nancy and Validating all require aspects before running it
    /// </summary>
    private bool StartWebHost()
    {
        if (webHost == null)
        {
            var settings = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>().GetSettings();
            webHost = InitWebHost(settings);
            Utils.ServiceContainer = webHost?.Services;
        }

        //JsonSettings.MaxJsonLength = int.MaxValue;

        // Even with error callbacks, this may still throw an error in some parts, so log it!
        try
        {
            if (webHost == null) return false;
            webHost.Start();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred starting the web host: {Ex}", ex);
            return false;
        }
    }

    public static void StopHost()
    {
        webHost?.Dispose();
        webHost = null;
    }

    private static void SetupAniDBProcessor()
    {
        var handler = Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
        var settings = Utils.ServiceContainer.GetRequiredService<ISettingsProvider>().GetSettings().AniDb;
        handler.Init(settings.Username, settings.Password, settings.ServerAddress, settings.ServerPort, settings.ClientPort);
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
    public bool SyncPlex()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        Analytics.PostEvent("Plex", "SyncAll");

        var flag = false;
        foreach (var user in RepoFactory.JMMUser.GetAll())
        {
            if (string.IsNullOrEmpty(user.PlexToken))
            {
                continue;
            }

            flag = true;
            commandFactory.Create<CommandRequest_PlexSyncWatched>(c => c.User = user).Save();
        }

        return flag;
    }

    public static void RunWorkSetupDB()
    {
        workerSetupDB.RunWorkerAsync();
    }

    //public static event EventHandler<ReasonedEventArgs> ServerError;
    public void ShutdownServer(ReasonedEventArgs args)
    {
        ServerShutdown?.Invoke(null, args);
    }

    public class ReasonedEventArgs : EventArgs
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string Reason { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public Exception Exception { get; set; }
    }
}
