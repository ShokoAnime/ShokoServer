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
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NLog.Web;
using Quartz;
using Sentry;
using Shoko.Commons.Properties;
using Shoko.Server.API;
using Shoko.Server.API.SignalR.NLog;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.ImageDownload;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.JMMAutoUpdates;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs;
using Shoko.Server.Settings;
using Shoko.Server.Settings.DI;
using Shoko.Server.UI;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.FileSystemWatcher;
using Trinet.Core.IO.Ntfs;
using Action = System.Action;
using LogLevel = NLog.LogLevel;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Server;

public class ShokoServer
{
    //private static bool doneFirstTrakTinfo = false;
    private static Logger logger = LogManager.GetCurrentClassLogger();
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
    
    internal static BackgroundWorker workerSetupDB = new();
    internal static BackgroundWorker LogRotatorWorker = new();

    private static Timer autoUpdateTimer;
    private static Timer autoUpdateTimerShort;
    internal static Timer LogRotatorTimer;

    private DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
    private List<RecoveringFileSystemWatcher> _fileWatchers;

    private BackgroundWorker downloadImagesWorker = new();

    public static List<UserCulture> userLanguages = new();

    public static IServiceProvider ServiceContainer => webHost?.Services;

    private Mutex mutex;
    private const string SentryDsn = "https://47df427564ab42f4be998e637b3ec45a@o330862.ingest.sentry.io/1851880";

    internal static void ConfigureServices(IServiceCollection services)
    {
        ServerSettings.ConfigureServices(services);
        // THIS IS BAD AND NOT WORKING
        services.AddSingleton(ServerSettings.Instance);

        services.AddSingleton<SettingsProvider>();
        services.AddSingleton(Loader.Instance);
        services.AddSingleton(ShokoService.CmdProcessorGeneral);
        services.AddSingleton<TraktTVHelper>();
        services.AddSingleton<TvDBApiHelper>();
        services.AddSingleton<MovieDBHelper>();
        AniDBStartup.ConfigureServices(services);
        CommandStartup.Configure(services);
        Loader.Instance.Load(services);
    }

    public string[] GetSupportedDatabases()
    {
        return new[] { "SQLite", "Microsoft SQL Server 2014", "MySQL/MariaDB" };
    }

    private ShokoServer()
    {
        InitWebHost();
    }

    ~ShokoServer()
    {
        _sentry.Dispose();
        ShutDown();
    }

    public void InitLogger()
    {
        var target = (FileTarget)LogManager.Configuration.FindTargetByName("file");
        if (target != null)
        {
            target.FileName = ServerSettings.ApplicationPath + "/logs/${shortdate}.log";
        }

#if LOGWEB
            // Disable blackhole http info logs
            LogManager.Configuration.LoggingRules.FirstOrDefault(r => r.LoggerNamePattern.StartsWith("Microsoft.AspNetCore"))?.DisableLoggingForLevel(LogLevel.Info);
            LogManager.Configuration.LoggingRules.FirstOrDefault(r => r.LoggerNamePattern.StartsWith("Shoko.Server.API.Authentication"))?.DisableLoggingForLevel(LogLevel.Info);
#endif
#if DEBUG
        // Enable debug logging
        LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Contains(target))
            ?.EnableLoggingForLevel(LogLevel.Debug);
#endif

