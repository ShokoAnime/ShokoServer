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
using System.Windows.Navigation;
using System.Windows.Shapes;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer
{
	/// <summary>
	/// Interaction logic for ImportFolderAdmin.xaml
	/// </summary>
	public partial class ImportFolderAdmin : UserControl
	{
		public ImportFolderAdmin()
		{
			InitializeComponent();

			btnAddImportFolder.Click += new RoutedEventHandler(btnAddImportFolder_Click);
			btnDeleteImportFolder.Click += new RoutedEventHandler(btnDeleteImportFolder_Click);
			lbImportFolders.MouseDoubleClick += new MouseButtonEventHandler(lbImportFolders_MouseDoubleClick);
		}

		void lbImportFolders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			object obj = lbImportFolders.SelectedItem;
			if (obj == null) return;

			ImportFolder ns = (ImportFolder)obj;
			ImportFolderForm frm = new ImportFolderForm();
			frm.Owner = GetTopParent();
			frm.Init(ns);
			bool? result = frm.ShowDialog();
		}

		void btnDeleteImportFolder_Click(object sender, RoutedEventArgs e)
		{
			object obj = lbImportFolders.SelectedItem;
			if (obj == null) return;

			try
			{
				if (obj.GetType() == typeof(ImportFolder))
				{
					ImportFolder ns = (ImportFolder)obj;

					MessageBoxResult res = MessageBox.Show(string.Format("Are you sure you want to delete the Import Folder: {0}", ns.ImportFolderLocation), "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (res == MessageBoxResult.Yes)
					{
						this.Cursor = Cursors.Wait;
						Importer.DeleteImportFolder(ns.ImportFolderID);
						this.Cursor = Cursors.Arrow;
					}
				}
			}
			catch (Exception ex)
			{
				this.Cursor = Cursors.Arrow;
				Utils.ShowErrorMessage(ex);
			}
		}

		void btnAddImportFolder_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ImportFolderForm frm = new ImportFolderForm();
				frm.Owner = GetTopParent();
				frm.Init(new ImportFolder());
				bool? result = frm.ShowDialog();

				ServerInfo.Instance.RefreshImportFolders();
			}
			catch (Exception ex)
			{
				Utils.ShowErrorMessage(ex);
			}
		}

		private Window GetTopParent()
		{
			DependencyObject dpParent = this.Parent;
			do
			{
				dpParent = LogicalTreeHelper.GetParent(dpParent);
			} 
			while (dpParent.GetType().BaseType != typeof(Window));

			return dpParent as Window;
		}
	}
}
