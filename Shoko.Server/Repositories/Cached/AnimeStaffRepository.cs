using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories
{
    public class AnimeStaffRepository : BaseCachedRepository<AnimeStaff, int>
    {
        private PocoIndex<int, AnimeStaff, int> AniDBIDs;

        private AnimeStaffRepository()
        {
        }

        public override void RegenerateDb()
        {
        }

        public static AnimeStaffRepository Create()
        {
            var repo = new AnimeStaffRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        protected override int SelectKey(AnimeStaff entity)
        {
            return entity.StaffID;
        }

        public override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, AnimeStaff, int>(Cache, a => a.AniDBID);
        }


        public AnimeStaff GetByAniDBID(int id)
        {
            lock (Cache)
            {
                return AniDBIDs.GetOne(id);
            }
        }
    }
}
