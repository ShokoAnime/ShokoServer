using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Cached;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Notification;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Application = System.Windows.Application;

namespace Shoko.Server
{
    /// <summary>
    /// Interaction logic for CloudAccountForm.xaml
    /// </summary>
    public partial class CloudAccountForm : Window, INotifyPropertyChangedExt
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static Scanner Instance { get; set; } = new Scanner();

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }

        public CloudAccountForm()
        {
            InitializeComponent();
            btnCloudConnect.Click += BtnCloudConnect_Click;
            btnCancel.Click += BtnCancel_Click;
            btnSave.Click += BtnSave_Click;
        }

        private IFileSystem fs;

        public bool IsConnected => (WorkingAccount != null && WorkingAccount.IsConnected);
        public bool IsNotConnected => (WorkingAccount == null || !WorkingAccount.IsConnected);

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(txtCloudAccountName.Text) && comboProvider.SelectedItem != null && !string.IsNullOrEmpty(WorkingAccount.ConnectionString) && (WorkingAccount?.FileSystem != null))
                {
                    if (ServerInfo.Instance.CloudAccounts.Any(a => a.Name == txtCloudAccountName.Text && a.CloudID != WorkingAccount.CloudID))
                    {
                        Utils.ShowErrorMessage(Shoko.Commons.Properties.Resources.CloudAccounts_CloudNameAlreadyExists);
                        return;
                    }
                    if (SaveAccount == null)
                        SaveAccount = WorkingAccount;
                    else
                    {
                        SaveAccount.FileSystem = null;
                        SaveAccount = WorkingAccount;
                    }
                    SaveAccount.FileSystem = WorkingAccount.FileSystem;
                    RepoFactory.CloudAccount.Save(SaveAccount);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCloudConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(txtCloudAccountName.Text) && comboProvider.SelectedItem != null)
                {
                    if (ServerInfo.Instance.CloudAccounts.Any(a => a.Name == txtCloudAccountName.Text && a.CloudID != WorkingAccount.CloudID))
                    {
                        Utils.ShowErrorMessage(Shoko.Commons.Properties.Resources.CloudAccounts_CloudNameAlreadyExists);
                        return;
                    }
                    WorkingAccount.Provider = ((ServerInfo.CloudProvider) comboProvider.SelectedItem).Name;
                    WorkingAccount.Name = txtCloudAccountName.Text;
                    WorkingAccount.ConnectionString = null;
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {

                            WorkingAccount.FileSystem = WorkingAccount.Connect(this);
                            SetConnectStatus();
                        }

                        catch (ReflectionTypeLoadException ex)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (Exception exSub in ex.LoaderExceptions)
                            {
                                sb.AppendLine(exSub.Message);
                                FileNotFoundException exFileNotFound = exSub as FileNotFoundException;
                                if (exFileNotFound != null)
                                {
                                    if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                                    {
                                        sb.AppendLine("Fusion Log:");
                                        sb.AppendLine(exFileNotFound.FusionLog);
                                    }
                                }
                                sb.AppendLine();
                            }
                            Application.Current.Dispatcher.Invoke(() => { TextStatus.Text = sb.ToString(); });          
                            logger.Error(sb.ToString());              
                            //Display or log the error based on your application.
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => { TextStatus.Text = ex.Message; });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public void SetConnectStatus()
        {
            Application.Current.Dispatcher.Invoke(() =>
                TextStatus.Text =
                    IsConnected
                        ? Shoko.Commons.Properties.Resources.CloudAccount_Connected
                        : Shoko.Commons.Properties.Resources.CloudAccount_NotConnected);
            this.OnPropertyChanged(() => IsConnected, () => IsNotConnected);

        }
        public bool EnableConnect => (comboProvider.SelectedIndex >= 0 && !string.IsNullOrEmpty(txtCloudAccountName.Text));

        private SVR_CloudAccount WorkingAccount;
        private SVR_CloudAccount SaveAccount;

        public void Init(SVR_CloudAccount account)
        {

            SaveAccount = account;

            WorkingAccount = account != null ? new SVR_CloudAccount
            {
                CloudID=account.CloudID,
                Name=account.Name,
                ConnectionString = account.ConnectionString,
                Provider=account.Provider
        } : new SVR_CloudAccount();
            SetConnectStatus();
            try
            {
                if (!string.IsNullOrEmpty(WorkingAccount.Provider))
                {
                    ServerInfo.CloudProvider v = ServerInfo.Instance.CloudProviders.FirstOrDefault(a => a.Name == WorkingAccount.Provider);
                    if (v != null)
                        comboProvider.SelectedItem = v;
                }
                if (!string.IsNullOrEmpty(WorkingAccount.Name))
                {
                    txtCloudAccountName.Text = WorkingAccount.Name;
                }
                comboProvider.Focus();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }
    }
}
