using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_UserRepository : BaseCachedRepository<SVR_VideoLocal_User, int>
{
    private PocoIndex<int, SVR_VideoLocal_User, int> VideoLocalIDs;
    private PocoIndex<int, SVR_VideoLocal_User, int> Users;
    private PocoIndex<int, SVR_VideoLocal_User, (int UserID, int VideoLocalID)> UsersVideoLocals;

    protected override int SelectKey(SVR_VideoLocal_User entity)
    {
        return entity.VideoLocal_UserID;
    }

    public override void PopulateIndexes()
    {
        VideoLocalIDs = new PocoIndex<int, SVR_VideoLocal_User, int>(Cache, a => a.VideoLocalID);
        Users = new PocoIndex<int, SVR_VideoLocal_User, int>(Cache, a => a.JMMUserID);
        UsersVideoLocals =
            new PocoIndex<int, SVR_VideoLocal_User, (int, int)>(Cache, a => (a.JMMUserID, a.VideoLocalID));
    }

    public override void RegenerateDb()
    {
    }

    public List<SVR_VideoLocal_User> GetByVideoLocalID(int vidid)
    {
        return ReadLock(() => VideoLocalIDs.GetMultiple(vidid));
    }

    public List<SVR_VideoLocal_User> GetByUserID(int userid)
    {
        return ReadLock(() => Users.GetMultiple(userid));
    }

    public SVR_VideoLocal_User GetByUserIDAndVideoLocalID(int userid, int vidid)
    {
        return ReadLock(() => UsersVideoLocals.GetOne((userid, vidid)));
    }
}
