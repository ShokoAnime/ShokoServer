using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;
using JMMServer.UI;
using JMMServer.WCFCompression;
using Microsoft.SqlServer.Management.Smo;
using NHibernate;
using NLog;

namespace JMMServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon TippuTrayNotify;
        private System.Windows.Forms.ContextMenuStrip ctxTrayMenu;
        private bool isAppExiting = false;
        private static bool doneFirstTrakTinfo = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static DateTime lastTraktInfoUpdate = DateTime.Now;
        private static DateTime lastVersionCheck = DateTime.Now;

        private static BlockingList<FileSystemEventArgs> queueFileEvents = new BlockingList<FileSystemEventArgs>();
        private static BackgroundWorker workerFileEvents = new BackgroundWorker();

        //private static Uri baseAddress = new Uri("http://localhost:8111/JMMServer");
        //private static string baseAddressImageString = @"http://localhost:{0}/JMMServerImage";
        private static string baseAddressStreamingString = @"http://localhost:{0}/JMMServerStreaming";
        private static string baseAddressStreamingStringMex = @"net.tcp://localhost:{0}/JMMServerStreaming/mex";
        private static string baseAddressBinaryString = @"http://localhost:{0}/JMMServerBinary";
        private static string baseAddressMetroString = @"http://localhost:{0}/JMMServerMetro";
        //private static string baseAddressMetroImageString = @"http://localhost:{0}/JMMServerMetroImage";
        //private static string baseAddressRESTString = @"http://localhost:{0}/JMMServerREST";
        //private static string baseAddressPlexString = @"http://localhost:{0}/JMMServerPlex";
        //private static string baseAddressKodiString = @"http://localhost:{0}/JMMServerKodi";

        public static string PathAddressREST = "JMMServerREST";
        public static string PathAddressPlex = "JMMServerPlex";
        public static string PathAddressKodi = "JMMServerKodi";

        //private static Uri baseAddressTCP = new Uri("net.tcp://localhost:8112/JMMServerTCP");
        //private static ServiceHost host = null;
        //private static ServiceHost hostTCP = null;
        //private static ServiceHost hostImage = null;
        private static ServiceHost hostStreaming = null;
        private static ServiceHost hostBinary = null;
        private static ServiceHost hostMetro = null;
        //private static ServiceHost hostMetroImage = null;
        //private static WebServiceHost hostREST = null;
        //private static WebServiceHost hostPlex = null;
        //private static WebServiceHost hostKodi = null;
        private static Nancy.Hosting.Self.NancyHost hostNancy = null;
        //private static MessagingServer hostFile = null;
        private static FileServer.FileServer hostFile = null;

        private static BackgroundWorker workerImport = new BackgroundWorker();
        private static BackgroundWorker workerScanFolder = new BackgroundWorker();
        private static BackgroundWorker workerScanDropFolders = new BackgroundWorker();
        private static BackgroundWorker workerRemoveMissing = new BackgroundWorker();
        private static BackgroundWorker workerDeleteImportFolder = new BackgroundWorker();
        private static BackgroundWorker workerMyAnime2 = new BackgroundWorker();
        private static BackgroundWorker workerMediaInfo = new BackgroundWorker();

        private static BackgroundWorker workerSyncHashes= new BackgroundWorker();


        internal static BackgroundWorker workerSetupDB = new BackgroundWorker();

        private static System.Timers.Timer autoUpdateTimer = null;
        private static System.Timers.Timer autoUpdateTimerShort = null;
        DateTime lastAdminMessage = DateTime.Now.Subtract(new TimeSpan(12, 0, 0));
        private static List<FileSystemWatcher> watcherVids = null;

        BackgroundWorker downloadImagesWorker = new BackgroundWorker();

        public static List<UserCulture> userLanguages = new List<UserCulture>();

        public static Uri baseAddressBinary
        {
            get { return new Uri(string.Format(baseAddressBinaryString, ServerSettings.JMMServerPort)); }
        }

        //public static Uri baseAddressImage
        //{
        //    get { return new Uri(string.Format(baseAddressImageString, ServerSettings.JMMServerPort)); }
        //}

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

        //public static Uri baseAddressMetroImage
        //{
        //    get { return new Uri(string.Format(baseAddressMetroImageString, ServerSettings.JMMServerPort)); }
        //}

        private Mutex mutex;
        private readonly string mutexName = "JmmServer3.0Mutex";

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
                    MessageBox.Show(JMMServer.Properties.Resources.Server_Running,
                        JMMServer.Properties.Resources.JMMServer, MessageBoxButton.OK, MessageBoxImage.Error);
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
            workerFileEvents.DoWork += new DoWorkEventHandler(workerFileEvents_DoWork);
            workerFileEvents.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(workerFileEvents_RunWorkerCompleted);


            //Create an instance of the NotifyIcon Class
            TippuTrayNotify = new System.Windows.Forms.NotifyIcon();

            // This icon file needs to be in the bin folder of the application
            TippuTrayNotify = new System.Windows.Forms.NotifyIcon();
            Stream iconStream =
                Application.GetResourceStream(new Uri("pack://application:,,,/JMMServer;component/db.ico")).Stream;
            TippuTrayNotify.Icon = new System.Drawing.Icon(iconStream);
            iconStream.Dispose();

            //show the Tray Notify IconbtnRemoveMissingFiles.Click
            TippuTrayNotify.Visible = true;


            CreateMenus();

            ServerState.Instance.DatabaseAvailable = false;
            ServerState.Instance.ServerOnline = false;
            ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();

            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);
            this.StateChanged += new EventHandler(MainWindow_StateChanged);
            TippuTrayNotify.MouseDoubleClick +=
                new System.Windows.Forms.MouseEventHandler(TippuTrayNotify_MouseDoubleClick);

            btnToolbarShutdown.Click += new RoutedEventHandler(btnToolbarShutdown_Click);
            btnHasherPause.Click += new RoutedEventHandler(btnHasherPause_Click);
            btnHasherResume.Click += new RoutedEventHandler(btnHasherResume_Click);
            btnGeneralPause.Click += new RoutedEventHandler(btnGeneralPause_Click);
            btnGeneralResume.Click += new RoutedEventHandler(btnGeneralResume_Click);
            btnImagesPause.Click += new RoutedEventHandler(btnImagesPause_Click);
            btnImagesResume.Click += new RoutedEventHandler(btnImagesResume_Click);
            btnAdminMessages.Click += btnAdminMessages_Click;

            btnRemoveMissingFiles.Click += new RoutedEventHandler(btnRemoveMissingFiles_Click);
            btnRunImport.Click += new RoutedEventHandler(btnRunImport_Click);
            btnSyncHashes.Click += BtnSyncHashes_Click;
            btnSyncMyList.Click += new RoutedEventHandler(btnSyncMyList_Click);
            btnSyncVotes.Click += new RoutedEventHandler(btnSyncVotes_Click);
            btnUpdateTvDBInfo.Click += new RoutedEventHandler(btnUpdateTvDBInfo_Click);
            btnUpdateAllStats.Click += new RoutedEventHandler(btnUpdateAllStats_Click);
            btnSyncTrakt.Click += new RoutedEventHandler(btnSyncTrakt_Click);
            btnImportManualLinks.Click += new RoutedEventHandler(btnImportManualLinks_Click);
            btnUpdateAniDBInfo.Click += new RoutedEventHandler(btnUpdateAniDBInfo_Click);
            btnUploadAzureCache.Click += new RoutedEventHandler(btnUploadAzureCache_Click);
            btnUpdateTraktInfo.Click += BtnUpdateTraktInfo_Click;

            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            downloadImagesWorker.DoWork += new DoWorkEventHandler(downloadImagesWorker_DoWork);
            downloadImagesWorker.WorkerSupportsCancellation = true;

            txtServerPort.Text = ServerSettings.JMMServerPort;
            chkEnableKodi.IsChecked = ServerSettings.EnableKodi;
            chkEnablePlex.IsChecked = ServerSettings.EnablePlex;


            btnToolbarHelp.Click += new RoutedEventHandler(btnToolbarHelp_Click);
            btnApplyServerPort.Click += new RoutedEventHandler(btnApplyServerPort_Click);
            btnUpdateMediaInfo.Click += new RoutedEventHandler(btnUpdateMediaInfo_Click);

            workerMyAnime2.DoWork += new DoWorkEventHandler(workerMyAnime2_DoWork);
            workerMyAnime2.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerMyAnime2_RunWorkerCompleted);
            workerMyAnime2.ProgressChanged += new ProgressChangedEventHandler(workerMyAnime2_ProgressChanged);
            workerMyAnime2.WorkerReportsProgress = true;

            workerMediaInfo.DoWork += new DoWorkEventHandler(workerMediaInfo_DoWork);

            workerImport.WorkerReportsProgress = true;
            workerImport.WorkerSupportsCancellation = true;
            workerImport.DoWork += new DoWorkEventHandler(workerImport_DoWork);

            workerScanFolder.WorkerReportsProgress = true;
            workerScanFolder.WorkerSupportsCancellation = true;
            workerScanFolder.DoWork += new DoWorkEventHandler(workerScanFolder_DoWork);


            workerScanDropFolders.WorkerReportsProgress = true;
            workerScanDropFolders.WorkerSupportsCancellation = true;
            workerScanDropFolders.DoWork += new DoWorkEventHandler(workerScanDropFolders_DoWork);


            workerSyncHashes.WorkerReportsProgress = true;
            workerSyncHashes.WorkerSupportsCancellation = true;
            workerSyncHashes.DoWork += WorkerSyncHashes_DoWork;


            workerRemoveMissing.WorkerReportsProgress = true;
            workerRemoveMissing.WorkerSupportsCancellation = true;
            workerRemoveMissing.DoWork += new DoWorkEventHandler(workerRemoveMissing_DoWork);

            workerDeleteImportFolder.WorkerReportsProgress = false;
            workerDeleteImportFolder.WorkerSupportsCancellation = true;
            workerDeleteImportFolder.DoWork += new DoWorkEventHandler(workerDeleteImportFolder_DoWork);

            workerSetupDB.DoWork += new DoWorkEventHandler(workerSetupDB_DoWork);
            workerSetupDB.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerSetupDB_RunWorkerCompleted);

            //StartUp();

            cboDatabaseType.Items.Clear();
            cboDatabaseType.Items.Add("SQLite");
            cboDatabaseType.Items.Add("Microsoft SQL Server 2014");
            cboDatabaseType.Items.Add("MySQL");
            cboDatabaseType.SelectionChanged +=
                new System.Windows.Controls.SelectionChangedEventHandler(cboDatabaseType_SelectionChanged);

            cboImagesPath.Items.Clear();
            cboImagesPath.Items.Add(JMMServer.Properties.Resources.Images_Default);
            cboImagesPath.Items.Add(JMMServer.Properties.Resources.Images_Custom);
            cboImagesPath.SelectionChanged +=
                new System.Windows.Controls.SelectionChangedEventHandler(cboImagesPath_SelectionChanged);
            btnChooseImagesFolder.Click += new RoutedEventHandler(btnChooseImagesFolder_Click);

            if (ServerSettings.BaseImagesPathIsDefault)
                cboImagesPath.SelectedIndex = 0;
            else
                cboImagesPath.SelectedIndex = 1;

            btnSaveDatabaseSettings.Click += new RoutedEventHandler(btnSaveDatabaseSettings_Click);
            btnRefreshMSSQLServerList.Click += new RoutedEventHandler(btnRefreshMSSQLServerList_Click);
            // btnInstallMSSQLServer.Click += new RoutedEventHandler(btnInstallMSSQLServer_Click);
            btnMaxOnStartup.Click += new RoutedEventHandler(toggleMinimizeOnStartup);
            btnMinOnStartup.Click += new RoutedEventHandler(toggleMinimizeOnStartup);
            btnLogs.Click += new RoutedEventHandler(btnLogs_Click);
            btnChooseVLCLocation.Click += new RoutedEventHandler(btnChooseVLCLocation_Click);
            btnJMMStartWithWindows.Click += new RoutedEventHandler(btnJMMStartWithWindows_Click);
            btnUpdateAniDBLogin.Click += new RoutedEventHandler(btnUpdateAniDBLogin_Click);


            btnAllowMultipleInstances.Click += new RoutedEventHandler(toggleAllowMultipleInstances);
            btnDisallowMultipleInstances.Click += new RoutedEventHandler(toggleAllowMultipleInstances);

            btnHasherClear.Click += new RoutedEventHandler(btnHasherClear_Click);
            btnGeneralClear.Click += new RoutedEventHandler(btnGeneralClear_Click);
            btnImagesClear.Click += new RoutedEventHandler(btnImagesClear_Click);

            chkEnableKodi.Click += ChkEnableKodi_Click;
            chkEnablePlex.Click += ChkEnablePlex_Click;

            //automaticUpdater.MenuItem = mnuCheckForUpdates;

            ServerState.Instance.LoadSettings();


            cboLanguages.SelectionChanged += new SelectionChangedEventHandler(cboLanguages_SelectionChanged);

            InitCulture();
        }

        private void BtnSyncHashes_Click(object sender, RoutedEventArgs e)
        {
            SyncHashes();
            MessageBox.Show(JMMServer.Properties.Resources.Server_SyncHashesRunning, JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void WorkerSyncHashes_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Importer.SyncHashes(); 
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
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
            this.Cursor = Cursors.Wait;
            TraktTVHelper.UpdateAllInfo();
            this.Cursor = Cursors.Arrow;
            MessageBox.Show(JMMServer.Properties.Resources.Command_UpdateTrakt,
            JMMServer.Properties.Resources.Success,
            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void workerFileEvents_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            logger.Info("Stopped thread for processing file creation events");
        }

        void workerFileEvents_DoWork(object sender, DoWorkEventArgs e)
        {
            logger.Info("Started thread for processing file creation events");
            foreach (FileSystemEventArgs evt in queueFileEvents)
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
                            string[] files = Directory.GetFiles(evt.FullPath, "*.*", SearchOption.AllDirectories);

                            foreach (string file in files)
                            {
                                if (FileHashHelper.IsVideo(file))
                                {
                                    logger.Info("Found file {0} under folder {1}", file, evt.FullPath);

                                    CommandRequest_HashFile cmd = new CommandRequest_HashFile(file, false);
                                    cmd.Save();
                                }
                            }
                        }
                        else if (FileHashHelper.IsVideo(evt.FullPath))
                        {
                            CommandRequest_HashFile cmd = new CommandRequest_HashFile(evt.FullPath, false);
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

        void btnUploadAzureCache_Click(object sender, RoutedEventArgs e)
        {
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            List<AniDB_Anime> allAnime = repAnime.GetAll();
            int cnt = 0;
            foreach (AniDB_Anime anime in allAnime)
            {
                cnt++;
                logger.Info(string.Format("Uploading anime {0} of {1} - {2}", cnt, allAnime.Count, anime.MainTitle));

                try
                {
                    CommandRequest_Azure_SendAnimeFull cmdAzure = new CommandRequest_Azure_SendAnimeFull(anime.AnimeID);
                    cmdAzure.Save();
                }
                catch
                {
                }
            }
        }

        void btnImagesClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                JMMService.CmdProcessorImages.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                CommandRequestRepository repCR = new CommandRequestRepository();
                foreach (CommandRequest cr in repCR.GetAllCommandRequestImages())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorImages.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            this.Cursor = Cursors.Arrow;
        }

        void btnGeneralClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                JMMService.CmdProcessorGeneral.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                CommandRequestRepository repCR = new CommandRequestRepository();
                foreach (CommandRequest cr in repCR.GetAllCommandRequestGeneral())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorGeneral.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            this.Cursor = Cursors.Arrow;
        }

        void btnHasherClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                JMMService.CmdProcessorHasher.Stop();

                // wait until the queue stops
                while (JMMService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                CommandRequestRepository repCR = new CommandRequestRepository();
                foreach (CommandRequest cr in repCR.GetAllCommandRequestHasher())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorHasher.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            this.Cursor = Cursors.Arrow;
        }


        void toggleAllowMultipleInstances(object sender, RoutedEventArgs e)
        {
            ServerSettings.AllowMultipleInstances = !ServerSettings.AllowMultipleInstances;
            ServerState.Instance.AllowMultipleInstances = !ServerState.Instance.AllowMultipleInstances;
            ServerState.Instance.DisallowMultipleInstances = !ServerState.Instance.DisallowMultipleInstances;
        }


        void btnAdminMessages_Click(object sender, RoutedEventArgs e)
        {
            AdminMessagesForm frm = new AdminMessagesForm();
            frm.Owner = this;
            frm.ShowDialog();
        }


        void btnLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appPath =
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string logPath = Path.Combine(appPath, "logs");

                Process.Start(new ProcessStartInfo(logPath));
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        void toggleMinimizeOnStartup(object sender, RoutedEventArgs e)
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

        void btnJMMStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(
                "http://jmediamanager.org/jmm-server/configuring-jmm-server/#jmm-start-with-windows");
        }

        void btnUpdateAniDBLogin_Click(object sender, RoutedEventArgs e)
        {
            InitialSetupForm frm = new InitialSetupForm();
            frm.Owner = this;
            frm.ShowDialog();
        }

        void cboLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetCulture();
        }

        void InitCulture()
        {
            try
            {
                string currentCulture = ServerSettings.Culture;

                cboLanguages.ItemsSource = UserCulture.SupportedLanguages;

                for (int i = 0; i < cboLanguages.Items.Count; i++)
                {
                    UserCulture ul = cboLanguages.Items[i] as UserCulture;
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
            UserCulture ul = cboLanguages.SelectedItem as UserCulture;
            bool isLanguageChanged = ServerSettings.Culture != ul.Culture;
            System.Windows.Forms.DialogResult result;

            try
            {
                CultureInfo ci = new CultureInfo(ul.Culture);
                CultureInfo.DefaultThreadCurrentUICulture = ci;
                CultureManager.UICulture = ci;
                ServerSettings.Culture = ul.Culture;
                if (isLanguageChanged)
                {
                    result = System.Windows.Forms.MessageBox.Show(JMMServer.Properties.Resources.Language_Info, JMMServer.Properties.Resources.Language_Switch, System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Information);
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        System.Windows.Forms.Application.Restart();
                        System.Windows.Application.Current.Shutdown();
                    }
                }

            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }


        void btnChooseVLCLocation_Click(object sender, RoutedEventArgs e)
        {
            string errorMsg = "";
            string streamingAddress = "";

            Utils.StartStreamingVideo("localhost",
                @"e:\test\[Frostii]_K-On!_-_S5_(1280x720_Blu-ray_H264)_[8B9E0A76].mkv",
                "12000", "30", "1280",
                "128", "44100", "8088", ref errorMsg, ref streamingAddress);

            return;

            System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServerSettings.VLCLocation = dialog.FileName;
            }
        }

        void btnChooseImagesFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServerSettings.BaseImagesPath = dialog.SelectedPath;
            }
        }

        void cboImagesPath_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboImagesPath.SelectedIndex == 0)
            {
                ServerSettings.BaseImagesPathIsDefault = true;
                btnChooseImagesFolder.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                ServerSettings.BaseImagesPathIsDefault = false;
                btnChooseImagesFolder.Visibility = System.Windows.Visibility.Visible;
            }
        }

        #region Database settings and initial start up

        void btnSaveDatabaseSettings_Click(object sender, RoutedEventArgs e)
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
                        MessageBox.Show(JMMServer.Properties.Resources.Server_FillOutSettings,
                            JMMServer.Properties.Resources.Error,
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
                        MessageBox.Show(JMMServer.Properties.Resources.Server_FillOutSettings,
                            JMMServer.Properties.Resources.Error,
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
                MessageBox.Show(JMMServer.Properties.Resources.Server_FailedToStart + ex.Message,
                    JMMServer.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void cboDatabaseType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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

                    bool anySettingsMSSQL = !string.IsNullOrEmpty(ServerSettings.DatabaseName) ||
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

                    bool anySettingsMySQL = !string.IsNullOrEmpty(ServerSettings.MySQL_SchemaName) ||
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

        void workerSetupDB_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool setupComplete = bool.Parse(e.Result.ToString());
            if (setupComplete)
            {
                ServerInfo.Instance.RefreshImportFolders();

                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_Complete;
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
                    InitialSetupForm frm = new InitialSetupForm();
                    frm.Owner = this;
                    frm.ShowDialog();
                }

                ImportFolderRepository repFolders = new ImportFolderRepository();
                List<ImportFolder> folders = repFolders.GetAll();
                if (folders.Count == 0)
                {
                    tabControl1.SelectedIndex = 1;
                }
            }
        }

        void btnRefreshMSSQLServerList_Click(object sender, RoutedEventArgs e)
        {
            btnSaveDatabaseSettings.IsEnabled = false;
            cboDatabaseType.IsEnabled = false;
            btnRefreshMSSQLServerList.IsEnabled = false;

            try
            {
                this.Cursor = Cursors.Wait;
                cboMSSQLServerList.Items.Clear();
                DataTable dt = SmoApplication.EnumAvailableSqlServers();
                foreach (DataRow dr in dt.Rows)
                {
                    cboMSSQLServerList.Items.Add(dr[0]);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                MessageBox.Show(ex.Message, JMMServer.Properties.Resources.Error, MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            this.Cursor = Cursors.Arrow;
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

        public void StartFileWorker()
        {
            if (!workerFileEvents.IsBusy)
                workerFileEvents.RunWorkerAsync();
        }

        void workerSetupDB_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            try
            {
                ServerState.Instance.ServerOnline = false;
                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_Cleaning;

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

                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_Initializing;
                Thread.Sleep(1000);

                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_DatabaseSetup;
                logger.Info("Setting up database...");
                if (!DatabaseHelper.InitDB())
                {
                    ServerState.Instance.DatabaseAvailable = false;

                    if (string.IsNullOrEmpty(ServerSettings.DatabaseType))
                        ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_DatabaseConfig;
                    else
                        ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_DatabaseFail;
                    e.Result = false;
                    return;
                }
                else
                    ServerState.Instance.DatabaseAvailable = true;
                logger.Info("Initializing Session Factory...");


                //init session factory
                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_InitializingSession;
                ISessionFactory temp = JMMService.SessionFactory;


                logger.Info("Initializing Hosts...");
                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_InitializingHosts;
                SetupAniDBProcessor();
                //StartImageHost();
                StartBinaryHost();
                StartMetroHost();
                //StartImageHostMetro();
                StartFileHost();
                StartStreamingHost();
                StartNancyHost();


                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_InitializingQueue;
                JMMService.CmdProcessorGeneral.Init();
                JMMService.CmdProcessorHasher.Init();
                JMMService.CmdProcessorImages.Init();


                // timer for automatic updates
                autoUpdateTimer = new System.Timers.Timer();
                autoUpdateTimer.AutoReset = true;
                autoUpdateTimer.Interval = 5 * 60 * 1000; // 5 * 60 seconds (5 minutes)
                autoUpdateTimer.Elapsed += new System.Timers.ElapsedEventHandler(autoUpdateTimer_Elapsed);
                autoUpdateTimer.Start();

                // timer for automatic updates
                autoUpdateTimerShort = new System.Timers.Timer();
                autoUpdateTimerShort.AutoReset = true;
                autoUpdateTimerShort.Interval = 5 * 1000; // 5 seconds, later we set it to 30 seconds
                autoUpdateTimerShort.Elapsed += new System.Timers.ElapsedEventHandler(autoUpdateTimerShort_Elapsed);
                autoUpdateTimerShort.Start();

                ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Server_InitializingFile;

                StartFileWorker();

                StartWatchingFiles();

                DownloadAllImages();

                ImportFolderRepository repFolders = new ImportFolderRepository();
                List<ImportFolder> folders = repFolders.GetAll();

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

        void btnUpdateMediaInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllMediaInfo();
            MessageBox.Show(JMMServer.Properties.Resources.Serrver_VideoMediaUpdate,
                JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void workerMediaInfo_DoWork(object sender, DoWorkEventArgs e)
        {
            VideoLocalRepository repVidLocals = new VideoLocalRepository();

            // first build a list of files that we already know about, as we don't want to process them again
            List<VideoLocal> filesAll = repVidLocals.GetAll();
            Dictionary<string, VideoLocal> dictFilesExisting = new Dictionary<string, VideoLocal>();
            foreach (VideoLocal vl in filesAll)
            {
                CommandRequest_ReadMediaInfo cr = new CommandRequest_ReadMediaInfo(vl.VideoLocalID);
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

        void workerMyAnime2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            MA2Progress ma2Progress = e.UserState as MA2Progress;
            if (!string.IsNullOrEmpty(ma2Progress.ErrorMessage))
            {
                txtMA2Progress.Text = ma2Progress.ErrorMessage;
                txtMA2Success.Visibility = System.Windows.Visibility.Hidden;
                return;
            }

            if (ma2Progress.CurrentFile <= ma2Progress.TotalFiles)
                txtMA2Progress.Text = string.Format("Processing unlinked file {0} of {1}", ma2Progress.CurrentFile,
                    ma2Progress.TotalFiles);
            else
                txtMA2Progress.Text = string.Format("Processed all unlinked files ({0})", ma2Progress.TotalFiles);
            txtMA2Success.Text = string.Format("{0} files sucessfully migrated", ma2Progress.MigratedFiles);
        }

        void workerMyAnime2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }

        void workerMyAnime2_DoWork(object sender, DoWorkEventArgs e)
        {
            MA2Progress ma2Progress = new MA2Progress();
            ma2Progress.CurrentFile = 0;
            ma2Progress.ErrorMessage = "";
            ma2Progress.MigratedFiles = 0;
            ma2Progress.TotalFiles = 0;

            try
            {
                string databasePath = e.Argument as string;

                string connString = string.Format(@"data source={0};useutf16encoding=True", databasePath);
                SQLiteConnection myConn = new SQLiteConnection(connString);
                myConn.Open();

                // get a list of unlinked files
                VideoLocalRepository repVids = new VideoLocalRepository();
                AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                AniDB_AnimeRepository repAniAnime = new AniDB_AnimeRepository();
                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();

                List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
                ma2Progress.TotalFiles = vids.Count;

                foreach (VideoLocal vid in vids)
                {
                    ma2Progress.CurrentFile = ma2Progress.CurrentFile + 1;
                    workerMyAnime2.ReportProgress(0, ma2Progress);

                    // search for this file in the XrossRef table in MA2
                    string sql =
                        string.Format(
                            "SELECT AniDB_EpisodeID from CrossRef_Episode_FileHash WHERE Hash = '{0}' AND FileSize = {1}",
                            vid.ED2KHash, vid.FileSize);
                    SQLiteCommand sqCommand = new SQLiteCommand(sql);
                    sqCommand.Connection = myConn;

                    SQLiteDataReader myReader = sqCommand.ExecuteReader();
                    while (myReader.Read())
                    {
                        int episodeID = 0;
                        if (!int.TryParse(myReader.GetValue(0).ToString(), out episodeID)) continue;
                        if (episodeID <= 0) continue;

                        sql = string.Format("SELECT AnimeID from AniDB_Episode WHERE EpisodeID = {0}", episodeID);
                        sqCommand = new SQLiteCommand(sql);
                        sqCommand.Connection = myConn;

                        SQLiteDataReader myReader2 = sqCommand.ExecuteReader();
                        while (myReader2.Read())
                        {
                            int animeID = myReader2.GetInt32(0);

                            // so now we have all the needed details we can link the file to the episode
                            // as long as wehave the details in JMM
                            AniDB_Anime anime = null;
                            AniDB_Episode ep = repAniEps.GetByEpisodeID(episodeID);
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
                            AniDB_GroupStatusRepository repStatus = new AniDB_GroupStatusRepository();
                            if (repStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                            {
                                CommandRequest_GetReleaseGroupStatus cmdStatus =
                                    new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                                cmdStatus.Save();
                            }

                            // update stats
                            ser.EpisodeAddedDate = DateTime.Now;
                            repSeries.Save(ser, false, false);

                            AnimeGroupRepository repGroups = new AnimeGroupRepository();
                            foreach (AnimeGroup grp in ser.AllGroupsAbove)
                            {
                                grp.EpisodeAddedDate = DateTime.Now;
                                repGroups.Save(grp, false, false);
                            }


                            AnimeEpisode epAnime = repEps.GetByAniDBEpisodeID(episodeID);
                            CrossRef_File_EpisodeRepository repXRefs = new CrossRef_File_EpisodeRepository();
                            JMMServer.Entities.CrossRef_File_Episode xref =
                                new JMMServer.Entities.CrossRef_File_Episode();

                            try
                            {
                                xref.PopulateManually(vid, epAnime);
                            }
                            catch (Exception ex)
                            {
                                string msg = string.Format("Error populating XREF: {0} - {1}", vid.ToStringDetailed(),
                                    ex.ToString());
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
                                CommandRequest_AddFileToMyList cmd = new CommandRequest_AddFileToMyList(vid.ED2KHash);
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

        void btnImportManualLinks_Click(object sender, RoutedEventArgs e)
        {
            if (workerMyAnime2.IsBusy)
            {
                MessageBox.Show(JMMServer.Properties.Resources.Server_Import, JMMServer.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtMA2Progress.Visibility = System.Windows.Visibility.Visible;
            txtMA2Success.Visibility = System.Windows.Visibility.Visible;

            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
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

        void btnApplyServerPort_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtServerPort.Text))
            {
                MessageBox.Show(JMMServer.Properties.Resources.Server_EnterAnyValue,
                    JMMServer.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtServerPort.Focus();
                return;
            }

            int port = 0;
            int.TryParse(txtServerPort.Text, out port);
            if (port <= 0 || port > 65535)
            {
                MessageBox.Show(JMMServer.Properties.Resources.Server_EnterCertainValue,
                    JMMServer.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtServerPort.Focus();
                return;
            }

            try
            {
                this.Cursor = Cursors.Wait;

                JMMService.CmdProcessorGeneral.Paused = true;
                JMMService.CmdProcessorHasher.Paused = true;
                JMMService.CmdProcessorImages.Paused = true;

                StopHost();

                if (Utils.SetNetworkRequirements(port.ToString(), oldPort: ServerSettings.JMMServerPort))
                    ServerSettings.JMMServerPort = port.ToString();
                else
                    txtServerPort.Text = ServerSettings.JMMServerPort;

                StartBinaryHost();
                //StartImageHost();
                //StartImageHostMetro();
                StartFileHost();
                StartStreamingHost();
                StartNancyHost();

                JMMService.CmdProcessorGeneral.Paused = false;
                JMMService.CmdProcessorHasher.Paused = false;
                JMMService.CmdProcessorImages.Paused = false;

                this.Cursor = Cursors.Arrow;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                MessageBox.Show(ex.Message, JMMServer.Properties.Resources.Error, MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        void btnToolbarHelp_Click(object sender, RoutedEventArgs e)
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


            AboutForm frm = new AboutForm();
            frm.Owner = this;
            frm.ShowDialog();
        }

        private void GenerateAzureList()
        {
            // get a lst of anime's that we already have
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            List<AniDB_Anime> allAnime = repAnime.GetAll();
            Dictionary<int, int> localAnimeIDs = new Dictionary<int, int>();
            foreach (AniDB_Anime anime in allAnime)
            {
                localAnimeIDs[anime.AnimeID] = anime.AnimeID;
            }

            // loop through the list of valid anime id's and add the ones we don't have yet
            Dictionary<int, int> validAnimeIDs = new Dictionary<int, int>();

            string line;
            System.IO.StreamReader file =
                new System.IO.StreamReader(@"e:\animetitles.txt");
            while ((line = file.ReadLine()) != null)
            {
                string[] titlesArray = line.Split('|');

                try
                {
                    int aid = int.Parse(titlesArray[0]);
                    if (!localAnimeIDs.ContainsKey(aid))
                        validAnimeIDs[aid] = aid;
                }
                catch
                {
                }
            }

            file.Close();

            string aids = "";
            var shuffledList = validAnimeIDs.Values.OrderBy(a => Guid.NewGuid());
            int i = 0;
            foreach (int animeID in shuffledList)
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
            DateTime dt = DateTime.Now.AddYears(-2);
            AniDB_AnimeRepository rep = new AniDB_AnimeRepository();
            List<AniDB_Anime> allAnime = rep.GetAll();

            int sentAnime = 0;
            foreach (AniDB_Anime anime in rep.GetAll())
            {
                if (!anime.EndDate.HasValue) continue;

                if (anime.EndDate.Value > dt) continue;

                sentAnime++;
                CommandRequest_Azure_SendAnimeXML cmd = new CommandRequest_Azure_SendAnimeXML(anime.AnimeID);
                cmd.Save();
            }

            logger.Info(string.Format("Sent Anime XML to Cache: {0} out of {1}", sentAnime, allAnime.Count));
        }

        private void SendToAzure()
        {
            Dictionary<int, int> validAnimeIDs = new Dictionary<int, int>();

            string line;

            // Read the file and display it line by line.
            System.IO.StreamReader file =
                new System.IO.StreamReader(@"e:\animetitles.txt");
            while ((line = file.ReadLine()) != null)
            {
                string[] titlesArray = line.Split('|');

                try
                {
                    int aid = int.Parse(titlesArray[0]);
                    validAnimeIDs[aid] = aid;
                }
                catch
                {
                }
            }

            file.Close();

            string aids =
                "9516,6719,9606,8751,7453,6969,7821,7738,6694,6854,6101,8267,9398,9369,7395,7687,7345,8748,6350,6437,6408,7824,6334,8976,4651,7329,6433,8750,9498,8306,6919,8598,6355,6084,6775,8482,6089,7441,7541,7130,9013,6299,6983,7740,6329,6401,9459,8458,8800,7290,8859,6957,8503,6057,7758,7086,7943,8007,8349,6858,7776,7194,8807,6822,8058,7274,6818,9309,9488,7564,9593,8906,6155,7191,7267,7861,7109,9617,7954,7944,6359,7877,7701,7447,8736,7260,8492,9107,9578,6843,7190,9036,7614,6404,6018,8895,6234,6855,7041,7504,6847,6889,7092,8672,9452,9086,8770,4515,8103,8100,8122,9441,7025,8403,6335,9607,8559,7193,7273,7553,6242,7108,7052,6171,9634,7846,8471,7772,7557,9597,7827,6039,6712,7784,7830,8330,6902,6187,8431,8258,7956,7373,8083,8130,7535,8003,8237,7153,8170,7439,8094,9332,6539,6773,6812,7220,7703,7406,7670,7876,8497,8407,7299,9299,7583,7825,7556,6950,8127,7147,7747,9009,6044,6393,6864,7616,9567,8612,6705,7139,7070,6804,7901,8133,7817,6596,6553,8073,6718,8303,7782,8724,6972,8671,6907,8030,7030,7141,6878,8036,8231,7594,6813,7920,7841,7922,7095,6927,6754,6936,7427,7497,9251,7253,8140,9601,6735,7160,7538,6893,7203,7346,6797,6516,8500,8245,8440,7863,7467,7975,8808,6277,6481,6733,8790,7117,7063,6924,8293,6208,6882,6892";
            string[] aidArray = aids.Split(',');

            logger.Info(string.Format("Queueing {0} anime updates", aidArray.Length));
            int cnt = 0;
            foreach (string animeid in aidArray)
            {
                if (validAnimeIDs.ContainsKey(int.Parse(animeid)))
                {
                    CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(int.Parse(animeid), true, false);
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

        void downloadImagesWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Importer.RunImport_GetImages();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //ServerInfo.Instance.RefreshImportFolders();

            if (ServerSettings.MinimizeOnStartup) MinimizeToTray();

            tabControl1.SelectedIndex = 4; // setup

            if (ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase))
            {
                btnUploadAzureCache.Visibility = System.Windows.Visibility.Visible;
            }
            logger.Info("Clearing Cache...");

            Utils.ClearAutoUpdateCache();

            ShowDatabaseSetup();
            logger.Info("Initializing DB...");

            workerSetupDB.RunWorkerAsync();

            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
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
                    Providers.JMMAutoUpdates.JMMVersions verInfo =
                        Providers.JMMAutoUpdates.JMMAutoUpdatesHelper.GetLatestVersionInfo();
                    if (verInfo == null) return;

                    // get the user's version
                    System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
                    if (a == null)
                    {
                        logger.Error("Could not get current version");
                        return;
                    }
                    System.Reflection.AssemblyName an = a.GetName();

                    verNew = verInfo.versions.ServerVersionAbs;

                    verCurrent = an.Version.Revision * 100 +
                                 an.Version.Build * 100 * 100 +
                                 an.Version.Minor * 100 * 100 * 100 +
                                 an.Version.Major * 100 * 100 * 100 * 100;
                }

                if (forceShowForm || verNew > verCurrent)
                {
                    UpdateForm frm = new UpdateForm();
                    frm.Owner = this;
                    frm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        #region UI events and methods

        private void CommandBinding_ScanFolder(object sender, ExecutedRoutedEventArgs e)
        {
            object obj = e.Parameter;
            if (obj == null) return;

            try
            {
                if (obj.GetType() == typeof(ImportFolder))
                {
                    ImportFolder fldr = (ImportFolder)obj;

                    ScanFolder(fldr.ImportFolderID);
                    MessageBox.Show(JMMServer.Properties.Resources.Server_ScanFolder,
                        JMMServer.Properties.Resources.Success,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        void btnUpdateAniDBInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                Importer.RunImport_UpdateAllAniDB();
                this.Cursor = Cursors.Arrow;
                MessageBox.Show(JMMServer.Properties.Resources.Server_AniDBInfoUpdate,
                    JMMServer.Properties.Resources.Success,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        void btnUpdateTvDBInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                Importer.RunImport_UpdateTvDB(false);
                this.Cursor = Cursors.Arrow;
                MessageBox.Show(JMMServer.Properties.Resources.Server_TvDBInfoUpdate,
                    JMMServer.Properties.Resources.Success,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        void btnUpdateAllStats_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            Importer.UpdateAllStats();
            this.Cursor = Cursors.Arrow;
            MessageBox.Show(JMMServer.Properties.Resources.Server_StatsInfoUpdate,
                JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnSyncVotes_Click(object sender, RoutedEventArgs e)
        {
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
            MessageBox.Show(JMMServer.Properties.Resources.Server_SyncVotes, JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
            //JMMService.AnidbProcessor.IsBanned = true;
        }

        void btnSyncMyList_Click(object sender, RoutedEventArgs e)
        {
            SyncMyList();
            MessageBox.Show(JMMServer.Properties.Resources.Server_SyncMyList, JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnSyncTrakt_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(true);
                cmd.Save();
            }
            this.Cursor = Cursors.Arrow;
            MessageBox.Show(JMMServer.Properties.Resources.Server_SyncTrakt, JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnRunImport_Click(object sender, RoutedEventArgs e)
        {
            RunImport();
            MessageBox.Show(JMMServer.Properties.Resources.Server_ImportRunning, JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnRemoveMissingFiles_Click(object sender, RoutedEventArgs e)
        {
            RemoveMissingFiles();
            MessageBox.Show(JMMServer.Properties.Resources.Server_RemoveMissingFiles, JMMServer.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnGeneralResume_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorGeneral.Paused = false;
        }

        void btnGeneralPause_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorGeneral.Paused = true;
        }

        void btnHasherResume_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorHasher.Paused = false;
        }

        void btnHasherPause_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorHasher.Paused = true;
        }

        void btnImagesResume_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorImages.Paused = false;
        }

        void btnImagesPause_Click(object sender, RoutedEventArgs e)
        {
            JMMService.CmdProcessorImages.Paused = true;
        }

        void btnToolbarShutdown_Click(object sender, RoutedEventArgs e)
        {
            shutdownServer();
        }

        void shutdownServer()
        {
            isAppExiting = true;
            this.Close();
            TippuTrayNotify.Visible = false;
            TippuTrayNotify.Dispose();
        }

        void restartServer()
        {
            System.Windows.Forms.Application.Restart();
        }

        #endregion

        private void StartUp()
        {
        }


        void autoUpdateTimerShort_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
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
                TimeSpan lastUpdate = DateTime.Now - lastAdminMessage;

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

        #region Tray Minimize

        void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Minimized) this.Hide();
        }

        void TippuTrayNotify_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            this.Show();
        }

        private void CreateMenus()
        {
            //Create a object for the context menu
            ctxTrayMenu = new System.Windows.Forms.ContextMenuStrip();

            //Add the Menu Item to the context menu
            System.Windows.Forms.ToolStripMenuItem mnuShow = new System.Windows.Forms.ToolStripMenuItem();
            mnuShow.Text = JMMServer.Properties.Resources.Toolbar_Show;
            mnuShow.Click += new EventHandler(mnuShow_Click);
            ctxTrayMenu.Items.Add(mnuShow);

            //Add the Menu Item to the context menu
            System.Windows.Forms.ToolStripMenuItem mnuExit = new System.Windows.Forms.ToolStripMenuItem();
            mnuExit.Text = JMMServer.Properties.Resources.Toolbar_Shutdown;
            mnuExit.Click += new EventHandler(mnuExit_Click);
            ctxTrayMenu.Items.Add(mnuExit);

            //Add the Context menu to the Notify Icon Object
            TippuTrayNotify.ContextMenuStrip = ctxTrayMenu;
        }

        void mnuShow_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void ShutDown()
        {
            StopWatchingFiles();
            AniDBDispose();
            StopHost();
        }

        private void MinimizeToTray()
        {
            this.Hide();
            TippuTrayNotify.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            TippuTrayNotify.BalloonTipTitle = JMMServer.Properties.Resources.JMMServer;
            TippuTrayNotify.BalloonTipText = JMMServer.Properties.Resources.Server_MinimizeInfo;
            //TippuTrayNotify.ShowBalloonTip(400);
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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

        void mnuExit_Click(object sender, EventArgs e)
        {
            isAppExiting = true;
            this.Close();
            TippuTrayNotify.Visible = false;
            TippuTrayNotify.Dispose();
        }

        #endregion

        static void autoUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
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

            ImportFolderRepository repNetShares = new ImportFolderRepository();
            foreach (ImportFolder share in repNetShares.GetAll())
            {
                try
                {
                    if (Directory.Exists(share.ImportFolderLocation) && share.FolderIsWatched)
                    {
                        logger.Info("Watching ImportFolder: {0} || {1}", share.ImportFolderName,
                            share.ImportFolderLocation);
                        FileSystemWatcher fsw = new FileSystemWatcher(share.ImportFolderLocation);
                        fsw.IncludeSubdirectories = true;
                        fsw.Created += new FileSystemEventHandler(fsw_Created);
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

            foreach (FileSystemWatcher fsw in watcherVids)
            {
                fsw.EnableRaisingEvents = false;
            }
        }

        static void fsw_Created(object sender, FileSystemEventArgs e)
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
        public static void SyncHashes()
        {
            if (!workerSyncHashes.IsBusy)
                workerSyncHashes.RunWorkerAsync();
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

        static void workerRemoveMissing_DoWork(object sender, DoWorkEventArgs e)
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

        void workerDeleteImportFolder_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                int importFolderID = int.Parse(e.Argument.ToString());
                Importer.DeleteImportFolder(importFolderID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.Message, ex);
            }
        }

        static void workerScanFolder_DoWork(object sender, DoWorkEventArgs e)
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

        void workerScanDropFolders_DoWork(object sender, DoWorkEventArgs e)
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

        static void workerImport_DoWork(object sender, DoWorkEventArgs e)
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
            BinaryMessageEncodingBindingElement encoding = new BinaryMessageEncodingBindingElement();
            encoding.CompressionFormat = CompressionFormat.GZip;
            HttpTransportBindingElement transport = new HttpTransportBindingElement();
            Binding binding = new CustomBinding(encoding, transport);
            binding.Name = "BinaryBinding";
            binding.Namespace = "";


            //binding.MessageEncoding = WSMessageEncoding.Mtom;
            //binding.MaxReceivedMessageSize = 2147483647;


            // Create the ServiceHost.
            hostBinary = new ServiceHost(typeof(JMMServiceImplementation), baseAddressBinary);
            // Enable metadata publishing.
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
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

        //private static void StartImageHost()
        //{
        //    BasicHttpBinding binding = new BasicHttpBinding();
        //    binding.MessageEncoding = WSMessageEncoding.Mtom;
        //    binding.MaxReceivedMessageSize = 2147483647;
        //    binding.Name = "httpLargeMessageStream";


        //    // Create the ServiceHost.
        //    //hostImage = new ServiceHost(typeof(JMMServiceImplementationImage), baseAddressImage);
        //    // Enable metadata publishing.
        //    ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
        //    smb.HttpGetEnabled = true;
        //    smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
        //    hostImage.Description.Behaviors.Add(smb);

        //    //hostImage.AddServiceEndpoint(typeof(IJMMServerImage), binding, baseAddressImage);
        //    //hostImage.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,MetadataExchangeBindings.CreateMexHttpBinding(),      "mex");

        //    // Open the ServiceHost to start listening for messages. Since
        //    // no endpoints are explicitly configured, the runtime will create
        //    // one endpoint per base address for each service contract implemented
        //    // by the service.

        //    hostImage.Open();
        //    logger.Trace("Now Accepting client connections for images...");
        //}

        private static void StartStreamingHost_HTTP()
        {
            BasicHttpBinding binding = new BasicHttpBinding();
            binding.TransferMode = TransferMode.Streamed;
            binding.ReceiveTimeout = TimeSpan.MaxValue;
            binding.SendTimeout = TimeSpan.MaxValue;
            //binding.MessageEncoding = WSMessageEncoding.Mtom;
            binding.MaxReceivedMessageSize = Int32.MaxValue;
            binding.CloseTimeout = TimeSpan.MaxValue;
            binding.Name = "FileStreaming";

            binding.Security.Mode = BasicHttpSecurityMode.None;


            // Create the ServiceHost.
            hostStreaming = new ServiceHost(typeof(JMMServiceImplementationStreaming), baseAddressStreaming);
            // Enable metadata publishing.
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
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
            BinaryOverHTTPBinding binding = new BinaryOverHTTPBinding();

            // Create the ServiceHost.
            hostStreaming = new ServiceHost(typeof(JMMServiceImplementationStreaming), baseAddressStreaming);
            // Enable metadata publishing.
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
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
            NetTcpBinding netTCPbinding = new NetTcpBinding();
            netTCPbinding.TransferMode = TransferMode.Streamed;
            netTCPbinding.ReceiveTimeout = TimeSpan.MaxValue;
            netTCPbinding.SendTimeout = TimeSpan.MaxValue;
            netTCPbinding.MaxReceivedMessageSize = Int32.MaxValue;
            netTCPbinding.CloseTimeout = TimeSpan.MaxValue;

            netTCPbinding.Security.Mode = SecurityMode.Transport;
            netTCPbinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
            //netTCPbinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
            //netTCPbinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.None;
            //netTCPbinding.Security.Message.ClientCredentialType = MessageCredentialType.None;

            hostStreaming = new ServiceHost(typeof(JMMServiceImplementationStreaming));
            hostStreaming.AddServiceEndpoint(typeof(IJMMServerStreaming), netTCPbinding, baseAddressStreaming);
            hostStreaming.Description.Behaviors.Add(new ServiceMetadataBehavior());

            Binding mexBinding = MetadataExchangeBindings.CreateMexTcpBinding();
            hostStreaming.AddServiceEndpoint(typeof(IMetadataExchange), mexBinding, baseAddressStreamingMex);

            hostStreaming.Open();
            logger.Trace("Now Accepting client connections for streaming...");
        }

        //private static void StartImageHostMetro()
        //{
        //    BasicHttpBinding binding = new BasicHttpBinding();
        //    binding.MessageEncoding = WSMessageEncoding.Text;
        //    binding.MaxReceivedMessageSize = 2147483647;
        //    binding.Name = "httpLargeMessageStream";


        //    // Create the ServiceHost.
        //    hostMetroImage = new ServiceHost(typeof(JMMServiceImplementationImage), baseAddressMetroImage);
        //    // Enable metadata publishing.
        //    ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
        //    smb.HttpGetEnabled = true;
        //    smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
        //    hostMetroImage.Description.Behaviors.Add(smb);

        //    hostMetroImage.AddServiceEndpoint(typeof(IJMMServerImage), binding, baseAddressMetroImage);
        //    hostMetroImage.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
        //        MetadataExchangeBindings.CreateMexHttpBinding(), "mex");

        //    // Open the ServiceHost to start listening for messages. Since
        //    // no endpoints are explicitly configured, the runtime will create
        //    // one endpoint per base address for each service contract implemented
        //    // by the service.
        //    hostMetroImage.Open();
        //    logger.Trace("Now Accepting client connections for images (metro)...");
        //}


        private static void StartMetroHost()
        {
            BasicHttpBinding binding = new BasicHttpBinding();
            binding.MaxReceivedMessageSize = 2147483647;
            binding.Name = "metroTest";


            // Create the ServiceHost.
            hostMetro = new ServiceHost(typeof(JMMServiceImplementationMetro), baseAddressMetro);
            // Enable metadata publishing.
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.HttpGetUrl = baseAddressMetro;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;

            hostMetro.Description.Behaviors.Add(smb);

            hostMetro.AddServiceEndpoint(typeof(IJMMServerMetro), binding, baseAddressMetro);
            hostMetro.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName,
                MetadataExchangeBindings.CreateMexHttpBinding(),
                "mex");

            // Open the ServiceHost to start listening for messages. Since
            // no endpoints are explicitly configured, the runtime will create
            // one endpoint per base address for each service contract implemented
            // by the service.
            hostMetro.Open();
            logger.Trace("Now Accepting client connections for metro apps...");
        }


        private static void AddCompressableEndpoint(ServiceHost host, Type t, SerializationFilter filter, object address = null)
        {
            CustomBinding custom = new CustomBinding(new WebHttpBinding() { ContentTypeMapper = new MultiContentTypeMapper() });
            for (int i = 0; i < custom.Elements.Count; i++)
            {
                if (custom.Elements[i] is WebMessageEncodingBindingElement)
                {
                    WebMessageEncodingBindingElement webBE = (WebMessageEncodingBindingElement)custom.Elements[i];
                    custom.Elements[i] = new CompressedMessageEncodingBindingElement(webBE);
                }
                else if (custom.Elements[i] is TransportBindingElement)
                {
                    ((TransportBindingElement)custom.Elements[i]).MaxReceivedMessageSize = int.MaxValue;
                }
            }
            ServiceEndpoint ep = null;
            string addr = address as string;
            if (addr != null)
                ep = host.AddServiceEndpoint(t, custom, addr);
            Uri addrurl = address as Uri;
            if (addrurl != null)
                ep = host.AddServiceEndpoint(t, custom, addrurl);
            if (ep == null)
                ep = host.AddServiceEndpoint(t, custom, "");
            ep.EndpointBehaviors.Add(new MultiBehavior { HelpEnabled = true, AutomaticFormatSelectionEnabled = true });
            ep.EndpointBehaviors.Add(new CompressionSelectionEndpointBehavior(filter));
        }

        private static void StartNancyHost()
        {
            hostNancy = new Nancy.Hosting.Self.NancyHost(new Uri("http://localhost:"+ ServerSettings.JMMServerPort));
            hostNancy.Start();
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
 
        private static void ReadFiles()
        {
            // Steps for processing a file
            // 1. Check if it is a video file
            // 2. Check if we have a VideoLocal record for that file
            // .........

            // get a complete list of files
            List<string> fileList = new List<string>();
            ImportFolderRepository repNetShares = new ImportFolderRepository();
            foreach (ImportFolder share in repNetShares.GetAll())
            {
                logger.Debug("Import Folder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);

                Utils.GetFilesForImportFolder(share.ImportFolderLocation, ref fileList);
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

            if (hostBinary != null)
                hostBinary.Close();

            if (hostMetro != null)
                hostMetro.Close();

            //if (hostMetroImage != null)
            //    hostMetroImage.Close();

            if (hostStreaming != null)
                hostStreaming.Close();

            if (hostFile != null)
                hostFile.Stop();

            if (hostNancy != null)
                hostNancy.Stop();
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

        #region Tests

        private static void ReviewsTest()
        {
            CommandRequest_GetReviews cmd = new CommandRequest_GetReviews(7525, true);
            cmd.Save();

            //CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(7727, false);
            //cmd.Save();
        }

        private static void HashTest()
        {
            string fileName = @"C:\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";
            //string fileName = @"M:\[ Anime Test ]\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";

            DateTime start = DateTime.Now;
            Hashes hashes = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            TimeSpan ts = DateTime.Now - start;

            double doubleED2k = ts.TotalMilliseconds;

            start = DateTime.Now;
            Hashes hashes2 = Hasher.CalculateHashes(fileName, OnHashProgress, true, false, false);
            ts = DateTime.Now - start;

            double doubleCRC32 = ts.TotalMilliseconds;

            start = DateTime.Now;
            Hashes hashes3 = Hasher.CalculateHashes(fileName, OnHashProgress, false, true, false);
            ts = DateTime.Now - start;

            double doubleMD5 = ts.TotalMilliseconds;

            start = DateTime.Now;
            Hashes hashes4 = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, true);
            ts = DateTime.Now - start;

            double doubleSHA1 = ts.TotalMilliseconds;

            start = DateTime.Now;
            Hashes hashes5 = Hasher.CalculateHashes(fileName, OnHashProgress, true, true, true);
            ts = DateTime.Now - start;

            double doubleAll = ts.TotalMilliseconds;

            logger.Info("ED2K only took {0} ms --- {1}/{2}/{3}/{4}", doubleED2k, hashes.ed2k, hashes.crc32, hashes.md5,
                hashes.sha1);
            logger.Info("ED2K + CRCR32 took {0} ms --- {1}/{2}/{3}/{4}", doubleCRC32, hashes2.ed2k, hashes2.crc32,
                hashes2.md5,
                hashes2.sha1);
            logger.Info("ED2K + MD5 took {0} ms --- {1}/{2}/{3}/{4}", doubleMD5, hashes3.ed2k, hashes3.crc32,
                hashes3.md5,
                hashes3.sha1);
            logger.Info("ED2K + SHA1 took {0} ms --- {1}/{2}/{3}/{4}", doubleSHA1, hashes4.ed2k, hashes4.crc32,
                hashes4.md5,
                hashes4.sha1);
            logger.Info("Everything took {0} ms --- {1}/{2}/{3}/{4}", doubleAll, hashes5.ed2k, hashes5.crc32,
                hashes5.md5,
                hashes5.sha1);
        }

        private static void HashTest2()
        {
            string fileName = @"C:\Anime\Code_Geass_R2_Ep14_Geass_Hunt_[720p,BluRay,x264]_-_THORA.mkv";
            FileInfo fi = new FileInfo(fileName);
            string fileSize1 = Utils.FormatByteSize(fi.Length);
            DateTime start = DateTime.Now;
            Hashes hashes = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            TimeSpan ts = DateTime.Now - start;

            double doubleFile1 = ts.TotalMilliseconds;

            fileName = @"C:\Anime\[Coalgirls]_Bakemonogatari_01_(1280x720_Blu-Ray_FLAC)_[CA425D15].mkv";
            fi = new FileInfo(fileName);
            string fileSize2 = Utils.FormatByteSize(fi.Length);
            start = DateTime.Now;
            Hashes hashes2 = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            ts = DateTime.Now - start;

            double doubleFile2 = ts.TotalMilliseconds;


            fileName = @"C:\Anime\Highschool_of_the_Dead_Ep01_Spring_of_the_Dead_[1080p,BluRay,x264]_-_gg-THORA.mkv";
            fi = new FileInfo(fileName);
            string fileSize3 = Utils.FormatByteSize(fi.Length);
            start = DateTime.Now;
            Hashes hashes3 = Hasher.CalculateHashes(fileName, OnHashProgress, false, false, false);
            ts = DateTime.Now - start;

            double doubleFile3 = ts.TotalMilliseconds;

            logger.Info("Hashed {0} in {1} ms --- {2}", fileSize1, doubleFile1, hashes.ed2k);
            logger.Info("Hashed {0} in {1} ms --- {2}", fileSize2, doubleFile2, hashes2.ed2k);
            logger.Info("Hashed {0} in {1} ms --- {2}", fileSize3, doubleFile3, hashes3.ed2k);
        }


        private static void UpdateStatsTest()
        {
            AnimeGroupRepository repGroups = new AnimeGroupRepository();
            foreach (AnimeGroup grp in repGroups.GetAllTopLevelGroups())
            {
                grp.UpdateStatsFromTopLevel(true, true);
            }
        }


        private static void CreateImportFolders_Test()
        {
            logger.Debug("Creating import folders...");
            ImportFolderRepository repImportFolders = new ImportFolderRepository();

            ImportFolder sn = repImportFolders.GetByImportLocation(@"M:\[ Anime Test ]");
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

            CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(15350, false);
            cr_procfile.Save();
        }


        private static void CreateImportFolders()
        {
            logger.Debug("Creating shares...");
            ImportFolderRepository repNetShares = new ImportFolderRepository();

            ImportFolder sn = repNetShares.GetByImportLocation(@"M:\[ Anime 2011 ]");
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
            ImportFolderRepository repNetShares = new ImportFolderRepository();

            ImportFolder sn = repNetShares.GetByImportLocation(@"F:\Anime1");
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
            CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(5415, false, true);
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