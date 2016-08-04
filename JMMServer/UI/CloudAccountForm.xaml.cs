using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using JMMServer.Entities;
using JMMServer.Repositories;
using NutzCode.CloudFileSystem;
using Application = System.Windows.Application;

namespace JMMServer
{
    /// <summary>
    /// Interaction logic for CloudAccountForm.xaml
    /// </summary>
    public partial class CloudAccountForm : Window, INotifyPropertyChanged
    { 

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, e);
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
                        Utils.ShowErrorMessage(Properties.Resources.CloudAccounts_CloudNameAlreadyExists);
                        return;
                    }
                    if (SaveAccount == null)
                        SaveAccount = WorkingAccount;
                    else
                    {
                        SaveAccount.FileSystem = null;
                        SaveAccount = WorkingAccount;
                    }
                    CloudAccountRepository crepo=new CloudAccountRepository();
                    SaveAccount.FileSystem = WorkingAccount.FileSystem;
                    crepo.Save(SaveAccount);
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
                        Utils.ShowErrorMessage(Properties.Resources.CloudAccounts_CloudNameAlreadyExists);
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
                        ? Properties.Resources.CloudAccount_Connected
                        : Properties.Resources.CloudAccount_NotConnected);
            OnPropertyChanged(new PropertyChangedEventArgs("IsConnected"));
            OnPropertyChanged(new PropertyChangedEventArgs("IsNotConnected"));

        }
        public bool EnableConnect => (comboProvider.SelectedIndex >= 0 && !string.IsNullOrEmpty(txtCloudAccountName.Text));

        private CloudAccount WorkingAccount;
        private CloudAccount SaveAccount;

        public void Init(CloudAccount account)
        {

            SaveAccount = account;

            WorkingAccount = account != null ? (CloudAccount)account.DeepCopy() : new CloudAccount();
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
