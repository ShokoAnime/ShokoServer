using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class ImportFolderRepository
    {
        private static PocoCache<int, ImportFolder> Cache;

        public static void InitCache()
        {
            string t = "Import Folder";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            ImportFolderRepository repo = new ImportFolderRepository();
            Cache = new PocoCache<int, ImportFolder>(repo.InternalGetAll(), a => a.ImportFolderID);
        }


        public void Save(ImportFolder obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                    Cache.Update(obj);
                }
            }
        }

        public ImportFolder GetByID(int id)
        {
            return Cache.Get(id);
        }

        public ImportFolder GetByImportLocation(string importloc)
        {
            return Cache.Values.FirstOrDefault(a => a.ImportFolderLocation == importloc);
        }

        public List<ImportFolder> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<ImportFolder> InternalGetAll()
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
                        Cache.Remove(cr);
                    }
                }
            }
        }
    }
}