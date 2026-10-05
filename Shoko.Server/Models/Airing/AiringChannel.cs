using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Airing;

/// <summary>
/// Where a run airs, from the shared channel registry. A channel is a metadata
/// entity, so images can be linked to it like to any other entity, and it is
/// never removed automatically.
/// </summary>
/// <remarks>
/// A channel is keyed by its type, its normalised name and its country, so the
/// same station spelled differently by two providers ends up on one ID, while
/// two names that never normalise alike stay two channels until merged. The service hands it out as an
/// <see cref="IAiringChannel"/> through <c>AiringChannelView</c>.
/// </remarks>
public class AiringChannel : IMetadata
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int AiringChannelID { get; set; }

    /// <summary>
    /// The public ID of the channel, derived from its <see cref="Type"/>, its
    /// <see cref="NormalizedName"/> and its <see cref="CountryCode"/>.
    /// </summary>
    public Guid ChannelID { get; set; }

    /// <summary>
    /// The display name of the channel, kept as it was first registered. It
    /// never carries the country.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The country the channel is for, as an upper-case ISO 3166-1 alpha-2
    /// code, or <c>null</c> for a global or unknown one. Part of the key.
    /// </summary>
    public string? CountryCode { get; set; }

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
    /// alias, of the channel's type and country, returns this channel, but an
    /// alias never changes an ID or widens a filter.
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
    /// aliases. One name resolves to at most one channel of a given type and
    /// country, so this is safe to index on.
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
    /// Registers a new channel under the given name, type and country.
    /// </summary>
    /// <param name="name">The display name of the channel.</param>
    /// <param name="type">The type of the channel.</param>
    /// <param name="countryCode">The country of the channel, or <c>null</c> when it has none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank, or <paramref name="countryCode"/> is not two letters.</exception>
    public AiringChannel(string name, AiringChannelType type, string? countryCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        CreatedAt = DateTime.UtcNow;
        Type = type;
        SetKey(name, countryCode);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sets the display name and the country, and the normalised name and the
    /// ID that follow from them.
    /// </summary>
    /// <param name="name">The display name of the channel.</param>
    /// <param name="countryCode">The country of the channel, or <c>null</c> when it has none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank, or <paramref name="countryCode"/> is not two letters.</exception>
    public void SetKey(string name, string? countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        NormalizedName = AiringScheduleUtility.NormalizeChannelName(Name);
        CountryCode = AiringScheduleUtility.NormalizeCountryCode(countryCode);
        ChannelID = AiringScheduleUtility.GetChannelID(Name, Type, CountryCode);
    }

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.Shoko, MetadataEntityType.Channel, ChannelID.ToString());

    #endregion
}
