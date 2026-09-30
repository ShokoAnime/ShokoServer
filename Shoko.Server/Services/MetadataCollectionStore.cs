using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every plugin source's collections and their members in the
///   store's own tables, cached in memory, with their titles and
///   descriptions in the text table.
/// </summary>
/// <param name="collectionRepository">The collections.</param>
/// <param name="memberRepository">The collections' members.</param>
/// <param name="textStore">Keeps the titles and descriptions.</param>
/// <param name="cleanup">Removes what the other stores hold for a removed collection.</param>
public class MetadataCollectionStore(
    Metadata_CollectionRepository collectionRepository,
    Metadata_Collection_MemberRepository memberRepository,
    MetadataTextStore textStore,
    MetadataEntityCleanup cleanup
) : IMetadataCollectionStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    public ICollection? GetCollection(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Collection ? collectionRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ICollection> GetAllCollections(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return collectionRepository.GetBySource(source);
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> GetMembers(MetadataGuid collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return collection.EntityType == MetadataEntityType.Collection
            ? [.. memberRepository.GetByCollectionID(collection.Source, collection.ID).Select(member => member.MemberGuid)]
            : [];
    }

    /// <inheritdoc />
    public IReadOnlyList<ICollection> GetCollectionsWith(MetadataGuid member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return
        [
            .. memberRepository.GetByMember(member)
                .Select(row => row.CollectionID)
                .Distinct(StringComparer.Ordinal)
                .Select(collectionID => collectionRepository.GetByProviderID(member.Source, collectionID))
                .OfType<Metadata_Collection>(),
        ];
    }

    #endregion

    #region Writing

    /// <inheritdoc />
    public int SaveCollection(MetadataCollectionData collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        MetadataEntries.CheckEntry(collection.ID, MetadataEntityType.Collection, nameof(collection));

        var source = collection.ID.Source;
        var members = MetadataRows.FirstOfEach(collection.Members ?? [], member => member);
        foreach (var member in members)
        {
            MetadataEntries.CheckReference(member, source, null, nameof(collection));
            if (member.EntityType != MetadataEntityType.Series && member.EntityType != MetadataEntityType.Movie)
                throw new ArgumentException($"\"{member}\" is neither a series nor a movie, so it can not be in a collection.", nameof(collection));
        }

        var changed = false;
        lock (_writeLock)
        {
            var stored = collectionRepository.GetByProviderID(source, collection.ID.ID);
            var row = MetadataRows.Copy(stored) ?? new Metadata_Collection();
            row.Source = source;
            row.ProviderID = collection.ID.ID;

            var storedMembers = memberRepository.GetByCollectionID(source, collection.ID.ID);
            var (membersSaving, membersDeleting) = MetadataRows.Replace(
                storedMembers,
                members,
                member => (member.MemberType, member.MemberID),
                member => (member.EntityType, member.ID),
                (member, id, position) =>
                {
                    member.Source = source;
                    member.CollectionID = collection.ID.ID;
                    member.MemberType = id.EntityType;
                    member.MemberID = id.ID;
                    member.Ordering = position;
                },
                (storedMember, member) => storedMember.Ordering == member.Ordering
            );

            // The collection is written when it is new, or when its members
            // or texts changed, and only then is it said to have changed.
            textStore.WriteWithTexts([(collection.ID, collection.Titles ?? [], collection.Overviews ?? [])], [], changedTexts =>
            {
                changed = stored is null || membersSaving.Count + membersDeleting.Count > 0 || changedTexts.Contains(collection.ID);
                if (!changed)
                    return [];

                row.LastUpdatedAt = DateTime.Now;
                return
                [
                    new MetadataRowChanges<Metadata_Collection>(collectionRepository, [row], []),
                    new MetadataRowChanges<Metadata_Collection_Member>(memberRepository, membersSaving, membersDeleting),
                ];
            });
        }

        return changed ? 1 : 0;
    }

    /// <inheritdoc />
    public int RemoveCollection(MetadataGuid id)
    {
        MetadataEntries.CheckEntry(id, MetadataEntityType.Collection, nameof(id));

        int count;
        IReadOnlyList<Metadata_Collection_Member> members;
        lock (_writeLock)
        {
            var row = collectionRepository.GetByProviderID(id.Source, id.ID);
            members = memberRepository.GetByCollectionID(id.Source, id.ID);
            if (row is null && members.Count is 0)
                return 0;

            textStore.WriteWithTexts([], [id], _ =>
            [
                new MetadataRowChanges<Metadata_Collection>(collectionRepository, [], row is null ? [] : [row]),
                new MetadataRowChanges<Metadata_Collection_Member>(memberRepository, [], members),
            ]);
            count = row is null ? 0 : 1;
        }

        // Outside the lock, since the other stores take their own.
        cleanup.Remove([id]);
        return count;
    }

    #endregion
}
