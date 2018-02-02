using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class VideoLocal_UserRepository : BaseRepository<VideoLocal_User, int>
    {
        private PocoIndex<int, VideoLocal_User, int> VideoLocalIDs;
        private PocoIndex<int, VideoLocal_User, int> Users;
        private PocoIndex<int, VideoLocal_User, int, int> UsersVideoLocals;


        internal override int SelectKey(VideoLocal_User entity) => entity.VideoLocal_UserID;

        internal override void PopulateIndexes()
        {
            VideoLocalIDs = new PocoIndex<int, VideoLocal_User, int>(Cache, a => a.VideoLocalID);
            Users = new PocoIndex<int, VideoLocal_User, int>(Cache, a => a.JMMUserID);
            UsersVideoLocals = new PocoIndex<int, VideoLocal_User, int, int>(Cache, a => a.JMMUserID, a => a.VideoLocalID);
        }

        internal override void ClearIndexes()
        {
            VideoLocalIDs = null;
            Users = null;
            UsersVideoLocals = null;
        }



        public List<VideoLocal_User> GetByVideoLocalID(int vidid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return VideoLocalIDs.GetMultiple(vidid);
                return Table.Where(a => a.VideoLocalID == vidid).ToList();
            }
        }

        public List<VideoLocal_User> GetByUserID(int userid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Users.GetMultiple(userid);
                return Table.Where(a => a.JMMUserID==userid).ToList();
            }
        }

        public VideoLocal_User GetByUserIDAndVideoLocalID(int userid, int vidid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UsersVideoLocals.GetOne(userid, vidid);
                return Table.FirstOrDefault(a => a.JMMUserID == userid && a.VideoLocalID==vidid);
            }
        }
    }
}