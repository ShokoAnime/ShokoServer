using JMMServer.Entities;

namespace JMMServer.Repositories.Cached
{
    public class CloudAccountRepository : BaseCachedRepository<CloudAccount, int>
    {
        public CloudAccountRepository()
        {
            EndSaveCallback = (obj) =>
            {
                obj.NeedSave = false;
            };
        }

        public override void PopulateIndexes()
        {
            
        }

        public override void RegenerateDb()
        {

        }
    }
}
