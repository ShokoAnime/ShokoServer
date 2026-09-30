using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Writes the typed metadata stores' changes, across as many tables as a
///   change touches, in one transaction.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class MetadataRowWriter(DatabaseFactory databaseFactory)
{
    /// <summary>
    ///   Writes the changes in one transaction, then brings each table's cache
    ///   in line. Nothing is cached when the write fails.
    /// </summary>
    /// <param name="changes">The changes, one per table.</param>
    public virtual void Write(params IReadOnlyList<MetadataRowChanges> changes)
    {
        var pending = changes.Where(change => !change.IsEmpty).ToList();
        if (pending.Count is 0)
            return;

        using (var session = databaseFactory.SessionFactory.OpenSession())
        using (var transaction = session.BeginTransaction())
        {
            foreach (var change in pending)
                change.Stage(session);
            transaction.Commit();
        }

        foreach (var change in pending)
            change.Apply();
    }
}

/// <summary>
///   The rows one write saves and removes in one table.
/// </summary>
public abstract class MetadataRowChanges
{
    /// <summary>
    ///   Whether there is nothing to write.
    /// </summary>
    internal abstract bool IsEmpty { get; }

    /// <summary>
    ///   The rows being saved, new ones with an ID of <c>0</c>.
    /// </summary>
    internal abstract IEnumerable<object> Saving { get; }

    /// <summary>
    ///   Hands the changes to an open session.
    /// </summary>
    /// <param name="session">The session, inside its transaction.</param>
    internal abstract void Stage(ISession session);

    /// <summary>
    ///   Brings the table's cache in line once the write has committed.
    /// </summary>
    internal abstract void Apply();

    /// <summary>
    ///   Sets a new row's ID, for a writer that hands them out itself.
    /// </summary>
    /// <param name="row">The row, which must be one being saved.</param>
    /// <param name="id">The ID.</param>
    internal abstract void AssignID(object row, int id);

    /// <summary>
    ///   The ID a row being saved has, which is <c>0</c> for a new one.
    /// </summary>
    /// <param name="row">The row, which must be one being saved.</param>
    /// <returns>The ID.</returns>
    internal abstract int IDOf(object row);
}

/// <summary>
///   The rows one write saves and removes in one table.
/// </summary>
/// <typeparam name="T">The table's row.</typeparam>
/// <param name="repository">The table.</param>
/// <param name="saving">The rows to insert or update.</param>
/// <param name="deleting">The rows to remove.</param>
public sealed class MetadataRowChanges<T>(MetadataStoreRepository<T> repository, IReadOnlyCollection<T> saving, IReadOnlyCollection<T> deleting)
    : MetadataRowChanges
    where T : class, IMetadataStoreRow<T>, new()
{
    /// <inheritdoc />
    internal override bool IsEmpty => saving.Count is 0 && deleting.Count is 0;

    /// <inheritdoc />
    internal override IEnumerable<object> Saving => saving;

    /// <inheritdoc />
    internal override void Stage(ISession session)
    {
        foreach (var row in deleting)
            session.Delete(row);
        foreach (var row in saving)
            session.SaveOrUpdate(row);
    }

    /// <inheritdoc />
    internal override void Apply()
        => repository.ApplyToCache(saving, deleting);

    /// <inheritdoc />
    internal override void AssignID(object row, int id)
        => ((T)row).RowID = id;

    /// <inheritdoc />
    internal override int IDOf(object row)
        => ((T)row).RowID;
}
