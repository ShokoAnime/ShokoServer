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
///   Keeps every source's suggestions in the store's own table, cached in
///   memory, and finds them from the entry suggested as well.
/// </summary>
/// <param name="repository">The suggestions.</param>
/// <param name="writer">Writes the changes.</param>
public class MetadataSuggestionStore(Metadata_SuggestionRepository repository, MetadataRowWriter writer) : IMetadataSuggestionStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    /// <remarks>
    ///   Ranked suggestions come first, best first, then the unranked ones in
    ///   the order they were given.
    /// </remarks>
    public IReadOnlyList<ISuggestedMetadata<TBase, TSuggested>> GetSuggestions<TBase, TSuggested>(MetadataGuid entry)
        where TBase : IMetadata
        where TSuggested : IMetadata
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!MetadataEntries.Fits(MetadataEntries.TypeOf<TBase>(), entry.EntityType))
            return [];

        var suggestedType = MetadataEntries.TypeOf<TSuggested>();
        return
        [
            .. repository.GetByBase(entry)
                .Where(row => MetadataEntries.Fits(suggestedType, row.SuggestedType))
                .OrderBy(row => row.Ranking is null)
                .ThenBy(row => row.Ranking)
                .ThenBy(row => row.Ordering)
                .Select(row => new MetadataSuggestion<TBase, TSuggested>(row)),
        ];
    }

    /// <inheritdoc />
    /// <remarks>
    ///   The suggestions come oldest first.
    /// </remarks>
    public IReadOnlyList<ISuggestedMetadata<TBase, TSuggested>> GetSuggestedBy<TBase, TSuggested>(MetadataGuid entry)
        where TBase : IMetadata
        where TSuggested : IMetadata
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!MetadataEntries.Fits(MetadataEntries.TypeOf<TSuggested>(), entry.EntityType))
            return [];

        var baseType = MetadataEntries.TypeOf<TBase>();
        return
        [
            .. repository.GetBySuggested(entry)
                .Where(row => MetadataEntries.Fits(baseType, row.BaseType))
                .Select(row => new MetadataSuggestion<TBase, TSuggested>(row)),
        ];
    }

    #endregion

    #region Writing

    /// <inheritdoc />
    /// <remarks>
    ///   A suggestion given twice, of the same kind, keeps its first place.
    /// </remarks>
    public void SetSuggestions(MetadataGuid entry, IEnumerable<MetadataSuggestionData> suggestions)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(suggestions);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = MetadataRows.FirstOfEach(suggestions, suggestion => (suggestion.SuggestedID, suggestion.Kind));
        foreach (var item in items)
            MetadataEntries.CheckReference(item.SuggestedID, entry.Source, null, nameof(suggestions));

        lock (_writeLock)
        {
            var (saving, deleting) = MetadataRows.Replace(
                repository.GetByBase(entry),
                items,
                row => (row.SuggestedType, row.SuggestedID, row.Kind),
                suggestion => (suggestion.SuggestedID.EntityType, suggestion.SuggestedID.ID, suggestion.Kind),
                (row, suggestion, position) =>
                {
                    row.Source = entry.Source;
                    row.BaseType = entry.EntityType;
                    row.BaseID = entry.ID;
                    row.SuggestedType = suggestion.SuggestedID.EntityType;
                    row.SuggestedID = suggestion.SuggestedID.ID;
                    row.Kind = suggestion.Kind;
                    row.Ranking = suggestion.Order;
                    row.ApprovalRating = suggestion.ApprovalRating;
                    row.ApprovalVotes = suggestion.ApprovalVotes;
                    row.Votes = suggestion.Votes;
                    row.Score = suggestion.Score;
                    row.Ordering = position;
                }
            );
            writer.Write(new MetadataRowChanges<Metadata_Suggestion>(repository, saving, deleting));
        }
    }

    /// <inheritdoc />
    public int RemoveSuggestions(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        lock (_writeLock)
        {
            var deleting = repository.GetByBase(entry);
            writer.Write(new MetadataRowChanges<Metadata_Suggestion>(repository, [], deleting));
            return deleting.Count;
        }
    }

    #endregion
}
