using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Repositories.Cached.Metadata;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Stands in for <see cref="MetadataRowWriter"/> without a database: it hands
/// new rows their IDs the way the database would, then brings the caches in
/// line, and can be told to refuse a write.
/// </summary>
public sealed class CacheOnlyRowWriter() : MetadataRowWriter(null!)
{
    private int _nextID = 1;

    /// <summary>
    /// How many writes went through.
    /// </summary>
    public int Writes { get; private set; }

    /// <summary>
    /// Makes every write fail, as a refused transaction would.
    /// </summary>
    public bool Fail { get; set; }

    public override void Write(params IReadOnlyList<MetadataRowChanges> changes)
    {
        if (Fail)
            throw new InvalidOperationException("The write was refused.");

        // Like the real writer, nothing is written when nothing changed.
        if (changes.All(change => change.IsEmpty))
            return;

        Writes++;
        foreach (var change in changes)
            foreach (var row in change.Saving)
                if (change.IDOf(row) is 0)
                    change.AssignID(row, _nextID++);

        foreach (var change in changes)
            change.Apply();
    }
}
