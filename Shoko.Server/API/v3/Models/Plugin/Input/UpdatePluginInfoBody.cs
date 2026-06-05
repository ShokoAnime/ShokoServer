
namespace Shoko.Server.API.v3.Models.Plugin.Input;

public class UpdatePluginInfoBody
{
    /// <summary>
    ///   Set the plugin to be enabled or disabled after next startup.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    ///   Pin or unpin the plugin to prevent or allow automatic updates.
    /// </summary>
    public bool? IsPinned { get; set; }
}
