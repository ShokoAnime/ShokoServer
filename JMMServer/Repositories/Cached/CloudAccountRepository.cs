using JMMServer.Entities;

namespace JMMServer.Repositories.Cached
{
    public class CloudAccountRepository : BaseCachedRepository<CloudAccount, int>
    {
        private CloudAccountRepository()
        {
            EndSaveCallback = (obj) =>
            {
                obj.NeedSave = false;
            };
        }

        public static CloudAccountRepository Create()
        {
            return new CloudAccountRepository();
        }
        public override void PopulateIndexes()
        {
            
        }

        public override void RegenerateDb()
        {

        }
    }
}
