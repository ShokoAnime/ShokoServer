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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using Sentry;
using Shoko.Commons.Properties;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.ImageDownload;
using Shoko.Server.Plugin;
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
    private readonly ISettingsProvider _settingsProvider;
    private static DateTime lastTraktInfoUpdate = DateTime.Now;
    private static DateTime lastVersionCheck = DateTime.Now;

    public static DateTime? StartTime;

    public static TimeSpan? UpTime => StartTime == null ? null : DateTime.Now - StartTime;
    private static IDisposable _sentry;

    public static string PathAddressREST = "api/Image";
    public static string PathAddressPlex = "api/Plex";
    public static string PathAddressKodi = "Kodi";

    private static BackgroundWorker workerImport = new();
    private static BackgroundWorker workerScanFolder = new();
    private static BackgroundWorker workerScanDropFolders = new();
    private static BackgroundWorker workerRemoveMissing = new();
    private static BackgroundWorker workerDeleteImportFolder = new();
    private static BackgroundWorker workerMediaInfo = new();

    internal static BackgroundWorker workerSetupDB = new();

    private static Timer autoUpdateTimer;
    private static Timer autoUpdateTimerShort;

    private DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
    private List<RecoveringFileSystemWatcher> _fileWatchers;

    private BackgroundWorker downloadImagesWorker = new();

    public static List<UserCulture> userLanguages = new();

    private Mutex mutex;

    public string[] GetSupportedDatabases()
    {
        return new[] { "SQLite", "Microsoft SQL Server 2014", "MySQL/MariaDB" };
    }

    public ShokoServer(ILogger<ShokoServer> logger, ISettingsProvider settingsProvider)
    {
        this.logger = logger;
        _settingsProvider = settingsProvider;
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
        _sentry?.Dispose();
        ShokoEventHandler.Instance.Shutdown -= ShutDown;
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
        var settings = _settingsProvider.GetSettings();
        // Only try to set up Sentry if the user DID NOT OPT __OUT__.
        if (!settings.SentryOptOut && Constants.SentryDsn.StartsWith("https://"))
        {
            // Get the release and extra info from the assembly.
            var extraInfo = Utils.GetApplicationExtraVersion();

            // Only initialize the SDK if we're not on a debug build.
            //
            // If the release channel is not set or if it's set to "debug" then
            // it's considered to be a debug build.
            if (extraInfo.TryGetValue("channel", out var environment) && environment != "debug")
                _sentry = SentrySdk.Init(opts =>
                {
                    // Assign the DSN key and release version.
                    opts.Dsn = Constants.SentryDsn;
                    opts.Environment = environment;
                    opts.Release = Utils.GetApplicationVersion();

                    // Conditionally assign the extra info if they're included in the assembly.
                    if (extraInfo.TryGetValue("commit", out var gitCommit))
                        opts.DefaultTags.Add("commit", gitCommit);
                    if (extraInfo.TryGetValue("tag", out var gitTag))
                        opts.DefaultTags.Add("commit.tag", gitTag);

                    // Append the release channel for the release on non-stable branches.
                    if (environment != "stable")
                        opts.Release += string.IsNullOrEmpty(gitCommit) ? $"-{environment}" : $"-{environment}-{gitCommit[0..7]}";
                });
        }

        // Check if any of the DLL are blocked, common issue with daily builds
        if (!CheckBlockedFiles())
        {
            Utils.ShowErrorMessage(Resources.ErrorBlockedDll);
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

        Loader.InitPlugins(Utils.ServiceContainer);

        _settingsProvider.DebugSettingsToLog();

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

        // run rotator once and set 24h delay
        Utils.ServiceContainer.GetRequiredService<LogRotator>().Start();

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
        ServerInfo.Instance.RefreshImportFolders();
        ServerState.Instance.ServerStartingStatus = Resources.Server_Complete;
        ServerState.Instance.ServerOnline = true;
        var settings = _settingsProvider.GetSettings();
        settings.FirstRun = false;
        _settingsProvider.SaveSettings();
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
        var settings = _settingsProvider.GetSettings();

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

            var settings = _settingsProvider.GetSettings();
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
        ShokoService.CancelAndWaitForQueues();
        AniDBDispose();
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

        if (!workerImport.IsBusy)
        {
            workerImport.RunWorkerAsync();
        }
    }

    public static void RemoveMissingFiles(bool removeMyList = true)
    {
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
}
