using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Shapes;
using JMMServer.Entities;
using JMMServer;
using JMMServer.Repositories;
using JMMContracts;
using System.IO;

namespace JMMServer
{
	/// <summary>
	/// Interaction logic for ImportFolder.xaml
	/// </summary>
	public partial class ImportFolderForm : Window
	{
		private ImportFolder importFldr = null;
		
		public ImportFolderForm()
		{
			InitializeComponent();

			btnCancel.Click += new RoutedEventHandler(btnCancel_Click);
			btnSave.Click += new RoutedEventHandler(btnSave_Click);
			btnChooseFolder.Click += new RoutedEventHandler(btnChooseFolder_Click);
		}

		void btnChooseFolder_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();

			if (!string.IsNullOrEmpty(txtImportFolderLocation.Text) && Directory.Exists(txtImportFolderLocation.Text))
				dialog.SelectedPath = txtImportFolderLocation.Text;

			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				txtImportFolderLocation.Text = dialog.SelectedPath;
			}
		}

		void btnSave_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// An import folder cannot be both the drop source and the drop destination
				if (chkDropDestination.IsChecked.HasValue && chkDropSource.IsChecked.HasValue && chkDropDestination.IsChecked.Value && chkDropSource.IsChecked.Value)
				{
					MessageBox.Show(JMMServer.Properties.Resources.ImportFolders_SameFolder, JMMServer.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				// The import folder location cannot be blank. Enter a valid path on OMM Server
				if (string.IsNullOrEmpty(txtImportFolderLocation.Text))
				{
					MessageBox.Show(JMMServer.Properties.Resources.ImportFolders_BlankImport, JMMServer.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
					txtImportFolderLocation.Focus();
					return;
				}

				Contract_ImportFolder contract = new Contract_ImportFolder();
				if (importFldr.ImportFolderID == 0)
					contract.ImportFolderID = null;
				else
					contract.ImportFolderID = importFldr.ImportFolderID;
				contract.ImportFolderName = "NA";
				contract.ImportFolderLocation = txtImportFolderLocation.Text.Trim();
				contract.IsDropDestination = chkDropDestination.IsChecked.Value ? 1 : 0;
				contract.IsDropSource = chkDropSource.IsChecked.Value ? 1 : 0;
				contract.IsWatched = chkIsWatched.IsChecked.Value ? 1 : 0; 

				JMMServiceImplementation imp = new JMMServiceImplementation();
				Contract_ImportFolder_SaveResponse response = imp.SaveImportFolder(contract);
				if (!string.IsNullOrEmpty(response.ErrorMessage))
					MessageBox.Show(response.ErrorMessage, JMMServer.Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);

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
