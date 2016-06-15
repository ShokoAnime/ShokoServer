using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class PlaylistRepository
    {
        public void Save(Playlist obj)
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

        public Playlist GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<Playlist>(id);
            }
        }

        public List<Playlist> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(Playlist))
                    .AddOrder(Order.Asc("PlaylistName"))
                    .List<Playlist>();

                return new List<Playlist>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    Playlist cr = GetByID(id);
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