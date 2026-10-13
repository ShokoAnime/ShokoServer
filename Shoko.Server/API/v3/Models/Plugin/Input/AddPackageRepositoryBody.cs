using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
    ///   Optional. What the repository holds and who stands behind it. It
    ///   cannot be changed once the repository is added.
    /// </summary>
    public string? Description { get; set; }

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

    /// <summary>
    ///   Whether the repository is added enabled. A disabled one is added
    ///   without being synced.
    /// </summary>
    [DefaultValue(true)]
    public bool IsEnabled { get; set; } = true;
}
