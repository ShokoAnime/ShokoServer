using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    // ReSharper disable once InconsistentNaming
    public abstract class BaseCachedRepository<T,S> : IRepository<T,S> where T: class
    {
        internal PocoCache<S, T> Cache;

        public abstract void PopulateIndexes();
        public abstract void RegenerateDb();

        public Action<ISession, T> DeleteCallback { get; set; }
        public Action<ISession, T> SaveCallback { get; set; }
        public Action<T> AfterCommitCallback { get; set; }

        public virtual void Populate(Func<T, S> key, bool displayname = true)
        {
            if (displayname)
                ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(T).Name, string.Empty);
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Cache = new PocoCache<S, T>(session.CreateCriteria(typeof(T)).List<T>(),key);
            }
            PopulateIndexes();
        }

        internal virtual void RegenerateDb(List<T> collection, Action<T> genaction, bool displayme = true)
        {
            int cnt = 0;
            int max = collection.Count;
            foreach (T g in collection)
            {
                try
                {
                    genaction(g);
                }
                catch (Exception)
                {
                    // ignored
                }
                if (displayme)
                {
                    cnt++;
                    if (cnt%10 == 0)
                        ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(T).Name, " DbRegen - " + cnt + "/" + max);
                }
            }
            if (displayme)
                ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(T).Name, " DbRegen - " + max + "/" + max);

        }


        // ReSharper disable once InconsistentNaming
        public virtual T GetByID(S id)
        {
            return Cache.Get(id);
        }

        public T GetByID(ISession session, S id)
        {
            return Cache.Get(id);
        }

        public T GetByID(ISessionWrapper session, S id)
        {
            return Cache.Get(id);
        }

        public virtual List<T> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<T> GetAll(ISession session)
        {
            return Cache.Values.ToList();
        }

        public List<T> GetAll(ISessionWrapper session)
        {
            return Cache.Values.ToList();
        }

        public virtual void Delete(S id)
        {
            Delete(GetByID(id));
        }
        public virtual void Delete(T cr)
        {
            if (cr != null)
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        DeleteCallback?.Invoke(session, cr);
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
        public virtual void Delete(List<T> objs)
        {
            if (objs.Count == 0)
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T cr in objs)
                    {
                        DeleteCallback?.Invoke(session, cr);
                        Cache.Remove(cr);
                        session.Delete(cr);
                    }
                    transaction.Commit();
                }
            }
        }
        public virtual void Delete(ISession session, S id)
        {
            Delete(session, GetByID(id));
        }
        public virtual void Delete(ISession session, T cr)
        {
            if (cr != null)
            {
                DeleteCallback?.Invoke(session, cr);
                Cache.Remove(cr);
                session.Delete(cr);
            }
        }
        public virtual void Delete(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs)
            {
                DeleteCallback?.Invoke(session, cr);
                Cache.Remove(cr);
                session.Delete(cr);
            }
        }
        public virtual void Save(T obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    SaveCallback?.Invoke(session, obj);
                    transaction.Commit();
                    Cache.Update(obj);
                    AfterCommitCallback?.Invoke(obj);
                }
            }
        }

        public virtual void Save(List<T> objs)
        {
            if (objs.Count==0)
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T obj in objs)
                    {
                        session.SaveOrUpdate(obj);
                        SaveCallback?.Invoke(session, obj);
                    }
                    transaction.Commit();
                    foreach (T obj in objs)
                    {
                       Cache.Update(obj);
                       AfterCommitCallback?.Invoke(obj);
                    }
                }
            }
        }

        //This two do not have after commit, and no cache update, the "UpdateCache" below should be used after commit
        public virtual void Save(ISession session, T obj)
        {
            session.SaveOrUpdate(obj);
            SaveCallback?.Invoke(session, obj);
            Cache.Update(obj);
        }
        //This two do not have after commit, and no cache update, the "UpdateCache" below should be used after commit
        public virtual void Save(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T obj in objs)
                session.SaveOrUpdate(obj);
            foreach (T obj in objs)
            {
                Cache.Update(obj);
                SaveCallback?.Invoke(session, obj);
            }
        }

        public virtual void UpdateCache(T obj)
        {
            Cache.Update(obj);
            AfterCommitCallback?.Invoke(obj);
        }

        public virtual void UpdateCache(List<T> objs)
        {
            foreach (T obj in objs)
            {
                Cache.Update(obj);
                AfterCommitCallback?.Invoke(obj);
            }
        }
    }
}
