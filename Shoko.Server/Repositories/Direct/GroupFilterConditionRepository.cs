using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class GroupFilterConditionRepository : BaseDirectRepository<GroupFilterCondition, int>
{
    public List<GroupFilterCondition> GetByGroupFilterID(int gfid)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var gfcs = session
                .CreateCriteria(typeof(GroupFilterCondition))
                .Add(Restrictions.Eq("GroupFilterID", gfid))
                .List<GroupFilterCondition>();

            return new List<GroupFilterCondition>(gfcs);
        });
    }
}
