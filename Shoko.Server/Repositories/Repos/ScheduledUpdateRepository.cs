using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class ScheduledUpdateRepository : BaseRepository<ScheduledUpdate, int>
    {
        private PocoIndex<int, ScheduledUpdate, int> UpdateTypes;
   
        internal override int SelectKey(ScheduledUpdate entity) => entity.ScheduledUpdateID;
            
        internal override void PopulateIndexes()
        {
            UpdateTypes = new PocoIndex<int, ScheduledUpdate, int>(Cache, a => a.UpdateType);
        }

        internal override void ClearIndexes()
        {
            UpdateTypes = null;
        }


        public ScheduledUpdate GetByUpdateType(int uptype)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UpdateTypes.GetOne(uptype);
                return Table.FirstOrDefault(a => a.UpdateType == uptype);
            }
        }
    }
}