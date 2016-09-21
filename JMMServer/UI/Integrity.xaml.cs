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
using System.Windows.Navigation;
using System.Windows.Shapes;
using JMMServer.Entities;
using JMMServer.Repositories;
using NutzCode.InMemoryIndex;

namespace JMMServer.UI
{
    /// <summary>
    /// Interaction logic for Integrity.xaml
    /// </summary>
    public partial class Integrity : UserControl
    {
        public Integrity()
        {
            InitializeComponent();
            btnAddcheck.Click += BtnAddcheck_Click;
            btnHasherClear.Click += BtnHasherClear_Click;
            btnHasherPause.Click += BtnHasherPause_Click;
            btnHasherResume.Click += BtnHasherResume_Click;
            comboProvider.SelectionChanged += ComboProvider_SelectionChanged;

        }

        private void ComboProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboProvider.SelectedItem!=null)
                Scanner.Instance.ActiveScan = (Scan)comboProvider.SelectedItem;
        }

        private void BtnHasherResume_Click(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.StartScan();
        }

        private void BtnHasherPause_Click(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.CancelScan();
        }

        private void BtnHasherClear_Click(object sender, RoutedEventArgs e)
        {
            if (
                MessageBox.Show("Are you sure you want to delete this Integrity Check?", "Delete Integrity Check",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Scanner.Instance.ClearScan();
                if (comboProvider.Items.Count > 0)
                    comboProvider.SelectedIndex = 0;
                else
                    comboProvider.SelectedIndex = -1;
            }
        }

        private void BtnAddcheck_Click(object sender, RoutedEventArgs e)
        {
            ScanImportFolders frm = new ScanImportFolders();
            frm.Owner = GetTopParent();
            bool? result = frm.ShowDialog();
            if (result.HasValue && result.Value)
            {
                this.IsEnabled = false;
                Cursor = Cursors.Wait;
                Scan s = frm.SelectedScan;
                HashSet<int> imp=new HashSet<int>(s.ImportFolderList);
                List<VideoLocal> vl=imp.SelectMany(a=>RepoFactory.VideoLocal.GetByImportFolder(a)).Distinct().ToList();
                List<ScanFile> files=new List<ScanFile>();
                foreach (VideoLocal v in vl)
                {
                    foreach (VideoLocal_Place p in v.Places.Where(a => imp.Contains(a.ImportFolderID)))
                    {
                        ScanFile sfile=new ScanFile();
                        sfile.Hash = v.ED2KHash;
                        sfile.FileSize = v.FileSize;
                        sfile.FullName = p.FullServerPath;
                        sfile.ScanID = s.ScanID;
                        sfile.Status = (int) ScanFileStatus.Waiting;
                        sfile.ImportFolderID = p.ImportFolderID;
                        sfile.VideoLocal_Place_ID = p.VideoLocal_Place_ID;
                        files.Add(sfile);
                    }
                }
                RepoFactory.ScanFile.Save(files);
                this.IsEnabled = true;
                Scanner.Instance.Scans.Add(s);
                comboProvider.SelectedItem = s;
                Cursor = Cursors.Arrow;
            }
            
        }
        private Window GetTopParent()
        {
            DependencyObject dpParent = this.Parent;
            do
            {
                dpParent = LogicalTreeHelper.GetParent(dpParent);
            } while (dpParent.GetType().BaseType != typeof(Window));

            return dpParent as Window;
        }
    }
}
