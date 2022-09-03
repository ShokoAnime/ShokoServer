using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Properties;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories
{
    // ReSharper disable once InconsistentNaming
    public abstract class BaseCachedRepository<T, S> : BaseRepository, ICachedRepository, IRepository<T, S> where T : class, new()
    {
        /* Dropping Global DB Lock and just locking on Cache for all OPs.
         * lock (GlobalLock) lock (GlobalLock)
         * lock (GlobalLock) lock (GlobalLock) is fine
         *
         * lock (GlobalLock) lock (dbLock)
         * lock (dbLock) lock (GlobalLock) will deadlock
         */
        internal PocoCache<S, T> Cache;

        public Action<T> BeginDeleteCallback { get; set; }
        public Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }
        public Action<T> EndDeleteCallback { get; set; }
        public Action<T> BeginSaveCallback { get; set; }
        public Action<ISessionWrapper, T> SaveWithOpenTransactionCallback { get; set; }
        public Action<T> EndSaveCallback { get; set; }

        public BaseCachedRepository()
        {
            RepoFactory.CachedRepositories.Add(this);
        }

        public virtual void Populate(ISessionWrapper session, bool displayname = true)
        {
            if (displayname)
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Cache, typeof(T).Name.Replace("SVR_", string.Empty),
                    string.Empty);

            // This is only called from main thread, so we don't need to lock
            Cache = new PocoCache<S, T>(session.CreateCriteria(typeof(T)).List<T>(), SelectKey);
            PopulateIndexes();
        }

        public virtual void Populate(bool displayname = true)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Populate(session.Wrap(), displayname);
            }
        }

        protected abstract S SelectKey(T entity);

        public void ClearCache()
        {
            lock (GlobalLock)
            {
                Cache.Clear();
            }
        }

        // ReSharper disable once InconsistentNaming
        public virtual T GetByID(S id)
        {
            lock (GlobalLock)
            {
                return Cache.Get(id);
            }
        }

        public T GetByID(ISession session, S id)
        {
            lock (GlobalLock)
            {
                return Cache.Get(id);
            }
        }

        public T GetByID(ISessionWrapper session, S id)
        {
            lock (GlobalLock)
            {
                return Cache.Get(id);
            }
        }

        public virtual IReadOnlyList<T> GetAll()
        {
            lock (GlobalLock)
            {
                return Cache.Values.ToList();
            }
        }

        public IReadOnlyList<T> GetAll(int max_limit)
        {
            lock (GlobalLock)
            {
                return Cache.Values.Take(max_limit).ToList();
            }
        }

        public IReadOnlyList<T> GetAll(ISession session)
        {
            lock (GlobalLock)
            {
                return Cache.Values.ToList();
            }
        }

        public IReadOnlyList<T> GetAll(ISessionWrapper session)
        {
            lock (GlobalLock)
            {
                return Cache.Values.ToList();
            }
        }

        public virtual void Delete(S id)
        {
            Delete(GetByID(id));
        }

        public virtual void Delete(T cr)
        {
            if (cr == null) return;
            BeginDeleteCallback?.Invoke(cr);
            lock (GlobalLock)
            {
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
            }

            EndDeleteCallback?.Invoke(cr);
        }

        public virtual void Delete(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs) BeginDeleteCallback?.Invoke(cr);
            lock (GlobalLock)
            {
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
            }

            foreach (T cr in objs) EndDeleteCallback?.Invoke(cr);
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void DeleteWithOpenTransaction(ISession session, S id)
        {
            DeleteWithOpenTransaction(session, GetByID(id));
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, T cr)
        {
            if (cr == null) return;
            lock (GlobalLock)
            {
                DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                Cache.Remove(cr);
                session.Delete(cr);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void DeleteWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (GlobalLock)
            {
                foreach (T cr in objs)
                {
                    DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                    Cache.Remove(cr);
                    session.Delete(cr);
                }
            }
        }

        public virtual void Save(T obj)
        {
            BeginSaveCallback?.Invoke(obj);
            lock (GlobalLock)
            {
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
            }
            EndSaveCallback?.Invoke(obj);
        }

        public virtual void Save(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (GlobalLock)
            {
                foreach (T obj in objs)
                {
                    BeginSaveCallback?.Invoke(obj);
                }

                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (T obj in objs)
                        {
                            session.SaveOrUpdate(obj);
                            SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);

                            Cache.Update(obj);
                        }

                        transaction.Commit();
                    }
                }

                foreach (T obj in objs)
                {
                    EndSaveCallback?.Invoke(obj);
                }
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISessionWrapper session, T obj)
        {
            lock (GlobalLock)
            {
                if (Equals(SelectKey(obj), default(S)))
                    session.Insert(obj);
                else
                    session.Update(obj);

                SaveWithOpenTransactionCallback?.Invoke(session, obj);
                Cache.Update(obj);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, T obj)
        {
            lock (GlobalLock)
            {
                session.SaveOrUpdate(obj);
                SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                Cache.Update(obj);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void SaveWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (GlobalLock)
            {
                foreach (T obj in objs)
                {
                    session.SaveOrUpdate(obj);
                    SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                    Cache.Update(obj);
                }
            }
        }

        public abstract void PopulateIndexes();
        public abstract void RegenerateDb();

        public virtual void PostProcess()
        {
        }
    }
}
