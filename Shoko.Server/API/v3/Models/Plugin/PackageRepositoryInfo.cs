using System;
using System.ComponentModel.DataAnnotations;

using AbstractPackageRepositoryInfo = Shoko.Abstractions.Plugin.Models.PackageRepositoryInfo;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Information about a repository containing one or more package manifests.
/// </summary>
public class PackageRepositoryInfo(AbstractPackageRepositoryInfo repositoryInfo)
{
    /// <summary>
    /// Unique repository identifier based on the URL.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = repositoryInfo.ID;

    /// <summary>
    /// Repository identifier/name.
    /// </summary>
    [Required]
    public string Name { get; init; } = repositoryInfo.Name;

    /// <summary>
    ///   What the repository holds and who stands behind it, as given when
    ///   it was added. <c>null</c> when none was given.
    /// </summary>
    public string? Description { get; init; } = repositoryInfo.Description;

    /// <summary>
    /// Repository API endpoint URL.
    /// </summary>
    [Required]
    public string Url { get; init; } = repositoryInfo.Url;

    /// <summary>
    ///   When this repository was last synced, or <c>null</c> if it
    ///   hasn't been fetched yet.
    /// </summary>
    public DateTime? LastFetchedAt { get; init; } = repositoryInfo.LastFetchedAt;

    /// <summary>
    ///   Custom stale time for this specific repo. It uses the default stale
    ///   time if not set.
    /// </summary>
    public TimeSpan? StaleTime { get; init; } = repositoryInfo.StaleTime;

    /// <summary>
    ///   Whether the repository is in use. A disabled repository is kept but
    ///   not synced, and its packages are not offered.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; } = repositoryInfo.IsEnabled;
}
