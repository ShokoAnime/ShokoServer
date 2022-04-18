namespace Shoko.Plugin.Abstractions
{
    /// <summary>
    /// This interface is primarily an identifier. The final model is serialized as json using the Server Settings manager.
    /// The Settings are saved in the data folder under Plugins. There can be only one! (per assembly)
    /// </summary>
    public interface IPluginSettings
    {
        
    }
}