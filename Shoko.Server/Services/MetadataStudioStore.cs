using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
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
public class MetadataStudioStore(
    Metadata_StudioRepository studioRepository,
    Metadata_Studio_EntryRepository entryRepository,
    Metadata_NetworkRepository networkRepository,
    Metadata_Network_EntryRepository networkEntryRepository,
    MetadataRowWriter writer,
    MetadataTextStore textStore
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

        lock (_writeLock)
        {
            // Every studio is looked up before anything is written, so one
            // that is missing leaves the entry as it was.
            var resolved = items
                .Select(item => (
                    Item: item,
                    StudioID: studioRepository.GetByProviderID(item.StudioID.Source, item.StudioID.ID)?.Metadata_StudioID
                        ?? throw new ArgumentException($"The studio \"{item.StudioID}\" is not stored.", nameof(studios))
                ))
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
        }
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
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(networks);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = MetadataRows.FirstOfEach(networks, network => network);
        foreach (var item in items)
            MetadataEntries.CheckReference(item, entry.Source, MetadataEntityType.Network, nameof(networks));

        lock (_writeLock)
        {
            // Every network is looked up before anything is written, so one
            // that is missing leaves the entry as it was.
            var resolved = items
                .Select(item => networkRepository.GetByProviderID(item.Source, item.ID)?.Metadata_NetworkID
                    ?? throw new ArgumentException($"The network \"{item}\" is not stored.", nameof(networks)))
                .ToList();
            var (saving, deleting) = MetadataRows.Replace(
                networkEntryRepository.GetByEntry(entry),
                resolved,
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
    }

    /// <inheritdoc />
    public int RemoveNetworks(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        lock (_writeLock)
        {
            var deleting = networkEntryRepository.GetByEntry(entry);
            writer.Write(new MetadataRowChanges<Metadata_Network_Entry>(networkEntryRepository, [], deleting));
            RestampNetworks([.. deleting.Select(row => row.NetworkID)]);
            return deleting.Count;
        }
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

    #region Helpers

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
    ///   <see cref="RestampStudios"/> does the studios.
    /// </summary>
    /// <param name="networkIDs">The store's IDs of the networks whose entries changed.</param>
    private void RestampNetworks(IReadOnlyCollection<int> networkIDs)
    {
        var now = DateTime.Now;
        var networks = networkIDs.Distinct()
            .Select(networkRepository.GetByID)
            .OfType<Metadata_Network>()
            .Where(network => IsUsed(network) == network.LastOrphanedAt.HasValue)
            .Select(network => Stamped(network, IsUsed(network) ? null : now))
            .ToList();
        if (networks.Count > 0)
            writer.Write(new MetadataRowChanges<Metadata_Network>(networkRepository, networks, []));
    }

    /// <summary>
    ///   Whether any entry names a stored studio.
    /// </summary>
    /// <param name="studio">The studio; a new one is named by nothing.</param>
    /// <returns><see langword="true"/> when an entry does.</returns>
    private bool IsUsed(Metadata_Studio studio)
        => studio.Metadata_StudioID is not 0 && entryRepository.GetByStudioID(studio.Metadata_StudioID).Count > 0;

    /// <summary>
    ///   Whether any entry names a stored network.
    /// </summary>
    /// <param name="network">The network; a new one is named by nothing.</param>
    /// <returns><see langword="true"/> when an entry does.</returns>
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
