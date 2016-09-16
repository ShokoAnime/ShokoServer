using JMMServer.Entities;

namespace JMMServer.Repositories.Cached
{
    public class CloudAccountRepository : BaseCachedRepository<CloudAccount, int>
    {
        public CloudAccountRepository()
        {
            SaveCallback = (ses, obj) =>
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
