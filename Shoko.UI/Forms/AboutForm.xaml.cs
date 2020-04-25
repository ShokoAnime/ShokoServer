using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shoko.Server;
using Shoko.Server.Settings;

namespace Shoko.UI.Forms
{
    /// <summary>
    /// Interaction logic for AboutForm.xaml
    /// </summary>
    public partial class AboutForm : Window
    {
        public AboutForm()
        {
            InitializeComponent();

            cbUpdateChannel.Items.Add("Stable");
            cbUpdateChannel.Items.Add("Beta");
            cbUpdateChannel.Items.Add("Alpha");
            cbUpdateChannel.Text = ServerSettings.Instance.UpdateChannel;
        }

        void btnUpdates_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mdw = Owner as MainWindow;
            if (mdw == null) return;

            Close();
            ShokoServer.Instance.CheckForUpdatesNew(true);
        }


        private void cbUpdateChannel_DropDownClosed(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(cbUpdateChannel.Text))
                ServerSettings.Instance.UpdateChannel = cbUpdateChannel.Text;
        }

        private void cbUpdateChannel_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(cbUpdateChannel.Text))
                ServerSettings.Instance.UpdateChannel = cbUpdateChannel.Text;
        }

        private void CommandBinding_SelectTextAndCopy(object sender, ExecutedRoutedEventArgs e)
        {
            string obj = e.Parameter as string;
            CopyToClipboard(obj);
        }

        public static void CopyToClipboard(string obj)
        {
            CopyToClipboardRecursiveRetry(obj, 0, 5);
        }

        private static void CopyToClipboardRecursiveRetry(string obj, int retryCount, int maxRetryCount)
        {
            if (obj == null) return;
            obj = obj.Replace('`', '\'');
            try
            {
                Clipboard.Clear();
                Thread.Sleep(50);
                Clipboard.SetDataObject(obj);
            }
            catch (COMException ex)
            {
                if (retryCount < maxRetryCount)
                {
                    Thread.Sleep(200);
                    CopyToClipboardRecursiveRetry(obj, retryCount + 1, maxRetryCount);
                }
            }
        }
    }
}