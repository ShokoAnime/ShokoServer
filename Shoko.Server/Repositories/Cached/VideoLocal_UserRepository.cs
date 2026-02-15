using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class VideoLocal_UserRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<VideoLocal_User, int>(databaseFactory)
{
    private PocoIndex<int, VideoLocal_User, int>? _videoLocalIDs;

    private PocoIndex<int, VideoLocal_User, int>? _userIDs;

    private PocoIndex<int, VideoLocal_User, (int UserID, int VideoLocalID)>? _userVideoLocalIDs;

    protected override int SelectKey(VideoLocal_User entity)
        => entity.VideoLocal_UserID;

    public override void PopulateIndexes()
    {
        _videoLocalIDs = Cache.CreateIndex(a => a.VideoLocalID);
        _userIDs = Cache.CreateIndex(a => a.JMMUserID);
        _userVideoLocalIDs = Cache.CreateIndex(a => (a.JMMUserID, a.VideoLocalID));
    }

    public IReadOnlyList<VideoLocal_User> GetByVideoLocalID(int videoLocalID)
        => ReadLock(() => _videoLocalIDs!.GetMultiple(videoLocalID));

    public IReadOnlyList<VideoLocal_User> GetByUserID(int userID)
        => ReadLock(() => _userIDs!.GetMultiple(userID));

    public VideoLocal_User? GetByUserAndVideoLocalID(int userID, int videoLocalID)
        => ReadLock(() => _userVideoLocalIDs!.GetOne((userID, videoLocalID)));
}
