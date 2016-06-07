using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JMMServer.Entities;

namespace JMMServer
{
    /// <summary>
    ///     Interaction logic for ImportFolderAdmin.xaml
    /// </summary>
    public partial class ImportFolderAdmin : UserControl
    {
        public ImportFolderAdmin()
        {
            InitializeComponent();

            btnAddImportFolder.Click += btnAddImportFolder_Click;
            btnAddUPnPSource.Click += btnAddUPnPSource_Click;
            btnDeleteImportFolder.Click += btnDeleteImportFolder_Click;
            lbImportFolders.MouseDoubleClick += lbImportFolders_MouseDoubleClick;
        }

        private void lbImportFolders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var obj = lbImportFolders.SelectedItem;
            if (obj == null) return;

            var ns = (ImportFolder)obj;
            var frm = new ImportFolderForm();
            frm.Owner = GetTopParent();
            frm.Init(ns);
            var result = frm.ShowDialog();
        }

        private void btnDeleteImportFolder_Click(object sender, RoutedEventArgs e)
        {
            var obj = lbImportFolders.SelectedItem;
            if (obj == null) return;

            try
            {
                if (obj.GetType() == typeof(ImportFolder))
                {
                    var ns = (ImportFolder)obj;

                    var res =
                        MessageBox.Show(
                            string.Format(Properties.Resources.ImportFolders_DeleteFolder, ns.ImportFolderLocation),
                            Properties.Resources.Confirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        Cursor = Cursors.Wait;
                        Importer.DeleteImportFolder(ns.ImportFolderID);
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

        private void btnAddImportFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var frm = new ImportFolderForm();
                frm.Owner = GetTopParent();
                frm.Init(new ImportFolder());
                var result = frm.ShowDialog();

                ServerInfo.Instance.RefreshImportFolders();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }

        private void btnAddUPnPSource_Click(object sender, RoutedEventArgs e)
        {
            var addSource = new UPnPServerBrowserDialog();
            addSource.ShowDialog();
        }

        private Window GetTopParent()
        {
            var dpParent = Parent;
            do
            {
                dpParent = LogicalTreeHelper.GetParent(dpParent);
            } while (dpParent.GetType().BaseType != typeof(Window));

            return dpParent as Window;
        }
    }
}