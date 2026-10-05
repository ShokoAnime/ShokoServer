using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Databases;
using Shoko.Server.Models.Airing;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Airing;

/// <summary>
/// Cached repository for <see cref="AiringChannel"/>, the shared channel
/// registry.
/// </summary>
/// <remarks>
/// A normalised name resolves to at most one channel of a given type and
/// country — a channel's own name, or one channel's alias, never both — so the
/// alias index is safe to take a single answer from.
/// </remarks>
/// <param name="databaseFactory">The database factory.</param>
public class AiringChannelRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AiringChannel, int>(databaseFactory)
{
    private PocoIndex<int, AiringChannel, Guid>? _channelIDs;

    private PocoIndex<int, AiringChannel, (AiringChannelType Type, string CountryCode, string NormalizedName)>? _names;

    private PocoIndex<int, AiringChannel, (AiringChannelType Type, string CountryCode, string NormalizedName)>? _anyNames;

    private PocoIndex<int, AiringChannel, (AiringChannelType Type, string NormalizedName)>? _namesInAnyCountry;

    /// <inheritdoc/>
    protected override int SelectKey(AiringChannel entity)
        => entity.AiringChannelID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
    {
        _channelIDs = Cache.CreateIndex(c => c.ChannelID);
        _names = Cache.CreateIndex(c => (c.Type, c.CountryCode ?? string.Empty, c.NormalizedName));
        _anyNames = Cache.CreateIndex(c => c.NormalizedNames.Select(name => (c.Type, c.CountryCode ?? string.Empty, name)));
        _namesInAnyCountry = Cache.CreateIndex(c => (c.Type, c.NormalizedName));
    }

    /// <summary>
    /// Gets the channel with the given ID.
    /// </summary>
    /// <param name="channelID">The ID of the channel.</param>
    /// <returns>The channel, or <c>null</c> when there is none.</returns>
    public AiringChannel? GetByChannelID(Guid channelID)
        => _channelIDs!.GetOne(channelID);

    /// <summary>
    /// Gets the channel a name resolves to, in any spelling, among the channels
    /// of one type and country. Own names answer before aliases.
    /// </summary>
    /// <param name="name">The name of the channel, in any spelling.</param>
    /// <param name="type">The type of the channel.</param>
    /// <param name="countryCode">The normalised country of the channel, or <c>null</c> for the channels with none.</param>
    /// <param name="useAliases">Whether an alias may answer. When <c>false</c> only a channel's own name does.</param>
    /// <returns>The channel, or <c>null</c> when no channel answers to the name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public AiringChannel? GetByName(string name, AiringChannelType type, string? countryCode, bool useAliases = true)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var key = (type, countryCode ?? string.Empty, AiringScheduleUtility.NormalizeChannelName(name));
        return _names!.GetOne(key) ?? (useAliases ? _anyNames!.GetOne(key) : null);
    }

    /// <summary>
    /// Gets every channel of one type and country whose own name, or one of
    /// whose aliases, matches the given name. Only more than one when the
    /// one-name-one-answer rule has been broken, which the service prevents.
    /// </summary>
    /// <param name="name">The name of the channel, in any spelling.</param>
    /// <param name="type">The type of the channel.</param>
    /// <param name="countryCode">The normalised country of the channel, or <c>null</c> for the channels with none.</param>
    /// <returns>The channels, ordered by their display name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public IReadOnlyList<AiringChannel> GetAllByName(string name, AiringChannelType type, string? countryCode)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            return [];

        return _anyNames!.GetMultiple((type, countryCode ?? string.Empty, AiringScheduleUtility.NormalizeChannelName(name)))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Gets every channel of one type whose own name matches the given name,
    /// in every country and without one. Aliases are not matched.
    /// </summary>
    /// <param name="name">The name of the channel, in any spelling.</param>
    /// <param name="type">The type of the channel.</param>
    /// <returns>The channels, ordered by their country, those without one first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public IReadOnlyList<AiringChannel> GetAllByNameInAnyCountry(string name, AiringChannelType type)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            return [];

        return _namesInAnyCountry!.GetMultiple((type, AiringScheduleUtility.NormalizeChannelName(name)))
            .OrderBy(c => c.CountryCode, StringComparer.Ordinal)
            .ToList();
    }
}
