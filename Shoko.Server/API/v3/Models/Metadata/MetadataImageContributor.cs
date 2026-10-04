using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// An image contributor: a plugin's part that adds images from a source of
/// its own to entries of other sources, and the sources and kinds it is on
/// for.
/// </summary>
public class MetadataImageContributor
{
    /// <summary>
    /// Describes a contributor as it is registered now.
    /// </summary>
    /// <param name="info">The contributor's registration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
    public MetadataImageContributor(MetadataImageContributorInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        ID = info.ID;
        Name = info.Name;
        Description = info.Description ?? string.Empty;
        Version = info.Version;
        Source = info.Source;
        Plugin = new(info.PluginInfo);
        MaxConcurrentJobs = info.MaxConcurrentJobs;
        HasIcon = info.Icon is not null;
        Available = MetadataEntityScopeEntry.From(info.AvailableScope);
        Enabled = MetadataEntityScopeEntry.From(info.EnabledScope);
        IsEnabled = info.Enabled;
    }

    /// <summary>
    /// The contributor's unique ID.
    /// </summary>
    [Required]
    public Guid ID { get; init; }

    /// <summary>
    /// The contributor's display name.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// What the contributor adds, or an empty string.
    /// </summary>
    [Required]
    public string Description { get; init; }

    /// <summary>
    /// The contributor's version.
    /// </summary>
    [Required]
    public Version Version { get; init; }

    /// <summary>
    /// The source the contributor's images and their links are kept under.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; }

    /// <summary>
    /// The plugin the contributor belongs to.
    /// </summary>
    [Required]
    public PluginInfo Plugin { get; init; }

    /// <summary>
    /// How many of the contributor's image jobs may run at once.
    /// </summary>
    [Required]
    public int MaxConcurrentJobs { get; init; }

    /// <summary>
    /// Whether the contributor has an icon, served at
    /// <c>Metadata/ImageContributor/{contributorID}/Icon</c>.
    /// </summary>
    [Required]
    public bool HasIcon { get; init; }

    /// <summary>
    /// The sources and kinds the contributor can add images for.
    /// </summary>
    [Required]
    public IReadOnlyList<MetadataEntityScopeEntry> Available { get; init; }

    /// <summary>
    /// The sources and kinds the contributor is on for.
    /// </summary>
    [Required]
    public IReadOnlyList<MetadataEntityScopeEntry> Enabled { get; init; }

    /// <summary>
    /// Whether the contributor is on for at least one kind.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; }
}

/// <summary>
/// The kinds of entries a scope holds on one source.
/// </summary>
public class MetadataEntityScopeEntry
{
    /// <summary>
    /// The source.
    /// </summary>
    [Required, RegisteredMetadataValues]
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    /// The kinds of entries on the source.
    /// </summary>
    [Required, RegisteredMetadataValues]
    public List<MetadataEntityType> EntityTypes { get; set; } = [];

    /// <summary>
    /// A scope as one entry per source, the sources and kinds in order.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is <c>null</c>.</exception>
    public static List<MetadataEntityScopeEntry> From(MetadataEntityScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return [.. scope.Sources.Order().Select(source => new MetadataEntityScopeEntry { Source = source, EntityTypes = [.. scope.GetEntityTypes(source).Order()] })];
    }

    /// <summary>
    /// The scope a list of entries names.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The scope.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/>, or a source or kind in them, is <c>null</c>.</exception>
    public static MetadataEntityScope ToScope(IEnumerable<MetadataEntityScopeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return MetadataEntityScope.FromPairs(entries.SelectMany(entry => (entry.EntityTypes ?? []).Select(entityType => (entry.Source, entityType))));
    }
}
