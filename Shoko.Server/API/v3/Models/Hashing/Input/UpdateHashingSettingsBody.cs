
#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing.Input;

/// <summary>
///   Settings for the hashing service's APIv3 REST API.
/// </summary>
public class UpdateHashingSettingsBody
{
    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode.
    /// </summary>
    public bool? ParallelMode { get; init; }
}
