using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin.Input;

/// <summary>
/// Request body for adding a new package repository.
/// </summary>
public class AddPackageRepositoryBody
{
    /// <summary>
    ///   Friendly name of the repository.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string Name { get; set; }

    /// <summary>
    ///   Repository API endpoint URL. Must be an absolute URL using http:// or https://.
    /// </summary>
    [Required]
    [Url]
    public required string Url { get; set; }

    /// <summary>
    ///   Optional custom stale time for this specific repository.
    ///   If not set, the default stale time will be used.
    /// </summary>
    public TimeSpan? StaleTime { get; set; }
}
