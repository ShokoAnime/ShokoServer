using System.Linq;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeStaffRepository : BaseRepository<AnimeStaff, int>
    {
        private PocoIndex<int, AnimeStaff, int> AniDBIDs;

        internal override int SelectKey(AnimeStaff entity)
        {
            return entity.StaffID;
        }

        internal override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, AnimeStaff, int>(Cache, a => a.AniDBID);
        }

        public AnimeStaff GetByAniDBID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return AniDBIDs.GetOne(id);
                return Table.FirstOrDefault(s => s.AniDBID == id);
            }
        }

        internal override void ClearIndexes()
        {
            AniDBIDs = null;
        }
    }
}
