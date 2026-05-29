using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing;

/// <summary>
///   Summary for the hashing service's APIv3 REST API.
/// </summary>
public class HashingSummary
{
    /// <summary>
    ///   Gets or sets a value indicating whether to use parallel mode.
    /// </summary>
    [Required]
    public required bool ParallelMode { get; init; }

    /// <summary>
    ///   Gets the number of available hash providers to pick from.
    /// </summary>
    [Required]
    public required int ProviderCount { get; init; }

    /// <summary>
    ///   Gets the set of all available hash types across all providers.
    /// </summary>
    [Required]
    public required IReadOnlySet<string> AllAvailableHashTypes { get; init; }

    /// <summary>
    ///   Gets the set of all enabled hash types across all providers.
    /// </summary>
    [Required]
    public required IReadOnlySet<string> AllEnabledHashTypes { get; init; }
}
