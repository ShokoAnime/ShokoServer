using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using JMMContracts;
using JMMServer.Entities;
using MessageBox = System.Windows.MessageBox;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Shapes;

namespace JMMServer
{
    /// <summary>
    ///     Interaction logic for ImportFolder.xaml
    /// </summary>
    public partial class ImportFolderForm : Window
    {
        private ImportFolder importFldr;

        public ImportFolderForm()
        {
            InitializeComponent();

            btnCancel.Click += btnCancel_Click;
            btnSave.Click += btnSave_Click;
            btnChooseFolder.Click += btnChooseFolder_Click;
        }

        private void btnChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (!string.IsNullOrEmpty(txtImportFolderLocation.Text) && Directory.Exists(txtImportFolderLocation.Text))
                dialog.SelectedPath = txtImportFolderLocation.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtImportFolderLocation.Text = dialog.SelectedPath;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // An import folder cannot be both the drop source and the drop destination
                if (chkDropDestination.IsChecked.HasValue && chkDropSource.IsChecked.HasValue &&
                    chkDropDestination.IsChecked.Value && chkDropSource.IsChecked.Value)
                {
                    MessageBox.Show(Properties.Resources.ImportFolders_SameFolder, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // The import folder location cannot be blank. Enter a valid path on OMM Server
                if (string.IsNullOrEmpty(txtImportFolderLocation.Text))
                {
                    MessageBox.Show(Properties.Resources.ImportFolders_BlankImport, Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    txtImportFolderLocation.Focus();
                    return;
                }

                var contract = new Contract_ImportFolder();
                if (importFldr.ImportFolderID == 0)
                    contract.ImportFolderID = null;
                else
                    contract.ImportFolderID = importFldr.ImportFolderID;
                contract.ImportFolderName = "NA";
                contract.ImportFolderLocation = txtImportFolderLocation.Text.Trim();
                contract.IsDropDestination = chkDropDestination.IsChecked.Value ? 1 : 0;
                contract.IsDropSource = chkDropSource.IsChecked.Value ? 1 : 0;
                contract.IsWatched = chkIsWatched.IsChecked.Value ? 1 : 0;

                var imp = new JMMServiceImplementation();
                var response = imp.SaveImportFolder(contract);
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                    MessageBox.Show(response.ErrorMessage, Properties.Resources.Error, MessageBoxButton.OK,
                        MessageBoxImage.Error);

                ServerInfo.Instance.RefreshImportFolders();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public void Init(ImportFolder ifldr)
        {
            try
            {
                importFldr = ifldr;

                txtImportFolderLocation.Text = importFldr.ImportFolderLocation;
                chkDropDestination.IsChecked = importFldr.IsDropDestination == 1;
                chkDropSource.IsChecked = importFldr.IsDropSource == 1;
                chkIsWatched.IsChecked = importFldr.IsWatched == 1;

                txtImportFolderLocation.Focus();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }
    }
}