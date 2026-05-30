using System;
using System.ComponentModel.DataAnnotations;

using AbstractPackageRepositoryInfo = Shoko.Abstractions.Plugin.Models.PackageRepositoryInfo;

#nullable enable
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
    /// Repository API endpoint URL.
    /// </summary>
    [Required]
    public string Url { get; init; } = repositoryInfo.Url;

    /// <summary>
    ///   When this repository was last synced, or <see langword="null"/> if it
    ///   hasn't been fetched yet.
    /// </summary>
    public DateTime? LastFetchedAt { get; init; } = repositoryInfo.LastFetchedAt;

    /// <summary>
    ///   Custom stale time for this specific repo. It uses the default stale
    ///   time if not set.
    /// </summary>
    public TimeSpan? StaleTime { get; init; } = repositoryInfo.StaleTime;
}
