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
using LeanWork.IO.FileSystem;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NHibernate;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NLog.Web;
using Sentry;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Server.API;
using Shoko.Server.API.SignalR.NLog;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.JMMAutoUpdates;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;
using Shoko.Server.Settings.DI;
using Shoko.Server.UI;
using Shoko.Server.Utilities;
using Trinet.Core.IO.Ntfs;
using Action = System.Action;
using LogLevel = NLog.LogLevel;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Server
{
    public class ShokoServer
    {
        //private static bool doneFirstTrakTinfo = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        internal static LogRotator logrotator = new LogRotator();
        private static DateTime lastTraktInfoUpdate = DateTime.Now;
        private static DateTime lastVersionCheck = DateTime.Now;

        public static DateTime? StartTime;

        public static TimeSpan? UpTime => StartTime == null ? null : DateTime.Now - StartTime;
        private static IDisposable _sentry;

        internal static BlockingList<FileSystemEventArgs> queueFileEvents = new BlockingList<FileSystemEventArgs>();
        private static BackgroundWorker workerFileEvents = new BackgroundWorker();

        public static string PathAddressREST = "api/Image";
        public static string PathAddressPlex = "api/Plex";
        public static string PathAddressKodi = "Kodi";

        private static IWebHost webHost;

        private static BackgroundWorker workerImport = new BackgroundWorker();
        private static BackgroundWorker workerScanFolder = new BackgroundWorker();
        private static BackgroundWorker workerScanDropFolders = new BackgroundWorker();
        private static BackgroundWorker workerRemoveMissing = new BackgroundWorker();
        private static BackgroundWorker workerDeleteImportFolder = new BackgroundWorker();
        private static BackgroundWorker workerMediaInfo = new BackgroundWorker();

        internal static BackgroundWorker workerSetupDB = new BackgroundWorker();
        internal static BackgroundWorker LogRotatorWorker = new BackgroundWorker();

        private static Timer autoUpdateTimer;
        private static Timer autoUpdateTimerShort;
        internal static Timer LogRotatorTimer;

        DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
        private static List<RecoveringFileSystemWatcher> watcherVids;

        BackgroundWorker downloadImagesWorker = new BackgroundWorker();

        public static List<UserCulture> userLanguages = new List<UserCulture>();

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
            return new[]
            {
                "SQLite",
                "Microsoft SQL Server 2014",
                "MySQL/MariaDB"
            };
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
            var target = (FileTarget) LogManager.Configuration.FindTargetByName("file");
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
            LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Contains(target))?.EnableLoggingForLevel(LogLevel.Debug);
#endif
 
            var signalrTarget =
                new AsyncTargetWrapper(
                    new SignalRTarget {Name = "signalr", MaxLogsCount = 1000, Layout = "${message}"}, 50,
                    AsyncTargetWrapperOverflowAction.Discard);
            LogManager.Configuration.AddTarget("signalr", signalrTarget);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, signalrTarget));
            var consoleTarget = (ColoredConsoleTarget) LogManager.Configuration.FindTargetByName("console");
            if (consoleTarget != null)
            {
                consoleTarget.Layout = "${date:format=HH\\:mm\\:ss}| ${logger:shortname=true} --- ${message}";
            }

            LogManager.ReconfigExistingLoggers();
        }

        public static void SetTraceLogging(bool enabled)
        {
            var rule = LogManager.Configuration.LoggingRules.FirstOrDefault(a => a.Targets.Any(b => b is FileTarget));
            if (rule == null) return;
            if (enabled)
                rule.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Debug);
            else
                rule.DisableLoggingForLevel(LogLevel.Trace);
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
                Analytics.PostEvent("Server", "Linux Startup");

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

            // First check if we have a settings.json in case migration had issues as otherwise might clear out existing old configurations
            string path = Path.Combine(ServerSettings.ApplicationPath, "settings.json");
            if (File.Exists(path))
            {
                Thread t = new Thread(UninstallJMMServer) {IsBackground = true};
                t.Start();
            }

            //HibernatingRhinos.Profiler.Appender.NHibernate.NHibernateProfiler.Initialize();
            CommandHelper.LoadCommands(ServiceContainer);

            try
            {
                UnhandledExceptionManager.AddHandler();
            }
            catch (Exception e)
            {
                logger.Log(LogLevel.Error, e);
            }

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

            workerFileEvents.WorkerReportsProgress = false;
            workerFileEvents.WorkerSupportsCancellation = false;
            workerFileEvents.DoWork += WorkerFileEvents_DoWork;
            workerFileEvents.RunWorkerCompleted += WorkerFileEvents_RunWorkerCompleted;

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

            ServerState.Instance.LoadSettings();

            InitCulture();
            Instance = this;

            // run rotator once and set 24h delay
            logrotator.Start();
            StartLogRotatorTimer();

            if (!SetupNetHosts()) return false;

            Analytics.PostEvent("Server", "StartupFinished");
            // for log readability, this will simply init the singleton
            ServiceContainer.GetService<IUDPConnectionHandler>();
            return true;
        }

        private bool CheckBlockedFiles()
        {
            if (Utils.IsRunningOnLinuxOrMac()) return true;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // do stuff on windows only
                return true;
            }
            string programlocation =
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string[] dllFiles = Directory.GetFiles(programlocation, "*.dll", SearchOption.AllDirectories);
            bool result = true;

            foreach (string dllFile in dllFiles)
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
            foreach (string dllFile in dllFiles)
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
            string oldApplicationPath =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "JMMServer");
            string newApplicationPath =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Assembly.GetEntryAssembly().GetName().Name);
            if (Directory.Exists(oldApplicationPath) && !Directory.Exists(newApplicationPath))
            {
                try
                {
                    List<MigrationDirectory> migrationdirs = new List<MigrationDirectory>
                    {
                        new MigrationDirectory
                        {
                            From = oldApplicationPath,
                            To = newApplicationPath
                        }
                    };

                    foreach (MigrationDirectory md in migrationdirs)
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

        void UninstallJMMServer()
        {
            if (Utils.IsRunningOnLinuxOrMac()) return; //This will be handled by the OS or user, as we cannot reliably learn what package management system they use.
            try
            {
                // Check in registry if installed
                string jmmServerUninstallPath =
                    (string)
                    Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{898530ED-CFC7-4744-B2B8-A8D98A2FA06C}_is1",
                        "UninstallString", null);

                if (!string.IsNullOrEmpty(jmmServerUninstallPath))
                {

                    // Ask if user wants to uninstall first
                    bool res = Utils.ShowYesNo(Resources.DuplicateInstallDetectedQuestion, Resources.DuplicateInstallDetected);

                    if (res)
                    {
                        try
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = jmmServerUninstallPath,
                                Arguments = " /SILENT"
                            };
                            Process.Start(startInfo);

                            logger.Log(LogLevel.Info, "JMM Server successfully uninstalled");
                        }
                        catch
                        {
                            logger.Log(LogLevel.Error, "Error occured during uninstall of JMM Server");
                        }
                    }
                    else
                    {
                        logger.Log(LogLevel.Info, "User cancelled JMM Server uninstall");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "Error occured during UninstallJMMServer: " + ex);
            }
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

        public static ShokoServer Instance { get; private set; } = new ShokoServer();

        private static void WorkerFileEvents_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            logger.Info("Stopped thread for processing file creation events");
        }

        private static void WorkerFileEvents_DoWork(object sender, DoWorkEventArgs e)
        {
            logger.Info("Started thread for processing file events");
            FileSystemEventArgs evt;
            try
            {
                evt = queueFileEvents.GetNextItem();
            }
            catch (Exception exception)
            {
                logger.Error(exception);
                evt = null;
            }
            while (evt != null)
            {
                try
                {
                    // this is a message to stop processing
                    ProcessFileEvent(evt);
                    queueFileEvents.Remove(evt);
                    try
                    {
                        evt = queueFileEvents.GetNextItem();
                    }
                    catch (Exception exception)
                    {
                        logger.Error(exception);
                        evt = null;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "FSEvents_DoWork file: {0}\n{1}", evt.Name, ex);
                    queueFileEvents.Remove(evt);
                    Thread.Sleep(1000);

                    //This is needed to prevent infinite looping of an event/file that causes an exception, 
                    //otherwise evt will not be cleared and the same event/file that caused the error will be looped over again.
                    evt = queueFileEvents.GetNextItem(); 
                }
            }
        }

        private static void ProcessFileEvent(FileSystemEventArgs evt)
        {
            if (evt.ChangeType != WatcherChangeTypes.Created && evt.ChangeType != WatcherChangeTypes.Renamed) return;
            // When the path that was created represents a directory we need to manually get the contained files to add.
            // The reason for this is that when a directory is moved into a source directory (from the same drive) we will only recieve
            // an event for the directory and not the contained files. However, if the folder is copied from a different drive then
            // a create event will fire for the directory and each file contained within it (As they are all treated as separate operations)

            // This is faster and doesn't throw on weird paths. I've had some UTF-16/UTF-32 paths cause serious issues
            var commandFactory = ServiceContainer.GetRequiredService<ICommandRequestFactory>();
            if (Directory.Exists(evt.FullPath)) // filter out invalid events
            {
                logger.Info("New folder detected: {0}: {1}", evt.FullPath, evt.ChangeType);

                var files = Directory.GetFiles(evt.FullPath, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (ServerSettings.Instance.Import.Exclude.Any(s => Regex.IsMatch(file, s)))
                    {
                        logger.Info("Import exclusion, skipping file {0}", file);
                    }
                    else if (FileHashHelper.IsVideo(file))
                    {
                        logger.Info("Found file {0} under folder {1}", file, evt.FullPath);

                        var tup = VideoLocal_PlaceRepository.GetFromFullPath(file);
                        ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(file));
                        var cmd = commandFactory.Create<CommandRequest_HashFile>(c => c.FileName = file);
                        cmd.Save();
                    }
                }
            }
            else if (File.Exists(evt.FullPath))
            {
                logger.Info("New file detected: {0}: {1}", evt.FullPath, evt.ChangeType);

                if (ServerSettings.Instance.Import.Exclude.Any(s => Regex.IsMatch(evt.FullPath, s)))
                {
                    logger.Info("Import exclusion, skipping file: {0}", evt.FullPath);
                }
                else if (FileHashHelper.IsVideo(evt.FullPath))
                {
                    logger.Info("Found file {0}", evt.FullPath);

                    var tup = VideoLocal_PlaceRepository.GetFromFullPath(evt.FullPath);
                    ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(evt.FullPath));
                    var cmd = commandFactory.Create<CommandRequest_HashFile>(c => c.FileName = evt.FullPath);
                    cmd.Save();
                }
            }
            // else it was deleted before we got here
        }

        void InitCulture()
        {

        }


        #region Database settings and initial start up

        public event EventHandler LoginFormNeeded;
        public event EventHandler DatabaseSetup;
        public event EventHandler DBSetupCompleted;
        void WorkerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ServerState.Instance.ServerStarting = false;
            bool setupComplete = bool.Parse(e.Result.ToString());
            if (!setupComplete)
            {
                ServerState.Instance.ServerOnline = false;
                if (!string.IsNullOrEmpty(ServerSettings.Instance.Database.Type)) return;
                ServerSettings.Instance.Database.Type = Constants.DatabaseType.Sqlite;
                ShowDatabaseSetup();
            }
        }

        void WorkerSetupDB_ReportProgress()
        {
            logger.Info("Starting Server: Complete!");
            ServerInfo.Instance.RefreshImportFolders();
            ServerState.Instance.ServerStartingStatus = Resources.Server_Complete;
            ServerState.Instance.ServerOnline = true;
            ServerSettings.Instance.FirstRun = false;
            ServerSettings.Instance.SaveSettings();
            if (string.IsNullOrEmpty(ServerSettings.Instance.AniDb.Username) ||
                string.IsNullOrEmpty(ServerSettings.Instance.AniDb.Password))
                LoginFormNeeded?.Invoke(Instance, null);
            DBSetupCompleted?.Invoke(Instance, null);
        }

        private void ShowDatabaseSetup() => DatabaseSetup?.Invoke(Instance, null);

        public static void StartFileWorker()
        {
            if (!workerFileEvents.IsBusy)
                workerFileEvents.RunWorkerAsync();
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
            bool started = true;
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

        void WorkerSetupDB_DoWork(object sender, DoWorkEventArgs e)
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
                        ShokoService.CmdProcessorImages.QueueCount == 0) break;
                    Thread.Sleep(250);
                }

                if (autoUpdateTimer != null) autoUpdateTimer.Enabled = false;
                if (autoUpdateTimerShort != null) autoUpdateTimerShort.Enabled = false;

                DatabaseFactory.CloseSessionFactory();

                ServerState.Instance.ServerStartingStatus = Resources.Server_Initializing;
                Thread.Sleep(1000);

                ServerState.Instance.ServerStartingStatus = Resources.Server_DatabaseSetup;

                logger.Info("Setting up database...");
                if (!DatabaseFactory.InitDB(out string errorMessage))
                {
                    ServerState.Instance.DatabaseAvailable = false;

                    if (string.IsNullOrEmpty(ServerSettings.Instance.Database.Type))
                        ServerState.Instance.ServerStartingStatus =
                            Resources.Server_DatabaseConfig;
                    e.Result = false;
                    ServerState.Instance.StartupFailed = true;
                    ServerState.Instance.StartupFailedMessage = errorMessage;
                    return;
                }

                logger.Info("Initializing Session Factory...");
                //init session factory
                ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingSession;
                ISessionFactory _ = DatabaseFactory.SessionFactory;

                // We need too much of the database initialized to do this anywhere else.
                // TODO make this a command request. Some people apparently have thousands (a different problem, but locks startup for hours)
                // DatabaseFixes.FixAniDB_EpisodesWithMissingTitles();

                Scanner.Instance.Init();

                ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingQueue;
                ShokoService.CmdProcessorGeneral.Init(ServiceContainer);
                ShokoService.CmdProcessorHasher.Init(ServiceContainer);
                ShokoService.CmdProcessorImages.Init(ServiceContainer);

                ServerState.Instance.DatabaseAvailable = true;


                // timer for automatic updates
                autoUpdateTimer = new Timer
                {
                    AutoReset = true,
                    Interval = 5 * 60 * 1000 // 5 * 60 seconds (5 minutes)
                };
                autoUpdateTimer.Elapsed += AutoUpdateTimer_Elapsed;
                autoUpdateTimer.Start();

                // timer for automatic updates
                autoUpdateTimerShort = new Timer
                {
                    AutoReset = true,
                    Interval = 5 * 1000 // 5 seconds, later we set it to 30 seconds
                };
                autoUpdateTimerShort.Elapsed += AutoUpdateTimerShort_Elapsed;
                autoUpdateTimerShort.Start();

                ServerState.Instance.ServerStartingStatus = Resources.Server_InitializingFile;

                StartFileWorker();

                StartWatchingFiles();

                DownloadAllImages();

                IReadOnlyList<SVR_ImportFolder> folders = RepoFactory.ImportFolder.GetAll();

                if (ServerSettings.Instance.Import.ScanDropFoldersOnStart) ScanDropFolders();
                if (ServerSettings.Instance.Import.RunOnStart && folders.Count > 0) RunImport();

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

        void WorkerMediaInfo_DoWork(object sender, DoWorkEventArgs e)
        {
            // first build a list of files that we already know about, as we don't want to process them again
            IReadOnlyList<SVR_VideoLocal> filesAll = RepoFactory.VideoLocal.GetAll();
            var commandFactory = ServiceContainer.GetRequiredService<ICommandRequestFactory>();
            foreach (SVR_VideoLocal vl in filesAll)
            {
                var cr = commandFactory.Create<CommandRequest_ReadMediaInfo>(c => c.VideoLocalID = vl.VideoLocalID);
                cr.Save();
            }
        }

        public static void RefreshAllMediaInfo()
        {
            if (workerMediaInfo.IsBusy) return;
            workerMediaInfo.RunWorkerAsync();
        }

        #endregion

        public void DownloadAllImages()
        {
            if (!downloadImagesWorker.IsBusy)
                downloadImagesWorker.RunWorkerAsync();
        }

        void DownloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
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
                Assembly a = Assembly.GetEntryAssembly();
                if (a == null)
                {
                    logger.Error("Could not get current version");
                    return;
                }
                AssemblyName an = a.GetName();

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
            string output = string.Empty;
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();

                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
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

        void ShutdownServer()
        {
            ServerShutdown?.Invoke(this, null);
        }

        void RestartServer()
        {
            ServerRestart?.Invoke(this, null);
        }

        #endregion

        void AutoUpdateTimerShort_Elapsed(object sender, ElapsedEventArgs e)
        {
            autoUpdateTimerShort.Enabled = false;
            ShokoService.CmdProcessorImages.NotifyOfNewCommand();

            CheckForAdminMesages();


            autoUpdateTimerShort.Interval = 30 * 1000; // 30 seconds
            autoUpdateTimerShort.Enabled = true;
        }

        private void CheckForAdminMesages()
        {
            try
            {
                TimeSpan lastUpdate = DateTime.Now - lastAdminMessage;

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

        static void AutoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
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

        public static void StartWatchingFiles(bool log = true)
        {
            StopWatchingFiles();
            watcherVids = new List<RecoveringFileSystemWatcher>();

            foreach (SVR_ImportFolder share in RepoFactory.ImportFolder.GetAll())
            {
                try
                {
                    if (share.FolderIsWatched && log)
                    {
                        logger.Info($"Watching ImportFolder: {share.ImportFolderName} || {share.ImportFolderLocation}");
                    }
                    if (Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
                    {
                        if (log) logger.Info($"Parsed ImportFolderLocation: {share.ImportFolderLocation}");
                        RecoveringFileSystemWatcher fsw = new RecoveringFileSystemWatcher
                        {
                            Path = share.ImportFolderLocation
                        };

                        // Handle all type of events not just created ones
                        fsw.Created += Fsw_CreateHandler;
                        fsw.Renamed += Fsw_RenameHandler;

                        // Commented out buffer size as it breaks on UNC paths or mapped drives
                        //fsw.InternalBufferSize = 81920;
                        fsw.IncludeSubdirectories = true;
                        fsw.EnableRaisingEvents = true;
                        watcherVids.Add(fsw);
                    }
                    else if (!share.FolderIsWatched)
                    {
                        if (log) logger.Info("ImportFolder found but not watching: {0} || {1}", share.ImportFolderName,
                            share.ImportFolderLocation);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.ToString());
                }
            }
        }

        public static void PauseWatchingFiles()
        {
            if (watcherVids == null || !watcherVids.Any())
                return;
            foreach (RecoveringFileSystemWatcher fsw in watcherVids)
            {
                fsw.DisableEvents = true;
            }
            
            logger.Info("Paused Filesystem Watching");
        }

        public static void UnpauseWatchingFiles()
        {
            if (watcherVids == null || !watcherVids.Any())
                return;
            foreach (RecoveringFileSystemWatcher fsw in watcherVids)
            {
                fsw.DisableEvents = false;
            }
            
            logger.Info("Unpaused Filesystem Watching");
        }

        public static void StopWatchingFiles()
        {
            if (watcherVids == null || !watcherVids.Any())
                return;
            foreach (RecoveringFileSystemWatcher fsw in watcherVids)
            {
                fsw.EnableRaisingEvents = false;
                fsw.Dispose();
            }
            watcherVids.Clear();
        }

        static void Fsw_CreateHandler(object sender, FileSystemEventArgs e)
        {
            try
            {
                queueFileEvents.Add(e);
                StartFileWorker();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        static void Fsw_RenameHandler(object sender, RenamedEventArgs e)
        {
            try
            {
                queueFileEvents.Add(e);
                StartFileWorker();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public static void ScanDropFolders()
        {
            if (!workerScanDropFolders.IsBusy)
                workerScanDropFolders.RunWorkerAsync();
        }

        public static void ScanFolder(int importFolderID)
        {
            if (!workerScanFolder.IsBusy)
                workerScanFolder.RunWorkerAsync(importFolderID);
        }

        public static void RunImport()
        {
            Analytics.PostEvent("Importer", "Run");

            if (!workerImport.IsBusy)
                workerImport.RunWorkerAsync();
        }

        public static void RemoveMissingFiles(bool removeMyList = true)
        {
            Analytics.PostEvent("Importer", "RemoveMissing");

            if (!workerRemoveMissing.IsBusy)
                workerRemoveMissing.RunWorkerAsync(removeMyList);
        }

        public static void SyncMyList()
        {
            Importer.CheckForMyListSyncUpdate(true);
        }

        public static void DeleteImportFolder(int importFolderID)
        {
            if (!workerDeleteImportFolder.IsBusy)
                workerDeleteImportFolder.RunWorkerAsync(importFolderID);
        }

        static void WorkerRemoveMissing_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RemoveRecordsWithoutPhysicalFiles((e.Argument as bool?) ?? true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        void WorkerDeleteImportFolder_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                int importFolderID = int.Parse(e.Argument.ToString());
                Importer.DeleteImportFolder(importFolderID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        static void WorkerScanFolder_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RunImport_ScanFolder(int.Parse(e.Argument.ToString()));
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        void WorkerScanDropFolders_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RunImport_DropFolders();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        static void WorkerImport_DoWork(object sender, DoWorkEventArgs e)
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
                logger.Error(ex, ex.ToString());
            }
        }

        private static void InitWebHost()
        {
            if (webHost != null)
                return;

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
            if (webHost == null) InitWebHost();

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

            bool flag = false;
            foreach (SVR_JMMUser user in RepoFactory.JMMUser.GetAll())
            {
                if (string.IsNullOrEmpty(user.PlexToken)) continue;
                flag = true;
                commandFactory.Create<CommandRequest_PlexSyncWatched>(c => c.User = user).Save();
            }
            return flag;
        }

        public static void RunWorkSetupDB() => workerSetupDB.RunWorkerAsync();
    }
}
