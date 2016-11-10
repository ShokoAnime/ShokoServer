using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
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
        public Action<T> BeginDeleteCallback { get; set; }
        public Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }
        public Action<T> EndDeleteCallback { get; set; }
        public Action<T> BeginSaveCallback { get; set; }
        public Action<ISessionWrapper, T> SaveWithOpenTransactionCallback { get; set; }
        public Action<T> EndSaveCallback { get; set; }

        public virtual void Populate(ISessionWrapper session, bool displayname = true)
        {
            if (displayname)
                ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(T).Name, string.Empty);

            Cache = new PocoCache<S, T>(session.CreateCriteria(typeof(T)).List<T>(), SelectKey);
            PopulateIndexes();
        }

        public void Populate(bool displayname = true)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Populate(session.Wrap());
            }
        }

        protected abstract S SelectKey(T entity);

        public virtual void ClearCache()
        {
            Cache.Clear();
        }

        internal virtual void RegenerateDb(List<T> collection, Action<T> genaction, bool displayme = true)
        {
            int cnt = 0;
            int max = collection.Count;
	        ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(T).Name, " DbRegen");
	        if (max <= 0) return;
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

        public virtual IReadOnlyList<T> GetAll()
        {
            return Cache.Values.ToList();
        }

        public virtual IReadOnlyList<T> GetAll(int max_limit)
        {
            return Cache.Values.Take(max_limit).ToList();
        }

        public IReadOnlyList<T> GetAll(ISession session)
        {
            return Cache.Values.ToList();
        }

        public IReadOnlyList<T> GetAll(ISessionWrapper session)
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
                BeginDeleteCallback?.Invoke(cr);
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
                EndDeleteCallback?.Invoke(cr);
            }
        }
        public virtual void Delete(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs)
                BeginDeleteCallback?.Invoke(cr);
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T cr in objs)
                    {
                        DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                        Cache.Remove(cr);
                        session.Delete(cr);
                    }
                    transaction.Commit();
                }
            }
            foreach (T cr in objs)
            {
                EndDeleteCallback?.Invoke(cr);
            }
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, S id)
        {
            DeleteWithOpenTransaction(session, GetByID(id));
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, T cr)
        {
            if (cr != null)
            {
                DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                Cache.Remove(cr);
                session.Delete(cr);
            }
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs)
            {
                DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                Cache.Remove(cr);
                session.Delete(cr);
            }
        }
        public virtual void Save(T obj)
        {
            BeginSaveCallback?.Invoke(obj);
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
            Cache.Update(obj);
            EndSaveCallback?.Invoke(obj);
        }

        public virtual void Save(IReadOnlyCollection<T> objs)
        {
            if (objs.Count==0)
                return;
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T obj in objs)
                    {
                        session.SaveOrUpdate(obj);
                        SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                    }
                    transaction.Commit();

                }
            }
            foreach (T obj in objs)
            {
                Cache.Update(obj);
                EndSaveCallback?.Invoke(obj);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISessionWrapper session, T obj)
        {
            if (Equals(SelectKey(obj), default(S)))
            {
                session.Insert(obj);
            }
            else
            {
                session.Update(obj);
            }

            SaveWithOpenTransactionCallback?.Invoke(session, obj);
            Cache.Update(obj);
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, T obj)
        {
            session.SaveOrUpdate(obj);
            SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
            Cache.Update(obj);
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T obj in objs)
            {
                session.SaveOrUpdate(obj);
                SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                Cache.Update(obj);
            }
        }
    }
}
