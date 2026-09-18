using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Airing;

/// <summary>
/// Where a run airs, from the shared channel registry. A channel is a metadata
/// entity, so images can be linked to it like to any other entity, and it is
/// never removed automatically.
/// </summary>
/// <remarks>
/// A channel is keyed by its type and its normalised name, so the same station
/// spelled differently by two providers ends up on one ID, while two names that
/// never normalise alike stay two channels.
/// </remarks>
public class AiringChannel : IAiringChannel
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int AiringChannelID { get; set; }

    /// <summary>
    /// The public ID of the channel, derived from its <see cref="Type"/> and
    /// its <see cref="NormalizedName"/>.
    /// </summary>
    public Guid ChannelID { get; set; }

    /// <summary>
    /// The display name of the channel, kept as it was first registered.
    /// Regional channels carry their region in the name, e.g.
    /// <c>Amazon (US)</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The normalised form of <see cref="Name"/>, which is what lookups and the
    /// channel's ID are built on.
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>
    /// What kind of channel it is. The type is part of the channel's identity,
    /// so the same name with another type is another channel.
    /// </summary>
    public AiringChannelType Type { get; set; }

    /// <summary>
    /// Other names for the channel, stored as a JSON array. A lookup by an
    /// alias returns this channel, but an alias never changes an ID, merges
    /// channels or widens a filter.
    /// </summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    /// When the channel was first registered.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Every name the channel answers to, normalised: its own first, then its
    /// aliases. One name resolves to at most one channel of a given type, so
    /// this is safe to index on.
    /// </summary>
    public IReadOnlyList<string> NormalizedNames
        => Aliases.Count is 0
            ? [NormalizedName]
            : Aliases
                .Select(AiringScheduleUtility.NormalizeChannelName)
                .Prepend(NormalizedName)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public AiringChannel() { }

    /// <summary>
    /// Registers a new channel under the given name and type.
    /// </summary>
    /// <param name="name">The display name of the channel.</param>
    /// <param name="type">The type of the channel.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public AiringChannel(string name, AiringChannelType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        NormalizedName = AiringScheduleUtility.NormalizeChannelName(Name);
        Type = type;
        ChannelID = AiringScheduleUtility.GetChannelID(Name, type);
        CreatedAt = DateTime.UtcNow;
    }

    #endregion

    #region IMetadata Implementation

    Guid IMetadata<Guid>.ID => ChannelID;

    DataEntityType IMetadata.EntityType => DataEntityType.Channel;

    DataSource IMetadata.Source => DataSource.Shoko;

    #endregion

    #region IAiringChannel Implementation

    Guid IAiringChannel.ID => ChannelID;

    IReadOnlyList<string> IAiringChannel.Aliases => Aliases;

    #endregion
}
