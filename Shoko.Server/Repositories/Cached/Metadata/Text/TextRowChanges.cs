using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   The titles and overviews one write saves and removes, written with the
///   other stores' rows in one transaction and handed to the text cache once
///   it has committed.
/// </summary>
/// <param name="cache">The text cache.</param>
/// <param name="saving">The rows to insert or update, titles and overviews alike.</param>
/// <param name="deleting">The rows to remove.</param>
internal sealed class TextRowChanges(TextCache cache, IReadOnlyCollection<MetadataTextRow> saving, IReadOnlyCollection<MetadataTextRow> deleting)
    : MetadataRowChanges
{
    /// <summary>
    ///   How many IDs one delete statement names.
    /// </summary>
    private const int DeleteBatchSize = 500;

    /// <summary>
    ///   What the write changed, per entry, once it has reached the cache.
    /// </summary>
    internal IReadOnlyList<TextEntityChange> Changes { get; private set; } = [];

    /// <inheritdoc />
    internal override bool IsEmpty => saving.Count is 0 && deleting.Count is 0;

    /// <inheritdoc />
    internal override IEnumerable<object> Saving => saving;

    /// <inheritdoc />
    internal override void Stage(ISession session)
    {
        foreach (var (kind, rows) in deleting.GroupBy(row => row.Kind).Select(group => (group.Key, group.Select(row => row.RowID).Distinct().ToList())))
        {
            var (entity, id) = kind is TextKind.Title ? (nameof(Metadata_Title), nameof(Metadata_Title.Metadata_TitleID)) : (nameof(Metadata_Overview), nameof(Metadata_Overview.Metadata_OverviewID));
            foreach (var batch in rows.Chunk(DeleteBatchSize))
                session.CreateQuery($"delete from {entity} where {id} in (:ids)").SetParameterList("ids", batch).ExecuteUpdate();
        }

        foreach (var row in saving)
            session.SaveOrUpdate(row);
    }

    /// <inheritdoc />
    internal override void Apply()
        => Changes = cache.Apply(saving, deleting);

    /// <inheritdoc />
    internal override void AssignID(object row, int id)
        => ((MetadataTextRow)row).RowID = id;

    /// <inheritdoc />
    internal override int IDOf(object row)
        => ((MetadataTextRow)row).RowID;
}
