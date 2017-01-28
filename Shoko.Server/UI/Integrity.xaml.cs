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
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.UI
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
            btnDelete.Click += BtnDeleteClick;
            btnPause.Click += BtnPauseClick;
            btnResume.Click += BtnResumeClick;
            comboProvider.SelectionChanged += ComboProvider_SelectionChanged;
            btnReAddAll.Click += BtnReAddAll_Click;

        }


        private void ComboProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboProvider.SelectedItem!=null)
                Scanner.Instance.ActiveScan = (Scan)comboProvider.SelectedItem;
        }

        private void BtnResumeClick(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.StartScan();
        }

        private void BtnPauseClick(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.CancelScan();
        }

        private void BtnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (
                MessageBox.Show(Shoko.Commons.Properties.Resources.Integrity_DeleteMessage, Shoko.Commons.Properties.Resources.Integrity_DeleteTitle,
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
                HashSet<int> imp=new HashSet<int>(s.GetImportFolderList());
                List<SVR_VideoLocal> vl=imp.SelectMany(a=>RepoFactory.VideoLocal.GetByImportFolder(a)).Distinct().ToList();
                List<ScanFile> files=new List<ScanFile>();
                foreach (SVR_VideoLocal v in vl)
                {
                    foreach (SVR_VideoLocal_Place p in v.Places.Where(a => imp.Contains(a.ImportFolderID)))
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
        private void BtnReAddAll_Click(object sender, RoutedEventArgs e)
        {
            Scan scan = Scanner.Instance.ActiveScan;
            if ((scan != null) && (Scanner.Instance.ActiveErrorFiles.Count>0))
            {
                if (scan.GetScanStatus() == ScanStatus.Running)
                {
                    MessageBox.Show(Shoko.Commons.Properties.Resources.Integerity_ReaddMessage, Shoko.Commons.Properties.Resources.Integerity_ReaddTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (scan.GetScanStatus() == ScanStatus.Finish)
                {
                    scan.Status = (int)ScanStatus.Standby;
                    RepoFactory.Scan.Save(scan);
                }
                List<ScanFile> files = Scanner.Instance.ActiveErrorFiles.ToList();
                Scanner.Instance.ActiveErrorFiles.Clear();
                files.ForEach(a =>
                {
                    a.Status = (int) ScanFileStatus.Waiting;
                });
                RepoFactory.ScanFile.Save(files);
                Scanner.Instance.Refresh();
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ScanFile item = (ScanFile)(sender as Button).DataContext;
            Scan scan = Scanner.Instance.ActiveScan;
            if (scan != null && scan.ScanID == item.ScanID)
            {
                if (scan.GetScanStatus() == ScanStatus.Running)
                {
                    MessageBox.Show(Shoko.Commons.Properties.Resources.Integerity_ReaddSingleMessage, Shoko.Commons.Properties.Resources.Integerity_ReaddSingleTitle, MessageBoxButton.OK,MessageBoxImage.Information);
                    return;
                }
                if (scan.GetScanStatus() == ScanStatus.Finish)
                {
                    scan.Status=(int)ScanStatus.Standby;
                    RepoFactory.Scan.Save(scan);
                }
                item.Status = (int) ScanFileStatus.Waiting;
                RepoFactory.ScanFile.Save(item);
                Scanner.Instance.ActiveErrorFiles.Remove(item);
                Scanner.Instance.Refresh();
            }
        }
    }
}
