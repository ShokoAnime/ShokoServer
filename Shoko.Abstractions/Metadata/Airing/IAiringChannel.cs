using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Where a run airs, from the shared channel registry. Channels are managed
///   through <c>IAiringScheduleService</c> only, and are never removed
///   automatically.
/// </summary>
/// <remarks>
///   A channel is an entry of its own, named
///   <c>shoko://channel/&lt;channel ID&gt;</c>, so images can be linked to it
///   like to any other entity. It is no <see cref="INetwork"/>, which is a
///   provider's network.
/// </remarks>
public interface IAiringChannel : IMetadata, IWithImages, IWithPrimaryImage
{
    /// <summary>
    ///   The ID of the channel, derived from its <see cref="Type"/> and its
    ///   normalised <see cref="Name"/>. <see cref="IMetadata.ID"/> holds the
    ///   same ID as text.
    /// </summary>
    Guid ChannelID { get; }

    /// <summary>
    ///   The display name of the channel, kept as it was first registered.
    ///   Regional channels carry their region in the name, e.g.
    ///   <c>Amazon (US)</c>.
    /// </summary>
    string Name { get; }

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
