using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Where a run airs, from the shared channel registry. Channels are managed
///   through <c>IAiringScheduleService</c> only, and are never removed
///   automatically.
/// </summary>
/// <remarks>
///   A channel is a metadata entity with <see cref="IMetadata.Source"/> set to
///   <see cref="DataSource.Shoko"/> and <see cref="IMetadata.EntityType"/> set
///   to <see cref="DataEntityType.Channel"/>, so images can be linked to it
///   like to any other entity.
/// </remarks>
public interface IAiringChannel : INetwork, IMetadata<Guid>
{
    /// <summary>
    ///   The ID of the channel, derived from its <see cref="Type"/> and its
    ///   normalised <see cref="Name"/>.
    /// </summary>
    new Guid ID { get; }

    /// <summary>
    ///   The display name of the channel, kept as it was first registered.
    ///   Regional channels carry their region in the name, e.g.
    ///   <c>Amazon (US)</c>.
    /// </summary>
    new string Name { get; }

    /// <summary>
    ///   What kind of channel it is. The type is part of the channel's
    ///   identity, so the same name with another type is another channel.
    /// </summary>
    AiringChannelType Type { get; }

    /// <summary>
    ///   Other names for the channel. A lookup by an alias returns this
    ///   channel, but an alias never changes an ID, merges channels or widens a
    ///   filter.
    /// </summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>
    ///   When the channel was first registered.
    /// </summary>
    DateTime CreatedAt { get; }
}
