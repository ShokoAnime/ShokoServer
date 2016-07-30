using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JMMServer.Entities;
using JMMServer.Repositories;
using NutzCode.CloudFileSystem;

namespace JMMServer.UI
{
    /// <summary>
    /// Interaction logic for CloudAccountForm.xaml
    /// </summary>
    public partial class CloudAccountForm : Window
    {
        public CloudAccountForm()
        {
            InitializeComponent();
            btnCloudConnect.Click += BtnCloudConnect_Click;
            btnCancel.Click += BtnCancel_Click;
            btnSave.Click += BtnSave_Click;
        }

        private IFileSystem fs;
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(txtCloudAccountName.Text) && comboProvider.SelectedItem != null && !string.IsNullOrEmpty(WorkingAccount.ConnectionString) && (fs!=null))
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
                    SaveAccount.FileSystem = fs;
                    crepo.Save(SaveAccount);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
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
                    WorkingAccount.Provider = ((CloudAccount) comboProvider.SelectedItem).Provider;
                    WorkingAccount.Name = txtCloudAccountName.Text;
                    WorkingAccount.ConnectionString = null;
                    fs=WorkingAccount.Connect();
                }
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        public bool EnableConnect => (comboProvider.SelectedIndex >= 0 && !string.IsNullOrEmpty(txtCloudAccountName.Text));

        private CloudAccount WorkingAccount;
        private CloudAccount SaveAccount;

        public void Init(CloudAccount account)
        {
            SaveAccount = account;
            WorkingAccount = account != null ? (CloudAccount)account.DeepCopy() : new CloudAccount();
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
