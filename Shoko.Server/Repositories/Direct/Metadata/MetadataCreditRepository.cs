using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;

namespace Shoko.Server.Repositories.Direct.Metadata;

/// <summary>
///   A table of credits on entries, read from the database on every call,
///   as a source may credit millions of people. The people store writes it
///   through <see cref="MetadataRowWriter"/>.
/// </summary>
/// <typeparam name="T">The table's row.</typeparam>
/// <param name="databaseFactory">The database factory.</param>
public abstract class MetadataCreditRepository<T>(DatabaseFactory databaseFactory) : BaseDirectRepository<T, int>(databaseFactory), IMetadataRowTable<T>
    where T : MetadataEntryRow, IMetadataStoreRow<T>, new()
{
    #region Fields

    /// <summary>
    ///   How many IDs go in one <c>IN</c> list, well under every backend's
    ///   limit on parameters.
    /// </summary>
    protected const int ChunkSize = 500;

    #endregion

    #region Reading

    /// <summary>
    ///   Every row an entry has.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The rows, in the order they were given.</returns>
    public virtual IReadOnlyList<T> GetByEntry(MetadataGuid entry)
    {
        var (source, entityType, entityID) = (entry.Source, entry.EntityType, entry.ID);
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        // The database may compare the ID ignoring case, so it is matched
        // again here, as the source wrote it.
        return
        [
            .. session.Query<T>()
                .Where(row => row.Source == source && row.EntityType == entityType && row.EntityID == entityID)
                .ToList()
                .Where(row => row.EntityID == entityID)
                .OrderBy(row => row.Ordering)
                .ThenBy(row => row.RowID),
        ];
    }

    /// <summary>
    ///   The credits of several entries of one source and kind, read in
    ///   chunks.
    /// </summary>
    /// <param name="source">The entries' source.</param>
    /// <param name="entityType">The entries' kind.</param>
    /// <param name="entityIDs">The source's IDs for the entries.</param>
    /// <returns>The credits, by entry and then in each entry's order.</returns>
    public IReadOnlyList<T> GetByEntries(MetadataSource source, MetadataEntityType entityType, IReadOnlyCollection<string> entityIDs)
    {
        if (entityIDs.Count is 0)
            return [];

        // The database may compare the IDs ignoring case, so they are matched
        // again here, as the source wrote them.
        var wanted = entityIDs.ToHashSet(StringComparer.Ordinal);
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return
        [
            .. wanted.Chunk(ChunkSize)
                .SelectMany(chunk => session.Query<T>()
                    .Where(row => row.Source == source && row.EntityType == entityType && chunk.Contains(row.EntityID))
                    .ToList())
                .Where(row => wanted.Contains(row.EntityID))
                .OrderBy(row => row.EntityID, StringComparer.Ordinal)
                .ThenBy(row => row.Ordering)
                .ThenBy(row => row.RowID),
        ];
    }

    protected IReadOnlyList<TResult> QueryInChunks<TResult>(IReadOnlyCollection<int> ids, Func<IStatelessSession, int[], IEnumerable<TResult>> query)
    {
        if (ids.Count is 0)
            return [];

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return [.. ids.Chunk(ChunkSize).SelectMany(chunk => query(session, chunk))];
    }

    #endregion

    #region IMetadataRowTable Implementation

    /// <summary>
    ///   Called once a write to the table has committed. The rows live only
    ///   in the database, so there is nothing to bring in line.
    /// </summary>
    /// <param name="saved">The rows written, each with its ID.</param>
    /// <param name="deleted">The rows removed.</param>
    protected virtual void OnCommitted(IEnumerable<T> saved, IEnumerable<T> deleted)
    {
    }

    void IMetadataRowTable<T>.OnCommitted(IEnumerable<T> saved, IEnumerable<T> deleted)
        => OnCommitted(saved, deleted);

    #endregion
}
