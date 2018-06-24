using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class CloudAccountRepository : BaseCachedRepository<SVR_CloudAccount, int>
    {
        private CloudAccountRepository()
        {
            EndSaveCallback = (obj) => { obj.NeedSave = false; };
        }

        public static CloudAccountRepository Create()
        {
            var repo = new CloudAccountRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        protected override int SelectKey(SVR_CloudAccount entity)
        {
            return entity.CloudID;
        }

        public override void PopulateIndexes()
        {
        }

        public override void RegenerateDb()
        {
        }
    }
}
