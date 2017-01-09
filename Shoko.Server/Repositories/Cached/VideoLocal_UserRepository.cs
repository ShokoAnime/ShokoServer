using System;
using System.Collections.Generic;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Cached
{
    public class VideoLocal_UserRepository : BaseCachedRepository<VideoLocal_User,int>
    {
        private PocoIndex<int, VideoLocal_User, int> VideoLocalIDs;
        private PocoIndex<int, VideoLocal_User, int> Users;
        private PocoIndex<int, VideoLocal_User, int, int> UsersVideoLocals;

        private VideoLocal_UserRepository()
        {
            
        }

        public static VideoLocal_UserRepository Create()
        {
            return new VideoLocal_UserRepository();
        }

        protected override int SelectKey(VideoLocal_User entity)
        {
            return entity.VideoLocal_UserID;
        }

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