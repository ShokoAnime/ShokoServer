using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class AnimeGroup_UserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AnimeGroup_User, int>(databaseFactory)
{
    private PocoIndex<int, AnimeGroup_User, int>? _groupIDs;

    private PocoIndex<int, AnimeGroup_User, int>? _userIDs;

    private PocoIndex<int, AnimeGroup_User, (int, int)>? _userGroupIDs;

    /// <summary>
    /// Striped locks guarding the creation of a user's row for a group.
    /// </summary>
    private readonly Lock[] _writeLocks = [.. Enumerable.Range(0, 64).Select(_ => new Lock())];

    protected override int SelectKey(AnimeGroup_User entity)
        => entity.AnimeGroup_UserID;

    public override void PopulateIndexes()
    {
        _groupIDs = Cache.CreateIndex(a => a.AnimeGroupID);
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _userGroupIDs = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeGroupID));
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

        await session.CreateSQLQuery("DELETE FROM AnimeGroup_User WHERE AnimeGroup_UserID > 0").ExecuteUpdateAsync();

        // Clear the cache so that it is in sync with the database
        ClearCache();
    }

    /// <summary>
    /// Gets the lock to hold from reading a user's row for a group until it
    /// is saved, so two writers that both find no row never both add one.
    /// </summary>
    /// <param name="userID">The user ID.</param>
    /// <param name="groupID">The group ID.</param>
    /// <returns>The lock, shared by every writer of that row.</returns>
    public Lock GetWriteLock(int userID, int groupID)
        => _writeLocks[(int)((uint)HashCode.Combine(userID, groupID) % (uint)_writeLocks.Length)];

    public AnimeGroup_User? GetByUserAndGroupID(int userID, int groupID)
        => _userGroupIDs!.GetOne((userID, groupID));

    public List<AnimeGroup_User> GetByUserID(int userID)
        => _userIDs!.GetMultiple(userID);

    public List<AnimeGroup_User> GetByGroupID(int groupID)
        => _groupIDs!.GetMultiple(groupID);
}
