using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class CloudAccountRepository : BaseRepository<SVR_CloudAccount, int>
    {

        internal override void EndSave(SVR_CloudAccount entity,  object returnFromBeginSave, object parameters)
        {
            entity.NeedSave = false;
        }

        internal override int SelectKey(SVR_CloudAccount entity) => entity.CloudID;

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }
    }
}