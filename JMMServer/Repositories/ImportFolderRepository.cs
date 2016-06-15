using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class ImportFolderRepository
    {
        public void Save(ImportFolder obj)
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

        public ImportFolder GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<ImportFolder>(id);
            }
        }

        public ImportFolder GetByImportLocation(string importloc)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                ImportFolder cr = session
                    .CreateCriteria(typeof(ImportFolder))
                    .Add(Restrictions.Eq("ImportFolderLocation", importloc))
                    .UniqueResult<ImportFolder>();
                return cr;
            }
        }

        public List<ImportFolder> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var importFolders = session
                    .CreateCriteria(typeof(ImportFolder))
                    .List<ImportFolder>();
                return new List<ImportFolder>(importFolders);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    ImportFolder cr = GetByID(id);
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