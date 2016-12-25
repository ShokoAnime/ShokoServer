using System.Windows;

namespace JMMServer.UI
{
    /// <summary>
    /// Interaction logic for AboutForm.xaml
    /// </summary>
    public partial class AboutForm : Window
    {
        public AboutForm()
        {
            InitializeComponent();

            //btnUpdates.Click += new RoutedEventHandler(btnUpdates_Click);
        }

        void btnUpdates_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mdw = this.Owner as MainWindow;
            if (mdw == null) return;

            this.Close();
            mdw.CheckForUpdatesNew(true);
        }


        private void cbUpdateChannel_DropDownClosed(object sender, System.EventArgs e)
        {
            if (!string.IsNullOrEmpty(cbUpdateChannel.Text))
                ServerSettings.UpdateChannel = cbUpdateChannel.Text;
        }
    }
}