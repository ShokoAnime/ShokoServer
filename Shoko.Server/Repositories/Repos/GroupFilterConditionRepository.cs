using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Shoko.Models.Enums;

namespace Shoko.Server.Repositories.Repos
{
    public class GroupFilterConditionRepository : BaseRepository<GroupFilterCondition, int>
    {
        public List<GroupFilterCondition> GetByGroupFilterID(int gfid)
        {
            using (RepoLock.ReaderLock())
            {
                return Where(s => s.GroupFilterID == gfid).ToList();
            }
        }

        public List<GroupFilterCondition> GetByConditionType(GroupFilterConditionType ctype)
        {

            using (RepoLock.ReaderLock())
            {
                return Where(s => s.ConditionType == (int)ctype).ToList();
            }
        }

        internal override int SelectKey(GroupFilterCondition entity) => entity.GroupFilterConditionID;

        internal override void PopulateIndexes() {}

        internal override void ClearIndexes() {}
    }
}
