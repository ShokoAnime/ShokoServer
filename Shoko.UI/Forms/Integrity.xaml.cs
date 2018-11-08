using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server;
using Shoko.Server.FileScanner;
using Shoko.Server.Models;
using Shoko.Server.Repositories;


namespace Shoko.UI.Forms
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
            btnCancel.Click += BtnCancelClick;
            btnStart.Click += BtnStartOnClick;
            btnPause.Click += BtnPauseClick;
            btnResume.Click += BtnResumeClick;
            btnReAddAll.Click += BtnReAddAll_Click;
            btnKill.Click +=BtnKillOnClick;
        }



        private void BtnStartOnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentScanInfo != null)
            {
                Scanner.Instance.Start(CurrentScanInfo);
            }
        }

        public bool IsSelectedScan => comboProvider.SelectedItem != null;

        public ScannerInfo CurrentScanInfo => comboProvider.SelectedItem as ScannerInfo;

        private void BtnResumeClick(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.Resume(CurrentScanInfo);
        }

        private void BtnPauseClick(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.Pause(CurrentScanInfo);
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            Scanner.Instance.Cancel(CurrentScanInfo);
        }
        private void BtnKillOnClick(object sender, RoutedEventArgs e)
        {
            if (
                MessageBox.Show(Shoko.Commons.Properties.Resources.Integrity_DeleteMessage,
                    Shoko.Commons.Properties.Resources.Integrity_DeleteTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Scanner.Instance.Destroy(CurrentScanInfo);
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
                Scan s = (Scan) frm.SelectedScan;
                Scanner.Instance.Add(s);
                comboProvider.SelectedItem = s;
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
            if (CurrentScanInfo!=null)
                Scanner.Instance.ReAddAllFiles(CurrentScanInfo);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ScanFile item = (ScanFile) (sender as Button)?.DataContext;
            if (item!=null)
                Scanner.Instance.ReAddErrorFile(item);
        }

    
    }
}