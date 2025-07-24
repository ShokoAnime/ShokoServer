using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeGroup_UserRepository : BaseCachedRepository<AnimeGroup_User, int>
{
    private PocoIndex<int, AnimeGroup_User, int>? _groupIDs;

    private PocoIndex<int, AnimeGroup_User, int>? _userIDs;

    private PocoIndex<int, AnimeGroup_User, (int, int)>? _userGroupIDs;

    private readonly Dictionary<int, ChangeTracker<int>> _changes = [];

    public AnimeGroup_UserRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        EndDeleteCallback = cr =>
        {
            _changes.TryAdd(cr.JMMUserID, new());
            _changes[cr.JMMUserID].Remove(cr.AnimeGroupID);
        };
    }

    protected override int SelectKey(AnimeGroup_User entity)
        => entity.AnimeGroup_UserID;

    public override void PopulateIndexes()
    {
        _groupIDs = Cache.CreateIndex(a => a.AnimeGroupID);
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _userGroupIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeGroupID));

        foreach (var n in Cache.Values.Select(a => a.JMMUserID).Distinct())
        {
            _changes[n] = new();
            _changes[n].AddOrUpdateRange(_userIDs.GetMultiple(n).Select(a => a.AnimeGroupID));
        }
    }

    public override void Save(AnimeGroup_User obj)
    {
        base.Save(obj);
        _changes.TryAdd(obj.JMMUserID, new());
        _changes[obj.JMMUserID].AddOrUpdate(obj.AnimeGroupID);
    }

    /// <summary>
    /// Inserts a batch of <see cref="AnimeGroup_User"/> into the database.
    /// </summary>
    /// <remarks>
    /// This method should NOT be used for updating existing entities.
    /// It is up to the caller of this method to manage transactions, etc.
    /// Group Filters, etc. will not be updated by this method.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="groupUsers">The batch of <see cref="AnimeGroup_User"/> to insert into the database.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="groupUsers"/> is <c>null</c>.</exception>
    public async Task InsertBatch(ISessionWrapper session, IEnumerable<AnimeGroup_User> groupUsers)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(groupUsers);

        using var transaction = session.BeginTransaction();
        foreach (var groupUser in groupUsers)
        {
            await session.InsertAsync(groupUser);
            UpdateCache(groupUser);
            if (!_changes.TryGetValue(groupUser.JMMUserID, out var changeTracker))
            {
                changeTracker = new();
                _changes[groupUser.JMMUserID] = changeTracker;
            }

            changeTracker.AddOrUpdate(groupUser.AnimeGroupID);
        }

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Inserts a batch of <see cref="AnimeGroup_User"/> into the database.
    /// </summary>
    /// <remarks>
    /// <para>It is up to the caller of this method to manage transactions, etc.</para>
    /// <para>Group Filters, etc. will not be updated by this method.</para>
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="groupUsers">The batch of <see cref="AnimeGroup_User"/> to insert into the database.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="groupUsers"/> is <c>null</c>.</exception>
    public async Task UpdateBatch(ISessionWrapper session, IEnumerable<AnimeGroup_User> groupUsers)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(groupUsers);

        using var transaction = session.BeginTransaction();
        foreach (var groupUser in groupUsers)
        {
            await session.UpdateAsync(groupUser);
            UpdateCache(groupUser);
            if (!_changes.TryGetValue(groupUser.JMMUserID, out var changeTracker))
            {
                changeTracker = new();
                _changes[groupUser.JMMUserID] = changeTracker;
            }

            changeTracker.AddOrUpdate(groupUser.AnimeGroupID);
        }

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Deletes all AnimeGroup_User records.
    /// </summary>
    /// <remarks>
    /// This method also makes sure that the cache is cleared.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    public async Task DeleteAll(ISessionWrapper session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // First, get all of the current user/groups so that we can inform the change tracker that they have been removed later
        var groupUsers = GetAll().GroupBy(g => g.JMMUserID, g => g.AnimeGroupID);

        // Then, actually delete the AnimeGroup_Users
        await Lock(async () => await session.CreateSQLQuery("DELETE FROM AnimeGroup_User WHERE AnimeGroup_UserID > 0").ExecuteUpdateAsync());

        // Now, update the change trackers with all removed records
        foreach (var groupUser in groupUsers)
        {
            var userId = groupUser.Key;
            if (!_changes.TryGetValue(userId, out var changeTracker))
            {
                changeTracker = new();
                _changes[userId] = changeTracker;
            }

            changeTracker.RemoveRange(groupUser);
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();
    }

    public AnimeGroup_User? GetByUserAndGroupID(int userID, int groupID)
        => ReadLock(() => _userGroupIDs!.GetOne((userID, groupID)));

    public List<AnimeGroup_User> GetByUserID(int userID)
        => ReadLock(() => _userIDs!.GetMultiple(userID));

    public List<AnimeGroup_User> GetByGroupID(int groupID)
        => ReadLock(() => _groupIDs!.GetMultiple(groupID));

    public ChangeTracker<int> GetChangeTracker(int userID)
        => ReadLock(() => _changes.TryGetValue(userID, out var change) ? change : new());
}
