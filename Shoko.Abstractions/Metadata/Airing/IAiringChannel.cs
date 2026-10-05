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
    ///   The ID of the channel, derived from its <see cref="Type"/>, its
    ///   normalised <see cref="Name"/> and its <see cref="CountryCode"/>.
    ///   <see cref="IMetadata.ID"/> holds the same ID as text.
    /// </summary>
    Guid ChannelID { get; }

    /// <summary>
    ///   The display name of the channel, kept as it was first registered. It
    ///   never carries the country, which is <see cref="CountryCode"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The country the channel is for, as an upper-case ISO 3166-1 alpha-2
    ///   code. A TV station has one when its provider knows it. A streaming
    ///   service has one only when the service itself is regional, e.g. Hulu
    ///   Japan, and <c>null</c> when it is global, e.g. Netflix.
    /// </summary>
    /// <remarks>
    ///   The country is part of the channel's identity, so <c>ABC</c> in
    ///   <c>JP</c> and <c>ABC</c> in <c>US</c> are two channels. A lookup in a
    ///   country with no match there falls back to a channel without one, and
    ///   a TV station registered in a country that way takes it.
    /// </remarks>
    string? CountryCode { get; }

    /// <summary>
    ///   What kind of channel it is. The type is part of the channel's
    ///   identity, so the same name with another type is another channel.
    /// </summary>
    AiringChannelType Type { get; }

    /// <summary>
    ///   Other names for the channel. A lookup by an alias, of the channel's
    ///   type and country, returns this channel, so a provider registering an
    ///   alias is handed this channel. An alias never changes an ID or widens
    ///   a filter. A merge adds the names of the merged channels here.
    /// </summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>
    ///   Whether the server hides the channel: an airing read naming no
    ///   channels leaves its airings out. Set it through
    ///   <see cref="Services.IAiringScheduleService.SetChannelHidden"/>.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    ///   When the channel was first registered.
    /// </summary>
    DateTime CreatedAt { get; }
}
