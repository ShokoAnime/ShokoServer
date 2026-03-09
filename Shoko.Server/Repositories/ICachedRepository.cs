using System.Threading;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories;

public interface ICachedRepository
{
    // Listed in order of call
    void Populate(bool displayName = true, CancellationToken cancellationToken = default);
    void Populate(ISessionWrapper session, bool displayName = true, CancellationToken cancellationToken = default);
    void PopulateIndexes();
    void RegenerateDb();
    void PostProcess();
}
