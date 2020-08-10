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
    public abstract class BaseCachedRepository<T, S> : ICachedRepository, IRepository<T, S> where T : class, new()
    {
        internal PocoCache<S, T> Cache;

        // Lock to allow updates from multiple threads. As a general rule, we lock the entire call to avoid stale state
        protected readonly object globalDBLock = new object();

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
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    Cache.Clear();
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        public virtual T GetByID(S id)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Get(id);
                }
            }
        }

        public T GetByID(ISession session, S id)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Get(id);
                }
            }
        }

        public T GetByID(ISessionWrapper session, S id)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Get(id);
                }
            }
        }

        public virtual IReadOnlyList<T> GetAll()
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Values.ToList();
                }
            }
        }

        public IReadOnlyList<T> GetAll(int max_limit)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Values.Take(max_limit).ToList();
                }
            }
        }

        public IReadOnlyList<T> GetAll(ISession session)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Values.ToList();
                }
            }
        }

        public IReadOnlyList<T> GetAll(ISessionWrapper session)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    return Cache.Values.ToList();
                }
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
            lock (globalDBLock)
            {
                lock (Cache)
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
            }
            EndDeleteCallback?.Invoke(cr);
        }

        public virtual void Delete(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs) BeginDeleteCallback?.Invoke(cr);
            lock (globalDBLock)
            {
                lock (Cache)
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
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                    Cache.Remove(cr);
                    session.Delete(cr);
                }
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void DeleteWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    foreach (T cr in objs)
                    {
                        DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                        Cache.Remove(cr);
                        session.Delete(cr);
                    }
                }
            }
        }

        public virtual void Save(T obj)
        {
            BeginSaveCallback?.Invoke(obj);
            lock (globalDBLock)
            {
                lock (obj)
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
                    lock (Cache)
                    {
                        Cache.Update(obj);
                    }
                }
            }
            EndSaveCallback?.Invoke(obj);
        }

        public virtual void Save(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T obj in objs)
            {
                BeginSaveCallback?.Invoke(obj);
            }
            lock (globalDBLock)
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (T obj in objs)
                        {
                            session.SaveOrUpdate(obj);
                            SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                            lock (Cache)
                            {
                                Cache.Update(obj);
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
            foreach (T obj in objs)
            {
                EndSaveCallback?.Invoke(obj);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISessionWrapper session, T obj)
        {
            lock (globalDBLock)
            {
                lock (obj)
                {
                    if (Equals(SelectKey(obj), default(S)))
                        session.Insert(obj);
                    else
                        session.Update(obj);

                    SaveWithOpenTransactionCallback?.Invoke(session, obj);
                    lock (Cache)
                    {
                        Cache.Update(obj);
                    }
                }
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, T obj)
        {
            lock (globalDBLock)
            {
                lock (obj)
                {
                    session.SaveOrUpdate(obj);
                    SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                    lock (Cache)
                    {
                        Cache.Update(obj);
                    }
                }
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void SaveWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (globalDBLock)
            {
                foreach (T obj in objs)
                {
                    lock (obj)
                    {
                        session.SaveOrUpdate(obj);
                        SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                        Cache.Update(obj);
                    }
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