        var signalrTarget =
            new AsyncTargetWrapper(
                new SignalRTarget { Name = "signalr", MaxLogsCount = 1000, Layout = "${message}" }, 50,
                AsyncTargetWrapperOverflowAction.Discard);
        LogManager.Configuration.AddTarget("signalr", signalrTarget);
        LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, signalrTarget));
        var consoleTarget = (ColoredConsoleTarget)LogManager.Configuration.FindTargetByName("console");
        if (consoleTarget != null)
        {
            consoleTarget.Layout = "${date:format=HH\\:mm\\:ss}| ${logger:shortname=true} --- ${message}";
        }

        LogManager.ReconfigExistingLoggers();
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

        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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
        CommandHelper.LoadCommands(ServiceContainer);

        if (!Utils.IsLinux)
        {
            try
            {
                mutex = Mutex.OpenExisting(ServerSettings.DefaultInstance + "Mutex");
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
                mutex = new Mutex(true, ServerSettings.DefaultInstance + "Mutex");
            }
        }

        // RenameFileHelper.InitialiseRenamers();
        // var services = new ServiceCollection();
        // ConfigureServices(services);
        // Plugin.Loader.Instance.Load(services);
        // ServiceContainer = services.BuildServiceProvider();
        // Plugin.Loader.Instance.InitPlugins(ServiceContainer);

        ServerSettings.Instance.DebugSettingsToLog();

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
        
        workerSetupDB.WorkerReportsProgress = true;
        workerSetupDB.ProgressChanged += (sender, args) => WorkerSetupDB_ReportProgress();
        workerSetupDB.DoWork += WorkerSetupDB_DoWork;
        workerSetupDB.RunWorkerCompleted += WorkerSetupDB_RunWorkerCompleted;

        ServerState.Instance.LoadSettings();

        InitCulture();
        Instance = this;

        // run rotator once and set 24h delay
        logrotator.Start();
        StartLogRotatorTimer();

        if (!SetupNetHosts())
        {
            return false;
        }

        Analytics.PostEvent("Server", "StartupFinished");
        // for log readability, this will simply init the singleton
        ServiceContainer.GetService<IUDPConnectionHandler>();
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
                logger.Log(LogLevel.Error, "Found blocked DLL file: " + dllFile);
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

                logger.Log(LogLevel.Info, "Successfully migrated programdata folder.");
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, "Error occured during MigrateProgramDataLocation()");
                logger.Error(e);
                return false;
            }
        }

        return true;
    }

    public bool NetPermissionWrapper(Action action)
    {
        try
        {
            action();
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
                    logger.Error("Unable to run task: " + (action.Method?.Name ?? action.ToString()));
                    logger.Error(exception);
                }
                finally
                {
                    ShutDown();
                }

                return false;
            }

            Utils.ShowErrorMessage("Unable to start hosting, please run JMMServer as administrator once.");
            logger.Error(e);
            ShutDown();
            return false;
        }

        return true;
    }

    public void ApplicationShutdown()
    {
        try
        {
            ThreadStart ts = () =>
            {
                ServerSettings.DoServerShutdown(new ServerSettings.ReasonedEventArgs());
                Environment.Exit(0);
            };
            new Thread(ts).Start();
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Error occured during ApplicationShutdown: {ex.Message}");
        }
    }

    private void LogRotatorWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        // for later use
    }

    private void LogRotatorWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        logrotator.Start();
    }

    public static ShokoServer Instance { get; private set; } = new();

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
            ServerState.Instance.ServerOnline = false;
            if (!string.IsNullOrEmpty(ServerSettings.Instance.Database.Type))
            {
                return;
            }

            ServerSettings.Instance.Database.Type = Constants.DatabaseType.Sqlite;
            ShowDatabaseSetup();
        }
    }

    private void WorkerSetupDB_ReportProgress()
    {
        logger.Info("Starting Server: Complete!");
        ServerInfo.Instance.RefreshImportFolders();
        ServerState.Instance.ServerStartingStatus = Resources.Server_Complete;
        ServerState.Instance.ServerOnline = true;
        ServerSettings.Instance.FirstRun = false;
        ServerSettings.Instance.SaveSettings();
        if (string.IsNullOrEmpty(ServerSettings.Instance.AniDb.Username) ||
            string.IsNullOrEmpty(ServerSettings.Instance.AniDb.Password))
        {
            LoginFormNeeded?.Invoke(Instance, null);
        }

        DBSetupCompleted?.Invoke(Instance, null);
        
        // Start queues
        ShokoService.CmdProcessorGeneral.Paused = false;
        ShokoService.CmdProcessorHasher.Paused = false;
        ShokoService.CmdProcessorImages.Paused = false;
    }

    private void ShowDatabaseSetup()
    {
        DatabaseSetup?.Invoke(Instance, null);
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
        logger.Info("Initializing Web Hosts...");
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
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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

            logger.Info("Setting up database...");
            if (!DatabaseFactory.InitDB(out var errorMessage))
            {
                ServerState.Instance.DatabaseAvailable = false;

                if (string.IsNullOrEmpty(ServerSettings.Instance.Database.Type))
                {
                    ServerState.Instance.ServerStartingStatus =
                        Resources.Server_DatabaseConfig;
                }

                e.Result = false;
                ServerState.Instance.StartupFailed = true;
                ServerState.Instance.StartupFailedMessage = errorMessage;
                return;
            }

            logger.Info("Initializing Session Factory...");
            //init session factory
            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingSession;
            var _ = DatabaseFactory.SessionFactory;

            Scanner.Instance.Init();

            ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingQueue;
            ShokoService.CmdProcessorGeneral.Init(ServiceContainer);
            ShokoService.CmdProcessorHasher.Init(ServiceContainer);
            ShokoService.CmdProcessorImages.Init(ServiceContainer);

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

            if (ServerSettings.Instance.Import.ScanDropFoldersOnStart)
            {
                ScanDropFolders();
            }

            if (ServerSettings.Instance.Import.RunOnStart && folders.Count > 0)
            {
                var schedulerFactory = ServiceContainer.GetRequiredService<ISchedulerFactory>();
                var scheduler = schedulerFactory.GetScheduler().Result;
                scheduler.TriggerJob(ImportJob.Key);
            }

            ServerState.Instance.ServerOnline = true;
            workerSetupDB.ReportProgress(100);

            StartTime = DateTime.Now;

            e.Result = true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            ServerState.Instance.ServerStartingStatus = ex.Message;
            ServerState.Instance.StartupFailed = true;
            ServerState.Instance.StartupFailedMessage = $"Startup Failed: {ex}";
            e.Result = false;
        }
    }

    #endregion

    #region Update all media info
    
    public static void RefreshAllMediaInfo()
    {
        var schedulerFactory = ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().Result;
        scheduler.TriggerJob(MediaInfoJob.Key);
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
                logger.Error("Could not get current version");
                return;
            }

            var an = a.GetName();

            //verNew = verInfo.versions.ServerVersionAbs;

            verNew =
                JMMAutoUpdatesHelper.ConvertToAbsoluteVersion(
                    JMMAutoUpdatesHelper.GetLatestVersionNumber(ServerSettings.Instance.UpdateChannel))
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
            logger.Error(ex, ex.ToString());
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

    public event EventHandler ServerShutdown;
    public event EventHandler ServerRestart;

    private void ShutdownServer()
    {
        ServerShutdown?.Invoke(this, null);
    }

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
            logger.Error(ex, ex.ToString());
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

        foreach (var share in RepoFactory.ImportFolder.GetAll())
        {
            try
            {
                if (share.FolderIsWatched)
                {
                    logger.Info($"Watching ImportFolder: {share.ImportFolderName} || {share.ImportFolderLocation}");
                }

                if (Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
                {
                    
                    logger.Info($"Parsed ImportFolderLocation: {share.ImportFolderLocation}");

                    var fsw = new RecoveringFileSystemWatcher(share.ImportFolderLocation,
                        filters: ServerSettings.Instance.Import.VideoExtensions.Select(a => "." + a.ToLowerInvariant().TrimStart('.')),
                        pathExclusions: ServerSettings.Instance.Import.Exclude);
                    fsw.Options = new FileSystemWatcherLockOptions
                    {
                        Enabled = ServerSettings.Instance.Import.FileLockChecking,
                        Aggressive = ServerSettings.Instance.Import.AggressiveFileLockChecking,
                        WaitTimeMilliseconds = ServerSettings.Instance.Import.FileLockWaitTimeMS,
                        FileAccessMode = share.IsDropSource == 1 ? FileAccess.ReadWrite : FileAccess.Read,
                        AggressiveWaitTimeSeconds = ServerSettings.Instance.Import.AggressiveFileLockWaitTimeSeconds
                    };
                    fsw.FileAdded += FileAdded;
                    fsw.Start();
                    _fileWatchers.Add(fsw);
                }
                else if (!share.FolderIsWatched)
                {
                    logger.Info("ImportFolder found but not watching: {0} || {1}", share.ImportFolderName,
                        share.ImportFolderLocation);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }
    }

    private static void FileAdded(object sender, string path)
    {
        var commandFactory = ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        if (!File.Exists(path)) return;
        if (!FileHashHelper.IsVideo(path)) return;

        logger.Info("Found file {0}", path);
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
        logger.Trace($"Added {path} to filesystem watcher exclusions");
    }

    public void RemoveFileWatcherExclusion(string path)
    {
        if (_fileWatchers == null || !_fileWatchers.Any()) return;
        var watcher = _fileWatchers.FirstOrDefault(a => a.IsPathWatched(path));
        watcher?.RemoveExclusion(path);
        logger.Trace($"Removed {path} from filesystem watcher exclusions");
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
        var schedulerFactory = ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().Result;
        scheduler.TriggerJob(ScanDropFoldersJob.Key);
    }

    public static void ScanFolder(int importFolderID)
    { 
        var schedulerFactory = ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().Result;
        scheduler.TriggerJob(ScanFolderJob.Key, new JobDataMap
        {
            {"importFolderID", importFolderID}
        });
    }
    
    public static void RemoveMissingFiles(bool removeMyList = true)
    {
        var schedulerFactory = ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().Result;
        scheduler.TriggerJob(RemoveMissingFilesJob.Key, new JobDataMap
        {
            {"removeMyList", removeMyList}
        });
    }

    public static void SyncMyList()
    {
        Importer.CheckForMyListSyncUpdate(true);
    }

    public static void DeleteImportFolder(int importFolderID)
    {
        var schedulerFactory = ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().Result;
        scheduler.TriggerJob(DeleteImportFolderJob.Key, new JobDataMap
        {
            {"importFolderID", importFolderID}
        });
    }
    
    private static void InitWebHost()
    {
        if (webHost != null)
        {
            return;
        }

        webHost = new WebHostBuilder().UseKestrel(options =>
            {
                options.ListenAnyIP(ServerSettings.Instance.ServerPort);
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
    }

    /// <summary>
    /// Running Nancy and Validating all require aspects before running it
    /// </summary>
    private static void StartWebHost()
    {
        if (webHost == null)
        {
            InitWebHost();
        }

        //JsonSettings.MaxJsonLength = int.MaxValue;

        // Even with error callbacks, this may still throw an error in some parts, so log it!
        try
        {
            webHost.Start();
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
    }

    public static void StopHost()
    {
        webHost?.Dispose();
        webHost = null;
    }

    private static void SetupAniDBProcessor()
    {
        var handler = ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
        handler.Init(
            ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password,
            ServerSettings.Instance.AniDb.ServerAddress,
            ServerSettings.Instance.AniDb.ServerPort, ServerSettings.Instance.AniDb.ClientPort
        );
    }

    private static void AniDBDispose()
    {
        var handler = ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
        handler.ForceLogout();
        handler.CloseConnections();
    }

    public static int OnHashProgress(string fileName, int percentComplete)
    {
        //string msg = Path.GetFileName(fileName);
        //if (msg.Length > 35) msg = msg.Substring(0, 35);
        //logger.Info("{0}% Hashing ({1})", percentComplete, Path.GetFileName(fileName));
        return 1; //continue hashing (return 0 to abort)
    }

    /// <summary>
    /// Sync plex watch status.
    /// </summary>
    /// <returns>true if there was any commands added to the queue, flase otherwise</returns>
    public bool SyncPlex()
    {
        var commandFactory = ServiceContainer.GetRequiredService<ICommandRequestFactory>();
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
}
