using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shoko.Server;
using Shoko.Server.Import;
using Shoko.Server.Models;
using Shoko.Server.Utilities;

namespace Shoko.UI.Forms
{
    /// <summary>
    /// Interaction logic for CloudAccounts.xaml
    /// </summary>
    public partial class CloudAccountAdmin : UserControl
    {
        public CloudAccountAdmin()
        {
            InitializeComponent();
            btnAddCloudAccount.Click += btnAddCloudAccount_Click;
            btnDeleteCloudAccount.Click += btnDeleteCloudAccount_Click;
            lbCloudAccounts.MouseDoubleClick += lbCloudAccount_MouseDoubleClick;
        }

        void lbCloudAccount_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            object obj = lbCloudAccounts.SelectedItem;
            if (obj == null) return;
            SVR_CloudAccount ns = (SVR_CloudAccount) obj;
            EditAccount(ns);
        }


        void btnDeleteCloudAccount_Click(object sender, RoutedEventArgs e)
        {
            object obj = lbCloudAccounts.SelectedItem;
            if (obj == null) return;

            try
            {
                if (obj.GetType() == typeof(SVR_CloudAccount))
                {
                    SVR_CloudAccount ns = (SVR_CloudAccount) obj;

                    MessageBoxResult res = MessageBox.Show(
                        string.Format(Shoko.Commons.Properties.Resources.CloudAccounts_RemoveMessage, ns.Name,
                            ns.Provider), Shoko.Commons.Properties.Resources.Confirm, MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        Cursor = Cursors.Wait;
                        Importer.DeleteCloudAccount(ns.CloudID);
                        Cursor = Cursors.Arrow;
                    }
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Arrow;
                Utils.ShowErrorMessage(ex);
            }
        }

        void EditAccount(SVR_CloudAccount account)
        {
            CloudAccountForm frm = new CloudAccountForm();
            frm.Owner = GetTopParent();
            frm.Init(account);
            frm.ShowDialog();
            ServerInfo.Instance.RefreshCloudAccounts();
        }

        void btnAddCloudAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EditAccount(null);
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }


        private Window GetTopParent()
        {
            DependencyObject dpParent = Parent;
            do
            {
                dpParent = LogicalTreeHelper.GetParent(dpParent);
            } while (dpParent.GetType().BaseType != typeof(Window));

            return dpParent as Window;
        }
    }
}