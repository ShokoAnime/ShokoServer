using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMServer.Databases;
using JMMServer.Entities;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class CloudAccountRepository
    {
        private static PocoCache<int, CloudAccount> Cache;

        public static void InitCache()
        {
            string t = "Cloud Accounts";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            CloudAccountRepository repo = new CloudAccountRepository();
            Cache = new PocoCache<int, CloudAccount>(repo.InternalGetAll(), a => a.CloudID);
        }


        public void Save(CloudAccount obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    obj.NeedSave = false;
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                    Cache.Update(obj);
                }
            }
        }

        public CloudAccount GetByID(int id)
        {
            return Cache.Get(id);
        }


        public List<CloudAccount> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<CloudAccount> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var ca = session
                    .CreateCriteria(typeof(CloudAccount))
                    .List<CloudAccount>();
                return new List<CloudAccount>(ca);
            }
        }
        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CloudAccount cr = GetByID(id);
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
