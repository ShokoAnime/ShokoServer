using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   What every row tying something to one entry carries: which entry, and
///   where the row sits among the entry's others.
/// </summary>
public abstract class MetadataEntryRow
{
    #region Database Columns

    /// <summary>
    ///   The source the entry belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   What kind of entry it is.
    /// </summary>
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the entry.
    /// </summary>
    public string EntityID { get; set; } = string.Empty;

    /// <summary>
    ///   Where the row sits among the entry's rows, from <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The entry the row belongs to.
    /// </summary>
    public MetadataGuid EntryID => new(Source, EntityType, EntityID);

    /// <summary>
    ///   The entry the row belongs to, as the columns that name it, which is
    ///   how the tables are indexed.
    /// </summary>
    internal (MetadataSource Source, MetadataEntityType EntityType, string EntityID) EntryKey => (Source, EntityType, EntityID);

    /// <summary>
    ///   The entry itself, when a provider holds it.
    /// </summary>
    /// <returns>The entry, or <c>null</c>.</returns>
    public IMetadata? GetEntry()
        => MetadataEntries.Resolve(Source, EntityType, EntityID);

    /// <summary>
    ///   Puts rows from several entries in a stable order: by entry, then by
    ///   where each sits within its entry.
    /// </summary>
    /// <typeparam name="TRow">The kind of row.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, in order.</returns>
    internal static IEnumerable<TRow> InOrder<TRow>(IEnumerable<TRow> rows) where TRow : MetadataEntryRow
        => rows
            .OrderBy(row => row.Source)
            .ThenBy(row => row.EntityType)
            .ThenBy(row => row.EntityID, StringComparer.Ordinal)
            .ThenBy(row => row.Ordering);

    #endregion
}
