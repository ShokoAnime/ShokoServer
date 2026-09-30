using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   One of the typed metadata stores' tables. The stores write through
///   <see cref="MetadataRowWriter"/>, so several tables change in one
///   transaction, and the cache follows once it has committed.
/// </summary>
/// <typeparam name="T">The table's row.</typeparam>
/// <param name="databaseFactory">The database factory.</param>
public abstract class MetadataStoreRepository<T>(DatabaseFactory databaseFactory) : BaseCachedRepository<T, int>(databaseFactory)
    where T : class, IMetadataStoreRow<T>, new()
{
    /// <inheritdoc />
    protected override int SelectKey(T entity)
        => entity.RowID;

    /// <summary>
    ///   Brings the cache in line with a write that has been committed.
    /// </summary>
    /// <param name="saved">The rows written, each with its ID.</param>
    /// <param name="deleted">The rows removed.</param>
    internal void ApplyToCache(IEnumerable<T> saved, IEnumerable<T> deleted)
    {
        foreach (var row in deleted)
            DeleteFromCache(row);
        foreach (var row in saved)
            UpdateCache(row);
    }
}

/// <summary>
///   A table whose rows each belong to one entry, kept in the order given.
/// </summary>
/// <typeparam name="T">The table's row.</typeparam>
/// <param name="databaseFactory">The database factory.</param>
public abstract class MetadataEntryRowRepository<T>(DatabaseFactory databaseFactory) : MetadataStoreRepository<T>(databaseFactory)
    where T : MetadataEntryRow, IMetadataStoreRow<T>, new()
{
    private PocoIndex<int, T, (MetadataSource, MetadataEntityType, string)>? _entries;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _entries = Cache.CreateIndex(row => row.EntryKey);
    }

    /// <summary>
    ///   Every row an entry has.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The rows, in the order they were given.</returns>
    public IReadOnlyList<T> GetByEntry(MetadataGuid entry)
        => [.. _entries!.GetMultiple((entry.Source, entry.EntityType, entry.ID)).OrderBy(row => row.Ordering).ThenBy(row => row.RowID)];
}

/// <summary>
///   A table of things a source names by its own ID, such as its creators or
///   tags, each given the store's own ID as well.
/// </summary>
/// <typeparam name="T">The table's row.</typeparam>
/// <param name="databaseFactory">The database factory.</param>
public abstract class MetadataSourceRowRepository<T>(DatabaseFactory databaseFactory) : MetadataStoreRepository<T>(databaseFactory)
    where T : class, IMetadataStoreRow<T>, new()
{
    private PocoIndex<int, T, (MetadataSource, string)>? _providerIDs;

    private PocoIndex<int, T, MetadataSource>? _sources;

    /// <summary>
    ///   The source the row belongs to.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The source.</returns>
    protected abstract MetadataSource SourceOf(T row);

    /// <summary>
    ///   The source's own ID for the row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The source's ID.</returns>
    protected abstract string ProviderIDOf(T row);

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        _providerIDs = Cache.CreateIndex(row => (SourceOf(row), ProviderIDOf(row)));
        _sources = Cache.CreateIndex(SourceOf);
    }

    /// <summary>
    ///   Looks up a row by the source's ID for it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="providerID">The source's ID.</param>
    /// <returns>The row, or <c>null</c> when there is none.</returns>
    public T? GetByProviderID(MetadataSource source, string providerID)
        => string.IsNullOrEmpty(providerID) ? null : _providerIDs!.GetOne((source, providerID));

    /// <summary>
    ///   Every row a source has.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<T> GetBySource(MetadataSource source)
        => _sources!.GetMultiple(source);
}
