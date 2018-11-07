using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class IgnoreAnimeRepository : BaseRepository<IgnoreAnime, int>
    {
        private PocoIndex<int, IgnoreAnime, int, int, int> AnimeUserIgnores;
        private PocoIndex<int, IgnoreAnime, int, int> UserIgnores;
        private PocoIndex<int, IgnoreAnime, int> Users;

        internal override int SelectKey(IgnoreAnime entity) => entity.IgnoreAnimeID;
        
        internal override void PopulateIndexes()
        {
            AnimeUserIgnores = new PocoIndex<int, IgnoreAnime, int,int,int>(Cache, a => a.AnimeID,a=>a.JMMUserID, a=>a.IgnoreType);
            UserIgnores = new PocoIndex<int, IgnoreAnime, int,int>(Cache, a => a.JMMUserID,a=>a.IgnoreType);
            Users = new PocoIndex<int, IgnoreAnime, int>(Cache, a => a.JMMUserID);
        }

        internal override void ClearIndexes()
        {
            AnimeUserIgnores = null;
            UserIgnores = null;
            Users = null;
        }

        public IgnoreAnime GetByAnimeUserType(int animeID, int userID, int ignoreType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeUserIgnores.GetOne(animeID,userID,ignoreType);
                return Table.FirstOrDefault(a => a.AnimeID == animeID && a.JMMUserID==userID && a.IgnoreType==ignoreType);
            }
        }

        public List<IgnoreAnime> GetByUserAndType(int userID, int ignoreType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UserIgnores.GetMultiple(userID, ignoreType);
                return Table.Where(a => a.JMMUserID==userID && a.IgnoreType==ignoreType).ToList();
            }
        }

        public List<IgnoreAnime> GetByUser(int userID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Users.GetMultiple(userID);
                return Table.Where(a => a.JMMUserID == userID).ToList();
            }
        }
    }
}