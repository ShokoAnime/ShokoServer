using System;
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

        protected override int SelectKey(CloudAccount entity)
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
