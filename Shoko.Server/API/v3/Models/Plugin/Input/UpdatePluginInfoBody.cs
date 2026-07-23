
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

    /// <summary>
    ///   When <c>true</c>, bypass safety checks for the operation. Used to
    ///   force-disable a plugin that other enabled plugins depend on, or
    ///   force-uninstall a plugin with dependents.
    /// </summary>
    public bool Force { get; set; }
}
