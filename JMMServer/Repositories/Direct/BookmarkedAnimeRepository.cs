using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class BookmarkedAnimeRepository : BaseDirectRepository<SVR_BookmarkedAnime, int>
    {
        private BookmarkedAnimeRepository()
        {
            
        }

        public static BookmarkedAnimeRepository Create()
        {
            return new BookmarkedAnimeRepository();
        }
        public SVR_BookmarkedAnime GetByAnimeID(int animeID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_BookmarkedAnime cr = session
                    .CreateCriteria(typeof(SVR_BookmarkedAnime))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .UniqueResult<SVR_BookmarkedAnime>();
                return cr;
            }
        }

        public override IReadOnlyList<SVR_BookmarkedAnime> GetAll()
        {
            return base.GetAll().OrderBy(a => a.Priority).ToList();
        }
        public override IReadOnlyList<SVR_BookmarkedAnime> GetAll(ISession session)
        {
            return GetAll();
        }
        public override IReadOnlyList<SVR_BookmarkedAnime> GetAll(ISessionWrapper session)
        {
            return GetAll();
        }
    }
}