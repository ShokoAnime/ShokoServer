using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;

namespace Shoko.Server.Services.MetadataStorage;

/// <summary>
///   Works out which rows a typed metadata store's write saves and removes.
///   Rows are changed as copies, so the cached ones stay as they were until
///   the write has committed.
/// </summary>
internal static class MetadataRows
{
    /// <summary>
    ///   Makes a set of rows match the items given, in their order. A row
    ///   already stored for an item is reused, so its ID stays the same.
    /// </summary>
    /// <typeparam name="TRow">The kind of row.</typeparam>
    /// <typeparam name="TItem">The kind of item written.</typeparam>
    /// <typeparam name="TKey">What makes a row and an item the same one.</typeparam>
    /// <param name="existing">The rows stored now.</param>
    /// <param name="items">The items to store, in order.</param>
    /// <param name="keyOfRow">The key of a stored row.</param>
    /// <param name="keyOfItem">The key of an item.</param>
    /// <param name="fill">Writes an item onto a row, given its position.</param>
    /// <param name="unchanged">
    ///   Optional. Tells whether a reused row, once filled, still equals the
    ///   stored one; such a row is left out of the rows to save.
    /// </param>
    /// <returns>The rows to save, in order, and the rows to remove.</returns>
    internal static (List<TRow> Saving, List<TRow> Deleting) Replace<TRow, TItem, TKey>(
        IReadOnlyList<TRow> existing,
        IReadOnlyList<TItem> items,
        Func<TRow, TKey> keyOfRow,
        Func<TItem, TKey> keyOfItem,
        Action<TRow, TItem, int> fill,
        Func<TRow, TRow, bool>? unchanged = null
    )
        where TRow : class, IMetadataStoreRow<TRow>, new()
        where TKey : notnull
    {
        var unused = existing
            .GroupBy(keyOfRow)
            .ToDictionary(group => group.Key, group => new Queue<TRow>(group));
        var saving = new List<TRow>(items.Count);
        for (var position = 0; position < items.Count; position++)
        {
            var item = items[position];
            var stored = unused.TryGetValue(keyOfItem(item), out var candidates) && candidates.TryDequeue(out var candidate) ? candidate : null;
            var row = stored?.Clone() ?? new TRow();
            fill(row, item, position);
            if (stored is null || unchanged is null || !unchanged(stored, row))
                saving.Add(row);
        }

        return (saving, [.. unused.Values.SelectMany(rows => rows)]);
    }

    /// <summary>
    ///   Adds or updates rows a source names by its own ID. When the same ID
    ///   comes twice, the last one wins.
    /// </summary>
    /// <typeparam name="TRow">The kind of row.</typeparam>
    /// <typeparam name="TItem">The kind of item written.</typeparam>
    /// <param name="repository">The table.</param>
    /// <param name="items">The items to store.</param>
    /// <param name="entityType">The kind of entry every item must name itself as.</param>
    /// <param name="idOf">The identifier of an item.</param>
    /// <param name="fill">Writes an item onto a row.</param>
    /// <returns>The rows to save.</returns>
    /// <exception cref="ArgumentNullException">An item, or its identifier, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   An item's identifier names another kind of entry, or is on a source
    ///   the core keeps itself.
    /// </exception>
    internal static List<TRow> Upsert<TRow, TItem>(
        MetadataSourceRowRepository<TRow> repository,
        IEnumerable<TItem> items,
        MetadataEntityType entityType,
        Func<TItem, MetadataGuid> idOf,
        Action<TRow, TItem> fill
    )
        where TRow : class, IMetadataStoreRow<TRow>, new()
        where TItem : class
    {
        var latest = new Dictionary<MetadataGuid, TItem>();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            var id = idOf(item);
            ArgumentNullException.ThrowIfNull(id, nameof(items));
            if (id.EntityType != entityType)
                throw new ArgumentException($"\"{id}\" does not name a {entityType.Value}.", nameof(items));
            MetadataEntries.CheckWritableSource(id.Source, nameof(items));
            latest.Remove(id);
            latest[id] = item;
        }

        var saving = new List<TRow>(latest.Count);
        foreach (var (id, item) in latest)
        {
            var row = repository.GetByProviderID(id.Source, id.ID)?.Clone() ?? new TRow();
            fill(row, item);
            saving.Add(row);
        }

        return saving;
    }

    /// <summary>
    ///   Keeps the first of each item that shares a key with an earlier one.
    /// </summary>
    /// <typeparam name="TItem">The kind of item written.</typeparam>
    /// <typeparam name="TKey">What makes two items the same one.</typeparam>
    /// <param name="items">The items, in order.</param>
    /// <param name="keyOf">The key of an item.</param>
    /// <returns>The items, in order, without repeats.</returns>
    /// <exception cref="ArgumentNullException">An item is <c>null</c>.</exception>
    internal static List<TItem> FirstOfEach<TItem, TKey>(IEnumerable<TItem> items, Func<TItem, TKey> keyOf) where TItem : class
    {
        var seen = new HashSet<TKey>();
        var kept = new List<TItem>();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            if (seen.Add(keyOf(item)))
                kept.Add(item);
        }

        return kept;
    }

    /// <summary>
    ///   A detached copy of a stored row, to change without touching the
    ///   cached one until the change has been written.
    /// </summary>
    /// <typeparam name="TRow">The kind of row.</typeparam>
    /// <param name="row">The stored row, if there is one.</param>
    /// <returns>The copy, or <c>null</c> when no row was given.</returns>
    internal static TRow? Copy<TRow>(TRow? row) where TRow : class, IMetadataStoreRow<TRow>
        => row?.Clone();

    /// <summary>
    ///   Puts a row on an entry, at a position.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="position">Where it sits, from <c>0</c>.</param>
    internal static void Place(MetadataEntryRow row, MetadataGuid entry, int position)
    {
        row.Source = entry.Source;
        row.EntityType = entry.EntityType;
        row.EntityID = entry.ID;
        row.Ordering = position;
    }
}
