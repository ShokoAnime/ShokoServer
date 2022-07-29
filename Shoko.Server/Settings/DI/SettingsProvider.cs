namespace Shoko.Server.Settings.DI
{
    public class SettingsProvider
    {
        public ServerSettings Settings => ServerSettings.Instance;
    }
}
