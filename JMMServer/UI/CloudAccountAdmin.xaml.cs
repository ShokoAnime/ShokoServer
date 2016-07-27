using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JMMServer.Entities;

namespace JMMServer.UI
{
    /// <summary>
    /// Interaction logic for CloudAccounts.xaml
    /// </summary>
    public partial class CloudAccounts : UserControl
    {
        public CloudAccounts()
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
            CloudAccount ns = (CloudAccount)obj;
            EditAccount(ns);

        }


        void btnDeleteCloudAccount_Click(object sender, RoutedEventArgs e)
        {
            object obj = lbCloudAccounts.SelectedItem;
            if (obj == null) return;

            try
            {
                if (obj.GetType() == typeof(CloudAccount))
                {
                    CloudAccount ns = (CloudAccount)obj;

                    MessageBoxResult res =
                        MessageBox.Show(string.Format(Properties.Resources.CloudAccounts_RemoveMessage,
                                ns.Name,ns.Provider),
                            Properties.Resources.Confirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
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

        void EditAccount(CloudAccount account)
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
                EditAccount(new CloudAccount());
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
