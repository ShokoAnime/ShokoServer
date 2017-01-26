using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Shoko.Models.Server;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.UI
{
    /// <summary>
    /// Interaction logic for ScanImportFolders.xaml
    /// </summary>
    public partial class ScanImportFolders : Window
    {
        public Scan SelectedScan { get; private set; }
        public class CheckedImportFolder : SVR_ImportFolder
        {
            public bool Checked { get; set; }

        }
        public ObservableCollection<CheckedImportFolder> ImportFolders { get; set; }


        public ScanImportFolders()
        {

            List<SVR_ImportFolder> flds = RepoFactory.ImportFolder.GetAll().Where(a => a.ImportFolderType <= (int)ImportFolderType.HDD).ToList();
            ObservableCollection<CheckedImportFolder> flds2 = new ObservableCollection<CheckedImportFolder>();
            foreach (SVR_ImportFolder fld in flds)
            {
                CheckedImportFolder c = new CheckedImportFolder();
                c.ImportFolderID = fld.ImportFolderID;
                c.CloudID = fld.CloudID;
                c.ImportFolderType=fld.ImportFolderType;
                c.ImportFolderName=fld.ImportFolderName;
                c.ImportFolderLocation=fld.ImportFolderLocation;

                flds2.Add(c);
            }
            ImportFolders = flds2;
            InitializeComponent();
            btnCancel.Click += BtnCancel_Click;
            btnAdd.Click += BtnAdd_Click;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            List<int> ids=new List<int>();
            foreach (CheckedImportFolder f in ImportFolders)
            {
                if (f.Checked)
                    ids.Add(f.ImportFolderID);

            }
            if (ids.Count == 0)
                return;
            Scan s=new Scan();
            s.Status = (int) ScanStatus.Standby;
            s.CreationTIme = DateTime.Now;
            s.ImportFolders = string.Join(",", ids.Select(a => a.ToString()));
            RepoFactory.Scan.Save(s);
            SelectedScan = s;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
