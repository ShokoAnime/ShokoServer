using System.Collections.Generic;
using JMMServer.Entities;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class VideoLocal_UserRepository : BaseCachedRepository<VideoLocal_User,int>
    {
        private PocoIndex<int, VideoLocal_User, int> VideoLocalIDs;
        private PocoIndex<int, VideoLocal_User, int> Users;
        private PocoIndex<int, VideoLocal_User, int, int> UsersVideoLocals;


        public override void PopulateIndexes()
        {
            VideoLocalIDs = new PocoIndex<int, VideoLocal_User, int>(Cache, a => a.VideoLocalID);
            Users = new PocoIndex<int, VideoLocal_User, int>(Cache, a => a.JMMUserID);
            UsersVideoLocals = new PocoIndex<int, VideoLocal_User, int, int>(Cache, a => a.JMMUserID, a => a.VideoLocalID);
        }

        public override void RegenerateDb()
        {

        }

        public List<VideoLocal_User> GetByVideoLocalID(int vidid)
        {
            return VideoLocalIDs.GetMultiple(vidid);
        }

        public List<VideoLocal_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }

        public VideoLocal_User GetByUserIDAndVideoLocalID(int userid, int vidid)
        {
            return UsersVideoLocals.GetOne(userid, vidid);
        }
    }
}