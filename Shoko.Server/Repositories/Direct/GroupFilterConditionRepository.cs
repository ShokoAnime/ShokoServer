using System.Collections.Generic;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class GroupFilterConditionRepository : BaseDirectRepository<GroupFilterCondition, int>
    {
        public List<GroupFilterCondition> GetByGroupFilterID(int gfid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByGroupFilterID(session, gfid);
            }
        }

        public List<GroupFilterCondition> GetByGroupFilterID(ISession session, int gfid)
        {
            var gfcs = session
                .CreateCriteria(typeof(GroupFilterCondition))
                .Add(Restrictions.Eq("GroupFilterID", gfid))
                .List<GroupFilterCondition>();

            return new List<GroupFilterCondition>(gfcs);
        }

        public List<GroupFilterCondition> GetByConditionType(GroupFilterConditionType ctype)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var gfcs = session
                    .CreateCriteria(typeof(GroupFilterCondition))
                    .Add(Restrictions.Eq("ConditionType", (int) ctype))
                    .List<GroupFilterCondition>();

                return new List<GroupFilterCondition>(gfcs);
            }
        }
    }
}