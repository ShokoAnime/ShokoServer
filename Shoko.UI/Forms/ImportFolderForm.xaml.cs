using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server;
using Shoko.Server.Models;
using Shoko.Server.Utilities;

//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Shapes;

namespace Shoko.UI.Forms
{
    /// <summary>
    /// Interaction logic for ImportFolder.xaml
    /// </summary>
    public partial class ImportFolderForm : Window
    {
        private SVR_ImportFolder importFldr = null;

        public ImportFolderForm()
        {
            InitializeComponent();

            btnCancel.Click += btnCancel_Click;
            btnSave.Click += btnSave_Click;
            btnChooseFolder.Click += btnChooseFolder_Click;
            comboProvider.SelectionChanged += ComboProvider_SelectionChanged;
            chkDropSource.Checked += ChkDropSource_Checked;
            chkDropDestination.Checked += ChkDropDestination_Checked;
        }

        private bool EventLock;

        private void ChkDropDestination_Checked(object sender, RoutedEventArgs e)
        {
            if (EventLock || !chkDropDestination.IsChecked.HasValue || !chkDropDestination.IsChecked.Value) return;
            if (chkDropSource.IsChecked.HasValue && chkDropSource.IsChecked.Value)
            {
                EventLock = true;
                chkDropSource.IsChecked = false;
                EventLock = false;
            }
            if (importFldr.CloudID.HasValue && chkIsWatched.IsChecked.HasValue && chkIsWatched.IsChecked.Value)
            {
                EventLock = true;
                chkIsWatched.IsChecked = false;
                EventLock = false;
            }
        }

        private void ChkDropSource_Checked(object sender, RoutedEventArgs e)
        {
            if (EventLock || !chkDropSource.IsChecked.HasValue || !chkDropSource.IsChecked.Value) return;
            if (chkDropDestination.IsChecked.HasValue && chkDropDestination.IsChecked.Value)
            {
                EventLock = true;
                chkDropDestination.IsChecked = false;
                EventLock = false;
            }
        }

        private void ComboProvider_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (importFldr == null)
                return;
            if (comboProvider.SelectedIndex < 0)
                return;
            if (comboProvider.SelectedIndex == 0)
            {
                importFldr.CloudID = null;
                chkIsWatched.IsEnabled = true;
            }
            else
            {
                importFldr.CloudID = ((SVR_CloudAccount) comboProvider.SelectedItem).CloudID;
                chkIsWatched.IsEnabled = false;
            }
        }

        void btnChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (comboProvider.SelectedIndex == 0)
            {
                //needed check, 
                if (CommonFileDialog.IsPlatformSupported)
                {
                    var dialog = new CommonOpenFileDialog();
                    dialog.IsFolderPicker = true;

                    if (!string.IsNullOrEmpty(txtImportFolderLocation.Text) &&
                        Directory.Exists(txtImportFolderLocation.Text))
                        dialog.InitialDirectory = txtImportFolderLocation.Text;
                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        txtImportFolderLocation.Text = dialog.FileName;
                    }

                }
                else
                {
                    System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();

                    if (!string.IsNullOrEmpty(txtImportFolderLocation.Text) &&
                        Directory.Exists(txtImportFolderLocation.Text))
                        dialog.SelectedPath = txtImportFolderLocation.Text;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        txtImportFolderLocation.Text = dialog.SelectedPath;
                    }
                }
            }
            else
            {
                CloudFolderBrowser frm = new CloudFolderBrowser();
                frm.Owner = this;
                frm.Init(importFldr, txtImportFolderLocation.Text);
                bool? result = frm.ShowDialog();
                if (result.HasValue && result.Value)
                    txtImportFolderLocation.Text = frm.SelectedPath;
            }
        }

        void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // An import folder cannot be both the drop source and the drop destination
                if (chkDropDestination.IsChecked.HasValue && chkDropSource.IsChecked.HasValue &&
                    chkDropDestination.IsChecked.Value &&
                    chkDropSource.IsChecked.Value)
                {
                    MessageBox.Show(Shoko.Commons.Properties.Resources.ImportFolders_SameFolder,
                        Shoko.Commons.Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // The import folder location cannot be blank. Enter a valid path on OMM Server
                if (string.IsNullOrEmpty(txtImportFolderLocation.Text))
                {
                    MessageBox.Show(Shoko.Commons.Properties.Resources.ImportFolders_BlankImport,
                        Shoko.Commons.Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    txtImportFolderLocation.Focus();
                    return;
                }

                ImportFolder contract = new ImportFolder();
                contract.ImportFolderID = importFldr.ImportFolderID;
                contract.ImportFolderType = (int) (importFldr.CloudID.HasValue
                    ? ImportFolderType.Cloud
                    : ImportFolderType.HDD);
                contract.ImportFolderName = "NA";
                contract.ImportFolderLocation = txtImportFolderLocation.Text.Trim();
                contract.IsDropDestination = chkDropDestination.IsChecked ?? false ? 1 : 0;
                contract.IsDropSource = chkDropSource.IsChecked ?? false ? 1 : 0;
                contract.IsWatched = chkIsWatched.IsChecked ?? false ? 1 : 0;
                contract.PhysicalTag = txtPyshicalTag.Text;
                if (comboProvider.SelectedIndex == 0)
                    contract.CloudID = null;
                else
                    contract.CloudID = ((SVR_CloudAccount) comboProvider.SelectedItem).CloudID;
                ShokoServiceImplementation imp = new ShokoServiceImplementation();
                CL_Response<ImportFolder> response = imp.SaveImportFolder(contract);
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    MessageBox.Show(response.ErrorMessage, Shoko.Commons.Properties.Resources.Error,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                importFldr = null;
                ServerInfo.Instance.RefreshImportFolders();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }

            this.DialogResult = true;
            this.Close();
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public void Init(SVR_ImportFolder ifldr)
        {
            try
            {
                ServerInfo.Instance.RefreshFolderProviders();
                importFldr = new SVR_ImportFolder()
                {
                    ImportFolderID = ifldr.ImportFolderID,
                    ImportFolderType = ifldr.ImportFolderType,
                    ImportFolderLocation = ifldr.ImportFolderLocation,
                    ImportFolderName = ifldr.ImportFolderName,
                    IsDropSource = ifldr.IsDropSource,
                    IsDropDestination = ifldr.IsDropDestination,
                    CloudID = ifldr.CloudID.HasValue && ifldr.CloudID == 0 ? null : ifldr.CloudID,
                    IsWatched = ifldr.IsWatched
                };
                txtImportFolderLocation.Text = importFldr.ImportFolderLocation;
                chkDropDestination.IsChecked = importFldr.IsDropDestination == 1;
                chkDropSource.IsChecked = importFldr.IsDropSource == 1;
                chkIsWatched.IsChecked = importFldr.IsWatched == 1;
                if (ifldr.CloudID.HasValue)
                    comboProvider.SelectedItem =
                        ServerInfo.Instance.FolderProviders.FirstOrDefault(a => a.CloudID == ifldr.CloudID.Value);
                else
                    comboProvider.SelectedIndex = 0;
                txtImportFolderLocation.Focus();
            }
            catch (Exception ex)
            {
                Utils.ShowErrorMessage(ex);
            }
        }
    }
}