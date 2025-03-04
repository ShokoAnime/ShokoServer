
#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
///   Summary for the video release service's APIv3 REST API.
/// </summary>
public class ReleaseInfoSummary
{
    /// <summary>
    ///   Gets a value indicating whether to use parallel mode.
    /// </summary>
    public required bool ParallelMode { get; init; }

    /// <summary>
    ///   Gets the number of available hash providers to pick from.
    /// </summary>
    public required int ProviderCount { get; init; }
}
