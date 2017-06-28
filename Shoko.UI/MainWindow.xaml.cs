using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Infralution.Localization.Wpf;
using Microsoft.SqlServer.Management.Smo;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Azure;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.UI.Forms;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using Utils = Shoko.Server.Utils;

namespace Shoko.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon TippuTrayNotify;
        private System.Windows.Forms.ContextMenuStrip ctxTrayMenu;
        private bool isAppExiting = false;

        public static List<UserCulture> userLanguages = new List<UserCulture>();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MainWindow()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            if (!ShokoServer.Instance.StartUpServer())
            {
                MessageBox.Show(Commons.Properties.Resources.Server_Running,
                    Commons.Properties.Resources.ShokoServer, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }

            //Create an instance of the NotifyIcon Class
            TippuTrayNotify = new System.Windows.Forms.NotifyIcon();

            // This icon file needs to be in the bin folder of the application
            TippuTrayNotify = new System.Windows.Forms.NotifyIcon();
            Stream iconStream =
                Application.GetResourceStream(new Uri("pack://application:,,,/ShokoServer;component/db.ico")).Stream;
            TippuTrayNotify.Icon = new System.Drawing.Icon(iconStream);
            iconStream.Dispose();

            //show the Tray Notify IconbtnRemoveMissingFiles.Click
            TippuTrayNotify.Visible = true;

            //-- for winforms applications
            System.Windows.Forms.Application.ThreadException -= UnhandledExceptionManager.ThreadExceptionHandler;
            System.Windows.Forms.Application.ThreadException += UnhandledExceptionManager.ThreadExceptionHandler;

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
            btnSyncMedias.Click += BtnSyncMedias_Click;
            btnSyncMyList.Click += new RoutedEventHandler(btnSyncMyList_Click);
            btnSyncVotes.Click += new RoutedEventHandler(btnSyncVotes_Click);
            btnUpdateTvDBInfo.Click += new RoutedEventHandler(btnUpdateTvDBInfo_Click);
            btnUpdateAllStats.Click += new RoutedEventHandler(btnUpdateAllStats_Click);
            btnSyncTrakt.Click += new RoutedEventHandler(btnSyncTrakt_Click);
            btnImportManualLinks.Click += new RoutedEventHandler(btnImportManualLinks_Click);
            btnUpdateAniDBInfo.Click += new RoutedEventHandler(btnUpdateAniDBInfo_Click);
            btnLaunchWebUI.Click += new RoutedEventHandler(btnLaunchWebUI_Click);
            btnUpdateImages.Click += new RoutedEventHandler(btnUpdateImages_Click);
            btnUploadAzureCache.Click += new RoutedEventHandler(btnUploadAzureCache_Click);
            btnUpdateTraktInfo.Click += BtnUpdateTraktInfo_Click;
            btnSyncPlex.Click += BtnSyncPlexOn_Click;

            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);

            txtServerPort.Text = ServerSettings.JMMServerPort;

            btnToolbarHelp.Click += new RoutedEventHandler(btnToolbarHelp_Click);
            btnApplyServerPort.Click += new RoutedEventHandler(btnApplyServerPort_Click);
            btnUpdateMediaInfo.Click += new RoutedEventHandler(btnUpdateMediaInfo_Click);

            //StartUp();

            cboDatabaseType.Items.Clear();
            ShokoServer.Instance.GetSupportedDatabases().ForEach(s => cboDatabaseType.Items.Add(s));
            cboDatabaseType.SelectionChanged +=
                new System.Windows.Controls.SelectionChangedEventHandler(cboDatabaseType_SelectionChanged);

            btnChooseImagesFolder.Click += new RoutedEventHandler(btnChooseImagesFolder_Click);
            btnSetDefault.Click += BtnSetDefault_Click;


            btnSaveDatabaseSettings.Click += new RoutedEventHandler(btnSaveDatabaseSettings_Click);
            btnRefreshMSSQLServerList.Click += new RoutedEventHandler(btnRefreshMSSQLServerList_Click);
            // btnInstallMSSQLServer.Click += new RoutedEventHandler(btnInstallMSSQLServer_Click);
            btnMaxOnStartup.Click += new RoutedEventHandler(toggleMinimizeOnStartup);
            btnMinOnStartup.Click += new RoutedEventHandler(toggleMinimizeOnStartup);
            btnLogs.Click += new RoutedEventHandler(btnLogs_Click);
            btnChooseVLCLocation.Click += new RoutedEventHandler(btnChooseVLCLocation_Click);
            btnJMMEnableStartWithWindows.Click += new RoutedEventHandler(btnJMMEnableStartWithWindows_Click);
            btnJMMDisableStartWithWindows.Click += new RoutedEventHandler(btnJMMDisableStartWithWindows_Click);
            btnUpdateAniDBLogin.Click += new RoutedEventHandler(btnUpdateAniDBLogin_Click);

            btnHasherClear.Click += new RoutedEventHandler(btnHasherClear_Click);
            btnGeneralClear.Click += new RoutedEventHandler(btnGeneralClear_Click);
            btnImagesClear.Click += new RoutedEventHandler(btnImagesClear_Click);

            //automaticUpdater.MenuItem = mnuCheckForUpdates;

            ServerState.Instance.LoadSettings();

            cboLanguages.SelectionChanged += new SelectionChangedEventHandler(cboLanguages_SelectionChanged);

            InitCulture();
            Instance = this;

            if (!ServerSettings.FirstRun)
            {
                logger.Info("Already been set up... Initializing DB...");
                ShokoServer.RunWorkSetupDB();
            }

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            ServerSettings.YesNoRequired += (sender, args) =>
            {
                System.Windows.Forms.DialogResult dr =
                    System.Windows.Forms.MessageBox.Show(args.Reason, args.FormTitle,
                        MessageBoxButtons.YesNo);
                if (dr == System.Windows.Forms.DialogResult.No) args.Cancel = true;
            };
            ServerSettings.LocateFile += (sender, args) =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "JMM config|JMMServer.exe.config;settings.json";
                DialogResult browseFile = openFileDialog.ShowDialog();
                if (browseFile == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(openFileDialog.FileName.Trim()))
                {
                    args.FileName = openFileDialog.FileName;
                }
            };

            Utils.ErrorMessage +=
                (sender, args) => MessageBox.Show(this, args.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            AniDBHelper.LoginFailed += (a, e) => Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.InitialSetup_LoginFail,
                    Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                InitialSetupForm frm = new InitialSetupForm();
                frm.ShowDialog();
            });
            ShokoServer.Instance.LoginFormNeeded += (a, e) => Application.Current.Dispatcher.Invoke(() => new InitialSetupForm().ShowDialog());

            ServerSettings.MigrationStarted += (a, e) =>
            {
                // Display the migration form.
                var migrationForm = new MigrationForm();
                migrationForm.Show();
            };

            ShokoServer.Instance.DBSetupCompleted += DBSetupCompleted;
            ShokoServer.Instance.DatabaseSetup += (sender, args) => ShowDatabaseSetup();
        }

        private void BtnSyncPlexOn_Click(object sender, RoutedEventArgs routedEventArgs)
        {
            ShokoServer.Instance.SyncPlex();
        }

        private void BtnSetDefault_Click(object sender, RoutedEventArgs e)
        {
            string imagePath = ServerSettings.DefaultImagePath;
            if (!Directory.Exists(imagePath))
                Directory.CreateDirectory(imagePath);
            ServerSettings.ImagesPath = imagePath;
        }

        public static MainWindow Instance { get; private set; }

        private void BtnSyncHashes_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.SyncHashes();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_SyncHashesRunning,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSyncMedias_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.SyncMedias();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_SyncMediasRunning,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnUpdateTraktInfo_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            TraktTVHelper.UpdateAllInfo();
            this.Cursor = Cursors.Arrow;
            MessageBox.Show(Shoko.Commons.Properties.Resources.Command_UpdateTrakt,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnUploadAzureCache_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<SVR_AniDB_Anime> allAnime = RepoFactory.AniDB_Anime.GetAll();
            int cnt = 0;
            foreach (SVR_AniDB_Anime anime in allAnime)
            {
                cnt++;
                logger.Info($"Uploading anime {cnt} of {allAnime.Count} - {anime.MainTitle}");

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
                ShokoService.CmdProcessorImages.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestImages());

                ShokoService.CmdProcessorImages.Init();
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
                ShokoService.CmdProcessorGeneral.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestGeneral());

                ShokoService.CmdProcessorGeneral.Init();
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
                ShokoService.CmdProcessorHasher.Stop();

                // wait until the queue stops
                while (ShokoService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestHasher());

                ShokoService.CmdProcessorHasher.Init();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex.Message);
            }
            this.Cursor = Cursors.Arrow;
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
                string logPath = Path.Combine(ServerSettings.ApplicationPath, "logs");

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

        void btnJMMEnableStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.Instance.EnableStartWithWindows();
        }

        void btnJMMDisableStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.Instance.DisableStartWithWindows();
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
                    result = System.Windows.Forms.MessageBox.Show(Shoko.Commons.Properties.Resources.Language_Info,
                        Shoko.Commons.Properties.Resources.Language_Switch,
                        System.Windows.Forms.MessageBoxButtons.OKCancel,
                        System.Windows.Forms.MessageBoxIcon.Information);

                    if (result != System.Windows.Forms.DialogResult.OK) return;

                    System.Windows.Forms.Application.Restart();
                    ShokoServer.Instance.ApplicationShutdown();
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }


        void btnChooseVLCLocation_Click(object sender, RoutedEventArgs e)
        {
            /*string errorMsg = "";
            string streamingAddress = "";

            Utils.StartStreamingVideo("localhost",
                @"e:\test\[Frostii]_K-On!_-_S5_(1280x720_Blu-ray_H264)_[8B9E0A76].mkv",
                "12000", "30", "1280",
                "128", "44100", "8088", ref errorMsg, ref streamingAddress);

            return;*/

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
                ServerSettings.ImagesPath = dialog.SelectedPath;
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
                        || string.IsNullOrEmpty(cboMSSQLServerList.Text) ||
                        string.IsNullOrEmpty(txtMSSQL_Username.Text))
                    {
                        MessageBox.Show(Shoko.Commons.Properties.Resources.Server_FillOutSettings,
                            Shoko.Commons.Properties.Resources.Error,
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
                        MessageBox.Show(Shoko.Commons.Properties.Resources.Server_FillOutSettings,
                            Shoko.Commons.Properties.Resources.Error,
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

                logger.Info("Initializing DB...");

                ShokoServer.RunWorkSetupDB();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                MessageBox.Show(Shoko.Commons.Properties.Resources.Server_FailedToStart + ex.Message,
                    Shoko.Commons.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void cboDatabaseType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            switch (cboDatabaseType.SelectedIndex)
            {
                case 0:
                    ServerState.Instance.DatabaseIsSQLite = true;
                    ServerState.Instance.DatabaseIsSQLServer = false;
                    ServerState.Instance.DatabaseIsMySQL = false;
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
                    ServerState.Instance.DatabaseIsSQLite = false;
                    ServerState.Instance.DatabaseIsMySQL = false;
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
                    ServerState.Instance.DatabaseIsSQLServer = false;
                    ServerState.Instance.DatabaseIsSQLite = false;
                    break;
            }
        }

        void  DBSetupCompleted(object sender, EventArgs e)
        {
            btnSaveDatabaseSettings.IsEnabled = true;
            cboDatabaseType.IsEnabled = true;
            btnRefreshMSSQLServerList.IsEnabled = true;
            cboLanguages.IsEnabled = true;
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
                logger.Error(ex, ex.ToString());
                MessageBox.Show(ex.Message, Shoko.Commons.Properties.Resources.Error, MessageBoxButton.OK,
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
            cboDatabaseType.IsEnabled = true;
            btnSaveDatabaseSettings.IsEnabled = true;
        }

        #endregion

        #region Update all media info

        void btnUpdateMediaInfo_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.RefreshAllMediaInfo();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Serrver_VideoMediaUpdate,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }


        #endregion

        #region MyAnime2 Migration
        void btnImportManualLinks_Click(object sender, RoutedEventArgs e)
        {
            if (ShokoServer.IsMyAnime2WorkerBusy())
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.Server_Import,
                    Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtMA2Progress.Visibility = System.Windows.Visibility.Visible;
            txtMA2Success.Visibility = System.Windows.Visibility.Visible;

            Microsoft.Win32.OpenFileDialog ofd =
                new Microsoft.Win32.OpenFileDialog {Filter = "Sqlite Files (*.DB3)|*.db3"};
            ofd.ShowDialog();
            if (!string.IsNullOrEmpty(ofd.FileName))
            {
                ShokoServer.RunMyAnime2Worker(ofd.FileName);
            }
        }

        #endregion

        void btnApplyServerPort_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtServerPort.Text))
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.Server_EnterAnyValue,
                    Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtServerPort.Focus();
                return;
            }

            bool success = ushort.TryParse(txtServerPort.Text, out ushort port);
            if (!success || port <= 0 || port > 65535)
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.Server_EnterCertainValue,
                    Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtServerPort.Focus();
                return;
            }
            if (!Utils.IsAdministrator())
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.Settings_ChangeServerPortFail,
                    Shoko.Commons.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                this.Cursor = Cursors.Wait;
                if (!ShokoServer.Instance.SetNancyPort(port)){
                    MessageBox.Show(Shoko.Commons.Properties.Resources.Settings_ChangeServerPortFail,
                        Shoko.Commons.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                this.Cursor = Cursors.Arrow;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                MessageBox.Show(ex.Message, Shoko.Commons.Properties.Resources.Error, MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        void btnToolbarHelp_Click(object sender, RoutedEventArgs e)
        {
            AboutForm frm = new AboutForm();
            frm.Owner = this;
            frm.ShowDialog();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //ServerInfo.Instance.RefreshImportFolders();

            if (ServerSettings.MinimizeOnStartup) MinimizeToTray();

            tabControl1.SelectedIndex = 6; // setup

            if (ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase))
            {
                btnUploadAzureCache.Visibility = System.Windows.Visibility.Visible;
            }
            logger.Info("Clearing Cache...");

            Utils.ClearAutoUpdateCache();

            ShowDatabaseSetup();

            ShokoServer.Instance.CheckForUpdates();
            ShokoServer.Instance.UpdateAvailable += (s, args) => new UpdateForm {Owner = Instance}.ShowDialog();
        }

        #region UI events and methods

        private void CommandBinding_ScanFolder(object sender, ExecutedRoutedEventArgs e)
        {
            object obj = e.Parameter;
            if (obj == null) return;

            try
            {
                if (obj.GetType() == typeof(SVR_ImportFolder))
                {
                    SVR_ImportFolder fldr = (SVR_ImportFolder) obj;

                    ShokoServer.ScanFolder(fldr.ImportFolderID);
                    MessageBox.Show(Shoko.Commons.Properties.Resources.Server_ScanFolder,
                        Shoko.Commons.Properties.Resources.Success,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        void btnLaunchWebUI_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string IP = GetLocalIPv4(NetworkInterfaceType.Ethernet);
                if (string.IsNullOrEmpty(IP))
                    IP = "127.0.0.1";

                string url = $"http://{IP}:{ServerSettings.JMMServerPort}";
                Process.Start(url);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        internal static string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();

                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                output = ip.Address.ToString();
                            }
                        }
                    }
                }
            }

            return output;
        }

        private void MsgBox(Func<NetworkInterfaceType, string> getLocalIPv4)
        {
            throw new NotImplementedException();
        }

        void btnUpdateAniDBInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                Importer.RunImport_UpdateAllAniDB();
                this.Cursor = Cursors.Arrow;
                MessageBox.Show(Shoko.Commons.Properties.Resources.Server_AniDBInfoUpdate,
                    Shoko.Commons.Properties.Resources.Success,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        void btnUpdateTvDBInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                Importer.RunImport_UpdateTvDB(false);
                this.Cursor = Cursors.Arrow;
                MessageBox.Show(Shoko.Commons.Properties.Resources.Server_TvDBInfoUpdate,
                    Shoko.Commons.Properties.Resources.Success,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        void btnUpdateAllStats_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            Importer.UpdateAllStats();
            this.Cursor = Cursors.Arrow;
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_StatsInfoUpdate,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // This forces an update of TVDb and tries to get any new Images
        void btnUpdateImages_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            Importer.RunImport_UpdateTvDB(true);
            ShokoServer.Instance.DownloadAllImages();
            this.Cursor = Cursors.Arrow;
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_UpdateImages,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnSyncVotes_Click(object sender, RoutedEventArgs e)
        {
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_SyncVotes,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
            //JMMService.AnidbProcessor.IsBanned = true;
        }

        void btnSyncMyList_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.SyncMyList();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_SyncMyList,
                Shoko.Commons.Properties.Resources.Success,
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
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_SyncTrakt,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnRunImport_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.RunImport();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_ImportRunning,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnRemoveMissingFiles_Click(object sender, RoutedEventArgs e)
        {
            ShokoServer.RemoveMissingFiles();
            MessageBox.Show(Shoko.Commons.Properties.Resources.Server_RemoveMissingFiles,
                Shoko.Commons.Properties.Resources.Success,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        void btnGeneralResume_Click(object sender, RoutedEventArgs e)
        {
            ShokoService.CmdProcessorGeneral.Paused = false;
        }

        void btnGeneralPause_Click(object sender, RoutedEventArgs e)
        {
            ShokoService.CmdProcessorGeneral.Paused = true;
        }

        void btnHasherResume_Click(object sender, RoutedEventArgs e)
        {
            ShokoService.CmdProcessorHasher.Paused = false;
        }

        void btnHasherPause_Click(object sender, RoutedEventArgs e)
        {
            ShokoService.CmdProcessorHasher.Paused = true;
        }

        void btnImagesResume_Click(object sender, RoutedEventArgs e)
        {
            ShokoService.CmdProcessorImages.Paused = false;
        }

        void btnImagesPause_Click(object sender, RoutedEventArgs e)
        {
            ShokoService.CmdProcessorImages.Paused = true;
        }

        void btnToolbarShutdown_Click(object sender, RoutedEventArgs e)
        {
            shutdownServer();
        }

        void shutdownServer()
        {
            isAppExiting = true;
            try
            {
                this.Close();
            }
            catch
            {
            }
            TippuTrayNotify.Visible = false;
            TippuTrayNotify.Dispose();
        }

        void restartServer()
        {
            System.Windows.Forms.Application.Restart();
        }

        #endregion

        #region Tray Minimize

        void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                Hide();
            else
                Show();
        }

        void TippuTrayNotify_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e) => this.Show();

        private void CreateMenus()
        {
            //Create a object for the context menu
            ctxTrayMenu = new System.Windows.Forms.ContextMenuStrip();

            //Add the Menu Item to the context menu
            System.Windows.Forms.ToolStripMenuItem mnuShow = new System.Windows.Forms.ToolStripMenuItem();
            mnuShow.Text = Shoko.Commons.Properties.Resources.Toolbar_Show;
            mnuShow.Click += new EventHandler(mnuShow_Click);
            ctxTrayMenu.Items.Add(mnuShow);

            //Add the Menu Item to the context menu
            System.Windows.Forms.ToolStripMenuItem mnuExit = new System.Windows.Forms.ToolStripMenuItem();
            mnuExit.Text = Shoko.Commons.Properties.Resources.Toolbar_Shutdown;
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
            ShokoServer.StopWatchingFiles();
            ShokoServer.AniDBDispose();
            ShokoServer.StopHost();
        }

        private void MinimizeToTray()
        {
            this.Hide();
            TippuTrayNotify.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            TippuTrayNotify.BalloonTipTitle = Shoko.Commons.Properties.Resources.ShokoServer;
            TippuTrayNotify.BalloonTipText = Shoko.Commons.Properties.Resources.Server_MinimizeInfo;
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
    }
}