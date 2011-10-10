using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JMMServer.Entities;
using JMMServer;
using JMMServer.Repositories;

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
		}

		void btnSave_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// An import folder cannot be both the drop source and the drop destination
				if (chkDropDestination.IsChecked.HasValue && chkDropSource.IsChecked.HasValue && chkDropDestination.IsChecked.Value && chkDropSource.IsChecked.Value)
				{
					MessageBox.Show("An import folder cannot be both the drop source and the drop destination", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				// The import folder location cannot be blank. Enter a valid path on OMM Server
				if (string.IsNullOrEmpty(txtImportFolderLocation.Text))
				{
					MessageBox.Show("The import folder location cannot be blank. Enter a valid path on OMM Server", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					txtImportFolderLocation.Focus();
					return;
				}

				if (string.IsNullOrEmpty(txtImportFolderName.Text))
					importFldr.ImportFolderName = "NA";
				else
					importFldr.ImportFolderName = txtImportFolderName.Text.Trim();

				importFldr.ImportFolderLocation = txtImportFolderLocation.Text.Trim();
				importFldr.IsDropDestination = chkDropDestination.IsChecked.Value ? 1 : 0;
				importFldr.IsDropSource = chkDropSource.IsChecked.Value ? 1 : 0;

				ImportFolderRepository repFolders = new ImportFolderRepository();
				repFolders.Save(importFldr);

				//JMMServerVM.Instance.RefreshImportFolders();
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
				txtImportFolderName.Text = importFldr.ImportFolderName;
				chkDropDestination.IsChecked = importFldr.IsDropDestination == 1;
				chkDropSource.IsChecked = importFldr.IsDropSource == 1;

				txtImportFolderName.Focus();
			}
			catch (Exception ex)
			{
				Utils.ShowErrorMessage(ex);
			}
		}
	}
}
