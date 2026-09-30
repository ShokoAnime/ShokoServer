using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class AnimeGroupRepository(
    ILogger<AnimeGroupRepository> logger,
    DatabaseFactory databaseFactory
) : BaseCachedRepository<AnimeGroup, int>(databaseFactory)
{
    private readonly ILogger<AnimeGroupRepository> _logger = logger;

    private PocoIndex<int, AnimeGroup, int>? _parentIDs;

    /// <summary>
    ///   What decides each cached group's name, as the cache last saw it, so
    ///   a save that changes none of it keeps the texts worked out.
    /// </summary>
    private readonly ConcurrentDictionary<int, GroupShape> _shapes = new();

    /// <summary>
    ///   What decides a group's name besides its series and its texts.
    /// </summary>
    /// <param name="ParentID">The parent group ID.</param>
    /// <param name="DefaultSeriesID">The series chosen as the main one.</param>
    /// <param name="MainAnimeID">The AniDB anime chosen as the main one.</param>
    private readonly record struct GroupShape(int? ParentID, int? DefaultSeriesID, int? MainAnimeID)
    {
        /// <summary>
        ///   A group's shape.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <returns>The shape.</returns>
        internal static GroupShape Of(AnimeGroup group)
            => new(group.AnimeGroupParentID, group.DefaultAnimeSeriesID, group.MainAniDBAnimeID);
    }

    protected override void OnBeginDelete(AnimeGroup obj)
    {
        RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByGroupID(obj.AnimeGroupID));
    }

    protected override void OnEndDelete(AnimeGroup obj)
    {
        // The name and overview a user gave the group go with it.
        TextAccess.Reachable?.RemoveTexts(((IMetadata)obj).ID);
        if (obj.AnimeGroupParentID.HasValue && obj.AnimeGroupParentID.Value > 0)
        {
            _logger.LogTrace("Updating group stats by group from AnimeGroupRepository.Delete: {Count}", obj.AnimeGroupParentID.Value);
            var parentGroup = GetByID(obj.AnimeGroupParentID.Value);
            if (parentGroup != null)
            {
                Save(parentGroup, true);
            }
        }
    }

    protected override int SelectKey(AnimeGroup entity)
        => entity.AnimeGroupID;

    protected override void UpdateCacheUnsafe(AnimeGroup cr)
    {
        base.UpdateCacheUnsafe(cr);

        var shape = GroupShape.Of(cr);
        var hadShape = _shapes.TryGetValue(cr.AnimeGroupID, out var before);
        _shapes[cr.AnimeGroupID] = shape;
        if (hadShape && before == shape)
            return;

        // The names above follow this group's: the old parent's is forgotten
        // with it, the new parent's here.
        TextAccess.Forget(((IMetadata)cr).ID);
        if (shape.ParentID is > 0 && shape.ParentID != before.ParentID)
            TextAccess.Forget(GroupEntry(shape.ParentID.Value));
    }

    protected override void DeleteFromCacheUnsafe(AnimeGroup cr)
    {
        base.DeleteFromCacheUnsafe(cr);
        _shapes.TryRemove(cr.AnimeGroupID, out _);
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A save forgets the group only when its shape changed, which
    ///   <see cref="UpdateCacheUnsafe"/> sees, as most saves only count.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(AnimeGroup entity, bool removed)
        => removed ? base.TextEntriesOf(entity, removed) : [];

    /// <summary>
    ///   The text manager's entry for a Shoko group.
    /// </summary>
    /// <param name="groupID">The Shoko group ID.</param>
    /// <returns>The entry.</returns>
    private static MetadataGuid GroupEntry(int groupID)
        => new(MetadataSource.Shoko, MetadataEntityType.Collection, groupID.ToString());

    public override void PopulateIndexes()
    {
        _parentIDs = Cache.CreateIndex(a => a.AnimeGroupParentID ?? 0);
        _shapes.Clear();
        foreach (var group in Cache.GetAll())
            _shapes[group.AnimeGroupID] = GroupShape.Of(group);
    }

    public override void Save(AnimeGroup obj)
        => Save(obj, true);

    public void Save(AnimeGroup group, bool recursive)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        //We are creating one, and we need the AnimeGroupID before Update the contracts
        if (group.AnimeGroupID == 0)
        {
            using var transaction = session.BeginTransaction();
            session.SaveOrUpdate(group);
            transaction.Commit();
        }

        UpdateCache(group);
        {
            using var transaction = session.BeginTransaction();
            SaveWithOpenTransaction(session, group);
            transaction.Commit();
        }

        if (group.AnimeGroupParentID.HasValue && recursive)
        {
            var parentGroup = GetByID(group.AnimeGroupParentID.Value);
            // This will avoid the recursive error that would be possible, it won't update it, but that would be
            // the least of the issues
            if (parentGroup != null && parentGroup.AnimeGroupParentID == group.AnimeGroupID)
            {
                Save(parentGroup, true);
            }
        }
    }

    public async Task InsertBatch(ISessionWrapper session, IReadOnlyCollection<AnimeGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(groups);

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            await session.InsertAsync(group);
            UpdateCache(group);
        }

        await trans.CommitAsync();
    }

    /// <summary>
    /// Deletes all AnimeGroup records.
    /// </summary>
    /// <remarks>
    /// This method also makes sure that the cache is cleared.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="excludeGroupId">The ID of the AnimeGroup to exclude from deletion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    public async Task DeleteAll(ISessionWrapper session, int? excludeGroupId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // First, get all of the current groups so the excluded one can be put back in the cache later
        var allGroups = GetAll();

        // Then, actually delete the AnimeGroups
        if (excludeGroupId != null)
        {
            await session.CreateSQLQuery("DELETE FROM AnimeGroup WHERE AnimeGroupID <> :excludeId")
                .SetInt32("excludeId", excludeGroupId.Value)
                .ExecuteUpdateAsync();
        }
        else
        {
            await session.CreateSQLQuery("DELETE FROM AnimeGroup WHERE AnimeGroupID > 0")
                .ExecuteUpdateAsync();
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();
        _shapes.Clear();
        foreach (var group in allGroups)
            TextAccess.Forget(((IMetadata)group).ID);

        // If we're excluding a group from deletion, and it was in the cache originally, then re-add it back in
        if (excludeGroupId != null)
        {
            var excludedGroup = allGroups.FirstOrDefault(g => g.AnimeGroupID == excludeGroupId.Value);

            if (excludedGroup != null)
            {
                UpdateCache(excludedGroup);
            }
        }
    }

    public List<AnimeGroup> GetByParentID(int parentID)
        => parentID <= 0 ? [] : _parentIDs!.GetMultiple(parentID);
}
