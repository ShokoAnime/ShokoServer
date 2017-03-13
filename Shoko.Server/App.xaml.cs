using System;
using System.Windows;

namespace Shoko.Server
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
        }

        protected override void OnStartup(StartupEventArgs e)
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
            ServerSettings.LoadSettings();
            base.OnStartup(e);
        }
    }
}