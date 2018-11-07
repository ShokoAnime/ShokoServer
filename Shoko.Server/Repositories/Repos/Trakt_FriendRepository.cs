using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class Trakt_FriendRepository : BaseRepository<Trakt_Friend, int>
    {
        private PocoIndex<int, Trakt_Friend, string> Usernames;

        internal override int SelectKey(Trakt_Friend entity) => entity.Trakt_FriendID;

        internal override void PopulateIndexes()
        {
            Usernames = new PocoIndex<int, Trakt_Friend, string>(Cache, a => a.Username);
        }

        internal override void ClearIndexes()
        {
            Usernames = null;
        }

        public Trakt_Friend GetByUsername(string username)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Usernames.GetOne(username);
                return Table.FirstOrDefault(a => a.Username == username);
            }
        }

    }
}