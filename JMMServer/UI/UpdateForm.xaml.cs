using NLog;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Xml.Serialization;

namespace JMMServer.UI
{
    /// <summary>
    /// Interaction logic for UpdateForm.xaml
    /// </summary>
    public partial class UpdateForm : Window
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public UpdateForm()
        {
            InitializeComponent();

            this.Loaded += UpdateForm_Loaded;
        }

        private void UpdateForm_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // get the latest version as according to the release
                Providers.JMMAutoUpdates.JMMVersions verInfo = Providers.JMMAutoUpdates.JMMAutoUpdatesHelper.GetLatestVersionInfo();
                if (verInfo == null) return;

            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }
    }
}
