using System.Windows;

namespace JMMServer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            ServerSettings.LoadSettings();
            //ServerSettings.CreateDefaultConfig();
        }
    }
}