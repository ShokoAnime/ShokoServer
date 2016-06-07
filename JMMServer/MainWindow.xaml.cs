using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Infralution.Localization.Wpf;
using JMMContracts;
using JMMFileHelper;
using JMMServer.Commands;
using JMMServer.Commands.Azure;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.MyAnime2Helper;
using JMMServer.Providers.JMMAutoUpdates;
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;
using JMMServer.UI;
using Microsoft.SqlServer.Management.Smo;
using NLog;
using Application = System.Windows.Application;
using Binding = System.ServiceModel.Channels.Binding;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using Timer = System.Timers.Timer;

namespace JMMServer
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static bool doneFirstTrakTinfo = false;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static DateTime lastTraktInfoUpdate = DateTime.Now;
        private static DateTime lastVersionCheck = DateTime.Now;

        private static readonly BlockingList<FileSystemEventArgs> queueFileEvents =
            new BlockingList<FileSystemEventArgs>();

        private static readonly BackgroundWorker workerFileEvents = new BackgroundWorker();

        //private static Uri baseAddress = new Uri("http://localhost:8111/JMMServer");
        private static readonly string baseAddressImageString = @"http://localhost:{0}/JMMServerImage";
        private static readonly string baseAddressStreamingString = @"http://localhost:{0}/JMMServerStreaming";
        private static readonly string baseAddressStreamingStringMex = @"net.tcp://localhost:{0}/JMMServerStreaming/mex";
        private static readonly string baseAddressBinaryString = @"http://localhost:{0}/JMMServerBinary";
        private static readonly string baseAddressMetroString = @"http://localhost:{0}/JMMServerMetro";
        private static readonly string baseAddressMetroImageString = @"http://localhost:{0}/JMMServerMetroImage";
        private static readonly string baseAddressRESTString = @"http://localhost:{0}/JMMServerREST";
        private static readonly string baseAddressPlexString = @"http://localhost:{0}/JMMServerPlex";
        private static readonly string baseAddressKodiString = @"http://localhost:{0}/JMMServerKodi";

        public static string PathAddressREST = "JMMServerREST";
        public static string PathAddressPlex = "JMMServerPlex";
        public static string PathAddressKodi = "JMMServerKodi";

        //private static Uri baseAddressTCP = new Uri("net.tcp://localhost:8112/JMMServerTCP");
        //private static ServiceHost host = null;
        //private static ServiceHost hostTCP = null;
        private static ServiceHost hostImage;
        private static ServiceHost hostStreaming;
        private static ServiceHost hostBinary;
        private static ServiceHost hostMetro;
        private static ServiceHost hostMetroImage;
        private static WebServiceHost hostREST;
        private static WebServiceHost hostPlex;
        private static WebServiceHost hostKodi;
        //private static MessagingServer hostFile = null;
        private static FileServer.FileServer hostFile;

        private static readonly BackgroundWorker workerImport = new BackgroundWorker();
        private static readonly BackgroundWorker workerScanFolder = new BackgroundWorker();
        private static readonly BackgroundWorker workerScanDropFolders = new BackgroundWorker();
        private static readonly BackgroundWorker workerRemoveMissing = new BackgroundWorker();
        private static readonly BackgroundWorker workerDeleteImportFolder = new BackgroundWorker();
        private static readonly BackgroundWorker workerMyAnime2 = new BackgroundWorker();
        private static readonly BackgroundWorker workerMediaInfo = new BackgroundWorker();

        private static readonly BackgroundWorker workerSetupDB = new BackgroundWorker();

        private static Timer autoUpdateTimer;
        private static Timer autoUpdateTimerShort;
        private static List<FileSystemWatcher> watcherVids;

        public static List<UserCulture> userLanguages = new List<UserCulture>();
        private readonly string mutexName = "JmmServer3.0Mutex";
        private ContextMenuStrip ctxTrayMenu;

        private readonly BackgroundWorker downloadImagesWorker = new BackgroundWorker();
        private bool isAppExiting;
        private DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));

        private Mutex mutex;


        private readonly NotifyIcon TippuTrayNotify;

        public MainWindow()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            //HibernatingRhinos.Profiler.Appender.NHibernate.NHibernateProfiler.Initialize();

            try
            {
                UnhandledExceptionManager.AddHandler();
            }
            catch
            {
            }

            if (!ServerSettings.AllowMultipleInstances)
            {
                try
                {
                    mutex = Mutex.OpenExisting(mutexName);
                    //since it hasn't thrown an exception, then we already have one copy of the app open.
                    MessageBox.Show(Properties.Resources.Server_Running,
                        Properties.Resources.JMMServer, MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                }
                catch (Exception Ex)
                {
                    //since we didn't find a mutex with that name, create one
                    Debug.WriteLine("Exception thrown:" + Ex.Message + " Creating a new mutex...");
                    mutex = new Mutex(true, mutexName);
                }
            }
            ServerSettings.DebugSettingsToLog();

            workerFileEvents.WorkerReportsProgress = false;
            workerFileEvents.WorkerSupportsCancellation = false;
            workerFileEvents.DoWork += workerFileEvents_DoWork;
            workerFileEvents.RunWorkerCompleted += workerFileEvents_RunWorkerCompleted;


            //Create an instance of the NotifyIcon Class
            TippuTrayNotify = new NotifyIcon();

            // This icon file needs to be in the bin folder of the application
            TippuTrayNotify = new NotifyIcon();
            var iconStream =
                Application.GetResourceStream(new Uri("pack://application:,,,/JMMServer;component/db.ico")).Stream;
            TippuTrayNotify.Icon = new Icon(iconStream);
            iconStream.Dispose();

            //show the Tray Notify Icon
            TippuTrayNotify.Visible = true;


            CreateMenus();

            ServerState.Instance.DatabaseAvailable = false;
            ServerState.Instance.ServerOnline = false;
            ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();

            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
            TippuTrayNotify.MouseDoubleClick += TippuTrayNotify_MouseDoubleClick;

            btnToolbarShutdown.Click += btnToolbarShutdown_Click;
            btnHasherPause.Click += btnHasherPause_Click;
            btnHasherResume.Click += btnHasherResume_Click;
            btnGeneralPause.Click += btnGeneralPause_Click;
            btnGeneralResume.Click += btnGeneralResume_Click;
            btnImagesPause.Click += btnImagesPause_Click;
            btnImagesResume.Click += btnImagesResume_Click;
            btnAdminMessages.Click += btnAdminMessages_Click;

            btnRemoveMissingFiles.Click += btnRemoveMissingFiles_Click;
            btnRunImport.Click += btnRunImport_Click;
            btnSyncMyList.Click += btnSyncMyList_Click;
            btnSyncVotes.Click += btnSyncVotes_Click;
            btnUpdateTvDBInfo.Click += btnUpdateTvDBInfo_Click;
            btnUpdateAllStats.Click += btnUpdateAllStats_Click;
            btnSyncTrakt.Click += btnSyncTrakt_Click;
            btnImportManualLinks.Click += btnImportManualLinks_Click;
            btnUpdateAniDBInfo.Click += btnUpdateAniDBInfo_Click;
            btnUploadAzureCache.Click += btnUploadAzureCache_Click;
            btnUpdateTraktInfo.Click += BtnUpdateTraktInfo_Click;

            Loaded += MainWindow_Loaded;
            downloadImagesWorker.DoWork += downloadImagesWorker_DoWork;
            downloadImagesWorker.WorkerSupportsCancellation = true;

            txtServerPort.Text = ServerSettings.JMMServerPort;
            chkEnableKodi.IsChecked = ServerSettings.EnableKodi;
            chkEnablePlex.IsChecked = ServerSettings.EnablePlex;


            btnToolbarHelp.Click += btnToolbarHelp_Click;
            btnApplyServerPort.Click += btnApplyServerPort_Click;
            btnUpdateMediaInfo.Click += btnUpdateMediaInfo_Click;

            workerMyAnime2.DoWork += workerMyAnime2_DoWork;
            workerMyAnime2.RunWorkerCompleted += workerMyAnime2_RunWorkerCompleted;
            workerMyAnime2.ProgressChanged += workerMyAnime2_ProgressChanged;
            workerMyAnime2.WorkerReportsProgress = true;

            workerMediaInfo.DoWork += workerMediaInfo_DoWork;

            workerImport.WorkerReportsProgress = true;
            workerImport.WorkerSupportsCancellation = true;
            workerImport.DoWork += workerImport_DoWork;

            workerScanFolder.WorkerReportsProgress = true;
            workerScanFolder.WorkerSupportsCancellation = true;
            workerScanFolder.DoWork += workerScanFolder_DoWork;


            workerScanDropFolders.WorkerReportsProgress = true;
            workerScanDropFolders.WorkerSupportsCancellation = true;
            workerScanDropFolders.DoWork += workerScanDropFolders_DoWork;

            workerRemoveMissing.WorkerReportsProgress = true;
            workerRemoveMissing.WorkerSupportsCancellation = true;
            workerRemoveMissing.DoWork += workerRemoveMissing_DoWork;

            workerDeleteImportFolder.WorkerReportsProgress = false;
            workerDeleteImportFolder.WorkerSupportsCancellation = true;
            workerDeleteImportFolder.DoWork += workerDeleteImportFolder_DoWork;

            workerSetupDB.DoWork += workerSetupDB_DoWork;
            workerSetupDB.RunWorkerCompleted += workerSetupDB_RunWorkerCompleted;

            //StartUp();

            cboDatabaseType.Items.Clear();
            cboDatabaseType.Items.Add("SQLite");
            cboDatabaseType.Items.Add("Microsoft SQL Server 2014");
            cboDatabaseType.Items.Add("MySQL");
            cboDatabaseType.SelectionChanged += cboDatabaseType_SelectionChanged;

            cboImagesPath.Items.Clear();
            cboImagesPath.Items.Add(Properties.Resources.Images_Default);
            cboImagesPath.Items.Add(Properties.Resources.Images_Custom);
            cboImagesPath.SelectionChanged += cboImagesPath_SelectionChanged;
            btnChooseImagesFolder.Click += btnChooseImagesFolder_Click;

            if (ServerSettings.BaseImagesPathIsDefault)
                cboImagesPath.SelectedIndex = 0;
            else
                cboImagesPath.SelectedIndex = 1;

            btnSaveDatabaseSettings.Click += btnSaveDatabaseSettings_Click;
            btnRefreshMSSQLServerList.Click += btnRefreshMSSQLServerList_Click;
            // btnInstallMSSQLServer.Click += new RoutedEventHandler(btnInstallMSSQLServer_Click);
            btnMaxOnStartup.Click += toggleMinimizeOnStartup;
            btnMinOnStartup.Click += toggleMinimizeOnStartup;
            btnLogs.Click += btnLogs_Click;
            btnChooseVLCLocation.Click += btnChooseVLCLocation_Click;
            btnJMMStartWithWindows.Click += btnJMMStartWithWindows_Click;
            btnUpdateAniDBLogin.Click += btnUpdateAniDBLogin_Click;


            btnAllowMultipleInstances.Click += toggleAllowMultipleInstances;
            btnDisallowMultipleInstances.Click += toggleAllowMultipleInstances;

            btnHasherClear.Click += btnHasherClear_Click;
            btnGeneralClear.Click += btnGeneralClear_Click;
            btnImagesClear.Click += btnImagesClear_Click;

            chkEnableKodi.Click += ChkEnableKodi_Click;
            chkEnablePlex.Click += ChkEnablePlex_Click;

            //automaticUpdater.MenuItem = mnuCheckForUpdates;

            ServerState.Instance.LoadSettings();
            workerFileEvents.RunWorkerAsync();

            cboLanguages.SelectionChanged += cboLanguages_SelectionChanged;

            InitCulture();
        }

        public static Uri baseAddressBinary
        {
            get { return new Uri(string.Format(baseAddressBinaryString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressImage
        {
            get { return new Uri(string.Format(baseAddressImageString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressStreaming
        {
            get { return new Uri(string.Format(baseAddressStreamingString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressStreamingMex
        {
            get { return new Uri(string.Format(baseAddressStreamingStringMex, ServerSettings.JMMServerFilePort)); }
        }

        public static Uri baseAddressMetro
        {
            get { return new Uri(string.Format(baseAddressMetroString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressMetroImage
        {
            get { return new Uri(string.Format(baseAddressMetroImageString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressREST
        {
            get { return new Uri(string.Format(baseAddressRESTString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressPlex
        {
            get { return new Uri(string.Format(baseAddressPlexString, ServerSettings.JMMServerPort)); }
        }

        public static Uri baseAddressKodi
        {
            get { return new Uri(string.Format(baseAddressKodiString, ServerSettings.JMMServerPort)); }
        }

        private void ChkEnablePlex_Click(object sender, RoutedEventArgs e)
        {
            ServerSettings.EnablePlex = chkEnablePlex.IsChecked.Value;
        }

        private void ChkEnableKodi_Click(object sender, RoutedEventArgs e)
        {
            ServerSettings.EnableKodi = chkEnableKodi.IsChecked.Value;
        }

        private void BtnUpdateTraktInfo_Click(object sender, RoutedEventArgs e)
        {
            TraktTVHelper.UpdateAllInfo();
        }

        private void workerFileEvents_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            logger.Info("Stopped thread for processing file creation events");
        }

        private void workerFileEvents_DoWork(object sender, DoWorkEventArgs e)
        {
            logger.Info("Started thread for processing file creation events");
            foreach (var evt in queueFileEvents)
            {
                try
                {
                    // this is a message to stop processing
                    if (evt == null) return;

                    logger.Info("New file created: {0}: {1}", evt.FullPath, evt.ChangeType);

                    if (evt.ChangeType == WatcherChangeTypes.Created)
                    {
                        if (Directory.Exists(evt.FullPath))
                        {
                            // When the path that was created represents a directory we need to manually get the contained files to add.
                            // The reason for this is that when a directory is moved into a source directory (from the same drive) we will only recieve
                            // an event for the directory and not the contained files. However, if the folder is copied from a different drive then
                            // a create event will fire for the directory and each file contained within it (As they are all treated as separate operations)
                            var files = Directory.GetFiles(evt.FullPath, "*.*", SearchOption.AllDirectories);

                            foreach (var file in files)
                            {
                                if (FileHashHelper.IsVideo(file))
                                {
                                    logger.Info("Found file {0} under folder {1}", file, evt.FullPath);

                                    var cmd = new CommandRequest_HashFile(file, false);
                                    cmd.Save();
                                }
                            }
                        }
                        else if (FileHashHelper.IsVideo(evt.FullPath))
                        {
                            var cmd = new CommandRequest_HashFile(evt.FullPath, false);
                            cmd.Save();
                        }
                    }

                    queueFileEvents.Remove(evt);
                }
                catch (Exception ex)
                {
                    logger.ErrorException(ex.Message, ex);
                    queueFileEvents.Remove(evt);
                    Thread.Sleep(1000);
                }
            }
        }

        private void btnUploadAzureCache_Click(object sender, RoutedEventArgs e)
        {
            var repAnime = new AniDB_AnimeRepository();
            var allAnime = repAnime.GetAll();
            var cnt = 0;
            foreach (var anime in allAnime)
            {
                cnt++;
                logger.Info(string.Format("Uploading anime {0} of {1} - {2}", cnt, allAnime.Count, anime.MainTitle));

                try
                {
                    var cmdAzure = new CommandRequest_Azure_SendAnimeFull(anime.AnimeID);
                    cmdAzure.Save();
                }
                catch
                {
                }
            }
        }

        private void btnImagesClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Cursor = Cursors.Wait;
                JMMService.CmdProcessorImages.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                var repCR = new CommandRequestRepository();
                foreach (var cr in repCR.GetAllCommandRequestImages())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorImages.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            Cursor = Cursors.Arrow;
        }

        private void btnGeneralClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Cursor = Cursors.Wait;
                JMMService.CmdProcessorGeneral.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                var repCR = new CommandRequestRepository();
                foreach (var cr in repCR.GetAllCommandRequestGeneral())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorGeneral.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            Cursor = Cursors.Arrow;
        }

        private void btnHasherClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Cursor = Cursors.Wait;
                JMMService.CmdProcessorHasher.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                var repCR = new CommandRequestRepository();
                foreach (var cr in repCR.GetAllCommandRequestHasher())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorHasher.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            Cursor = Cursors.Arrow;
        }


        private void toggleAllowMultipleInstances(object sender, RoutedEventArgs e)
        {
            ServerSettings.AllowMultipleInstances = !ServerSettings.AllowMultipleInstances;
            ServerState.Instance.AllowMultipleInstances = !ServerState.Instance.AllowMultipleInstances;
            ServerState.Instance.DisallowMultipleInstances = !ServerState.Instance.DisallowMultipleInstances;
        }


        private void btnAdminMessages_Click(object sender, RoutedEventArgs e)
        {
            var frm = new AdminMessagesForm();
            frm.Owner = this;
            frm.ShowDialog();
        }


        private void btnLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var logPath = Path.Combine(appPath, "logs");

                Process.Start(new ProcessStartInfo(logPath));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private void toggleMinimizeOnStartup(object sender, RoutedEventArgs e)
        {
            ServerSettings.MinimizeOnStartup = !ServerSettings.MinimizeOnStartup;
            ServerState.Instance.MinOnStartup = !ServerState.Instance.MinOnStartup;
            ServerState.Instance.MaxOnStartup = !ServerState.Instance.MaxOnStartup;
        }

        /*
        void btnInstallMSSQLServer_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.microsoft.com/web/gallery/install.aspx?appsxml=&appid=SQLExpressTools");
        }
        */

        private void btnJMMStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://jmediamanager.org/jmm-server/configuring-jmm-server/#jmm-start-with-windows");
        }

        private void btnUpdateAniDBLogin_Click(object sender, RoutedEventArgs e)
        {
            var frm = new InitialSetupForm();
            frm.Owner = this;
            frm.ShowDialog();
        }

        private void cboLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetCulture();
        }

        private void InitCulture()
        {
            try
            {
                var currentCulture = ServerSettings.Culture;

                cboLanguages.ItemsSource = UserCulture.SupportedLanguages;

                for (var i = 0; i < cboLanguages.Items.Count; i++)
                {
                    var ul = cboLanguages.Items[i] as UserCulture;
                    if (ul.Culture.Trim().ToUpper() == currentCulture.Trim().ToUpper())
                    {
                        cboLanguages.SelectedIndex = i;
                        break;
                    }
                }
                if (cboLanguages.SelectedIndex < 0)
                    cboLanguages.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private void SetCulture()
        {
            if (cboLanguages.SelectedItem == null) return;
            var ul = cboLanguages.SelectedItem as UserCulture;

            try
            {
                var ci = new CultureInfo(ul.Culture);
                CultureManager.UICulture = ci;

                ServerSettings.Culture = ul.Culture;
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }


        private void btnChooseVLCLocation_Click(object sender, RoutedEventArgs e)
        {
            var errorMsg = "";
            var streamingAddress = "";

            Utils.StartStreamingVideo("localhost",
                @"e:\test\[Frostii]_K-On!_-_S5_(1280x720_Blu-ray_H264)_[8B9E0A76].mkv", "12000", "30", "1280",
                "128", "44100", "8088", ref errorMsg, ref streamingAddress);

            return;

            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServerSettings.VLCLocation = dialog.FileName;
            }
        }

        private void btnChooseImagesFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServerSettings.BaseImagesPath = dialog.SelectedPath;
            }
        }

        private void cboImagesPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboImagesPath.SelectedIndex == 0)
            {
                ServerSettings.BaseImagesPathIsDefault = true;
                btnChooseImagesFolder.Visibility = Visibility.Hidden;
            }
            else
            {
                ServerSettings.BaseImagesPathIsDefault = false;
                btnChooseImagesFolder.Visibility = Visibility.Visible;
            }
        }

        private void btnApplyServerPort_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtServerPort.Text))
            {
                MessageBox.Show(Properties.Resources.Server_EnterAnyValue, Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtServerPort.Focus();
                return;
            }

            var port = 0;
            int.TryParse(txtServerPort.Text, out port);
            if (port <= 0 || port > 65535)
            {
                MessageBox.Show(Properties.Resources.Server_EnterCertainValue, Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtServerPort.Focus();
                return;
            }

            try
            {
                Cursor = Cursors.Wait;

                JMMService.CmdProcessorGeneral.Paused = true;
                JMMService.CmdProcessorHasher.Paused = true;
                JMMService.CmdProcessorImages.Paused = true;

                StopHost();

                if (Utils.SetNetworkRequirements(port.ToString(), oldPort: ServerSettings.JMMServerPort))
                    ServerSettings.JMMServerPort = port.ToString();
                else
                    txtServerPort.Text = ServerSettings.JMMServerPort;

                StartBinaryHost();
                StartImageHost();
                StartImageHostMetro();
                StartPlexHost();
                StartKodiHost();
                StartFileHost();
                StartStreamingHost();
                StartRESTHost();

                JMMService.CmdProcessorGeneral.Paused = false;
                JMMService.CmdProcessorHasher.Paused = false;
                JMMService.CmdProcessorImages.Paused = false;

                Cursor = Cursors.Arrow;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                MessageBox.Show(ex.Message, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnToolbarHelp_Click(object sender, RoutedEventArgs e)
        {
            //AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
            //AnimeSeries ser = repSeries.GetByID(222);
            //ser.UpdateStats(true, true, true);

            //TraktTVHelper.GetFriendsRequests();

            //FileHashHelper.GetMediaInfo(@"C:\[Hiryuu] Maken-Ki! 09 [Hi10P 1280x720 H264] [EE47C947].mkv", true);

            //CommandRequest_ReadMediaInfo cr1 = new CommandRequest_ReadMediaInfo(2038);
            //cr1.Save();

            //CommandRequest_ReadMediaInfo cr2 = new CommandRequest_ReadMediaInfo(2037);
            //cr2.Save();

            //anime temp = MALHelper.SearchAnimesByTitle("Naruto");
            //MALHelper.VerifyCredentials();

            //JMMService.DebugFlag = !JMMService.DebugFlag;

            //AnimeEpisodeRepository repEp = new AnimeEpisodeRepository();
            //AnimeEpisode ep = repEp.GetByID(2430);
            //MALHelper.UpdateMAL(ep);

            //CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(8107);
            //cmdMAL.ProcessCommand();


            //CommandRequest_MALDownloadStatusFromMAL cmd = new CommandRequest_MALDownloadStatusFromMAL();
            //cmd.Save();

            //AppVersionsResult appv = XMLService.GetAppVersions();

            //JMMServiceImplementation imp = new JMMServiceImplementation();
            //imp.GetMissingEpisodes(1, true, true);

            //VideoLocalRepository repVidLocal = new VideoLocalRepository();
            /*VideoLocal vlocal = new VideoLocal();
            vlocal.DateTimeUpdated = DateTime.Now;
            vlocal.DateTimeCreated = vlocal.DateTimeUpdated;
            vlocal.FilePath = "";
            vlocal.FileSize = 656181746;
            vlocal.ImportFolderID = 1;
            vlocal.Hash = "453063B2993D4AC4BA51F4A64170260A";
            vlocal.CRC32 = "";
            vlocal.MD5 = "";
            vlocal.SHA1 = "";
            vlocal.IsIgnored = 0;
            vlocal.HashSource = (int)HashSource.DirectHash;
            repVidLocal.Save(vlocal);*/

            //JMMService.AnidbProcessor.UpdateMyListStats();

            //UpdateVersion();

            /*VideoLocalRepository repVidLocal = new VideoLocalRepository();
            VideoLocal vid = repVidLocal.GetByID(194); RenameFileHelper.Test(vid);

            vid = repVidLocal.GetByID(295); RenameFileHelper.Test(vid);
            vid = repVidLocal.GetByID(396); RenameFileHelper.Test(vid);
            vid = repVidLocal.GetByID(497); RenameFileHelper.Test(vid);
            vid = repVidLocal.GetByID(598); RenameFileHelper.Test(vid);

            return;

            

            JMMService.AnidbProcessor.GetFileInfo(vid);

            return;*/

            //Importer.UpdateAniDBFileData(true, true);

            //JMMServiceImplementationMetro imp = new JMMServiceImplementationMetro();
            //imp.GetAnimeDetail(4880);

            /*CrossRef_AniDB_MALRepository rep = new CrossRef_AniDB_MALRepository();
            foreach (JMMServer.Entities.CrossRef_AniDB_MAL xref in rep.GetAll())
            {
                //AzureWebAPI.Send_CrossRef_AniDB_MAL(xref);
                break;
            }

            AniDB_Anime anime2 = JMMService.AnidbProcessor.GetAnimeInfoHTTP(9127, true, false);

            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            List<AniDB_Anime> allAnime = repAnime.GetAll();
            int cnt = 0;
            foreach (AniDB_Anime anime in allAnime)
            {
                cnt++;
                logger.Info(string.Format("Uploading anime {0} of {1} - {2}", cnt, allAnime.Count, anime.MainTitle));

                try
                {
                    //CommandRequest_Azure_SendAnimeFull cmdAzure = new CommandRequest_Azure_SendAnimeFull(anime.AnimeID);
                    //cmdAzure.Save();
                }
                catch { }
            }
            */


            /*try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    AniDB_Anime anime = JMMService.AnidbProcessor.GetAnimeInfoHTTPFromCache(session, 5842, false);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }*/

            //CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(3482, false, false);
            //cmd.Save();

            //string xml = AzureWebAPI.Get_AnimeXML(3483);
            //XmlDocument docAnime = new XmlDocument();
            //docAnime.LoadXml(xml);

            //JMMService.AnidbProcessor.IsBanned = true;
            //JMMService.AnidbProcessor.BanOrigin = "HTTP";
            //JMMService.AnidbProcessor.BanTime = DateTime.Now;

            //GenerateAzureList();
            //SendToAzure();
            //SendToAzureXML();

            //CommandRequest_GetAniDBTitles cmd = new CommandRequest_GetAniDBTitles();
            //cmd.Save();

            //AzureWebAPI.Delete_CrossRefAniDBTvDB();

            /*
            CrossRef_AniDB_TvDBV2Repository rep = new CrossRef_AniDB_TvDBV2Repository();
            List<CrossRef_AniDB_TvDBV2> xrefs = rep.GetAll();
            AzureWebAPI.Send_CrossRefAniDBTvDB(xrefs[0], "Test");
            */

            //Azure_AnimeLink aid = AzureWebAPI.Admin_GetRandomLinkForApproval(AzureLinkType.TvDB);

            /*
            IEnumerable<TraktV2SearchShowResult> results = TraktTVHelper.SearchShowNew("Trinity");
            if (results != null)
            {
                foreach (TraktV2SearchShowResult res in results)
                    Console.WriteLine(res.show.Title);
            }
            */

            // trinity-seven - 10441
            //TraktV2ShowExtended show = TraktTVHelper.GetShowInfoV2("madan-no-ou-to-vanadis");
            //TraktTVHelper.GetShowCommentsV2(8660);
            //TraktTVHelper.GetFriendsV2();
            //TraktTVHelper.RefreshAuthToken();


            //D003BB3D
            //string ret = TraktTVHelper.EnterTraktPIN("D003BB3D");

            //string x = "";
            //TraktTVHelper.PostCommentShow("mayday", "this is a test comment", false, ref x);

            //AnimeEpisodeRepository repEp = new AnimeEpisodeRepository();
            //AnimeEpisode ep = repEp.GetByID(32);
            //TraktTVHelper.SyncEpisodeToTrakt(ep, TraktSyncType.HistoryAdd);

            //TraktTVHelper.SearchShowByIDV2("tvdb", "279827");
            //TraktTVHelper.SearchShowV2("Monster Musume");
            //TraktTVHelper.SyncCollectionToTrakt();
            //TraktTVHelper.SyncEpisodeToTrakt(TraktSyncType.HistoryAdd, "mad-men", 1, 1, false);
            //TraktTVHelper.SyncEpisodeToTrakt(TraktSyncType.HistoryRemove, "mad-men", 1, 1, false);
            //TraktTVHelper.SyncEpisodeToTrakt(TraktSyncType.CollectionAdd, "mad-men", 1, 3, false);
            //TraktTVHelper.SyncEpisodeToTrakt(TraktSyncType.CollectionRemove, "mad-men", 1, 3, false);

            //AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

            //AnimeSeries ser1 = repSeries.GetByAnimeID(10445);
            //TraktTVHelper.SyncCollectionToTrakt_Series(ser1);

            //AnimeSeries ser2 = repSeries.GetByAnimeID(10846);
            //TraktTVHelper.SyncCollectionToTrakt_Series(ser2);
            //TraktTVHelper.UpdateAllInfoAndImages("my-teen-romantic-comedy-snafu", true);

            //TraktTVHelper.CleanupDatabase();
            //TraktTVHelper.SyncCollectionToTrakt();

            //JMMServer.Providers.Azure.Azure_AnimeLink link2 = JMMServer.Providers.Azure.AzureWebAPI.Admin_GetRandomTraktLinkForApproval();
            //List<Providers.Azure.CrossRef_AniDB_Trakt> xrefs= JMMServer.Providers.Azure.AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(link2.RandomAnimeID);


            //TraktTVHelper.RefreshAuthToken();


            var frm = new AboutForm();
            frm.Owner = this;
            frm.ShowDialog();
        }

        private void GenerateAzureList()
        {
            // get a lst of anime's that we already have
            var repAnime = new AniDB_AnimeRepository();
            var allAnime = repAnime.GetAll();
            var localAnimeIDs = new Dictionary<int, int>();
            foreach (var anime in allAnime)
            {
                localAnimeIDs[anime.AnimeID] = anime.AnimeID;
            }

            // loop through the list of valid anime id's and add the ones we don't have yet
            var validAnimeIDs = new Dictionary<int, int>();

            string line;
            var file =
                new StreamReader(@"e:\animetitles.txt");
            while ((line = file.ReadLine()) != null)
            {
                var titlesArray = line.Split('|');

                try
                {
                    var aid = int.Parse(titlesArray[0]);
                    if (!localAnimeIDs.ContainsKey(aid))
                        validAnimeIDs[aid] = aid;
                }
                catch
                {
                }
            }

            file.Close();

            var aids = "";
            var shuffledList = validAnimeIDs.Values.OrderBy(a => Guid.NewGuid());
            var i = 0;
            foreach (var animeID in shuffledList)
            {
                i++;
                if (!string.IsNullOrEmpty(aids)) aids += ",";
                aids += animeID;

                if (i == 250)
                {
                    logger.Info(aids);
                    aids = "";
                    i = 0;
                }
            }

            logger.Info(aids);
        }

        private void SendToAzureXML()
        {
            var dt = DateTime.Now.AddYears(-2);
            var rep = new AniDB_AnimeRepository();
            var allAnime = rep.GetAll();

            var sentAnime = 0;
            foreach (var anime in rep.GetAll())
            {
                if (!anime.EndDate.HasValue) continue;

                if (anime.EndDate.Value > dt) continue;

                sentAnime++;
                var cmd = new CommandRequest_Azure_SendAnimeXML(anime.AnimeID);
                cmd.Save();
            }

            logger.Info(string.Format("Sent Anime XML to Cache: {0} out of {1}", sentAnime, allAnime.Count));
        }

        private void SendToAzure()
        {
            var validAnimeIDs = new Dictionary<int, int>();

            string line;

            // Read the file and display it line by line.
            var file =
                new StreamReader(@"e:\animetitles.txt");
            while ((line = file.ReadLine()) != null)
            {
                var titlesArray = line.Split('|');

                try
                {
                    var aid = int.Parse(titlesArray[0]);
                    validAnimeIDs[aid] = aid;
                }
                catch
                {
                }
            }

            file.Close();

            var aids =
                "9516,6719,9606,8751,7453,6969,7821,7738,6694,6854,6101,8267,9398,9369,7395,7687,7345,8748,6350,6437,6408,7824,6334,8976,4651,7329,6433,8750,9498,8306,6919,8598,6355,6084,6775,8482,6089,7441,7541,7130,9013,6299,6983,7740,6329,6401,9459,8458,8800,7290,8859,6957,8503,6057,7758,7086,7943,8007,8349,6858,7776,7194,8807,6822,8058,7274,6818,9309,9488,7564,9593,8906,6155,7191,7267,7861,7109,9617,7954,7944,6359,7877,7701,7447,8736,7260,8492,9107,9578,6843,7190,9036,7614,6404,6018,8895,6234,6855,7041,7504,6847,6889,7092,8672,9452,9086,8770,4515,8103,8100,8122,9441,7025,8403,6335,9607,8559,7193,7273,7553,6242,7108,7052,6171,9634,7846,8471,7772,7557,9597,7827,6039,6712,7784,7830,8330,6902,6187,8431,8258,7956,7373,8083,8130,7535,8003,8237,7153,8170,7439,8094,9332,6539,6773,6812,7220,7703,7406,7670,7876,8497,8407,7299,9299,7583,7825,7556,6950,8127,7147,7747,9009,6044,6393,6864,7616,9567,8612,6705,7139,7070,6804,7901,8133,7817,6596,6553,8073,6718,8303,7782,8724,6972,8671,6907,8030,7030,7141,6878,8036,8231,7594,6813,7920,7841,7922,7095,6927,6754,6936,7427,7497,9251,7253,8140,9601,6735,7160,7538,6893,7203,7346,6797,6516,8500,8245,8440,7863,7467,7975,8808,6277,6481,6733,8790,7117,7063,6924,8293,6208,6882,6892";
            var aidArray = aids.Split(',');

            logger.Info(string.Format("Queueing {0} anime updates", aidArray.Length));
            var cnt = 0;
            foreach (var animeid in aidArray)
            {
                if (validAnimeIDs.ContainsKey(int.Parse(animeid)))
                {
                    var cmd = new CommandRequest_GetAnimeHTTP(int.Parse(animeid), true, false);
                    cmd.Save();
                    cnt++;
                }
            }
            logger.Info(string.Format("Queued {0} anime updates", cnt));
        }

        private void DownloadAllImages()
        {
            if (!downloadImagesWorker.IsBusy)
                downloadImagesWorker.RunWorkerAsync();
        }

        private void downloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Importer.RunImport_GetImages();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //ServerInfo.Instance.RefreshImportFolders();

            if (ServerSettings.MinimizeOnStartup) MinimizeToTray();

            tabControl1.SelectedIndex = 4; // setup

            if (ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase))
            {
                btnUploadAzureCache.Visibility = Visibility.Visible;
            }
            logger.Info("Clearing Cache...");

            Utils.ClearAutoUpdateCache();

            ShowDatabaseSetup();
            logger.Info("Initializing DB...");

            workerSetupDB.RunWorkerAsync();

            var a = Assembly.GetExecutingAssembly();
            if (a != null)
            {
                ServerState.Instance.ApplicationVersion = Utils.GetApplicationVersion(a);
            }

            logger.Info("Checking for updates...");
            CheckForUpdatesNew(false);
        }

        public void CheckForUpdatesNew(bool forceShowForm)
        {
            try
            {
                long verCurrent = 0;
                long verNew = 0;

                // get the latest version as according to the release
                if (!forceShowForm)
                {
                    var verInfo = JMMAutoUpdatesHelper.GetLatestVersionInfo();
                    if (verInfo == null) return;

                    // get the user's version
                    var a = Assembly.GetExecutingAssembly();
                    if (a == null)
                    {
                        logger.Error("Could not get current version");
                        return;
                    }
                    var an = a.GetName();

                    verNew = verInfo.versions.ServerVersionAbs;

                    verCurrent = an.Version.Revision * 100 +
                                 an.Version.Build * 100 * 100 +
                                 an.Version.Minor * 100 * 100 * 100 +
                                 an.Version.Major * 100 * 100 * 100 * 100;
                }

                if (forceShowForm || verNew > verCurrent)
                {
                    var frm = new UpdateForm();
                    frm.Owner = this;
                    frm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private void StartUp()
        {
        }


        private void autoUpdateTimerShort_Elapsed(object sender, ElapsedEventArgs e)
        {
            autoUpdateTimerShort.Enabled = false;
            JMMService.CmdProcessorImages.NotifyOfNewCommand();

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
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private static void autoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Importer.CheckForCalendarUpdate(false);
            Importer.CheckForAnimeUpdate(false);
            Importer.CheckForTvDBUpdates(false);
            Importer.CheckForMyListSyncUpdate(false);
            Importer.CheckForTraktAllSeriesUpdate(false);
            Importer.CheckForTraktTokenUpdate(false);
            Importer.CheckForMALUpdate(false);
            Importer.CheckForMyListStatsUpdate(false);
            Importer.CheckForAniDBFileUpdate(false);
            Importer.UpdateAniDBTitles();
            Importer.SendUserInfoUpdate(false);
        }

        public static void StartWatchingFiles()
        {
            StopWatchingFiles();

            watcherVids = new List<FileSystemWatcher>();

            var repNetShares = new ImportFolderRepository();
            foreach (var share in repNetShares.GetAll())
            {
                try
                {
                    if (Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
                    {
                        logger.Info("Watching ImportFolder: {0} || {1}", share.ImportFolderName,
                            share.ImportFolderLocation);
                        var fsw = new FileSystemWatcher(share.ImportFolderLocation);
                        fsw.IncludeSubdirectories = true;
                        fsw.Created += fsw_Created;
                        fsw.EnableRaisingEvents = true;
                        watcherVids.Add(fsw);
                    }
                    else
                    {
                        logger.Info("ImportFolder found but not watching: {0} || {1}", share.ImportFolderName,
                            share.ImportFolderLocation);
                    }
                }
                catch (Exception ex)
                {
                    logger.ErrorException(ex.ToString(), ex);
                }
            }
        }


        public static void StopWatchingFiles()
        {
            if (watcherVids == null) return;

            foreach (var fsw in watcherVids)
            {
                fsw.EnableRaisingEvents = false;
            }
        }

        private static void fsw_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                queueFileEvents.Add(e);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
            if (!workerImport.IsBusy)
                workerImport.RunWorkerAsync();
        }

        public static void RemoveMissingFiles()
        {
            if (!workerRemoveMissing.IsBusy)
                workerRemoveMissing.RunWorkerAsync();
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

        private static void workerRemoveMissing_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RemoveRecordsWithoutPhysicalFiles();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        private void workerDeleteImportFolder_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var importFolderID = int.Parse(e.Argument.ToString());
                Importer.DeleteImportFolder(importFolderID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        private static void workerScanFolder_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RunImport_ScanFolder(int.Parse(e.Argument.ToString()));
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        private void workerScanDropFolders_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RunImport_DropFolders();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        private static void workerImport_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.RunImport_NewFiles();
                Importer.RunImport_IntegrityCheck();

                // TODO drop folder

                // TvDB association checks
                Importer.RunImport_ScanTvDB();

                // Trakt association checks
                Importer.RunImport_ScanTrakt();

                // MovieDB association checks
                Importer.RunImport_ScanMovieDB();

                // Check for missing images
                Importer.RunImport_GetImages();

                // MAL association checks
                Importer.RunImport_ScanMAL();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }


        private static void StartBinaryHost()
        {
            var encoding = new BinaryMessageEncodingBindingElement();
            var transport = new HttpTransportBindingElement();
            Binding binding = new CustomBinding(encoding, transport);
            binding.Name = "BinaryBinding";
            binding.Namespace = "";


            //binding.MessageEncoding = WSMessageEncoding.Mtom;
            //binding.MaxReceivedMessageSize = 2147483647;


            // Create the ServiceHost.
            hostBinary = new ServiceHost(typeof(JMMServiceImplementation), baseAddressBinary);
            // Enable metadata publishing.
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            hostBinary.Description.Behaviors.Add(smb);

            hostBinary.AddServiceEndpoint(typeof(IJMMServer), binding, baseAddressBinary);

            // ** DISCOVERY ** //
            // make the service discoverable by adding the discovery behavior
            //hostBinary.Description.Behaviors.Add(new ServiceDiscoveryBehavior());

            // ** DISCOVERY ** //
            // add the discovery endpoint that specifies where to publish the services
            //hostBinary.AddServiceEndpoint(new UdpDiscoveryEndpoint());


            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.
            hostBinary.Open();
            logger.Trace("Now Accepting client connections for test host...");
        }

        private static void StartImageHost()
        {
            var binding = new BasicHttpBinding();
            binding.MessageEncoding = WSMessageEncoding.Mtom;
            binding.MaxReceivedMessageSize = 2147483647;
            binding.Name = "httpLargeMessageStream";


            // Create the ServiceHost.
            hostImage = new ServiceHost(typeof(JMMServiceImplementationImage), baseAddressImage);
            // Enable metadata publishing.
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            hostImage.Description.Behaviors.Add(smb);

            hostImage.AddServiceEndpoint(typeof(IJMMServerImage), binding, baseAddressImage);
            hostImage.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.

            hostImage.Open();
            logger.Trace("Now Accepting client connections for images...");
        }

        private static void StartStreamingHost_HTTP()
        {
            var binding = new BasicHttpBinding();
            binding.TransferMode = TransferMode.Streamed;
            binding.ReceiveTimeout = TimeSpan.MaxValue;
            binding.SendTimeout = TimeSpan.MaxValue;
            //binding.MessageEncoding = WSMessageEncoding.Mtom;
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.CloseTimeout = TimeSpan.MaxValue;
            binding.Name = "FileStreaming";

            binding.Security.Mode = BasicHttpSecurityMode.None;


            // Create the ServiceHost.
            hostStreaming = new ServiceHost(typeof(JMMServiceImplementationStreaming), baseAddressStreaming);
            // Enable metadata publishing.
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            hostStreaming.Description.Behaviors.Add(smb);

            hostStreaming.AddServiceEndpoint(typeof(IJMMServerStreaming), binding, baseAddressStreaming);
            hostStreaming.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.
            hostStreaming.Open();
            logger.Trace("Now Accepting client connections for images...");
        }

        private static void StartStreamingHost()
        {
            var binding = new BinaryOverHTTPBinding();

            // Create the ServiceHost.
            hostStreaming = new ServiceHost(typeof(JMMServiceImplementationStreaming), baseAddressStreaming);
            // Enable metadata publishing.
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            hostStreaming.Description.Behaviors.Add(smb);

            hostStreaming.AddServiceEndpoint(typeof(IJMMServerStreaming), binding, baseAddressStreaming);
            hostStreaming.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.
            hostStreaming.Open();
            logger.Trace("Now Accepting client connections for images...");
        }

        private static void StartStreamingHost_TCP()
        {
            var netTCPbinding = new NetTcpBinding();
            netTCPbinding.TransferMode = TransferMode.Streamed;
            netTCPbinding.ReceiveTimeout = TimeSpan.MaxValue;
            netTCPbinding.SendTimeout = TimeSpan.MaxValue;
            netTCPbinding.MaxReceivedMessageSize = int.MaxValue;
            netTCPbinding.CloseTimeout = TimeSpan.MaxValue;

            netTCPbinding.Security.Mode = SecurityMode.Transport;
            netTCPbinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
            //netTCPbinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
            //netTCPbinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.None;
            //netTCPbinding.Security.Message.ClientCredentialType = MessageCredentialType.None;

            hostStreaming = new ServiceHost(typeof(JMMServiceImplementationStreaming));
            hostStreaming.AddServiceEndpoint(typeof(IJMMServerStreaming), netTCPbinding, baseAddressStreaming);
            hostStreaming.Description.Behaviors.Add(new ServiceMetadataBehavior());

            var mexBinding = MetadataExchangeBindings.CreateMexTcpBinding();
            hostStreaming.AddServiceEndpoint(typeof(IMetadataExchange), mexBinding, baseAddressStreamingMex);

            hostStreaming.Open();
            logger.Trace("Now Accepting client connections for streaming...");
        }

        private static void StartImageHostMetro()
        {
            var binding = new BasicHttpBinding();
            binding.MessageEncoding = WSMessageEncoding.Text;
            binding.MaxReceivedMessageSize = 2147483647;
            binding.Name = "httpLargeMessageStream";


            // Create the ServiceHost.
            hostMetroImage = new ServiceHost(typeof(JMMServiceImplementationImage), baseAddressMetroImage);
            // Enable metadata publishing.
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            hostMetroImage.Description.Behaviors.Add(smb);

            hostMetroImage.AddServiceEndpoint(typeof(IJMMServerImage), binding, baseAddressMetroImage);
            hostMetroImage.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.
            hostMetroImage.Open();
            logger.Trace("Now Accepting client connections for images (metro)...");
        }


        private static void StartMetroHost()
        {
            var binding = new BasicHttpBinding();
            binding.MaxReceivedMessageSize = 2147483647;
            binding.Name = "metroTest";


            // Create the ServiceHost.
            hostMetro = new ServiceHost(typeof(JMMServiceImplementationMetro), baseAddressMetro);
            // Enable metadata publishing.
            var smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.HttpGetUrl = baseAddressMetro;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;

            hostMetro.Description.Behaviors.Add(smb);

            hostMetro.AddServiceEndpoint(typeof(IJMMServerMetro), binding, baseAddressMetro);
            hostMetro.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.
            hostMetro.Open();
            logger.Trace("Now Accepting client connections for metro apps...");
        }


        private static void StartPlexHost()
        {
            hostPlex = new WebServiceHost(typeof(JMMServiceImplementationPlex), baseAddressPlex);
            var ep = hostPlex.AddServiceEndpoint(typeof(IJMMServerPlex), new WebHttpBinding(), "");
            var stp = hostPlex.Description.Behaviors.Find<ServiceDebugBehavior>();
            stp.HttpHelpPageEnabled = false;
            hostPlex.Open();
        }

        private static void StartKodiHost()
        {
            hostKodi = new WebServiceHost(typeof(JMMServiceImplementationKodi), baseAddressKodi);
            var ep = hostKodi.AddServiceEndpoint(typeof(IJMMServerKodi), new WebHttpBinding(), "");
            var stp = hostKodi.Description.Behaviors.Find<ServiceDebugBehavior>();
            stp.HttpHelpPageEnabled = false;
            hostKodi.Open();
        }

        private static void StartFileHost()
        {
            hostFile = new FileServer.FileServer(int.Parse(ServerSettings.JMMServerFilePort));
            hostFile.Start();
            if (ServerSettings.ExperimentalUPnP)
                FileServer.FileServer.UPnPJMMFilePort(int.Parse(ServerSettings.JMMServerFilePort));
            //                new MessagingServer(new ServiceFactory(), new MessagingServerConfiguration(new HttpMessageFactory()));
            //           hostFile.Start(new IPEndPoint(IPAddress.Any, int.Parse(ServerSettings.JMMServerFilePort)));
        }

        private static void StartRESTHost()
        {
            hostREST = new WebServiceHost(typeof(JMMServiceImplementationREST), baseAddressREST);
            var ep = hostREST.AddServiceEndpoint(typeof(IJMMServerREST),
                new WebHttpBinding
                {
                    CloseTimeout = TimeSpan.FromMinutes(20),
                    OpenTimeout = TimeSpan.FromMinutes(20),
                    SendTimeout = TimeSpan.FromMinutes(20),
                    MaxBufferSize = 65536,
                    MaxBufferPoolSize = 524288,
                    MaxReceivedMessageSize = 107374182400,
                    TransferMode = TransferMode.StreamedResponse
                }, "");
            var stp = hostREST.Description.Behaviors.Find<ServiceDebugBehavior>();
            stp.HttpHelpPageEnabled = false;
            hostREST.Open();
        }

        private static void StartRESTHost_New()
        {
            hostREST = new WebServiceHost(typeof(JMMServiceImplementationREST), baseAddressREST);

            var ep = hostREST.AddServiceEndpoint(typeof(IJMMServerREST), new WebHttpBinding
            {
                CloseTimeout = TimeSpan.FromMinutes(20),
                OpenTimeout = TimeSpan.FromMinutes(20),
                SendTimeout = TimeSpan.FromMinutes(20),
                MaxBufferSize = 65536,
                MaxBufferPoolSize = 524288,
                MaxReceivedMessageSize = 107374182400,
                TransferMode = TransferMode.StreamedResponse
            }, "");

            // modify behaviours

            var wbb = hostREST.Description.Behaviors.Find<WebHttpBehavior>();
            wbb.AutomaticFormatSelectionEnabled = true;

            var stp = hostREST.Description.Behaviors.Find<ServiceDebugBehavior>();
            stp.HttpHelpPageEnabled = false;

            hostREST.Open();
        }

        private static void ReadFiles()
        {
            // Steps for processing a file
            // 1. Check if it is a video file
            // 2. Check if we have a VideoLocal record for that file
            // .........

            // get a complete list of files
            var fileList = new List<string>();
            var repNetShares = new ImportFolderRepository();
            foreach (var share in repNetShares.GetAll())
            {
                logger.Debug("Import Folder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);

                Utils.GetFilesForImportFolder(share.ImportFolderLocation, ref fileList);
            }


            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            var i = 0;

            // get a list of all files in the share
            foreach (var fileName in fileList)
            {
                i++;
                filesFound++;

                if (fileName.Contains("Sennou"))
                {
                    logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);
                }

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        private static void StopHost()
        {
            // Close the ServiceHost.
            //host.Close();

            if (hostImage != null)
                hostImage.Close();

            if (hostBinary != null)
                hostBinary.Close();

            if (hostMetro != null)
                hostMetro.Close();

            if (hostMetroImage != null)
                hostMetroImage.Close();

            if (hostREST != null)
                hostREST.Close();


            if (hostStreaming != null)
                hostStreaming.Close();

            if (hostPlex != null)
                hostPlex.Close();

            if (hostKodi != null)
                hostKodi.Close();

            if (hostFile != null)
                hostFile.Stop();
        }

        private static void SetupAniDBProcessor()
        {
            JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                ServerSettings.AniDB_ServerAddress,
                ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);
        }

        private static void AniDBDispose()
        {
            logger.Info("Disposing...");
            if (JMMService.AnidbProcessor != null)
            {
                JMMService.AnidbProcessor.ForceLogout();
                JMMService.AnidbProcessor.Dispose();
                Thread.Sleep(1000);
            }
        }

        public static int OnHashProgress(string fileName, int percentComplete)
        {
            //string msg = Path.GetFileName(fileName);
            //if (msg.Length > 35) msg = msg.Substring(0, 35);
            //logger.Info("{0}% Hashing ({1})", percentComplete, Path.GetFileName(fileName));
            return 1; //continue hashing (return 0 to abort)
        }

        #region Database settings and initial start up

        private void btnSaveDatabaseSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnSaveDatabaseSettings.IsEnabled = false;
                cboDatabaseType.IsEnabled = false;
                btnRefreshMSSQLServerList.IsEnabled = false;

                if (ServerState.Instance.DatabaseIsSQLite)
                {
                    ServerSettings.DatabaseType = "SQLite";
                }
                else if (ServerState.Instance.DatabaseIsSQLServer)
                {
                    if (string.IsNullOrEmpty(txtMSSQL_DatabaseName.Text) ||
                        string.IsNullOrEmpty(txtMSSQL_Password.Password)
                        || string.IsNullOrEmpty(cboMSSQLServerList.Text) || string.IsNullOrEmpty(txtMSSQL_Username.Text))
                    {
                        MessageBox.Show(Properties.Resources.Server_FillOutSettings, Properties.Resources.Error,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        txtMSSQL_DatabaseName.Focus();
                        return;
                    }

                    ServerSettings.DatabaseType = "SQLServer";
                    ServerSettings.DatabaseName = txtMSSQL_DatabaseName.Text;
                    ServerSettings.DatabasePassword = txtMSSQL_Password.Password;
                    ServerSettings.DatabaseServer = cboMSSQLServerList.Text;
                    ServerSettings.DatabaseUsername = txtMSSQL_Username.Text;
                }
                else if (ServerState.Instance.DatabaseIsMySQL)
                {
                    if (string.IsNullOrEmpty(txtMySQL_DatabaseName.Text) ||
                        string.IsNullOrEmpty(txtMySQL_Password.Password)
                        || string.IsNullOrEmpty(txtMySQL_ServerAddress.Text) ||
                        string.IsNullOrEmpty(txtMySQL_Username.Text))
                    {
                        MessageBox.Show(Properties.Resources.Server_FillOutSettings, Properties.Resources.Error,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        txtMySQL_DatabaseName.Focus();
                        return;
                    }

                    ServerSettings.DatabaseType = "MySQL";
                    ServerSettings.MySQL_SchemaName = txtMySQL_DatabaseName.Text;
                    ServerSettings.MySQL_Password = txtMySQL_Password.Password;
                    ServerSettings.MySQL_Hostname = txtMySQL_ServerAddress.Text;
                    ServerSettings.MySQL_Username = txtMySQL_Username.Text;
                }

                workerSetupDB.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
                MessageBox.Show(Properties.Resources.Server_FailedToStart + ex.Message, Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void cboDatabaseType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ServerState.Instance.DatabaseIsSQLite = false;
            ServerState.Instance.DatabaseIsSQLServer = false;
            ServerState.Instance.DatabaseIsMySQL = false;

            switch (cboDatabaseType.SelectedIndex)
            {
                case 0:
                    ServerState.Instance.DatabaseIsSQLite = true;
                    break;
                case 1:

                    var anySettingsMSSQL = !string.IsNullOrEmpty(ServerSettings.DatabaseName) ||
                                           !string.IsNullOrEmpty(ServerSettings.DatabasePassword)
                                           || !string.IsNullOrEmpty(ServerSettings.DatabaseServer) ||
                                           !string.IsNullOrEmpty(ServerSettings.DatabaseUsername);

                    if (anySettingsMSSQL)
                    {
                        txtMSSQL_DatabaseName.Text = ServerSettings.DatabaseName;
                        txtMSSQL_Password.Password = ServerSettings.DatabasePassword;

                        cboMSSQLServerList.Text = ServerSettings.DatabaseServer;
                        txtMSSQL_Username.Text = ServerSettings.DatabaseUsername;
                    }
                    else
                    {
                        txtMSSQL_DatabaseName.Text = "JMMServer";
                        txtMSSQL_Password.Password = "";
                        cboMSSQLServerList.Text = "localhost";
                        txtMSSQL_Username.Text = "sa";
                    }
                    ServerState.Instance.DatabaseIsSQLServer = true;
                    break;
                case 2:

                    var anySettingsMySQL = !string.IsNullOrEmpty(ServerSettings.MySQL_SchemaName) ||
                                           !string.IsNullOrEmpty(ServerSettings.MySQL_Password)
                                           || !string.IsNullOrEmpty(ServerSettings.MySQL_Hostname) ||
                                           !string.IsNullOrEmpty(ServerSettings.MySQL_Username);

                    if (anySettingsMySQL)
                    {
                        txtMySQL_DatabaseName.Text = ServerSettings.MySQL_SchemaName;
                        txtMySQL_Password.Password = ServerSettings.MySQL_Password;
                        txtMySQL_ServerAddress.Text = ServerSettings.MySQL_Hostname;
                        txtMySQL_Username.Text = ServerSettings.MySQL_Username;
                    }
                    else
                    {
                        txtMySQL_DatabaseName.Text = "JMMServer";
                        txtMySQL_Password.Password = "";
                        txtMySQL_ServerAddress.Text = "localhost";
                        txtMySQL_Username.Text = "root";
                    }

                    ServerState.Instance.DatabaseIsMySQL = true;
                    break;
            }
        }

        private void workerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var setupComplete = bool.Parse(e.Result.ToString());
            if (setupComplete)
            {
                ServerInfo.Instance.RefreshImportFolders();

                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_Complete;
                ServerState.Instance.ServerOnline = true;

                tabControl1.SelectedIndex = 0;
            }
            else
            {
                ServerState.Instance.ServerOnline = false;
                if (string.IsNullOrEmpty(ServerSettings.DatabaseType))
                {
                    ServerSettings.DatabaseType = "SQLite";
                    ShowDatabaseSetup();
                }
            }

            btnSaveDatabaseSettings.IsEnabled = true;
            cboDatabaseType.IsEnabled = true;
            btnRefreshMSSQLServerList.IsEnabled = true;

            if (setupComplete)
            {
                if (string.IsNullOrEmpty(ServerSettings.AniDB_Username) ||
                    string.IsNullOrEmpty(ServerSettings.AniDB_Password))
                {
                    var frm = new InitialSetupForm();
                    frm.Owner = this;
                    frm.ShowDialog();
                }

                var repFolders = new ImportFolderRepository();
                var folders = repFolders.GetAll();
                if (folders.Count == 0)
                {
                    tabControl1.SelectedIndex = 1;
                }
            }
        }

        private void btnRefreshMSSQLServerList_Click(object sender, RoutedEventArgs e)
        {
            btnSaveDatabaseSettings.IsEnabled = false;
            cboDatabaseType.IsEnabled = false;
            btnRefreshMSSQLServerList.IsEnabled = false;

            try
            {
                Cursor = Cursors.Wait;
                cboMSSQLServerList.Items.Clear();
                var dt = SmoApplication.EnumAvailableSqlServers();
                foreach (DataRow dr in dt.Rows)
                {
                    cboMSSQLServerList.Items.Add(dr[0]);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                MessageBox.Show(ex.Message, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Cursor = Cursors.Arrow;
            btnSaveDatabaseSettings.IsEnabled = true;
            cboDatabaseType.IsEnabled = true;
            btnRefreshMSSQLServerList.IsEnabled = true;
        }

        private void ShowDatabaseSetup()
        {
            if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE") cboDatabaseType.SelectedIndex = 0;
            if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER") cboDatabaseType.SelectedIndex = 1;
            if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL") cboDatabaseType.SelectedIndex = 2;
        }

        private void workerSetupDB_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            try
            {
                ServerState.Instance.ServerOnline = false;
                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_Cleaning;

                StopWatchingFiles();
                AniDBDispose();
                StopHost();

                JMMService.CmdProcessorGeneral.Stop();
                JMMService.CmdProcessorHasher.Stop();
                JMMService.CmdProcessorImages.Stop();


                // wait until the queue count is 0
                // ie the cancel has actuall worked
                while (true)
                {
                    if (JMMService.CmdProcessorGeneral.QueueCount == 0 && JMMService.CmdProcessorHasher.QueueCount == 0 &&
                        JMMService.CmdProcessorImages.QueueCount == 0) break;
                    Thread.Sleep(250);
                }

                if (autoUpdateTimer != null) autoUpdateTimer.Enabled = false;
                if (autoUpdateTimerShort != null) autoUpdateTimerShort.Enabled = false;

                JMMService.CloseSessionFactory();

                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_Initializing;
                Thread.Sleep(1000);

                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_DatabaseSetup;
                logger.Info("Setting up database...");
                if (!DatabaseHelper.InitDB())
                {
                    ServerState.Instance.DatabaseAvailable = false;

                    if (string.IsNullOrEmpty(ServerSettings.DatabaseType))
                        ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_DatabaseConfig;
                    else
                        ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_DatabaseFail;
                    e.Result = false;
                    return;
                }
                ServerState.Instance.DatabaseAvailable = true;
                logger.Info("Initializing Session Factory...");


                //init session factory
                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_InitializingSession;
                var temp = JMMService.SessionFactory;

                logger.Info("Initializing Hosts...");
                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_InitializingHosts;
                SetupAniDBProcessor();
                StartImageHost();
                StartBinaryHost();
                StartMetroHost();
                StartImageHostMetro();
                StartPlexHost();
                StartKodiHost();
                StartFileHost();
                StartRESTHost();
                StartStreamingHost();

                //  Load all stats
                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_InitializingStats;
                StatsCache.Instance.InitStats();

                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_InitializingQueue;
                JMMService.CmdProcessorGeneral.Init();
                JMMService.CmdProcessorHasher.Init();
                JMMService.CmdProcessorImages.Init();

                // timer for automatic updates
                autoUpdateTimer = new Timer();
                autoUpdateTimer.AutoReset = true;
                autoUpdateTimer.Interval = 5 * 60 * 1000; // 5 * 60 seconds (5 minutes)
                autoUpdateTimer.Elapsed += autoUpdateTimer_Elapsed;
                autoUpdateTimer.Start();

                // timer for automatic updates
                autoUpdateTimerShort = new Timer();
                autoUpdateTimerShort.AutoReset = true;
                autoUpdateTimerShort.Interval = 5 * 1000; // 5 seconds, later we set it to 30 seconds
                autoUpdateTimerShort.Elapsed += autoUpdateTimerShort_Elapsed;
                autoUpdateTimerShort.Start();

                ServerState.Instance.CurrentSetupStatus = Properties.Resources.Server_InitializingFile;
                StartWatchingFiles();

                DownloadAllImages();

                var repFolders = new ImportFolderRepository();
                var folders = repFolders.GetAll();

                if (ServerSettings.ScanDropFoldersOnStart) ScanDropFolders();
                if (ServerSettings.RunImportOnStart && folders.Count > 0) RunImport();

                ServerState.Instance.ServerOnline = true;
                e.Result = true;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                ServerState.Instance.CurrentSetupStatus = ex.Message;
                e.Result = false;
            }
        }

        #endregion

        #region Update all media info

        private void btnUpdateMediaInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllMediaInfo();
            MessageBox.Show(Properties.Resources.Serrver_VideoMediaUpdate, Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void workerMediaInfo_DoWork(object sender, DoWorkEventArgs e)
        {
            var repVidLocals = new VideoLocalRepository();

            // first build a list of files that we already know about, as we don't want to process them again
            var filesAll = repVidLocals.GetAll();
            var dictFilesExisting = new Dictionary<string, VideoLocal>();
            foreach (var vl in filesAll)
            {
                var cr = new CommandRequest_ReadMediaInfo(vl.VideoLocalID);
                cr.Save();
            }
        }

        public static void RefreshAllMediaInfo()
        {
            if (workerMediaInfo.IsBusy) return;
            workerMediaInfo.RunWorkerAsync();
        }

        #endregion

        #region MyAnime2 Migration

        private void workerMyAnime2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var ma2Progress = e.UserState as MA2Progress;
            if (!string.IsNullOrEmpty(ma2Progress.ErrorMessage))
            {
                txtMA2Progress.Text = ma2Progress.ErrorMessage;
                txtMA2Success.Visibility = Visibility.Hidden;
                return;
            }

            if (ma2Progress.CurrentFile <= ma2Progress.TotalFiles)
                txtMA2Progress.Text = string.Format("Processing unlinked file {0} of {1}", ma2Progress.CurrentFile,
                    ma2Progress.TotalFiles);
            else
                txtMA2Progress.Text = string.Format("Processed all unlinked files ({0})", ma2Progress.TotalFiles);
            txtMA2Success.Text = string.Format("{0} files sucessfully migrated", ma2Progress.MigratedFiles);
        }

        private void workerMyAnime2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        private void workerMyAnime2_DoWork(object sender, DoWorkEventArgs e)
        {
            var ma2Progress = new MA2Progress();
            ma2Progress.CurrentFile = 0;
            ma2Progress.ErrorMessage = "";
            ma2Progress.MigratedFiles = 0;
            ma2Progress.TotalFiles = 0;

            try
            {
                var databasePath = e.Argument as string;

                var connString = string.Format(@"data source={0};useutf16encoding=True", databasePath);
                var myConn = new SQLiteConnection(connString);
                myConn.Open();

                // get a list of unlinked files
                var repVids = new VideoLocalRepository();
                var repAniEps = new AniDB_EpisodeRepository();
                var repAniAnime = new AniDB_AnimeRepository();
                var repSeries = new AnimeSeriesRepository();
                var repEps = new AnimeEpisodeRepository();

                var vids = repVids.GetVideosWithoutEpisode();
                ma2Progress.TotalFiles = vids.Count;

                foreach (var vid in vids)
                {
                    ma2Progress.CurrentFile = ma2Progress.CurrentFile + 1;
                    workerMyAnime2.ReportProgress(0, ma2Progress);

                    // search for this file in the XrossRef table in MA2
                    var sql =
                        string.Format(
                            "SELECT AniDB_EpisodeID from CrossRef_Episode_FileHash WHERE Hash = '{0}' AND FileSize = {1}",
                            vid.ED2KHash, vid.FileSize);
                    var sqCommand = new SQLiteCommand(sql);
                    sqCommand.Connection = myConn;

                    var myReader = sqCommand.ExecuteReader();
                    while (myReader.Read())
                    {
                        var episodeID = 0;
                        if (!int.TryParse(myReader.GetValue(0).ToString(), out episodeID)) continue;
                        if (episodeID <= 0) continue;

                        sql = string.Format("SELECT AnimeID from AniDB_Episode WHERE EpisodeID = {0}", episodeID);
                        sqCommand = new SQLiteCommand(sql);
                        sqCommand.Connection = myConn;

                        var myReader2 = sqCommand.ExecuteReader();
                        while (myReader2.Read())
                        {
                            var animeID = myReader2.GetInt32(0);

                            // so now we have all the needed details we can link the file to the episode
                            // as long as wehave the details in JMM
                            AniDB_Anime anime = null;
                            var ep = repAniEps.GetByEpisodeID(episodeID);
                            if (ep == null)
                            {
                                logger.Debug("Getting Anime record from AniDB....");
                                anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true,
                                    ServerSettings.AutoGroupSeries);
                            }
                            else
                                anime = repAniAnime.GetByAnimeID(animeID);

                            // create the group/series/episode records if needed
                            AnimeSeries ser = null;
                            if (anime == null) continue;

                            logger.Debug("Creating groups, series and episodes....");
                            // check if there is an AnimeSeries Record associated with this AnimeID
                            ser = repSeries.GetByAnimeID(animeID);
                            if (ser == null)
                            {
                                // create a new AnimeSeries record
                                ser = anime.CreateAnimeSeriesAndGroup();
                            }


                            ser.CreateAnimeEpisodes();

                            // check if we have any group status data for this associated anime
                            // if not we will download it now
                            var repStatus = new AniDB_GroupStatusRepository();
                            if (repStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                            {
                                var cmdStatus = new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                                cmdStatus.Save();
                            }

                            // update stats
                            ser.EpisodeAddedDate = DateTime.Now;
                            repSeries.Save(ser);

                            var repGroups = new AnimeGroupRepository();
                            foreach (var grp in ser.AllGroupsAbove)
                            {
                                grp.EpisodeAddedDate = DateTime.Now;
                                repGroups.Save(grp);
                            }


                            var epAnime = repEps.GetByAniDBEpisodeID(episodeID);
                            if (epAnime == null)
                                continue;

                            var repXRefs = new CrossRef_File_EpisodeRepository();
                            var xref = new CrossRef_File_Episode();

                            try
                            {
                                xref.PopulateManually(vid, epAnime);
                            }
                            catch (Exception ex)
                            {
                                var msg = string.Format("Error populating XREF: {0} - {1}", vid.ToStringDetailed(), ex);
                                throw;
                            }

                            repXRefs.Save(xref);

                            vid.RenameIfRequired();
                            vid.MoveFileIfRequired();

                            // update stats for groups and series
                            if (ser != null)
                            {
                                // update all the groups above this series in the heirarchy
                                ser.QueueUpdateStats();
                                //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                            }


                            // Add this file to the users list
                            if (ServerSettings.AniDB_MyList_AddFiles)
                            {
                                var cmd = new CommandRequest_AddFileToMyList(vid.ED2KHash);
                                cmd.Save();
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
                logger.ErrorException(ex.ToString(), ex);
                ma2Progress.ErrorMessage = ex.Message;
                workerMyAnime2.ReportProgress(0, ma2Progress);
            }
        }

        private void btnImportManualLinks_Click(object sender, RoutedEventArgs e)
        {
            if (workerMyAnime2.IsBusy)
            {
                MessageBox.Show(Properties.Resources.Server_Import, Properties.Resources.Error, MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            txtMA2Progress.Visibility = Visibility.Visible;
            txtMA2Success.Visibility = Visibility.Visible;

            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Sqlite Files (*.DB3)|*.db3";
            ofd.ShowDialog();
            if (!string.IsNullOrEmpty(ofd.FileName))
            {
                workerMyAnime2.RunWorkerAsync(ofd.FileName);
            }
        }

        private void ImportLinksFromMA2(string databasePath)
        {
        }

        #endregion

        #region UI events and methods

        private void CommandBinding_ScanFolder(object sender, ExecutedRoutedEventArgs e)
        {
            var obj = e.Parameter;
            if (obj == null) return;

            try
            {
                if (obj.GetType() == typeof(ImportFolder))
                {
                    var fldr = (ImportFolder)obj;

                    ScanFolder(fldr.ImportFolderID);
                    MessageBox.Show(Properties.Resources.Server_ScanFolder, Properties.Resources.Success,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private void btnUpdateAniDBInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Cursor = Cursors.Wait;
                Importer.RunImport_UpdateAllAniDB();
                Cursor = Cursors.Arrow;
                MessageBox.Show(Properties.Resources.Server_AniDBInfoUpdate, Properties.Resources.Success,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        private void btnUpdateTvDBInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Cursor = Cursors.Wait;
                Importer.RunImport_UpdateTvDB(false);
                Cursor = Cursors.Arrow;
                MessageBox.Show(Properties.Resources.Server_TvDBInfoUpdate, Properties.Resources.Success,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        private void btnUpdateAllStats_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            Importer.UpdateAllStats();
            Cursor = Cursors.Arrow;
            MessageBox.Show(Properties.Resources.Server_StatsInfoUpdate, Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnSyncVotes_Click(object sender, RoutedEventArgs e)
        {
            var cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
            MessageBox.Show(Properties.Resources.Server_SyncVotes, Properties.Resources.Success, MessageBoxButton.OK,
                MessageBoxImage.Information);
            //JMMService.AnidbProcessor.IsBanned = true;
        }

        private void btnSyncMyList_Click(object sender, RoutedEventArgs e)
        {
            SyncMyList();
            MessageBox.Show(Properties.Resources.Server_SyncMyList, Properties.Resources.Success, MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void btnSyncTrakt_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                var cmd = new CommandRequest_TraktSyncCollection(true);
                cmd.Save();
            }
            Cursor = Cursors.Arrow;
            MessageBox.Show(Properties.Resources.Server_SyncTrakt, Properties.Resources.Success, MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void btnRunImport_Click(object sender, RoutedEventArgs e)
        {
            RunImport();
            MessageBox.Show(Properties.Resources.Server_ImportRunning, Properties.Resources.Success, MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void btnRemoveMissingFiles_Click(object sender, RoutedEventArgs e)
        {
            RemoveMissingFiles();
            MessageBox.Show(Properties.Resources.Server_SyncMyList, Properties.Resources.Success, MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void btnGeneralResume_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorGeneral.Paused = false;
        }

        private void btnGeneralPause_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorGeneral.Paused = true;
        }

        private void btnHasherResume_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorHasher.Paused = false;
        }

        private void btnHasherPause_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorHasher.Paused = true;
        }

        private void btnImagesResume_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorImages.Paused = false;
        }

        private void btnImagesPause_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorImages.Paused = true;
        }

        private void btnToolbarShutdown_Click(object sender, RoutedEventArgs e)
        {
            isAppExiting = true;
            Close();
            TippuTrayNotify.Visible = false;
            TippuTrayNotify.Dispose();
        }

        #endregion

        #region Tray Minimize

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized) Hide();
        }

        private void TippuTrayNotify_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
        }

        private void CreateMenus()
        {
            //Create a object for the context menu
            ctxTrayMenu = new ContextMenuStrip();

            //Add the Menu Item to the context menu
            var mnuShow = new ToolStripMenuItem();
            mnuShow.Text = Properties.Resources.Toolbar_Show;
            mnuShow.Click += mnuShow_Click;
            ctxTrayMenu.Items.Add(mnuShow);

            //Add the Menu Item to the context menu
            var mnuExit = new ToolStripMenuItem();
            mnuExit.Text = Properties.Resources.Toolbar_Shutdown;
            mnuExit.Click += mnuExit_Click;
            ctxTrayMenu.Items.Add(mnuExit);

            //Add the Context menu to the Notify Icon Object
            TippuTrayNotify.ContextMenuStrip = ctxTrayMenu;
        }

        private void mnuShow_Click(object sender, EventArgs e)
        {
            Show();
        }

        private void ShutDown()
        {
            StopWatchingFiles();
            AniDBDispose();
            StopHost();
        }

        private void MinimizeToTray()
        {
            Hide();
            TippuTrayNotify.BalloonTipIcon = ToolTipIcon.Info;
            TippuTrayNotify.BalloonTipTitle = Properties.Resources.JMMServer;
            TippuTrayNotify.BalloonTipText = Properties.Resources.Server_MinimizeInfo;
            //TippuTrayNotify.ShowBalloonTip(400);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            //When the application is closed, check wether the application is 
            //exiting from menu or forms close button
            if (!isAppExiting)
            {
                //if the forms close button is triggered, cancel the event and hide the form
                //then show the notification ballon tip
                e.Cancel = true;
                MinimizeToTray();
            }
            else
            {
                ShutDown();
            }
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
            isAppExiting = true;
            Close();
            TippuTrayNotify.Visible = false;
            TippuTrayNotify.Dispose();
        }

        #endregion

        #region Tests

        private static void ReviewsTest()
        {
            var cmd = new CommandRequest_GetReviews(7525, true);
            cmd.Save();

            //CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(7727, false);
            //cmd.Save();
        }

        private static void HashTest()
        {
            var fileName = @"C:\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";
            //string fileName = @"M:\[ Anime Test ]\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";

            var start = DateTime.Now;
            var hashes = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            var ts = DateTime.Now - start;

            var doubleED2k = ts.TotalMilliseconds;

            start = DateTime.Now;
            var hashes2 = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false);
            ts = DateTime.Now - start;

            var doubleCRC32 = ts.TotalMilliseconds;

            start = DateTime.Now;
            var hashes3 = Hasher.CalculateHashes(fileName, OnHashProgress, false, true, false);
            ts = DateTime.Now - start;

            var doubleMD5 = ts.TotalMilliseconds;

            start = DateTime.Now;
            var hashes4 = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, true);
            ts = DateTime.Now - start;

            var doubleSHA1 = ts.TotalMilliseconds;

            start = DateTime.Now;
            var hashes5 = Hasher.CalculateHashes(fileName, OnHashProgress, true, true, true);
            ts = DateTime.Now - start;

            var doubleAll = ts.TotalMilliseconds;

            logger.Info("ED2K only took {0} ms --- {1}/{2}/{3}/{4}", doubleED2k, hashes.ed2k, hashes.crc32, hashes.md5,
                hashes.sha1);
            logger.Info("ED2K + CRCR32 took {0} ms --- {1}/{2}/{3}/{4}", doubleCRC32, hashes2.ed2k, hashes2.crc32,
                hashes2.md5, hashes2.sha1);
            logger.Info("ED2K + MD5 took {0} ms --- {1}/{2}/{3}/{4}", doubleMD5, hashes3.ed2k, hashes3.crc32,
                hashes3.md5, hashes3.sha1);
            logger.Info("ED2K + SHA1 took {0} ms --- {1}/{2}/{3}/{4}", doubleSHA1, hashes4.ed2k, hashes4.crc32,
                hashes4.md5, hashes4.sha1);
            logger.Info("Everything took {0} ms --- {1}/{2}/{3}/{4}", doubleAll, hashes5.ed2k, hashes5.crc32,
                hashes5.md5, hashes5.sha1);
        }

        private static void HashTest2()
        {
            var fileName = @"C:\Anime\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";
            var fi = new FileInfo(fileName);
            var fileSize1 = Utils.FormatByteSize(fi.Length);
            var start = DateTime.Now;
            var hashes = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            var ts = DateTime.Now - start;

            var doubleFile1 = ts.TotalMilliseconds;

            fileName = @"C:\Anime\[Coalgirls]_Bakemonogatari_01_(1280x720_Blu-Ray_FLAC)_[CA425D15].mkv";
            fi = new FileInfo(fileName);
            var fileSize2 = Utils.FormatByteSize(fi.Length);
            start = DateTime.Now;
            var hashes2 = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            ts = DateTime.Now - start;

            var doubleFile2 = ts.TotalMilliseconds;


            fileName = @"C:\Anime\Highschool_of_the_Dead_Ep01_Spring_of_the_Dead_[1080p,BluRay,x264]_-_gg-THORA.mkv";
            fi = new FileInfo(fileName);
            var fileSize3 = Utils.FormatByteSize(fi.Length);
            start = DateTime.Now;
            var hashes3 = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            ts = DateTime.Now - start;

            var doubleFile3 = ts.TotalMilliseconds;

            logger.Info("Hashed {0} in {1} ms --- {2}", fileSize1, doubleFile1, hashes.ed2k);
            logger.Info("Hashed {0} in {1} ms --- {2}", fileSize2, doubleFile2, hashes2.ed2k);
            logger.Info("Hashed {0} in {1} ms --- {2}", fileSize3, doubleFile3, hashes3.ed2k);
        }


        private static void UpdateStatsTest()
        {
            var repGroups = new AnimeGroupRepository();
            foreach (var grp in repGroups.GetAllTopLevelGroups())
            {
                grp.UpdateStatsFromTopLevel(true, true);
            }
        }


        private static void CreateImportFolders_Test()
        {
            logger.Debug("Creating import folders...");
            var repImportFolders = new ImportFolderRepository();

            var sn = repImportFolders.GetByImportLocation(@"M:\[ Anime Test ]");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderName = "Anime";
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderLocation = @"M:\[ Anime Test ]";
                repImportFolders.Save(sn);
            }

            logger.Debug("Complete!");
        }

        private static void ProcessFileTest()
        {
            //CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(@"M:\[ Anime Test ]\[HorribleSubs] Dragon Crisis! - 02 [720p].mkv", false);
            //CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(@"M:\[ Anime Test ]\[Doki] Saki - 01 (720x480 h264 DVD AAC) [DC73ACB9].mkv");
            //cr_hashfile.Save();

            var cr_procfile = new CommandRequest_ProcessFile(15350, false);
            cr_procfile.Save();
        }


        private static void CreateImportFolders()
        {
            logger.Debug("Creating shares...");
            var repNetShares = new ImportFolderRepository();

            var sn = repNetShares.GetByImportLocation(@"M:\[ Anime 2011 ]");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime 2011";
                sn.ImportFolderLocation = @"M:\[ Anime 2011 ]";
                repNetShares.Save(sn);
            }

            sn = repNetShares.GetByImportLocation(@"M:\[ Anime - DVD and Bluray IN PROGRESS ]");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime - DVD and Bluray IN PROGRESS";
                sn.ImportFolderLocation = @"M:\[ Anime - DVD and Bluray IN PROGRESS ]";
                repNetShares.Save(sn);
            }

            sn = repNetShares.GetByImportLocation(@"M:\[ Anime - DVD and Bluray COMPLETE ]");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime - DVD and Bluray COMPLETE";
                sn.ImportFolderLocation = @"M:\[ Anime - DVD and Bluray COMPLETE ]";
                repNetShares.Save(sn);
            }

            sn = repNetShares.GetByImportLocation(@"M:\[ Anime ]");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime";
                sn.ImportFolderLocation = @"M:\[ Anime ]";
                repNetShares.Save(sn);
            }

            logger.Debug("Creating shares complete!");
        }

        private static void CreateImportFolders2()
        {
            logger.Debug("Creating shares...");
            var repNetShares = new ImportFolderRepository();

            var sn = repNetShares.GetByImportLocation(@"F:\Anime1");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime1";
                sn.ImportFolderLocation = @"F:\Anime1";
                repNetShares.Save(sn);
            }

            sn = repNetShares.GetByImportLocation(@"H:\Anime2");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime2";
                sn.ImportFolderLocation = @"H:\Anime2";
                repNetShares.Save(sn);
            }

            sn = repNetShares.GetByImportLocation(@"G:\Anime3");
            if (sn == null)
            {
                sn = new ImportFolder();
                sn.ImportFolderType = (int)ImportFolderType.HDD;
                sn.ImportFolderName = "Anime3";
                sn.ImportFolderLocation = @"G:\Anime3";
                repNetShares.Save(sn);
            }

            logger.Debug("Creating shares complete!");
        }


        private static void CreateTestCommandRequests()
        {
            var cr_anime = new CommandRequest_GetAnimeHTTP(5415, false, true);
            cr_anime.Save();

            /*
            cr_anime = new CommandRequest_GetAnimeHTTP(7382); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(6239); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(69); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(6751); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(3168); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(4196); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(634); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(2002); cr_anime.Save();


            
            cr_anime = new CommandRequest_GetAnimeHTTP(1); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(2); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(3); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(4); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(5); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(6); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(7); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(8); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(9); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(10); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(11); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(12); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(13); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(14); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(15); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(16); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(17); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(18); cr_anime.Save();
            cr_anime = new CommandRequest_GetAnimeHTTP(19); cr_anime.Save();*/
        }

        #endregion
    }
}