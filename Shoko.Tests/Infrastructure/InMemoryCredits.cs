using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Direct.Metadata;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Stands in for <see cref="Metadata_CastRepository"/> without a database:
/// the rows a committed write saved live in a list, read the way the
/// database would answer.
/// </summary>
public sealed class InMemoryCastRepository() : Metadata_CastRepository(null!)
{
    private readonly InMemoryCredits<Metadata_Cast> _rows = new();

    public override IReadOnlyList<Metadata_Cast> GetAll()
        => _rows.Where(_ => true);

    public override IReadOnlyList<Metadata_Cast> GetByEntry(MetadataGuid entry)
        => _rows.ByEntry(entry);

    public override IReadOnlyList<Metadata_Cast> GetByCreatorID(int creatorID)
        => _rows.Where(row => creatorID is not 0 && row.CreatorID == creatorID);

    public override IReadOnlyList<Metadata_Cast> GetByCharacterID(int characterID)
        => _rows.Where(row => characterID is not 0 && row.CharacterID == characterID);

    public override IReadOnlySet<int> GetCreditedCreatorIDs(IEnumerable<int> creatorIDs)
        => creatorIDs.Where(id => GetByCreatorID(id).Count > 0).ToHashSet();

    public override IReadOnlySet<int> GetCreditedCharacterIDs(IEnumerable<int> characterIDs)
        => characterIDs.Where(id => GetByCharacterID(id).Count > 0).ToHashSet();

    protected override void OnCommitted(IEnumerable<Metadata_Cast> saved, IEnumerable<Metadata_Cast> deleted)
        => _rows.Apply(saved, deleted);
}

/// <summary>
/// Stands in for <see cref="Metadata_CrewRepository"/> without a database, as
/// <see cref="InMemoryCastRepository"/> does for the cast.
/// </summary>
public sealed class InMemoryCrewRepository() : Metadata_CrewRepository(null!)
{
    private readonly InMemoryCredits<Metadata_Crew> _rows = new();

    public override IReadOnlyList<Metadata_Crew> GetAll()
        => _rows.Where(_ => true);

    public override IReadOnlyList<Metadata_Crew> GetByEntry(MetadataGuid entry)
        => _rows.ByEntry(entry);

    public override IReadOnlyList<Metadata_Crew> GetByCreatorID(int creatorID)
        => _rows.Where(row => creatorID is not 0 && row.CreatorID == creatorID);

    public override IReadOnlySet<int> GetCreditedCreatorIDs(IEnumerable<int> creatorIDs)
        => creatorIDs.Where(id => GetByCreatorID(id).Count > 0).ToHashSet();

    protected override void OnCommitted(IEnumerable<Metadata_Crew> saved, IEnumerable<Metadata_Crew> deleted)
        => _rows.Apply(saved, deleted);
}

/// <summary>
/// The rows of an in-memory credit table, keyed by their IDs.
/// </summary>
/// <typeparam name="T">The table's row.</typeparam>
internal sealed class InMemoryCredits<T> where T : MetadataEntryRow, IMetadataStoreRow<T>
{
    private readonly Dictionary<int, T> _rows = [];

    public IReadOnlyList<T> ByEntry(MetadataGuid entry)
        => [.. _rows.Values.Where(row => row.EntryID == entry).OrderBy(row => row.Ordering).ThenBy(row => row.RowID)];

    public IReadOnlyList<T> Where(Func<T, bool> predicate)
        => [.. _rows.Values.Where(predicate)];

    public void Apply(IEnumerable<T> saved, IEnumerable<T> deleted)
    {
        foreach (var row in deleted)
            _rows.Remove(row.RowID);
        foreach (var row in saved)
            _rows[row.RowID] = row;
    }
}
