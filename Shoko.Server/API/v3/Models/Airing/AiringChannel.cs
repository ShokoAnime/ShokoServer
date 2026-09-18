using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// Where a run airs, from the shared channel registry. Channels are managed by
/// the providers that use them, so everything here is read-only over REST.
/// </summary>
/// <param name="channel">The channel.</param>
/// <exception cref="ArgumentNullException"><paramref name="channel"/> is <c>null</c>.</exception>
public class AiringChannel(IAiringChannel channel)
{
    /// <summary>
    /// The ID of the channel, derived from its <see cref="Type"/> and its
    /// normalised <see cref="Name"/>.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = channel.ID;

    /// <summary>
    /// The display name of the channel, kept as it was first registered.
    /// Regional services carry their region in the name, e.g.
    /// <c>Amazon (US)</c>.
    /// </summary>
    [Required]
    public string Name { get; init; } = channel.Name;

    /// <summary>
    /// What kind of channel it is. The type is part of the channel's identity,
    /// so the same name with another type is another channel.
    /// </summary>
    [Required]
    public AiringChannelType Type { get; init; } = channel.Type;

    /// <summary>
    /// Other names the channel answers to. An alias never changes an ID, merges
    /// channels or widens a filter.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Aliases { get; init; } = [.. channel.Aliases];

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
    public Guid ID { get; init; } = channel.ID;

    /// <summary>
    /// The display name of the channel.
    /// </summary>
    [Required]
    public string Name { get; init; } = channel.Name;

    /// <summary>
    /// What kind of channel it is.
    /// </summary>
    [Required]
    public AiringChannelType Type { get; init; } = channel.Type;
}
