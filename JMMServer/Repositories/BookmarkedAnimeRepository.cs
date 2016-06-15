using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class BookmarkedAnimeRepository
    {
        public void Save(BookmarkedAnime obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public BookmarkedAnime GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<BookmarkedAnime>(id);
            }
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

        public List<BookmarkedAnime> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(BookmarkedAnime))
                    .AddOrder(Order.Asc("Priority"))
                    .List<BookmarkedAnime>();

                return new List<BookmarkedAnime>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    BookmarkedAnime cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}