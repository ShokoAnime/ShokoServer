using System;
using System.IO;
using System.Linq;
using System.Windows;
using FluentNHibernate.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Entities;

//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Shapes;

namespace Shoko.Server
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

            btnCancel.Click += new RoutedEventHandler(btnCancel_Click);
            btnSave.Click += new RoutedEventHandler(btnSave_Click);
            btnChooseFolder.Click += new RoutedEventHandler(btnChooseFolder_Click);
            comboProvider.SelectionChanged += ComboProvider_SelectionChanged;
            chkDropSource.Checked += ChkDropSource_Checked;
            chkDropDestination.Checked += ChkDropDestination_Checked;
            chkIsWatched.Checked += ChkIsWatched_Checked;
        }

        private void ChkIsWatched_Checked(object sender, RoutedEventArgs e)
        {
            if (!checkchange && importFldr.CloudID.HasValue && chkDropDestination.IsChecked.HasValue &&
                chkDropDestination.IsChecked.Value)
            {
                checkchange = true;
                chkIsWatched.IsChecked = false;
                checkchange = false;
            }
        }

        private void ChkDropDestination_Checked(object sender, RoutedEventArgs e)
        {
            if (!checkchange && chkDropDestination.IsChecked.HasValue && chkDropDestination.IsChecked.Value)
            {
                if (chkDropSource.IsChecked.HasValue && chkDropSource.IsChecked.Value)
                {
                    checkchange = true;
                    chkDropSource.IsChecked = false;
                    checkchange = false;
                }
                if (importFldr.CloudID.HasValue && chkIsWatched.IsChecked.HasValue && chkIsWatched.IsChecked.Value)
                {
                    checkchange = true;
                    chkIsWatched.IsChecked = false;
                    checkchange = false;
                }
            }
        }

        private bool checkchange = false;

        private void ChkDropSource_Checked(object sender, RoutedEventArgs e)
        {
            if (!checkchange && chkDropSource.IsChecked.HasValue && chkDropSource.IsChecked.Value)
            {
                if (chkDropDestination.IsChecked.HasValue && chkDropDestination.IsChecked.Value)
                {
                    checkchange = true;
                    chkDropDestination.IsChecked = false;
                    checkchange = false;
                }
            }
        }

        private void ComboProvider_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (importFldr == null)
                return;
            if (comboProvider.SelectedIndex < 0)
                return;
            if (comboProvider.SelectedIndex == 0)
                importFldr.CloudID = null;
            else
                importFldr.CloudID = ((SVR_CloudAccount)comboProvider.SelectedItem).CloudID;
        }

        void btnChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (comboProvider.SelectedIndex == 0)
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
            else
            {
                CloudFolderBrowser frm=new CloudFolderBrowser();
                frm.Owner = this;
                frm.Init(importFldr, txtImportFolderLocation.Text);
                bool? result=frm.ShowDialog();
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
                    MessageBox.Show(Shoko.Server.Properties.Resources.ImportFolders_SameFolder,
                        Shoko.Server.Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // The import folder location cannot be blank. Enter a valid path on OMM Server
                if (string.IsNullOrEmpty(txtImportFolderLocation.Text))
                {
                    MessageBox.Show(Shoko.Server.Properties.Resources.ImportFolders_BlankImport,
                        Shoko.Server.Properties.Resources.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    txtImportFolderLocation.Focus();
                    return;
                }

                ImportFolder contract = new ImportFolder();
                contract.ImportFolderID = importFldr.ImportFolderID;
                contract.ImportFolderType = (int)(importFldr.CloudID.HasValue ? ImportFolderType.Cloud : ImportFolderType.HDD);
                contract.ImportFolderName = "NA";
                contract.ImportFolderLocation = txtImportFolderLocation.Text.Trim();
                contract.IsDropDestination = chkDropDestination.IsChecked.Value ? 1 : 0;
                contract.IsDropSource = chkDropSource.IsChecked.Value ? 1 : 0;
                contract.IsWatched = chkIsWatched.IsChecked.Value ? 1 : 0;
                if (comboProvider.SelectedIndex == 0)
                    contract.CloudID = null;
                else
                    contract.CloudID = ((SVR_CloudAccount) comboProvider.SelectedItem).CloudID;
                JMMServiceImplementation imp = new JMMServiceImplementation();
                CL_Response<ImportFolder> response = imp.SaveImportFolder(contract);
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                    MessageBox.Show(response.ErrorMessage, Shoko.Server.Properties.Resources.Error, MessageBoxButton.OK,
                        MessageBoxImage.Error);
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
                    comboProvider.SelectedItem = ServerInfo.Instance.FolderProviders.FirstOrDefault(a => a.CloudID == ifldr.CloudID.Value);
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