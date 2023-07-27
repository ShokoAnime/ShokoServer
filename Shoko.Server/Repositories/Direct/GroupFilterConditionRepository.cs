using System.Collections.Generic;
using System.Linq;
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
            return session
                .Query<GroupFilterCondition>()
                .Where(a => a.GroupFilterID == gfid)
                .ToList();
        });
    }
}
