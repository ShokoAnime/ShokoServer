using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Timers;
using LeanWork.IO.FileSystem;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;
using NutzCode.CloudFileSystem.OAuth2;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue;
using Shoko.Server.CommandQueue.Commands.AniDB;
using Shoko.Server.CommandQueue.Commands.Hash;
using Shoko.Server.CommandQueue.Commands.Plex;
using Shoko.Server.CommandQueue.Commands.Schedule;
using Shoko.Server.CommandQueue.Commands.Server;
using Shoko.Server.CommandQueue.Commands.WebCache;
using Shoko.Server.Extensions;
using Shoko.Server.FileScanner;
using Shoko.Server.ImageDownload;
using Shoko.Server.Import;
using Shoko.Server.Models;

using Shoko.Server.Native.Trinet.NTFS;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Providers.WebUpdates;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.FileSystemWatcher;
//using Trinet.Core.IO.Ntfs;
using Action = System.Action;
using LogLevel = NLog.LogLevel;
using Timer = System.Timers.Timer;
using Utils = Shoko.Server.Utilities.Utils;

namespace Shoko.Server
{
    public class ShokoServer
    {
        //private static bool doneFirstTrakTinfo = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static DateTime lastTraktInfoUpdate = DateTime.Now;
        private static DateTime lastVersionCheck = DateTime.Now;

        public static DateTime? StartTime = null;

        public static TimeSpan? UpTime => StartTime == null ? null : DateTime.Now - StartTime;

        internal static BlockingList<FileSystemEventArgs> queueFileEvents = new BlockingList<FileSystemEventArgs>();
        private static BackgroundWorker workerFileEvents = new BackgroundWorker();

        public static string PathAddressREST = "api/Image";
        public static string PathAddressPlex = "api/Plex";
        public static string PathAddressKodi = "Kodi";

        private static IWebHost hostNancy;




        //private static BackgroundWorker workerMyAnime2 = new BackgroundWorker();
   



        internal static BackgroundWorker workerSetupDB = new BackgroundWorker();
        private static Timer cloudWatchTimer;


        DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
        private static List<RecoveringFileSystemWatcher> watcherVids;


        public static List<UserCulture> userLanguages = new List<UserCulture>();

        public IOAuthProvider OAuthProvider { get; set; } = new AuthProviderNEEDREDO();

        internal IServiceProvider DepProvider { get; set; }
        private Mutex mutex;

        internal static ManualResetEvent _pauseFileWatchDog = new ManualResetEvent(true);

        public string[] GetSupportedDatabases()
        {
            return new[]
            {
                "SQLite",
                "Microsoft SQL Server 2014",
                "MySQL/MariaDB"
            };
        }

        private ShokoServer() { }

        private static IServiceProvider BuildDi()
        {
            var services = new ServiceCollection();

            //Runner is the custom class
            //services.AddTransient<Runner>();

            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddLogging((builder) => builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

            var serviceProvider = services.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            //configure NLog
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true,  });
            NLog.LogManager.LoadConfiguration("nlog.config");
            //Reconfigure log file to applicationpath
            var target = (FileTarget) LogManager.Configuration.FindTargetByName("file");
            target.FileName = ServerSettings.ApplicationPath + "/logs/${shortdate}.log";
            LogManager.ReconfigExistingLoggers();

            return serviceProvider;
        }

