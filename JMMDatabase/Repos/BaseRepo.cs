using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public abstract class BaseRepo<T> where T: class, new()
    {
        internal Dictionary<string,T> Items=new Dictionary<string, T>();


        public virtual T Find(string id)
        {
            return Items.Find(id);
        }
        internal abstract void InternalSave(T obj, IDocumentSession s, UpdateType type=UpdateType.All);
        internal abstract void InternalDelete(T obj, IDocumentSession s);
        public abstract void Populate(IDocumentSession session);

        public virtual IQueryable<T> AsQueryable()
        {
            return Items.Values.AsQueryable();
        }

        public virtual List<T> ToList()
        {
            return Items.Values.ToList();
        } 

        public void Save(T obj, UpdateType type = UpdateType.All)
        {
            using (IDocumentSession session = Store.GetSession())
            {
                Save(obj, session, type);
                session.SaveChanges();
            }
        }

        public void Delete(T obj, IDocumentSession s)
        {
            lock (obj)
            {
                InternalDelete(obj,s);
            }
        }
        public void Save(T obj, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            lock (obj)
            {
                InternalSave(obj,s, type);
            }
        }
        public void Update(string id, IDocumentSession session, UpdateType type = UpdateType.All)
        {
            T t = Find(id);
            if (t!=null)
                Save(t,session,type);
        }
        public void Delete(T obj)
        {
            using (IDocumentSession session = Store.GetSession())
            {
                Delete(obj, session);
                session.SaveChanges();
            }
        }

    }
}
