using System;
using System.Windows;
using NLog;
using Shoko.Server;

namespace Shoko.UI.Forms
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
            tbUpdateAvailable.Visibility = IsNewVersionAvailable() ? Visibility.Visible : Visibility.Hidden;
            this.Loaded += UpdateForm_Loaded;
        }

        private void UpdateForm_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // get the latest version as according to the release
                //Providers.JMMAutoUpdates.JMMVersions verInfo =
                //    Providers.JMMAutoUpdates.JMMAutoUpdatesHelper.GetLatestVersionInfo();
                //if (verInfo == null) return;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public bool IsNewVersionAvailable()
        {
            if (ServerState.Instance.ApplicationVersion == ServerState.Instance.ApplicationVersionLatest)
                return false;

            return true;
        }
    }
}