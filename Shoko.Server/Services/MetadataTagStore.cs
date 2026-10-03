using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every source's tags and genres, and the entries they are on, in
///   the store's own tables, cached in memory.
/// </summary>
/// <param name="tagRepository">The tags.</param>
/// <param name="entryRepository">The tags on entries.</param>
/// <param name="writer">Writes the changes.</param>
/// <param name="textStore">Writes the tags, whose names and overviews stay on their rows, and tells the text manager.</param>
public class MetadataTagStore(Metadata_TagRepository tagRepository, Metadata_Tag_EntryRepository entryRepository, MetadataRowWriter writer, MetadataTextStore textStore)
    : IMetadataTagStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    public ITag? GetTag(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Tag ? tagRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ITag> GetAllTags(MetadataSource source, TagKind? kind = null)
        => [
            .. tagRepository.GetBySource(source)
                .Where(tag => kind is null || tag.Kind == kind)
                .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(tag => tag.ProviderID, StringComparer.Ordinal),
        ];

    /// <inheritdoc />
    public IReadOnlyList<ITag> GetTags(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entryRepository.GetByEntry(entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> GetEntriesWithTag(MetadataGuid tag)
        => GetTag(tag) is Metadata_Tag stored
            ? [.. MetadataEntryRow.InOrder(entryRepository.GetByTagID(stored.Metadata_TagID)).Select(row => row.EntryID).Distinct()]
            : [];

    #endregion

    #region Writing

    /// <inheritdoc />
    public void SaveTags(IEnumerable<MetadataTagData> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        lock (_writeLock)
        {
            var now = DateTime.Now;
            var saving = MetadataRows.Upsert(tagRepository, tags, MetadataEntityType.Tag, tag => tag.ID, (row, tag) =>
            {
                row.Source = tag.ID.Source;
                row.ProviderID = tag.ID.ID;
                row.Name = tag.Name ?? string.Empty;
                row.Description = tag.Overview ?? string.Empty;
                row.Kind = tag.Kind;
                row.Category = tag.Category;
                row.IsSpoiler = tag.IsSpoiler;
                row.IsRestricted = tag.IsRestricted;
                row.LastUpdatedAt = now;
            });
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Tag>(tagRepository, saving, []));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A tag given twice keeps its first place and weight.
    /// </remarks>
    public void SetTags(MetadataGuid entry, IEnumerable<MetadataEntryTagData> tags)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(tags);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = MetadataRows.FirstOfEach(tags, tag => tag.TagID);
        foreach (var item in items)
            MetadataEntries.CheckReference(item.TagID, entry.Source, MetadataEntityType.Tag, nameof(tags));

        lock (_writeLock)
        {
            // Every tag is looked up before anything is written, so one that is
            // missing leaves the entry as it was.
            var resolved = items
                .Select(item => (
                    Item: item,
                    TagID: tagRepository.GetByProviderID(item.TagID.Source, item.TagID.ID)?.Metadata_TagID
                        ?? throw new ArgumentException($"The tag \"{item.TagID}\" is not stored.", nameof(tags))
                ))
                .ToList();
            var (saving, deleting) = MetadataRows.Replace(
                entryRepository.GetByEntry(entry),
                resolved,
                row => row.TagID,
                tag => tag.TagID,
                (row, tag, position) =>
                {
                    MetadataRows.Place(row, entry, position);
                    row.TagID = tag.TagID;
                    row.Weight = tag.Item.Weight;
                    row.IsSpoiler = tag.Item.IsSpoiler;
                }
            );
            writer.Write(new MetadataRowChanges<Metadata_Tag_Entry>(entryRepository, saving, deleting));
        }
    }

    /// <inheritdoc />
    public int RemoveTags(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        lock (_writeLock)
        {
            var deleting = entryRepository.GetByEntry(entry);
            writer.Write(new MetadataRowChanges<Metadata_Tag_Entry>(entryRepository, [], deleting));
            return deleting.Count;
        }
    }

    /// <summary>
    ///   Removes a source's tags that no entry is tagged with any more and
    ///   that its provider has not saved since a time, such as the name-keyed
    ///   tags a source replaced with ones keyed by its own IDs.
    /// </summary>
    /// <remarks>
    ///   A provider saves a tag before tagging an entry with it, so a tag
    ///   saved since the time is kept even while nothing is tagged with it.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="savedBefore">Remove only the tags last saved before this time, in local time.</param>
    /// <returns>The tags removed.</returns>
    internal IReadOnlyList<MetadataGuid> RemoveUnused(MetadataSource source, DateTime savedBefore)
    {
        ArgumentNullException.ThrowIfNull(source);
        MetadataEntries.CheckWritableSource(source, nameof(source));

        lock (_writeLock)
        {
            var deleting = tagRepository.GetBySource(source)
                .Where(tag => tag.LastUpdatedAt < savedBefore && entryRepository.GetByTagID(tag.Metadata_TagID).Count is 0)
                .ToList();
            if (deleting.Count is 0)
                return [];

            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Tag>(tagRepository, [], deleting));
            return [.. deleting.Select(tag => ((ITag)tag).ID)];
        }
    }

    #endregion
}
