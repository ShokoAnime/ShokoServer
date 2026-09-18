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
/// A normalised name resolves to at most one channel of a given type — a
/// channel's own name, or one channel's alias, never both — so the alias index
/// is safe to take a single answer from.
/// </remarks>
/// <param name="databaseFactory">The database factory.</param>
public class AiringChannelRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AiringChannel, int>(databaseFactory)
{
    private PocoIndex<int, AiringChannel, Guid>? _channelIDs;

    private PocoIndex<int, AiringChannel, (AiringChannelType Type, string NormalizedName)>? _names;

    private PocoIndex<int, AiringChannel, (AiringChannelType Type, string NormalizedName)>? _anyNames;

    /// <inheritdoc/>
    protected override int SelectKey(AiringChannel entity)
        => entity.AiringChannelID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
    {
        _channelIDs = Cache.CreateIndex(c => c.ChannelID);
        _names = Cache.CreateIndex(c => (c.Type, c.NormalizedName));
        _anyNames = Cache.CreateIndex(c => c.NormalizedNames.Select(name => (c.Type, name)));
    }

    /// <summary>
    /// Gets the channel with the given ID.
    /// </summary>
    /// <param name="channelID">The ID of the channel.</param>
    /// <returns>The channel, or <c>null</c> when there is none.</returns>
    public AiringChannel? GetByChannelID(Guid channelID)
        => _channelIDs!.GetOne(channelID);

    /// <summary>
    /// Gets the channel a name resolves to, in any spelling.
    /// </summary>
    /// <param name="name">The name of the channel, in any spelling.</param>
    /// <param name="type">The type of the channel.</param>
    /// <param name="useAliases">Whether an alias may answer. When <c>false</c> only a channel's own name does.</param>
    /// <returns>The channel, or <c>null</c> when no channel answers to the name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public AiringChannel? GetByName(string name, AiringChannelType type, bool useAliases = true)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalizedName = AiringScheduleUtility.NormalizeChannelName(name);
        return _names!.GetOne((type, normalizedName)) ?? (useAliases ? _anyNames!.GetOne((type, normalizedName)) : null);
    }

    /// <summary>
    /// Gets every channel whose own name, or one of whose aliases, matches the
    /// given name. Only more than one when the one-name-one-answer rule has
    /// been broken, which the service prevents.
    /// </summary>
    /// <param name="name">The name of the channel, in any spelling.</param>
    /// <param name="type">The type of the channel.</param>
    /// <returns>The channels, ordered by their display name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<AiringChannel> GetAllByName(string name, AiringChannelType type)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
            return [];

        return _anyNames!.GetMultiple((type, AiringScheduleUtility.NormalizeChannelName(name)))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }
}
