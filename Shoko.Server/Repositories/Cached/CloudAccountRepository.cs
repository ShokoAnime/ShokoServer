using System;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Cached
{
    public class CloudAccountRepository : BaseCachedRepository<SVR_CloudAccount, int>
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
