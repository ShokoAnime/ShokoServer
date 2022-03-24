namespace Shoko.Plugin.Abstractions
{
    public interface ISettingsProvider
    {
        void SaveSettings(IPluginSettings settings);
    }
}