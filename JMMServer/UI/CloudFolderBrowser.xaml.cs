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
using NutzCode.CloudFileSystem;

namespace JMMServer
{
    /// <summary>
    /// Interaction logic for CloudFolderBrowser.xaml
    /// </summary>
    public partial class CloudFolderBrowser : Window
    {
        private object obj = new object();

        private CloudAccount _account;
        public string SelectedPath { get; set; } = string.Empty;

        public CloudFolderBrowser()
        {
            InitializeComponent();
            TrView.SelectedItemChanged += TrView_SelectedItemChanged;
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += BtnCancel_Click;
        }



        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public void Init(ImportFolder acc)
        {
            _account = acc.CloudAccount;
            PopulateMainDir();
        }

        public void PopulateMainDir()
        {
            this.Cursor = Cursors.Wait;
            _account.FileSystem.Populate();
            foreach (IDirectory d in _account.FileSystem.Directories.OrderBy(a=>a.Name))
                TrView.Items.Add(GenerateFromDirectory(d));
            this.Cursor = Cursors.Arrow;
        }

        private TreeViewItem GenerateFromDirectory(IDirectory d)
        {
            TreeViewItem item = new TreeViewItem();
            item.Header = d.Name;
            item.Tag = d;
            item.FontWeight = FontWeights.Normal;
            item.Items.Add(obj);
            item.Expanded += Item_Expanded;
            return item;
        }
        private void Item_Expanded(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count>0 && item.Items[0]==obj)
            {
                item.Items.Clear();
                try
                {
                    IDirectory dir = (IDirectory) item.Tag;
                    dir.Populate();
                    foreach(IDirectory d in dir.Directories.OrderBy(a=>a.Name))
                        item.Items.Add(GenerateFromDirectory(d));
                }
                catch (Exception) { }
            }
            this.Cursor = Cursors.Arrow;
        }
        private void TrView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeView tree = (TreeView)sender;
            TreeViewItem item = tree.SelectedItem as TreeViewItem;
            if (item != null)
            {
                IDirectory dir = (IDirectory)item.Tag;
                SelectedPath = dir.FullName;
            }
        }

    }
}
