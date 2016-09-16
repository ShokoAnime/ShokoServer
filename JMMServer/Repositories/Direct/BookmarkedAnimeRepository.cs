using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class BookmarkedAnimeRepository : BaseDirectRepository<BookmarkedAnime, int>
    {
        private BookmarkedAnimeRepository()
        {
            
        }

        public static BookmarkedAnimeRepository Create()
        {
            return new BookmarkedAnimeRepository();
        }
        public BookmarkedAnime GetByAnimeID(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                BookmarkedAnime cr = session
                    .CreateCriteria(typeof(BookmarkedAnime))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .UniqueResult<BookmarkedAnime>();
                return cr;
            }
        }

        public override List<BookmarkedAnime> GetAll()
        {
            return base.GetAll().OrderBy(a => a.Priority).ToList();
        }
        public override List<BookmarkedAnime> GetAll(ISession session)
        {
            return base.GetAll(session).OrderBy(a => a.Priority).ToList();
        }
        public override List<BookmarkedAnime> GetAll(ISessionWrapper session)
        {
            return base.GetAll(session).OrderBy(a => a.Priority).ToList();
        }

    }
}