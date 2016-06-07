using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace JMMServer.UI
{
    /// <summary>
    ///     Interaction logic for InitialSetupForm.xaml
    /// </summary>
    public partial class InitialSetupForm : Window
    {
        private static readonly BackgroundWorker workerTestLogin = new BackgroundWorker();

        public InitialSetupForm()
        {
            InitializeComponent();

            txtUsername.TextChanged += txtUsername_TextChanged;
            txtPassword.PasswordChanged += txtPassword_PasswordChanged;
            txtClientPort.TextChanged += txtClientPort_TextChanged;

            btnTestConnection.Click += btnTestConnection_Click;

            workerTestLogin.DoWork += workerTestLogin_DoWork;
            workerTestLogin.ProgressChanged += workerTestLogin_ProgressChanged;
            workerTestLogin.RunWorkerCompleted += workerTestLogin_RunWorkerCompleted;
            workerTestLogin.WorkerReportsProgress = true;

            Loaded += InitialSetupForm_Loaded;
        }

        private void InitialSetupForm_Loaded(object sender, RoutedEventArgs e)
        {
            txtUsername.Focus();
        }

        private void workerTestLogin_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnTestConnection.IsEnabled = true;
        }

        private void workerTestLogin_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ServerState.Instance.AniDB_TestStatus = e.UserState.ToString();
        }

        private void workerTestLogin_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                workerTestLogin.ReportProgress(0, Properties.Resources.InitialSetup_Disposing);
                JMMService.AnidbProcessor.ForceLogout();
                JMMService.AnidbProcessor.CloseConnections();
                Thread.Sleep(1000);

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                workerTestLogin.ReportProgress(0, Properties.Resources.Server_Initializing);
                JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                    ServerSettings.AniDB_ServerAddress,
                    ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

                workerTestLogin.ReportProgress(0, Properties.Resources.InitialSetup_Login);
                if (JMMService.AnidbProcessor.Login())
                {
                    workerTestLogin.ReportProgress(0, Properties.Resources.InitialSetup_LoginPass1);
                    JMMService.AnidbProcessor.ForceLogout();
                    workerTestLogin.ReportProgress(0, Properties.Resources.InitialSetup_LoginPass2);
                }
                else
                {
                    workerTestLogin.ReportProgress(0, Properties.Resources.InitialSetup_LoginFail);
                }
            }
            catch (Exception ex)
            {
                workerTestLogin.ReportProgress(0, ex.Message);
            }
        }

        private void btnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (txtUsername.Text.Trim().Length == 0)
            {
                MessageBox.Show(Properties.Resources.InitialSetup_EnterUsername, Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtUsername.Focus();
                return;
            }

            if (txtPassword.Password.Trim().Length == 0)
            {
                MessageBox.Show(Properties.Resources.InitialSetup_EnterPassword, Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Focus();
                return;
            }

            if (txtClientPort.Text.Trim().Length == 0)
            {
                MessageBox.Show(Properties.Resources.InitialSetup_EnterPort, Properties.Resources.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtClientPort.Focus();
                return;
            }

            btnTestConnection.IsEnabled = false;
            workerTestLogin.RunWorkerAsync();
        }

        private void txtClientPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            ServerSettings.AniDB_ClientPort = txtClientPort.Text.Trim();
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ServerSettings.AniDB_Password = txtPassword.Password;
        }

        private void txtUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            ServerSettings.AniDB_Username = txtUsername.Text.Trim();
        }
    }
}