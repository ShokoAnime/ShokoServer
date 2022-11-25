using System.Threading.Tasks;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories;

public interface ICachedRepository
{
    // Listed in order of call
    Task Populate(bool displayname = true);
    Task Populate(ISessionWrapper session, bool displayname = true);
    void PopulateIndexes();
    void RegenerateDb();
    void PostProcess();
}
