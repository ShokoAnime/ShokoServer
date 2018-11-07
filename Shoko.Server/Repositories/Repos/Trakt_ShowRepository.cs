using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class Trakt_ShowRepository : BaseRepository<Trakt_Show, int>
    {
        private PocoIndex<int, Trakt_Show, string> Slugs;

        internal override int SelectKey(Trakt_Show entity) => entity.Trakt_ShowID;
        
        internal override void PopulateIndexes()
        {
            Slugs = new PocoIndex<int, Trakt_Show, string>(Cache, a => a.TraktID);
        }

        internal override void ClearIndexes()
        {
            Slugs = null;
        }

        public Trakt_Show GetByTraktSlug(string slug)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Slugs.GetOne(slug);
                return Table.FirstOrDefault(a => a.TraktID == slug);
            }

        }
    }
}