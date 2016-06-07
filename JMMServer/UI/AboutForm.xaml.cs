using System.Windows;

namespace JMMServer.UI
{
    /// <summary>
    ///     Interaction logic for AboutForm.xaml
    /// </summary>
    public partial class AboutForm : Window
    {
        public AboutForm()
        {
            InitializeComponent();

            btnUpdates.Click += btnUpdates_Click;
        }

        private void btnUpdates_Click(object sender, RoutedEventArgs e)
        {
            var mdw = Owner as MainWindow;
            if (mdw == null) return;

            Close();
            mdw.CheckForUpdatesNew(true);
        }
    }
}