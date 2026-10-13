using System;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Data transfer object (DTO) for adding a new package repository.
/// </summary>
public sealed class PackageRepositoryData
{
    /// <summary>
    ///   Friendly name of the repository.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///   Optional. What the repository holds and who stands behind it. It
    ///   cannot be changed once the repository is added.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   Repository API endpoint URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    ///   Optional. Custom stale time for this specific repository.
    /// </summary>
    public TimeSpan? StaleTime { get; set; }

    /// <summary>
    ///   Optional. Whether the repository is added enabled. A disabled one
    ///   is added without being synced.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
