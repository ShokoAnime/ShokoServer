﻿using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_UserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_VideoLocal_User, int>(databaseFactory)
{
    private PocoIndex<int, SVR_VideoLocal_User, int>? _videoLocalIDs;

    private PocoIndex<int, SVR_VideoLocal_User, int>? _userIDs;

    private PocoIndex<int, SVR_VideoLocal_User, (int UserID, int VideoLocalID)>? _userVideoLocalIDs;

    protected override int SelectKey(SVR_VideoLocal_User entity)
        => entity.VideoLocal_UserID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _userVideoLocalIDs = Cache.CreateIndex(a => (a.JMMUserID, a.VideoLocalID));
    }

    public IReadOnlyList<SVR_VideoLocal_User> GetByVideoLocalID(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));

    public IReadOnlyList<SVR_VideoLocal_User> GetByUserID(int userID)
        => ReadLock(() => _userIDs!.GetMultiple(userID));

    public SVR_VideoLocal_User? GetByUserIDAndVideoLocalID(int userID, int videoLocalID)
        => ReadLock(() => _userVideoLocalIDs!.GetOne((userID, videoLocalID)));
}
