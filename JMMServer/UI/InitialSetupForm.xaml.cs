using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Threading;

namespace JMMServer.UI
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

			workerTestLogin.DoWork += new DoWorkEventHandler(workerTestLogin_DoWork);
			workerTestLogin.ProgressChanged += new ProgressChangedEventHandler(workerTestLogin_ProgressChanged);
			workerTestLogin.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerTestLogin_RunWorkerCompleted);
			workerTestLogin.WorkerReportsProgress = true;

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
				workerTestLogin.ReportProgress(0, "Disposing...");
				JMMService.AnidbProcessor.ForceLogout();
				JMMService.AnidbProcessor.CloseConnections();
				Thread.Sleep(1000);

				workerTestLogin.ReportProgress(0, "Init...");
				JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password, ServerSettings.AniDB_ServerAddress,
				ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

				workerTestLogin.ReportProgress(0, "Login...");
				if (JMMService.AnidbProcessor.Login())
				{
					workerTestLogin.ReportProgress(0, "Login Success! Logging out...");
					JMMService.AnidbProcessor.ForceLogout();
					workerTestLogin.ReportProgress(0, "Login Success! Logged out.");
				}
				else
				{
					workerTestLogin.ReportProgress(0, "Login FAILED!");
				}
			}
			catch (Exception ex)
			{
				workerTestLogin.ReportProgress(0, ex.Message);
			}
		}

		void btnTestConnection_Click(object sender, RoutedEventArgs e)
		{
			if (txtUsername.Text.Trim().Length == 0)
			{
				MessageBox.Show("Please enter a username", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				txtUsername.Focus();
				return;
			}

			if (txtPassword.Password.Trim().Length == 0)
			{
				MessageBox.Show("Please enter a Password", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				txtPassword.Focus();
				return;
			}

			if (txtClientPort.Text.Trim().Length == 0)
			{
				MessageBox.Show("Please enter a client port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
	}
}
