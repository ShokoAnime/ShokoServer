using System;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Contains information about an <see cref="IMetadataImageContributor"/>.
/// </summary>
public class MetadataImageContributorInfo
{
    /// <summary>
    ///   The unique ID of the contributor.
    /// </summary>
    /// <remarks>
    ///   Derived from the contributor's type and its plugin, so it survives a
    ///   rename and a reinstall. What a client refers to a contributor by.
    /// </remarks>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The version of the contributor.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The display name of the contributor.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what the contributor adds, or an empty string.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The <see cref="IMetadataImageContributor"/> that this info is for.
    /// </summary>
    public required IMetadataImageContributor Contributor { get; init; }

    /// <summary>
    ///   Information about the plugin that the contributor belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    ///   The source the contributor's images and their links are kept under.
    /// </summary>
    /// <remarks>
    ///   Read from <see cref="IMetadataImageContributor.Source"/> once, at
    ///   registration.
    /// </remarks>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   How many of the contributor's image jobs may run at once.
    /// </summary>
    /// <remarks>
    ///   Read from <see cref="IMetadataImageContributor.MaxConcurrentJobs"/>
    ///   once, at registration, with the core's default of two for none.
    /// </remarks>
    public required int MaxConcurrentJobs { get; init; }

    /// <summary>
    ///   The icon the contributor declared, extracted beside its plugin, or
    ///   <c>null</c> when it has none.
    /// </summary>
    public PackageImageInfo? Icon { get; init; }

    /// <summary>
    ///   The sources and kinds the contributor can add images for: its
    ///   declared <see cref="IMetadataImageContributor.Scope"/>, less the
    ///   pairs on its own <see cref="Source"/>.
    /// </summary>
    public required MetadataEntityScope AvailableScope { get; init; }

    /// <summary>
    ///   The sources and kinds the contributor is on for. Every pair of
    ///   <see cref="AvailableScope"/> until an admin turns it off.
    /// </summary>
    public required MetadataEntityScope EnabledScope { get; set; }

    /// <summary>
    ///   Whether the contributor is on for at least one pair.
    /// </summary>
    public bool Enabled => !EnabledScope.IsEmpty;
}
