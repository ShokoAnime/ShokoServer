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
    ///   Repository API endpoint URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    ///   Optional. Custom stale time for this specific repository.
    /// </summary>
    public TimeSpan? StaleTime { get; set; }
}