        public bool StartUpServer()
        {
            this.DepProvider = BuildDi();

            try
            {
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
                
                try
                {
                    UnhandledExceptionManager.AddHandler();
                }
                catch (Exception e)
                {
                    logger.Log(LogLevel.Error, e);
                }

                try
                {
                    mutex = Mutex.OpenExisting(ServerSettings.DefaultInstance + "Mutex");
                    //since it hasn't thrown an exception, then we already have one copy of the app open.
                    return false;
                    //MessageBox.Show(Shoko.Commons.Properties.Resources.Server_Running,
                    //    Shoko.Commons.Properties.Resources.ShokoServer, MessageBoxButton.OK, MessageBoxImage.Error);
                    //Environment.Exit(0);
                }
                catch (Exception Ex)
                {
                    //since we didn't find a mutex with that name, create one
                    Debug.WriteLine("Exception thrown:" + Ex.Message + " Creating a new mutex...");
                    mutex = new Mutex(true, ServerSettings.DefaultInstance + "Mutex");
                }
                ServerSettings.Instance.DebugSettingsToLog();
                RenameFileHelper.InitialiseRenamers();

                workerFileEvents.WorkerReportsProgress = false;
                workerFileEvents.WorkerSupportsCancellation = false;
                workerFileEvents.DoWork += WorkerFileEvents_DoWork;
                workerFileEvents.RunWorkerCompleted += WorkerFileEvents_RunWorkerCompleted;

                

                ServerState.Instance.DatabaseAvailable = false;
                ServerState.Instance.ServerOnline = false;
                ServerState.Instance.ServerStarting = false;
                ServerState.Instance.StartupFailed = false;
                ServerState.Instance.StartupFailedMessage = string.Empty;
                ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();




                workerSetupDB.WorkerReportsProgress = true;
                workerSetupDB.ProgressChanged += (sender, args) => WorkerSetupDB_ReportProgress();
                workerSetupDB.DoWork += WorkerSetupDB_DoWork;
                workerSetupDB.RunWorkerCompleted += WorkerSetupDB_RunWorkerCompleted;

#if false
#region LoggingConfig
            LogManager.Configuration = new NLog.Config.LoggingConfiguration();
            ColoredConsoleTarget conTarget = new ColoredConsoleTarget("console") { Layout = "${date:format=HH\\:mm\\:ss}| --- ${message}" };
            FileTarget fileTarget = new FileTarget("file")
            {
                Layout = "[${shortdate} ${date:format=HH\\:mm\\:ss\\:fff}] ${level}|${stacktrace} ${message}",
                FileName = "${basedir}/logs/${shortdate}.txt"
            };
            LogManager.Configuration.AddTarget(conTarget);
            LogManager.Configuration.AddTarget(fileTarget);
            LogManager.Configuration.AddRuleForAllLevels(conTarget);

            LogManager.Configuration.AddRule(ServerSettings.Instance.TraceLog ? LogLevel.Trace : LogLevel.Info, LogLevel.Fatal, fileTarget);
#endregion
#endif

                ServerState.Instance.LoadSettings();

                InitCulture();
                Instance = this;

                
                SetupNetHosts();

                return true;
            }
            catch (Exception e)
            {
                logger.Error(e);
                return false;
            }
        }

        private bool CheckBlockedFiles()
        {
            if (Utils.IsLinux) return true;
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
            if (Utils.IsLinux) return; //This will be handled by the OS or user, as we cannot reliably learn what package management system they use.
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


        public static ShokoServer Instance { get; private set; } = new ShokoServer();



        void WorkerFileEvents_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            logger.Info("Stopped thread for processing file creation events");
        }

