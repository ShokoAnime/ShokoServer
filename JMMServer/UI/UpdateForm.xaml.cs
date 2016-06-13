using System;
using System.Windows;
using NLog;

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
                Providers.JMMAutoUpdates.JMMVersions verInfo =
                    Providers.JMMAutoUpdates.JMMAutoUpdatesHelper.GetLatestVersionInfo();
                if (verInfo == null) return;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }
    }
}