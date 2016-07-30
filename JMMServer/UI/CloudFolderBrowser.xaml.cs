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

namespace JMMServer.UI
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

        public void Init(CloudAccount acc)
        {
            _account = acc;
        }

        public void PopulateMainDir()
        {
            _account.FileSystem.Populate();
            foreach (IDirectory d in _account.FileSystem.Directories)
                TrView.Items.Add(GenerateFromDirectory(d));
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
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count>0 && item.Items[0]==obj)
            {
                item.Items.Clear();
                try
                {
                    IDirectory dir = (IDirectory) item.Tag;
                    dir.Populate();
                    foreach(IDirectory d in dir.Directories)
                        item.Items.Add(GenerateFromDirectory(d));
                }
                catch (Exception) { }
            }
        }
        private void TrView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeView tree = (TreeView)sender;
            TreeViewItem temp = ((TreeViewItem)tree.SelectedItem);

            if (temp == null)
                return;
            string temp1 = "";
            string temp2 = "";
            while (true)
            {
                temp1 = temp.Header.ToString();
                if (temp1.Contains(@"\"))
                {
                    temp2 = string.Empty;
                }
                SelectedPath = temp1 + temp2 + SelectedPath;
                if (temp.Parent.GetType() == typeof(TreeView))
                    break;
                temp = (TreeViewItem)temp.Parent;
                temp2 = @"\";
            }
        }

    }
}