        void WorkerFileEvents_DoWork(object sender, DoWorkEventArgs e)
        {
            logger.Info("Started thread for processing file events");
            _pauseFileWatchDog.WaitOne(Timeout.Infinite);
            foreach (FileSystemEventArgs evt in queueFileEvents)
            {
                try
                {
                    // this is a message to stop processing
                    if (evt == null)
                    {
                        return;
                    }
                    if (evt.ChangeType == WatcherChangeTypes.Created || evt.ChangeType == WatcherChangeTypes.Renamed)
                    {
                        if (evt.FullPath.StartsWith("|CLOUD|"))
                        {
                            int shareid = int.Parse(evt.Name);
                            Importer.RunImport_ImportFolderNewFiles(Repo.Instance.ImportFolder.GetByID(shareid));
                        }
                        else
                        {
                            // When the path that was created represents a directory we need to manually get the contained files to add.
                            // The reason for this is that when a directory is moved into a source directory (from the same drive) we will only recieve
                            // an event for the directory and not the contained files. However, if the folder is copied from a different drive then
                            // a create event will fire for the directory and each file contained within it (As they are all treated as separate operations)

                            // This is faster and doesn't throw on weird paths. I've had some UTF-16/UTF-32 paths cause serious issues
                            if (Directory.Exists(evt.FullPath)) // filter out invalid events
                            {
                                logger.Info("New folder detected: {0}: {1}", evt.FullPath, evt.ChangeType);

                                string[] files = Directory.GetFiles(evt.FullPath, "*.*", SearchOption.AllDirectories);

                                foreach (string file in files)
                                {
                                    if (Utils.IsVideo(file))
                                    {
                                        logger.Info("Found file {0} under folder {1}", file, evt.FullPath);

                                        CommandQueue.Queue.Instance.Add(new CmdHashFile(file, false));
                                    }
                                }
                            }
                            else if (File.Exists(evt.FullPath))
                            {
                                logger.Info("New file detected: {0}: {1}", evt.FullPath, evt.ChangeType);

                                if (Utils.IsVideo(evt.FullPath))
                                {
                                    logger.Info("Found file {0}", evt.FullPath);

                                    CommandQueue.Queue.Instance.Add(new CmdHashFile(evt.FullPath, false));
                                }
                            }
                            // else it was deleted before we got here
                        }
                    }
                    if (queueFileEvents.Contains(evt))
                    {
                        queueFileEvents.Remove(evt);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "FSEvents_DoWork file: {0}\n{1}", evt.Name, ex);
                    queueFileEvents.Remove(evt);
                    Thread.Sleep(1000);
                }
            }
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
                //if (!string.IsNullOrEmpty(ServerSettings.Instance.Database.Type)) return;
                //ServerSettings.Instance.Database.Type = Constants.DatabaseType.Sqlite;
                ShowDatabaseSetup();
            }
        }

        void WorkerSetupDB_ReportProgress()
        {
            logger.Info("Starting Server: Complete!");
            ServerInfo.Instance.RefreshImportFolders();
            ServerInfo.Instance.RefreshCloudAccounts();
            ServerState.Instance.CurrentSetupStatus = Resources.Server_Complete;
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

        public static void StartCloudWatchTimer()
        {
            cloudWatchTimer = new Timer
            {
                AutoReset = true,
                Interval = ServerSettings.Instance.CloudWatcherTime * 60 * 1000
            };
            cloudWatchTimer.Elapsed += CloudWatchTimer_Elapsed;
            cloudWatchTimer.Start();
        }


        public static void StopCloudWatchTimer()
        {
            cloudWatchTimer?.Stop();
        }

        private static void CloudWatchTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (SVR_ImportFolder share in Repo.Instance.ImportFolder.GetAll()
                    .Where(a => a.CloudID.HasValue && a.FolderIsWatched))
                {
                    //Little hack in there to reuse the file queue
                    FileSystemEventArgs args = new FileSystemEventArgs(WatcherChangeTypes.Created, "|CLOUD|",
                        share.ImportFolderID.ToString());
                    queueFileEvents.Add(args);
                    StartFileWorker();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void SetupNetHosts()
        {
            logger.Info($"Initializing Web Hosts on port {ServerSettings.Instance.ServerPort}...");
            ServerState.Instance.CurrentSetupStatus = Resources.Server_InitializingHosts;
            bool started = true;
            started &= NetPermissionWrapper(StartNancyHost);
            if (!started)
            {
                StopHost();
                throw new Exception("Failed to start all of the network hosts");
            }
        }
        
        public void RestartAniDBSocket()
        {
            AniDBDispose();
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
                ServerState.Instance.CurrentSetupStatus = Resources.Server_Cleaning;

                StopWatchingFiles();

                RestartAniDBSocket();

                CommandQueue.Queue.Instance.Stop();




                //DatabaseFactory.CloseSessionFactory();

                ServerState.Instance.CurrentSetupStatus = Resources.Server_Initializing;
                Thread.Sleep(1000);

                ServerState.Instance.CurrentSetupStatus = Resources.Server_DatabaseSetup;

                logger.Info("Setting up database...");
                //Repo.Init(new ShokoContext(ServerSettings.Instance.Database.Type, ))
                var repo = new Repo();
                if (!repo.Start() || !repo.Migrate() || !repo.DoInit())
                //if (!DatabaseFactory.InitDB(out string errorMessage))
                {
                    ServerState.Instance.DatabaseAvailable = false;

                    /*if (string.IsNullOrEmpty(ServerSettings.Instance.Database.Type))
                        ServerState.Instance.CurrentSetupStatus =
                            Resources.Server_DatabaseConfig;*/
                    e.Result = false;
                    ServerState.Instance.StartupFailed = true;
                    ServerState.Instance.StartupFailedMessage = "An error occured";//errorMessage;
                    return;
                }

                ServerState.Instance.DatabaseAvailable = true;
                

                Scanner.Instance.Init();
                logger.Info("Logging into WebCache if needed...");
                WebCacheAPI.Instance.RefreshToken();

                logger.Info("Initializing Session Factory...");

                //init session factory
                ServerState.Instance.CurrentSetupStatus = Resources.Server_InitializingSession;
                //ISessionFactory temp = DatabaseFactory.SessionFactory;


                ServerState.Instance.CurrentSetupStatus = Resources.Server_InitializingQueue;

                Queue.Instance.Start();


                //Add Logrotator if needed
                Queue.Instance.Add(new CmdScheduleLogRotation(),DateTime.UtcNow.AddSeconds(CmdScheduleLogRotation.LogRotationTimeInSeconds));
               Queue.Instance.Add(new CmdScheduleShortUpdate());
                Queue.Instance.Add(new CmdScheduleUpdate());





                ServerState.Instance.CurrentSetupStatus = Resources.Server_InitializingFile;

                StartFileWorker();

                StartWatchingFiles();

                DownloadAllImages();

                IReadOnlyList<SVR_ImportFolder> folders = Repo.Instance.ImportFolder.GetAll();

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
                ServerState.Instance.CurrentSetupStatus = ex.Message;
                ServerState.Instance.StartupFailed = true;
                ServerState.Instance.StartupFailedMessage = $"Startup Failed: {ex}";
                e.Result = false;
            }
        }

#endregion

#region Update all media info

  

        public static void RefreshAllMediaInfo()
        {
            Queue.Instance.Add(new CmdServerReadAllMediaInfo());
        }

#endregion

        
#region MyAnime2 Migration
        /*
        public event EventHandler<ProgressChangedEventArgs> MyAnime2ProgressChanged;
        
        void WorkerMyAnime2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            MyAnime2ProgressChanged?.Invoke(Instance, e);
        }

        void WorkerMyAnime2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        void WorkerMyAnime2_DoWork(object sender, DoWorkEventArgs e)
        {
            MA2Progress ma2Progress = new MA2Progress
            {
                CurrentFile = 0,
                ErrorMessage = string.Empty,
                MigratedFiles = 0,
                TotalFiles = 0
            };
            try
            {
                string databasePath = e.Argument as string;

                string connString = string.Format(@"data source={0};useutf16encoding=True", databasePath);
                SQLiteConnection myConn = new SQLiteConnection(connString);
                myConn.Open();

                // get a list of unlinked files


                List<SVR_VideoLocal> vids = Repo.Instance.VideoLocal.GetVideosWithoutEpisode();
                ma2Progress.TotalFiles = vids.Count;

                foreach (SVR_VideoLocal vid in vids.Where(a => !string.IsNullOrEmpty(a.Hash)))
                {
                    ma2Progress.CurrentFile = ma2Progress.CurrentFile + 1;
                    workerMyAnime2.ReportProgress(0, ma2Progress);

                    // search for this file in the XrossRef table in MA2
                    string sql =
                        string.Format(
                            "SELECT AniDB_EpisodeID from CrossRef_Episode_FileHash WHERE Hash = '{0}' AND FileSize = {1}",
                            vid.ED2KHash, vid.FileSize);
                    SQLiteCommand sqCommand = new SQLiteCommand(sql)
                    {
                        Connection = myConn
                    };
                    SQLiteDataReader myReader = sqCommand.ExecuteReader();
                    while (myReader.Read())
                    {
                        if (!int.TryParse(myReader.GetValue(0).ToString(), out int episodeID)) continue;
                        if (episodeID <= 0) continue;

                        sql = string.Format("SELECT AnimeID from AniDB_Episode WHERE EpisodeID = {0}", episodeID);
                        sqCommand = new SQLiteCommand(sql)
                        {
                            Connection = myConn
                        };
                        SQLiteDataReader myReader2 = sqCommand.ExecuteReader();
                        while (myReader2.Read())
                        {
                            int animeID = myReader2.GetInt32(0);

                            // so now we have all the needed details we can link the file to the episode
                            // as long as wehave the details in JMM
                            SVR_AniDB_Anime anime = null;
                            AniDB_Episode ep = Repo.Instance.AniDB_Episode.GetByEpisodeID(episodeID);
                            if (ep == null)
                            {
                                logger.Debug("Getting Anime record from AniDB....");
                                anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true,
                                    ServerSettings.Instance.AutoGroupSeries);
                            }
                            else
                                anime = Repo.Instance.AniDB_Anime.GetByID(animeID);

                            // create the group/series/episode records if needed
                            SVR_AnimeSeries ser = null;
                            if (anime == null) continue;

                            logger.Debug("Creating groups, series and episodes....");
                            // check if there is an AnimeSeries Record associated with this AnimeID
                            using (var upd = Repo.Instance.AnimeSeries.BeginAddOrUpdate(() => Repo.Instance.AnimeSeries.GetByAnimeID(animeID), () => anime.CreateAnimeSeriesAndGroup()))
                            {
                                upd.Entity.CreateAnimeEpisodes();

                                // check if we have any group status data for this associated anime
                                // if not we will download it now
                                if (Repo.Instance.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                                {
                                    CommandQueue.Queue.Instance.Add(new CmdGetReleaseGroupStatus(anime.AnimeID, false));
                                }

                                // update stats
                                upd.Entity.EpisodeAddedDate = DateTime.Now;
                                ser = upd.Commit();
                            }

                            Repo.Instance.AnimeGroup.BatchAction(ser.AllGroupsAbove, ser.AllGroupsAbove.Count, (grp, _) => grp.EpisodeAddedDate = DateTime.Now);

                            SVR_AnimeEpisode epAnime = Repo.Instance.AnimeEpisode.GetByAniDBEpisodeID(episodeID);

                            using (var upd = Repo.Instance.CrossRef_File_Episode.BeginAdd())
                            {
                                try
                                {
                                    upd.Entity.PopulateManually_RA(vid, epAnime);
                                }
                                catch (Exception ex)
                                {
                                    string msg = string.Format("Error populating XREF: {0} - {1}", vid.ToStringDetailed(),
                                        ex);
                                    throw;
                                }
                                upd.Commit();
                            }

                            vid.Places.ForEach(a => a.RenameAndMoveAsRequired());

                            // update stats for groups and series
                            if (ser != null)
                            {
                                // update all the groups above this series in the heirarchy
                                ser.QueueUpdateStats();
                                //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                            }


                            // Add this file to the users list
                            if (ServerSettings.Instance.AniDb.MyList_AddFiles)
                            {
                                CommandQueue.Queue.Instance.Add(new CmdAddFileToMyList(vid.ED2KHash));
                            }

                            ma2Progress.MigratedFiles = ma2Progress.MigratedFiles + 1;
                            workerMyAnime2.ReportProgress(0, ma2Progress);
                        }
                        myReader2.Close();


                        //Console.WriteLine(myReader.GetString(0));
                    }
                    myReader.Close();
                }


                myConn.Close();

                ma2Progress.CurrentFile = ma2Progress.CurrentFile + 1;
                workerMyAnime2.ReportProgress(0, ma2Progress);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                ma2Progress.ErrorMessage = ex.Message;
                workerMyAnime2.ReportProgress(0, ma2Progress);
            }
        }

        private void ImportLinksFromMA2(string databasePath)
        {
        }
        */
#endregion

        public void DownloadAllImages()
        {
            Queue.Instance.Add(new CmdServerGetImages());
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
                    AutoUpdatesHelper.ConvertToAbsoluteVersion(
                        AutoUpdatesHelper.GetLatestVersionNumber(ServerSettings.Instance.UpdateChannel))
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





#region Tray Minimize

        private void ShutDown()
        {
            StopWatchingFiles();
            AniDBDispose();
            StopHost();
            ServerShutdown?.Invoke(this, null);
        }

#endregion



        public static void StartWatchingFiles(bool log = true)
        {
            StopWatchingFiles();
            StopCloudWatchTimer();
            watcherVids = new List<RecoveringFileSystemWatcher>();

            foreach (SVR_ImportFolder share in Repo.Instance.ImportFolder.GetAll())
            {
                try
                {
                    if (share.FolderIsWatched && log)
                    {
                        logger.Info($"Watching ImportFolder: {share.ImportFolderName} || {share.ImportFolderLocation}");
                    }
                    if (share.CloudID == null && Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
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
            StartCloudWatchTimer();
        }


        public static void StopWatchingFiles()
        {
            if (watcherVids == null)
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
            Queue.Instance.Add(new CmdServerScanDropFolders());
        }

        public static void SyncHashes()
        {
            CommandQueue.Queue.Instance.Add(new CmdServerSyncHashes());
        }

        public static void SyncMedias()
        {
            Queue.Instance.Add(new CmdServerSyncMediaInfos());
        }

        public static void ScanFolder(int importFolderID)
        {
            Queue.Instance.Add(new CmdServerScanFolder(importFolderID));
        }

        public static void RunImport()
        {
            Queue.Instance.Add(new CmdServerImport());
        }

        public static void RemoveMissingFiles()
        {
            Queue.Instance.Add(new CmdServerRemoveMissingFiles());
        }

        public static void SyncMyList()
        {
            Importer.CheckForMyListSyncUpdate(true);
        }

        public static void DeleteImportFolder(int importFolderID)
        {
            Queue.Instance.Add(new CmdServerDeleteFolder(importFolderID));
        }





      

        /// <summary>
        /// Running Nancy and Validating all require aspects before running it
        /// </summary>
        private static void StartNancyHost()
        {
            /*foreach (string ext in SubtitleHelper.Extensions.Keys)
            {
                if (!MimeTypes.GetMimeType("file." + ext)
                    .Equals("application/octet-stream", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (!SubtitleHelper.Extensions[ext]
                    .Equals("application/octet-stream", StringComparison.InvariantCultureIgnoreCase))
                    MimeTypes.AddType(ext, SubtitleHelper.Extensions[ext]);
            }

            if (MimeTypes.GetMimeType("file.mkv") == "application/octet-stream")
            {
                MimeTypes.AddType("mkv", "video/x-matroska");
                MimeTypes.AddType("mka", "audio/x-matroska");
                MimeTypes.AddType("mk3d", "video/x-matroska-3d");
                MimeTypes.AddType("ogm", "video/ogg");
                MimeTypes.AddType("flv", "video/x-flv");
            }*/

            if (hostNancy != null)
                return;

            // This requires admin, so throw an error if it fails
            // Don't let Nancy do this. We do it ourselves.
            // This needs to throw an error for our url registration to call.


            /*config.UrlReservations.CreateAutomatically = false;
            config.RewriteLocalhost = true;
            config.AllowChunkedEncoding = false;*/

            hostNancy = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.ListenAnyIP(ServerSettings.Instance.ServerPort);
                })
                .UseStartup<API.Startup>()
                #if DEBUG
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    logging.AddNLog();
                })
                #endif
                .Build();

            //JsonSettings.MaxJsonLength = int.MaxValue;

            // Even with error callbacks, this may still throw an error in some parts, so log it!
            try
            {
                hostNancy.Start();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private static void ReadFiles()
        {
            // Steps for processing a file
            // 1. Check if it is a video file
            // 2. Check if we have a VideoLocal record for that file
            // .........

            // get a complete list of files
            List<string> fileList = new List<string>();
            foreach (SVR_ImportFolder share in Repo.Instance.ImportFolder.GetAll())
            {
                logger.Debug("Import Folder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
                Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
            }
            
            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            int i = 0;

            // get a list of all files in the share
            foreach (string fileName in fileList)
            {
                i++;
                filesFound++;

                if (fileName.Contains("Sennou"))
                {
                    logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);
                }

                if (!Utils.IsVideo(fileName)) continue;

                videosFound++;
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        public static void StopHost()
        {
            hostNancy?.Dispose();
            hostNancy = null;
        }

        private static void SetupAniDBProcessor()
        {
            ShokoService.AnidbProcessor.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password,
                ServerSettings.Instance.AniDb.ServerAddress,
                ServerSettings.Instance.AniDb.ServerPort, ServerSettings.Instance.AniDb.ClientPort);
        }

        public static void AniDBDispose()
        {
            logger.Info("Disposing...");
            if (ShokoService.AnidbProcessor != null)
            {
                ShokoService.AnidbProcessor.ForceLogout();
                ShokoService.AnidbProcessor.Dispose();
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Sync plex watch status.
        /// </summary>
        /// <returns>true if there was any commands added to the queue, flase otherwise</returns>
        public bool SyncPlex()
        {
            bool flag = false;
            foreach (SVR_JMMUser user in Repo.Instance.JMMUser.GetAll())
            {
                if (!string.IsNullOrEmpty(user.PlexToken))
                {
                    flag = true;
                    CommandQueue.Queue.Instance.Add(new CmdPlexSyncWatched(user));
                }
            }
            return flag;
        }

        public void EnableStartWithWindows()
        {
            ServerState state = ServerState.Instance;

            if (state.IsAutostartEnabled)
            {
                return;
            }

            if (state.autostartMethod == AutostartMethod.Registry)
            {
                try
                {
                    state.AutostartRegistryKey.SetValue(state.autostartKey,
                        "\"" + Assembly.GetEntryAssembly().Location + "\"");
                    state.LoadSettings();
                }
                catch (Exception ex)
                {
                    logger.Debug(ex , "Creating autostart key");
                }
            }
            else if (state.autostartMethod == AutostartMethod.TaskScheduler)
            {
                Task task = TaskService.Instance.GetTask(state.autostartTaskName);
                if (task != null)
                {
                    TaskService.Instance.RootFolder.DeleteTask(task.Name);
                }

                TaskDefinition td = TaskService.Instance.NewTask();
                td.RegistrationInfo.Description = "Auto start task for JMM Server";

                td.Principal.RunLevel = TaskRunLevel.Highest;

                td.Triggers.Add(new BootTrigger());
                td.Triggers.Add(new LogonTrigger());

                //needs to have the "path:" else it fails
                td.Actions.Add(path: "\"" + Assembly.GetEntryAssembly().Location + "\"");

                TaskService.Instance.RootFolder.RegisterTaskDefinition(state.autostartTaskName, td);
                state.LoadSettings();
            }
        }

        public void DisableStartWithWindows()
        {
            ServerState state = ServerState.Instance;
            if (!state.IsAutostartEnabled)
            {
                return;
            }

            if (state.autostartMethod == AutostartMethod.Registry)
            {
                try
                {
                    state.AutostartRegistryKey.DeleteValue(state.autostartKey, false);
                    state.LoadSettings();
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Deleting autostart key");
                }
            }
            else if (state.autostartMethod == AutostartMethod.TaskScheduler)
            {
                Task task = TaskService.Instance.GetTask(state.autostartTaskName);

                if (task == null) return;
                TaskService.Instance.RootFolder.DeleteTask(task.Name);
                state.LoadSettings();
            }
        }

        public bool SetNancyPort(ushort port)
        {
            if (!Utils.IsAdministrator()) return false;


            StopHost();

            ServerSettings.Instance.ServerPort = port;

            bool started = NetPermissionWrapper(StartNancyHost);
            if (!started)
            {
                StopHost();
                throw new Exception("Failed to start all of the network hosts");
            }

            return true;
        }

        public void CheckForUpdates()
        {
            Assembly a = Assembly.GetExecutingAssembly();
                ServerState.Instance.ApplicationVersion = Utils.GetApplicationVersion(a);
                ServerState.Instance.ApplicationVersionExtra = Utils.GetApplicationExtraVersion(a);

            logger.Info("Checking for updates...");
            CheckForUpdatesNew(false);
        }

        //public static bool IsMyAnime2WorkerBusy() => workerMyAnime2.IsBusy;

        //public static void RunMyAnime2Worker(string filename) => workerMyAnime2.RunWorkerAsync(filename);
        public static void RunWorkSetupDB() => workerSetupDB.RunWorkerAsync();
    }
}
