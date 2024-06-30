using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached;

public class AnimeGroupRepository : BaseCachedRepository<SVR_AnimeGroup, int>
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, SVR_AnimeGroup, int> Parents;

    private ChangeTracker<int> Changes = new();

    public AnimeGroupRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByGroupID(cr.AnimeGroupID));
        };
        EndDeleteCallback = cr =>
        {
            if (cr.AnimeGroupParentID.HasValue && cr.AnimeGroupParentID.Value > 0)
            {
                logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}",
                    cr.AnimeGroupParentID.Value);
                var ngrp = GetByID(cr.AnimeGroupParentID.Value);
                if (ngrp != null)
                {
                    Save(ngrp, true);
                }
            }
        };
    }

    protected override int SelectKey(SVR_AnimeGroup entity)
    {
        return entity.AnimeGroupID;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        Parents = Cache.CreateIndex(a => a.AnimeGroupParentID ?? 0);
    }

    public override void RegenerateDb()
    {
    }

    public override void Save(SVR_AnimeGroup obj)
    {
        Save(obj, true);
    }

    public void Save(SVR_AnimeGroup grp, bool recursive)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        Lock(session, s =>
        {
            //We are creating one, and we need the AnimeGroupID before Update the contracts
            if (grp.AnimeGroupID == 0)
            {
                using var transaction = s.BeginTransaction();
                s.SaveOrUpdate(grp);
                transaction.Commit();
            }
        });

        UpdateCache(grp);
        Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            SaveWithOpenTransaction(s, grp);
            transaction.Commit();
        });

        Changes.AddOrUpdate(grp.AnimeGroupID);

        if (grp.AnimeGroupParentID.HasValue && recursive)
        {
            var pgroup = GetByID(grp.AnimeGroupParentID.Value);
            // This will avoid the recursive error that would be possible, it won't update it, but that would be
            // the least of the issues
            if (pgroup != null && pgroup.AnimeGroupParentID == grp.AnimeGroupID)
            {
                Save(pgroup, true);
            }
        }
    }

    public async Task InsertBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (groups == null)
        {
            throw new ArgumentNullException(nameof(groups));
        }

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            await session.InsertAsync(group);
            UpdateCache(group);
        }
        await trans.CommitAsync();

        Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
    }

    public async Task UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (groups == null)
        {
            throw new ArgumentNullException(nameof(groups));
        }

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            await session.UpdateAsync(group);
            UpdateCache(group);
        }
        await trans.CommitAsync();

        Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
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
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        // First, get all of the current groups so that we can inform the change tracker that they have been removed later
        var allGrps = GetAll();

        await Lock(async () =>
        {
            // Then, actually delete the AnimeGroups
            if (excludeGroupId != null)
            {
                await session.CreateQuery("delete SVR_AnimeGroup ag where ag.id <> :excludeId")
                    .SetInt32("excludeId", excludeGroupId.Value)
                    .ExecuteUpdateAsync();
            }
            else
            {
                await session.CreateQuery("delete SVR_AnimeGroup ag")
                    .ExecuteUpdateAsync();
            }
        });

        if (excludeGroupId != null)
        {
            Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID)
                .Where(id => id != excludeGroupId.Value));
        }
        else
        {
            Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID));
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();

        // If we're excluding a group from deletion, and it was in the cache originally, then re-add it back in
        if (excludeGroupId != null)
        {
            var excludedGroup = allGrps.FirstOrDefault(g => g.AnimeGroupID == excludeGroupId.Value);

            if (excludedGroup != null)
            {
                UpdateCache(excludedGroup);
            }
        }
    }

    public List<SVR_AnimeGroup> GetByParentID(int parentid)
    {
        return ReadLock(() => Parents.GetMultiple(parentid));
    }

    public List<SVR_AnimeGroup> GetAllTopLevelGroups()
    {
        return GetByParentID(0);
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }
}
