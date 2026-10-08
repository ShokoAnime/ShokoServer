using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Airing;

namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// Where a run airs, from the shared channel registry. Channels are registered
/// by the providers that use them; an admin manages their aliases and merges
/// them.
/// </summary>
/// <param name="channel">The channel.</param>
/// <exception cref="ArgumentNullException"><paramref name="channel"/> is <c>null</c>.</exception>
public class AiringChannel(IAiringChannel channel)
{
    /// <summary>
    /// The ID of the channel, derived from its <see cref="Type"/>, its
    /// normalised <see cref="Name"/> and its <see cref="CountryCode"/>.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = channel.ChannelID;

    /// <summary>
    /// The display name of the channel, kept as it was first registered. It
    /// never carries the country.
    /// </summary>
    [Required]
    public string Name { get; init; } = channel.Name;

    /// <summary>
    /// The country the channel is for, as an upper-case ISO 3166-1 alpha-2
    /// code, or <c>null</c> for a global service or an unknown country. Part
    /// of the channel's identity.
    /// </summary>
    public string? CountryCode { get; init; } = channel.CountryCode;

    /// <summary>
    /// What kind of channel it is. The type is part of the channel's identity,
    /// so the same name with another type is another channel.
    /// </summary>
    [Required]
    public AiringChannelType Type { get; init; } = channel.Type;

    /// <summary>
    /// Other names the channel answers to, among the channels of its type and
    /// country. A merge adds the names of the merged channels here. An alias
    /// never changes an ID or widens a filter.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Aliases { get; init; } = [.. channel.Aliases];

    /// <summary>
    /// Whether the server hides the channel: airing reads leave it out unless
    /// they name it in <c>channel</c>.
    /// </summary>
    [Required]
    public bool IsHidden { get; init; } = channel.IsHidden;

    /// <summary>
    /// When the channel was first registered.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; } = channel.CreatedAt.ToUtc();
}

/// <summary>
/// The slim shape of a channel, as it appears on a schedule or an airing. Use
/// the channel routes for the aliases and the registration date.
/// </summary>
/// <param name="channel">The channel.</param>
/// <exception cref="ArgumentNullException"><paramref name="channel"/> is <c>null</c>.</exception>
public class AiringChannelReference(IAiringChannel channel)
{
    /// <summary>
    /// The ID of the channel.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = channel.ChannelID;

    /// <summary>
    /// The display name of the channel.
    /// </summary>
    [Required]
    public string Name { get; init; } = channel.Name;

    /// <summary>
    /// The country the channel is for, or <c>null</c> for a global service or
    /// an unknown country.
    /// </summary>
    public string? CountryCode { get; init; } = channel.CountryCode;

    /// <summary>
    /// What kind of channel it is.
    /// </summary>
    [Required]
    public AiringChannelType Type { get; init; } = channel.Type;
}
