using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                BookmarkedAnime cr = session
                    .CreateCriteria(typeof(BookmarkedAnime))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .UniqueResult<BookmarkedAnime>();
                return cr;
            }
        }

        public override IReadOnlyList<BookmarkedAnime> GetAll()
        {
            return base.GetAll().OrderBy(a => a.Priority).ToList();
        }

        public override IReadOnlyList<BookmarkedAnime> GetAll(ISession session)
        {
            return GetAll();
        }

        public override IReadOnlyList<BookmarkedAnime> GetAll(ISessionWrapper session)
        {
            return GetAll();
        }
    }
}