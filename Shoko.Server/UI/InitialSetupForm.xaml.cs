using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Shoko.Server.UI
{
    /// <summary>
    /// Interaction logic for InitialSetupForm.xaml
    /// </summary>
    public partial class InitialSetupForm : Window
    {
        private static BackgroundWorker workerTestLogin = new BackgroundWorker();

        public InitialSetupForm()
        {
            InitializeComponent();

            txtUsername.TextChanged += new TextChangedEventHandler(txtUsername_TextChanged);
            txtPassword.PasswordChanged += new RoutedEventHandler(txtPassword_PasswordChanged);
            txtClientPort.TextChanged += new TextChangedEventHandler(txtClientPort_TextChanged);

            btnTestConnection.Click += new RoutedEventHandler(btnTestConnection_Click);
            btnClose.Click += new RoutedEventHandler(btnClose_Click);

            workerTestLogin.DoWork += new DoWorkEventHandler(workerTestLogin_DoWork);
            workerTestLogin.ProgressChanged += new ProgressChangedEventHandler(workerTestLogin_ProgressChanged);
            workerTestLogin.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerTestLogin_RunWorkerCompleted);
            workerTestLogin.WorkerReportsProgress = true;
            workerTestLogin.WorkerSupportsCancellation = true;

            this.Loaded += new RoutedEventHandler(InitialSetupForm_Loaded);
        }

        void InitialSetupForm_Loaded(object sender, RoutedEventArgs e)
        {
            txtUsername.Focus();
        }

        void workerTestLogin_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnTestConnection.IsEnabled = true;
        }

        void workerTestLogin_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ServerState.Instance.AniDB_TestStatus = e.UserState.ToString();
        }

        void workerTestLogin_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                workerTestLogin.ReportProgress(0, Shoko.Commons.Properties.Resources.InitialSetup_Disposing);
                ShokoService.AnidbProcessor.ForceLogout();
                ShokoService.AnidbProcessor.CloseConnections();
                Thread.Sleep(1000);

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                workerTestLogin.ReportProgress(0, Shoko.Commons.Properties.Resources.Server_Initializing);
                ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                    ServerSettings.AniDB_ServerAddress,
                    ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

                workerTestLogin.ReportProgress(0, Shoko.Commons.Properties.Resources.InitialSetup_Login);
                if (ShokoService.AnidbProcessor.Login())
                {
                    workerTestLogin.ReportProgress(0, Shoko.Commons.Properties.Resources.InitialSetup_LoginPass1);
                    ShokoService.AnidbProcessor.ForceLogout();
                    workerTestLogin.ReportProgress(0, Shoko.Commons.Properties.Resources.InitialSetup_LoginPass2);
                }
                else
                {
                    workerTestLogin.ReportProgress(0, Shoko.Commons.Properties.Resources.InitialSetup_LoginFail);
                }
            }
            catch (Exception ex)
            {
                workerTestLogin.ReportProgress(0, ex.Message);
            }
        }

        void btnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            // Check if already running and cancel if needed
            if (workerTestLogin.IsBusy)
                workerTestLogin.CancelAsync();

            // If still running cancel action entirely
            if (workerTestLogin.IsBusy)
                return;

            if (txtUsername.Text.Trim().Length == 0)
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.InitialSetup_EnterUsername, Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                txtUsername.Focus();
                return;
            }

            if (txtPassword.Password.Trim().Length == 0)
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.InitialSetup_EnterPassword, Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                txtPassword.Focus();
                return;
            }

            if (txtClientPort.Text.Trim().Length == 0)
            {
                MessageBox.Show(Shoko.Commons.Properties.Resources.InitialSetup_EnterPort, Shoko.Commons.Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                txtClientPort.Focus();
                return;
            }

            btnTestConnection.IsEnabled = false;
            workerTestLogin.RunWorkerAsync();
        }

        void txtClientPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            ServerSettings.AniDB_ClientPort = txtClientPort.Text.Trim();
        }

        void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ServerSettings.AniDB_Password = txtPassword.Password;
        }

        void txtUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            ServerSettings.AniDB_Username = txtUsername.Text.Trim();
        }

        void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}