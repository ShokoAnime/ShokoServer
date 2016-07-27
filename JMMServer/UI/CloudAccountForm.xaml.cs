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

namespace JMMServer.UI
{
    /// <summary>
    /// Interaction logic for CloudAccountForm.xaml
    /// </summary>
    public partial class CloudAccountForm : Window
    {
        public CloudAccountForm()
        {
            InitializeComponent();
        }
        public void Init(CloudAccount account)
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
