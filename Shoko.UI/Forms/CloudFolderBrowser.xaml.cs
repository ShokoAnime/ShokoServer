using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NutzCode.CloudFileSystem;
using Shoko.Server.Models;

namespace Shoko.UI.Forms
{
    /// <summary>
    /// Interaction logic for CloudFolderBrowser.xaml
    /// </summary>
    public partial class CloudFolderBrowser : Window
    {
        private object obj = new object();

        private SVR_CloudAccount _account;
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

        public void Init(SVR_ImportFolder acc, string initialpath)
        {
            _account = acc.CloudAccount;
            PopulateMainDir(initialpath);
        }

        private void RecursiveAddFromDirectory(ItemCollection coll, IDirectory d, string[] parts, int pos)
        {
            foreach (IDirectory n in d.Directories.OrderBy(a => a.Name))
            {
                TreeViewItem item = GenerateFromDirectory(n);
                if (parts.Length > pos)
                {
                    if (n.Name.Equals(parts[pos], StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (pos < parts.Length - 1)
                            RecursiveAddFromDirectory(item.Items, n, parts, pos + 1);
                        item.IsSelected = true;
                    }
                }
                coll.Add(item);
            }
        }

        public void PopulateMainDir(string initialpath)
        {
            Cursor = Cursors.Wait;
            _account.FileSystem.Populate();
            initialpath = initialpath.Replace("/", "\\");
            while (initialpath.StartsWith("\\"))
                initialpath = initialpath.Substring(1);
            string[] pars = initialpath.Split(new[] {'\\'}, StringSplitOptions.RemoveEmptyEntries);
            if (pars.Length > 0 && _account.FileSystem.Name == pars[0])
            {
                string[] pars2 = new string[pars.Length - 1];
                Array.Copy(pars, 1, pars2, 0, pars.Length - 1);
                pars = pars2;
            }
            RecursiveAddFromDirectory(TrView.Items, _account.FileSystem, pars, 0);
            Cursor = Cursors.Arrow;
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
            Cursor = Cursors.Wait;
            TreeViewItem item = (TreeViewItem) sender;
            if (item.Items.Count > 0 && item.Items[0] == obj)
            {
                item.Items.Clear();
                try
                {
                    IDirectory dir = (IDirectory) item.Tag;
                    dir.Populate();
                    foreach (IDirectory d in dir.Directories.OrderBy(a => a.Name))
                        item.Items.Add(GenerateFromDirectory(d));
                }
                catch (Exception)
                {
                }
            }
            Cursor = Cursors.Arrow;
        }

        private void TrView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeView tree = (TreeView) sender;
            TreeViewItem item = tree.SelectedItem as TreeViewItem;
            if (item != null)
            {
                IDirectory dir = (IDirectory) item.Tag;
                SelectedPath = dir.FullName;
            }
        }
    }
}