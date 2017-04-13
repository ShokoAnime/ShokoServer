using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Shoko.Server;

namespace Shoko.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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
