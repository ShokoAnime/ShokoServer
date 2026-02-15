using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeGroupRepository : BaseCachedRepository<AnimeGroup, int>
{
    private readonly ILogger<AnimeGroupRepository> _logger;

    private PocoIndex<int, AnimeGroup, int>? _parentIDs;

    private readonly ChangeTracker<int> _changes = new();

    public AnimeGroupRepository(ILogger<AnimeGroupRepository> logger, DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        _logger = logger;
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByGroupID(cr.AnimeGroupID));
        };
        EndDeleteCallback = cr =>
        {
            if (cr.AnimeGroupParentID.HasValue && cr.AnimeGroupParentID.Value > 0)
            {
                _logger.LogTrace("Updating group stats by group from AnimeGroupRepository.Delete: {Count}", cr.AnimeGroupParentID.Value);
                var parentGroup = GetByID(cr.AnimeGroupParentID.Value);
                if (parentGroup != null)
                {
                    Save(parentGroup, true);
                }
            }
        };
    }

    protected override int SelectKey(AnimeGroup entity)
        => entity.AnimeGroupID;

    public override void PopulateIndexes()
    {
        _changes.AddOrUpdateRange(Cache.Keys);
        _parentIDs = Cache.CreateIndex(a => a.AnimeGroupParentID ?? 0);
    }

    public override void Save(AnimeGroup obj)
        => Save(obj, true);

    public void Save(AnimeGroup group, bool recursive)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        Lock(session, s =>
        {
            //We are creating one, and we need the AnimeGroupID before Update the contracts
            if (group.AnimeGroupID == 0)
            {
                using var transaction = s.BeginTransaction();
                s.SaveOrUpdate(group);
                transaction.Commit();
            }
        });

        UpdateCache(group);
        Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            SaveWithOpenTransaction(s, group);
            transaction.Commit();
        });

        _changes.AddOrUpdate(group.AnimeGroupID);

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

        _changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
    }

    public async Task UpdateBatch(ISessionWrapper session, IReadOnlyCollection<AnimeGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(groups);

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            await session.UpdateAsync(group);
            UpdateCache(group);
        }

        await trans.CommitAsync();

        _changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
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

        // First, get all of the current groups so that we can inform the change tracker that they have been removed later
        var allGroups = GetAll();

        await Lock(async () =>
        {
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
        });

        if (excludeGroupId != null)
        {
            _changes.RemoveRange(allGroups.Select(g => g.AnimeGroupID)
                .Where(id => id != excludeGroupId.Value));
        }
        else
        {
            _changes.RemoveRange(allGroups.Select(g => g.AnimeGroupID));
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();

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
        => ReadLock(() => _parentIDs!.GetMultiple(parentID));

    public List<AnimeGroup> GetAllTopLevelGroups()
        => GetByParentID(0);

    public ChangeTracker<int> GetChangeTracker()
        => _changes;
}
