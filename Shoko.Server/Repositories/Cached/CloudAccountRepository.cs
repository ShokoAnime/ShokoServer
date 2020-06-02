using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class CloudAccountRepository : BaseCachedRepository<SVR_CloudAccount, int>
    {
        public CloudAccountRepository()
        {
            EndSaveCallback = obj => { obj.NeedSave = false; };
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
