using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB;

public class TMDB_CompanyRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_Company, int>(databaseFactory)
{
    public TMDB_Company? GetByTmdbCompanyID(int companyId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company>()
            .Where(a => a.TmdbCompanyID == companyId)
            .Take(1)
            .SingleOrDefault();
    }
}
