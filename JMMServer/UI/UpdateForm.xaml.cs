using System;
using System.Windows;
using JMMServer.Providers.JMMAutoUpdates;
using NLog;

namespace JMMServer.UI
{
    /// <summary>
    ///     Interaction logic for UpdateForm.xaml
    /// </summary>
    public partial class UpdateForm : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public UpdateForm()
        {
            InitializeComponent();

            Loaded += UpdateForm_Loaded;
        }

        private void UpdateForm_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // get the latest version as according to the release
                var verInfo = JMMAutoUpdatesHelper.GetLatestVersionInfo();
                if (verInfo == null) return;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }
    }
}