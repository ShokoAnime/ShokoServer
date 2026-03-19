
#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin.Input;

/// <summary>
/// Request body for checking for plugin updates.
/// </summary>
public class CheckForUpdatesBody
{
    /// <summary>
    ///   Force sync even if repositories are not stale.
    ///   If null, checks the configured schedule.
    /// </summary>
    public bool? ForceSync { get; set; }

    /// <summary>
    ///   Whether to upgrade enabled plugins automatically.
    ///   If null, uses settings default.
    /// </summary>
    public bool? PerformUpgrade { get; set; }
}
