using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class FileFfdshowPresetRepository
    {
        public void Save(FileFfdshowPreset obj)
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

        public FileFfdshowPreset GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<FileFfdshowPreset>(id);
            }
        }


        public FileFfdshowPreset GetByHashAndSize(string hash, long fsize)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var obj = session
                    .CreateCriteria(typeof(FileFfdshowPreset))
                    .Add(Restrictions.Eq("Hash", hash))
                    .Add(Restrictions.Eq("FileSize", fsize))
                    .UniqueResult<FileFfdshowPreset>();

                return obj;
            }
        }

        public List<FileFfdshowPreset> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(FileFfdshowPreset))
                    .List<FileFfdshowPreset>();

                return new List<FileFfdshowPreset>(objs);
            }
        }


        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
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