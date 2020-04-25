using System;
using System.Windows;
using Shoko.Server;
using Shoko.Server.Settings;

namespace Shoko.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void OnStartup(object a, StartupEventArgs e)
        {
            for (int x = 0; x < e.Args.Length; x++)
            {
                if (e.Args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (x + 1 < e.Args.Length)
                    {
                        ServerSettings.DefaultInstance = e.Args[x + 1];
                    }
                }
            }
            ShokoServer.Instance.InitLogger();
            ServerSettings.LoadSettings();
            var main = new MainWindow();
            main.Show();
        }
    }
}
