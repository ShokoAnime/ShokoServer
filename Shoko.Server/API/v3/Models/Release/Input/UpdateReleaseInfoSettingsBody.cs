
#nullable enable
namespace Shoko.Server.API.v3.Models.Release.Input;

/// <summary>
///   Settings for the release information service's APIv3 REST API.
/// </summary>
public class UpdateReleaseInfoSettingsBody
{
    /// <summary>
    ///   Sets a value indicating whether to use parallel mode.
    /// </summary>
    public bool? ParallelMode { get; init; }
}
