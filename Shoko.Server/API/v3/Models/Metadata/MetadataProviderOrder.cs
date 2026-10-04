using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// The providers claiming one kind of entry on a source, in the order they
/// are tried. The first enabled one answers; the rest stand by and take over,
/// in order, when it is turned off or removed.
/// </summary>
public class MetadataProviderOrder
{
    /// <summary>
    /// Describes the order of one kind of entry.
    /// </summary>
    /// <param name="entityType">The kind of entry.</param>
    /// <param name="providers">The providers, in order, with whether each is enabled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entityType"/> or <paramref name="providers"/> is <c>null</c>.</exception>
    public MetadataProviderOrder(MetadataEntityType entityType, IEnumerable<(MetadataProviderInfo Info, bool IsEnabled)> providers)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(providers);

        EntityType = entityType;
        var active = false;
        Providers = [.. providers.Select((provider, priority) =>
        {
            var isActive = provider.IsEnabled && !active;
            active |= isActive;
            return new MetadataProviderOrderEntry(provider.Info, priority, provider.IsEnabled, isActive);
        })];
    }

    /// <summary>
    /// The kind of entry, spelt as in <see cref="MetadataProvider.AvailableEntityTypes"/>.
    /// </summary>
    [Required]
    public MetadataEntityType EntityType { get; init; }

    /// <summary>
    /// Every provider claiming the kind on the source, in the order they are
    /// tried.
    /// </summary>
    [Required]
    public List<MetadataProviderOrderEntry> Providers { get; init; }
}

/// <summary>
/// One provider's place in the order of a kind of entry on a source.
/// </summary>
public class MetadataProviderOrderEntry
{
    /// <summary>
    /// Describes a provider's place.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="priority">Its place, from <c>0</c> for the first.</param>
    /// <param name="isEnabled">Whether it may answer.</param>
    /// <param name="isActive">Whether it is the one answering.</param>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <c>null</c>.</exception>
    public MetadataProviderOrderEntry(MetadataProviderInfo info, int priority, bool isEnabled, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(info);

        ProviderID = info.ID;
        Name = info.Name;
        PluginID = info.PluginInfo.ID;
        Priority = priority;
        IsEnabled = isEnabled;
        IsActive = isActive;
    }

    /// <summary>
    /// The provider's ID.
    /// </summary>
    [Required]
    public Guid ProviderID { get; init; }

    /// <summary>
    /// The provider's display name.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// The plugin the provider belongs to.
    /// </summary>
    [Required]
    public Guid PluginID { get; init; }

    /// <summary>
    /// The provider's place in the order, from <c>0</c> for the first.
    /// </summary>
    [Required]
    public int Priority { get; init; }

    /// <summary>
    /// Whether the provider may answer, now or once those before it are off.
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether the provider is the one answering: the first enabled one.
    /// </summary>
    [Required]
    public bool IsActive { get; init; }
}
