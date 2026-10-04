using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every source's studios and networks, and the entries they worked
///   on or aired, in the store's own tables, cached in memory.
/// </summary>
/// <param name="studioRepository">The studios.</param>
/// <param name="entryRepository">The studios on entries.</param>
/// <param name="networkRepository">The networks.</param>
/// <param name="networkEntryRepository">The networks on entries.</param>
/// <param name="writer">Writes the changes.</param>
/// <param name="textStore">Writes the studios and networks, whose names stay on their rows, and tells the text manager.</param>
/// <param name="entityScheduler">Routes the refresh of the stub and stale studios and networks a write names, if set.</param>
public class MetadataStudioStore(
    Metadata_StudioRepository studioRepository,
    Metadata_Studio_EntryRepository entryRepository,
    Metadata_NetworkRepository networkRepository,
    Metadata_Network_EntryRepository networkEntryRepository,
    MetadataRowWriter writer,
    MetadataTextStore textStore,
    MetadataEntityRefreshScheduler? entityScheduler = null
) : IMetadataStudioStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    public IStudio? GetStudio(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Studio ? studioRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IStudio> GetStudios(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entryRepository.GetByEntry(entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> GetEntriesForStudio(MetadataGuid studio)
        => GetStudio(studio) is Metadata_Studio stored
            ? [.. MetadataEntryRow.InOrder(entryRepository.GetByStudioID(stored.Metadata_StudioID)).Select(row => row.EntryID).Distinct()]
            : [];

    /// <inheritdoc />
    public INetwork? GetNetwork(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Network ? networkRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<INetwork> GetNetworks(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return networkEntryRepository.GetByEntry(entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> GetEntriesForNetwork(MetadataGuid network)
        => GetNetwork(network) is Metadata_Network stored
            ? [.. MetadataEntryRow.InOrder(networkEntryRepository.GetByNetworkID(stored.Metadata_NetworkID)).Select(row => row.EntryID).Distinct()]
            : [];

    #endregion

    #region Writing

    /// <inheritdoc />
    public void SaveStudios(IEnumerable<MetadataStudioData> studios)
    {
        ArgumentNullException.ThrowIfNull(studios);

        lock (_writeLock)
        {
            var now = DateTime.Now;
            var saving = MetadataRows.Upsert(studioRepository, studios, MetadataEntityType.Studio, studio => studio.ID, (row, studio) =>
            {
                row.Source = studio.ID.Source;
                row.ProviderID = studio.ID.ID;
                row.Name = studio.Name ?? string.Empty;
                row.OriginalName = studio.OriginalName;
                row.CountryOfOrigin = string.IsNullOrEmpty(studio.CountryOfOrigin) ? null : studio.CountryOfOrigin;
                row.ExtraData = MetadataDefaultImages.Apply(row.ExtraData ?? new(), studio.DefaultImageResourceIDs).NullIfEmpty();
                row.LastUpdatedAt = now;
                row.LastOrphanedAt = IsUsed(row) ? null : now;
            });
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Studio>(studioRepository, saving, []));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A studio given twice in the same part keeps its first place.
    /// </remarks>
    public void SetStudios(MetadataGuid entry, IEnumerable<MetadataEntryStudioData> studios)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(studios);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = MetadataRows.FirstOfEach(studios, studio => (studio.StudioID, studio.Type));
        foreach (var item in items)
            MetadataEntries.CheckReference(item.StudioID, entry.Source, MetadataEntityType.Studio, nameof(studios));

        List<Metadata_Studio> named;
        lock (_writeLock)
        {
            // A studio not stored yet is stored as a stub first, and a stub with no name takes the one given.
            var now = DateTime.Now;
            var names = items.Select(item => (item.StudioID, item.StudioName)).ToList();
            var stubs = MetadataRows
                .Missing(names, id => studioRepository.GetByProviderID(id.Source, id.ID) is null)
                .Select(pair => new Metadata_Studio { Source = pair.ID.Source, ProviderID = pair.ID.ID, Name = pair.Name, LastOrphanedAt = now })
                .Concat(MetadataRows.Named(names, id => studioRepository.GetByProviderID(id.Source, id.ID)))
                .ToList();
            if (stubs.Count > 0)
                textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Studio>(studioRepository, stubs, []));

            var resolved = items
                .Select(item => (Item: item, StudioID: studioRepository.GetByProviderID(item.StudioID.Source, item.StudioID.ID)!.Metadata_StudioID))
                .ToList();
            var (saving, deleting) = MetadataRows.Replace(
                entryRepository.GetByEntry(entry),
                resolved,
                row => (row.StudioID, row.StudioType),
                studio => (studio.StudioID, studio.Item.Type),
                (row, studio, position) =>
                {
                    MetadataRows.Place(row, entry, position);
                    row.StudioID = studio.StudioID;
                    row.StudioType = studio.Item.Type;
                }
            );
            writer.Write(new MetadataRowChanges<Metadata_Studio_Entry>(entryRepository, saving, deleting));
            RestampStudios([.. saving.Concat(deleting).Select(row => row.StudioID)]);
            named = [.. resolved.Select(studio => studio.StudioID).Distinct().Select(studioRepository.GetByID).OfType<Metadata_Studio>()];
        }

        entityScheduler?.ScheduleDue(named);
    }

    /// <inheritdoc />
    public int RemoveStudios(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        lock (_writeLock)
        {
            var deleting = entryRepository.GetByEntry(entry);
            writer.Write(new MetadataRowChanges<Metadata_Studio_Entry>(entryRepository, [], deleting));
            RestampStudios([.. deleting.Select(row => row.StudioID)]);
            return deleting.Count;
        }
    }

    /// <inheritdoc />
    public void SaveNetworks(IEnumerable<MetadataNetworkData> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);

        lock (_writeLock)
        {
            var now = DateTime.Now;
            var saving = MetadataRows.Upsert(networkRepository, networks, MetadataEntityType.Network, network => network.ID, (row, network) =>
            {
                row.Source = network.ID.Source;
                row.ProviderID = network.ID.ID;
                row.Name = network.Name ?? string.Empty;
                row.CountryOfOrigin = string.IsNullOrEmpty(network.CountryOfOrigin) ? null : network.CountryOfOrigin;
                row.ExtraData = MetadataDefaultImages.Apply(row.ExtraData ?? new(), network.DefaultImageResourceIDs).NullIfEmpty();
                row.LastUpdatedAt = now;
                row.LastOrphanedAt = IsUsed(row) ? null : now;
            });
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Network>(networkRepository, saving, []));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A network given twice keeps its first place.
    /// </remarks>
    public void SetNetworks(MetadataGuid entry, IEnumerable<MetadataGuid> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        SetNetworks(entry, networks.Select(network => network is null ? null! : new MetadataEntryNetworkData { NetworkID = network }));
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A network given twice keeps its first place.
    /// </remarks>
    public void SetNetworks(MetadataGuid entry, IEnumerable<MetadataEntryNetworkData> networks)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(networks);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var given = networks.ToList();
        foreach (var item in given)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(networks));
            MetadataEntries.CheckReference(item.NetworkID, entry.Source, MetadataEntityType.Network, nameof(networks));
        }

        var items = MetadataRows.FirstOfEach(given, network => network.NetworkID);
        List<Metadata_Network> named;
        lock (_writeLock)
        {
            // A network not stored yet is stored as a stub first, and a stub with no name takes the one given.
            AddNetworkStubs(given.Select(item => (item.NetworkID, item.NetworkName)));
            var resolved = items
                .Select(item => networkRepository.GetByProviderID(item.NetworkID.Source, item.NetworkID.ID)!.Metadata_NetworkID)
                .ToList();
            LinkNetworks(entry, resolved);
            named = [.. resolved.Select(networkRepository.GetByID).OfType<Metadata_Network>()];
        }

        entityScheduler?.ScheduleDue(named);
    }

    /// <inheritdoc />
    public int RemoveNetworks(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        lock (_writeLock)
            return UnlinkNetworks(entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> RemoveOrphaned(MetadataSource source, DateTime orphanedBefore)
    {
        ArgumentNullException.ThrowIfNull(source);
        MetadataEntries.CheckWritableSource(source, nameof(source));

        lock (_writeLock)
        {
            var now = DateTime.Now;
            var studiosStamping = new List<Metadata_Studio>();
            var studiosDeleting = new List<Metadata_Studio>();
            foreach (var studio in studioRepository.GetBySource(source).Where(studio => !IsUsed(studio)))
            {
                if (studio.LastOrphanedAt is not { } orphanedAt)
                    studiosStamping.Add(Stamped(studio, now));
                else if (orphanedAt < orphanedBefore)
                    studiosDeleting.Add(studio);
            }

            var networksStamping = new List<Metadata_Network>();
            var networksDeleting = new List<Metadata_Network>();
            foreach (var network in networkRepository.GetBySource(source).Where(network => !IsUsed(network)))
            {
                if (network.LastOrphanedAt is not { } orphanedAt)
                    networksStamping.Add(Stamped(network, now));
                else if (orphanedAt < orphanedBefore)
                    networksDeleting.Add(network);
            }

            List<MetadataGuid> removed =
            [
                .. studiosDeleting.Select(studio => ((IMetadata)studio).ID),
                .. networksDeleting.Select(network => ((IMetadata)network).ID),
            ];
            textStore.WriteWithoutEntries(
                removed,
                new MetadataRowChanges<Metadata_Studio>(studioRepository, studiosStamping, studiosDeleting),
                new MetadataRowChanges<Metadata_Network>(networkRepository, networksStamping, networksDeleting)
            );
            return removed;
        }
    }

    #endregion

    #region Users' Orderings

    /// <summary>
    ///   Checks the networks a user's ordering names, before anything is
    ///   written: each must name a network on a source this server knows.
    ///   Any source will do, as the ordering is the user's own.
    /// </summary>
    /// <param name="networks">The networks, in order.</param>
    /// <param name="paramName">The argument they came in through.</param>
    /// <returns>The networks, in order, each once.</returns>
    /// <exception cref="ArgumentNullException">A network is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A network names another kind, or a source this server does not know.</exception>
    internal static IReadOnlyList<MetadataGuid> CheckUserOrderingNetworks(IEnumerable<MetadataGuid> networks, string paramName)
    {
        var items = networks.ToList();
        foreach (var network in items)
        {
            ArgumentNullException.ThrowIfNull(network, paramName);
            if (network.EntityType != MetadataEntityType.Network)
                throw new ArgumentException($"\"{network}\" does not name a network.", paramName);
            if (!network.Source.IsRegistered)
                throw new ArgumentException($"The network \"{network}\" is on {network.Source.Value}, a source this server does not know.", paramName);
        }

        return MetadataRows.FirstOfEach(items, network => network);
    }

    /// <summary>
    ///   Links a user's ordering to its networks, in order, first adding a
    ///   stub for each network not stored yet. Only the core calls this: the
    ///   store's own methods refuse to write under <c>user</c>.
    /// </summary>
    /// <param name="orderingID">The user's ordering.</param>
    /// <param name="networks">The networks, of any source this server knows.</param>
    /// <returns>The networks added as stubs.</returns>
    /// <exception cref="ArgumentException">The ID is not a user's ordering, or a network is refused by <see cref="CheckUserOrderingNetworks"/>.</exception>
    internal IReadOnlyList<MetadataGuid> SetUserOrderingNetworks(MetadataGuid orderingID, IEnumerable<MetadataGuid> networks)
    {
        CheckUserOrdering(orderingID);
        ArgumentNullException.ThrowIfNull(networks);
        var items = CheckUserOrderingNetworks(networks, nameof(networks));

        IReadOnlyList<MetadataGuid> stubs;
        List<Metadata_Network> named;
        lock (_writeLock)
        {
            stubs = AddNetworkStubs(items.Select(network => (network, (string?)null)));
            var resolved = items.Select(network => networkRepository.GetByProviderID(network.Source, network.ID)!.Metadata_NetworkID).ToList();
            LinkNetworks(orderingID, resolved);
            named = [.. resolved.Select(networkRepository.GetByID).OfType<Metadata_Network>()];
        }

        entityScheduler?.ScheduleDue(named);
        return stubs;
    }

    /// <summary>
    ///   Removes every network of a user's ordering. Only the core calls this.
    /// </summary>
    /// <param name="orderingID">The user's ordering.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="ArgumentException">The ID is not a user's ordering.</exception>
    internal int RemoveUserOrderingNetworks(MetadataGuid orderingID)
    {
        CheckUserOrdering(orderingID);

        lock (_writeLock)
            return UnlinkNetworks(orderingID);
    }

    /// <summary>
    ///   Checks that an ID names a user's ordering.
    /// </summary>
    /// <param name="orderingID">The ID.</param>
    /// <exception cref="ArgumentNullException">The ID is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The ID is not under <c>user</c> or does not name an ordering.</exception>
    private static void CheckUserOrdering(MetadataGuid orderingID)
    {
        ArgumentNullException.ThrowIfNull(orderingID);
        if (orderingID.Source != MetadataSource.User || orderingID.EntityType != MetadataEntityType.Ordering)
            throw new ArgumentException($"\"{orderingID}\" does not name a user's ordering.", nameof(orderingID));
    }

    #endregion

    #region Refresh State

    /// <summary>
    ///   Stamps when the core last asked the source to refresh a stored
    ///   studio or network, on its own row, leaving the rest of it as it is.
    /// </summary>
    /// <param name="id">The studio or network.</param>
    /// <param name="refreshedAt">When the refresh finished, in local time.</param>
    /// <returns><c>true</c> if the studio or network is stored.</returns>
    internal bool SetLastRefreshedAt(MetadataGuid id, DateTime refreshedAt)
    {
        lock (_writeLock)
        {
            // A copy, so the cached row stays as it was until the write has committed.
            if (id.EntityType == MetadataEntityType.Studio && studioRepository.GetByProviderID(id.Source, id.ID) is { } studio)
            {
                var row = MetadataRows.Copy(studio)!;
                row.LastRefreshedAt = refreshedAt;
                writer.Write(new MetadataRowChanges<Metadata_Studio>(studioRepository, [row], []));
                return true;
            }

            if (id.EntityType == MetadataEntityType.Network && networkRepository.GetByProviderID(id.Source, id.ID) is { } network)
            {
                var row = MetadataRows.Copy(network)!;
                row.LastRefreshedAt = refreshedAt;
                writer.Write(new MetadataRowChanges<Metadata_Network>(networkRepository, [row], []));
                return true;
            }

            return false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Stores a stub for each network named that is not stored yet, with
    ///   the first name given for it, and names a stored stub that has none.
    ///   Called under the write lock, once the networks are checked.
    /// </summary>
    /// <remarks>
    ///   A plugin source's stub is stamped as orphaned until its link is
    ///   written, so a write that fails after leaves it to the purge.
    /// </remarks>
    /// <param name="networks">The networks named, with the names given for them.</param>
    /// <returns>The networks stored as stubs.</returns>
    private IReadOnlyList<MetadataGuid> AddNetworkStubs(IEnumerable<(MetadataGuid ID, string? Name)> networks)
    {
        var now = DateTime.Now;
        var names = networks.ToList();
        var named = MetadataRows.Named(names, id => networkRepository.GetByProviderID(id.Source, id.ID));
        var stubs = MetadataRows.Missing(names, id => networkRepository.GetByProviderID(id.Source, id.ID) is null)
            .Select(pair => new Metadata_Network
            {
                Source = pair.ID.Source,
                ProviderID = pair.ID.ID,
                Name = pair.Name,
                LastOrphanedAt = pair.ID.Source.IsCore ? null : now,
            })
            .ToList();
        if (stubs.Count + named.Count > 0)
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Network>(networkRepository, [.. stubs, .. named], []));

        return [.. stubs.Select(stub => ((IMetadata)stub).ID)];
    }

    /// <summary>
    ///   Replaces an entry's networks, in order, and stamps the networks it
    ///   changed. Called under the write lock.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="networkIDs">The store's IDs of the networks, in order, each once.</param>
    private void LinkNetworks(MetadataGuid entry, IReadOnlyList<int> networkIDs)
    {
        var (saving, deleting) = MetadataRows.Replace(
            networkEntryRepository.GetByEntry(entry),
            networkIDs,
            row => row.NetworkID,
            networkID => networkID,
            (row, networkID, position) =>
            {
                MetadataRows.Place(row, entry, position);
                row.NetworkID = networkID;
            },
            (stored, row) => stored.Ordering == row.Ordering
        );
        writer.Write(new MetadataRowChanges<Metadata_Network_Entry>(networkEntryRepository, saving, deleting));
        RestampNetworks([.. saving.Concat(deleting).Select(row => row.NetworkID)]);
    }

    /// <summary>
    ///   Removes every network of an entry and stamps them. Called under the
    ///   write lock.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many links were removed.</returns>
    private int UnlinkNetworks(MetadataGuid entry)
    {
        var deleting = networkEntryRepository.GetByEntry(entry);
        writer.Write(new MetadataRowChanges<Metadata_Network_Entry>(networkEntryRepository, [], deleting));
        RestampNetworks([.. deleting.Select(row => row.NetworkID)]);
        return deleting.Count;
    }

    /// <summary>
    ///   Stamps the studios whose entries a write changed: one no entry names
    ///   any more is orphaned from now, and one named again is not orphaned
    ///   any more. Called under the write lock, once the entries are written.
    /// </summary>
    /// <param name="studioIDs">The store's IDs of the studios whose entries changed.</param>
    private void RestampStudios(IReadOnlyCollection<int> studioIDs)
    {
        var now = DateTime.Now;
        var studios = studioIDs.Distinct()
            .Select(studioRepository.GetByID)
            .OfType<Metadata_Studio>()
            .Where(studio => IsUsed(studio) == studio.LastOrphanedAt.HasValue)
            .Select(studio => Stamped(studio, IsUsed(studio) ? null : now))
            .ToList();
        if (studios.Count > 0)
            writer.Write(new MetadataRowChanges<Metadata_Studio>(studioRepository, studios, []));
    }

    /// <summary>
    ///   Stamps the networks whose entries a write changed, as
    ///   <see cref="RestampStudios"/> does the studios. A core source's stub
    ///   goes with its last link instead, as no purge runs for core sources.
    /// </summary>
    /// <param name="networkIDs">The store's IDs of the networks whose entries changed.</param>
    private void RestampNetworks(IReadOnlyCollection<int> networkIDs)
    {
        var now = DateTime.Now;
        var changed = networkIDs.Distinct()
            .Select(networkRepository.GetByID)
            .OfType<Metadata_Network>()
            .ToList();
        var deleting = changed
            .Where(network => network.IsStub && network.Source.IsCore && !IsUsed(network))
            .ToList();
        var stamping = changed
            .Except(deleting)
            .Where(network => IsUsed(network) == network.LastOrphanedAt.HasValue)
            .Select(network => Stamped(network, IsUsed(network) ? null : now))
            .ToList();
        if (stamping.Count + deleting.Count > 0)
            writer.Write(new MetadataRowChanges<Metadata_Network>(networkRepository, stamping, deleting));
    }

    /// <summary>
    ///   Whether any entry names a stored studio.
    /// </summary>
    /// <param name="studio">The studio; a new one is named by nothing.</param>
    /// <returns><c>true</c> when an entry does.</returns>
    private bool IsUsed(Metadata_Studio studio)
        => studio.Metadata_StudioID is not 0 && entryRepository.GetByStudioID(studio.Metadata_StudioID).Count > 0;

    /// <summary>
    ///   Whether any entry names a stored network.
    /// </summary>
    /// <param name="network">The network; a new one is named by nothing.</param>
    /// <returns><c>true</c> when an entry does.</returns>
    private bool IsUsed(Metadata_Network network)
        => network.Metadata_NetworkID is not 0 && networkEntryRepository.GetByNetworkID(network.Metadata_NetworkID).Count > 0;

    /// <summary>
    ///   A copy of a stored studio with its orphan stamp set or cleared.
    /// </summary>
    /// <param name="studio">The stored studio.</param>
    /// <param name="orphanedAt">The stamp, or <c>null</c> to clear it.</param>
    /// <returns>The copy.</returns>
    private static Metadata_Studio Stamped(Metadata_Studio studio, DateTime? orphanedAt)
    {
        var copy = MetadataRows.Copy(studio)!;
        copy.LastOrphanedAt = orphanedAt;
        return copy;
    }

    /// <summary>
    ///   A copy of a stored network with its orphan stamp set or cleared.
    /// </summary>
    /// <param name="network">The stored network.</param>
    /// <param name="orphanedAt">The stamp, or <c>null</c> to clear it.</param>
    /// <returns>The copy.</returns>
    private static Metadata_Network Stamped(Metadata_Network network, DateTime? orphanedAt)
    {
        var copy = MetadataRows.Copy(network)!;
        copy.LastOrphanedAt = orphanedAt;
        return copy;
    }

    #endregion
}
