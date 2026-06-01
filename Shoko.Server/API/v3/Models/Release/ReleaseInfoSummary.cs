using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
///   Summary for the video release service's APIv3 REST API.
/// </summary>
public class ReleaseInfoSummary
{
    /// <summary>
    ///   Gets the number of available hash providers to pick from.
    /// </summary>
    [Required]
    public required int ProviderCount { get; init; }
}
