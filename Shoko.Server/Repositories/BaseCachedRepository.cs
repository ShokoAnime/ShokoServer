using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
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
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            Populate(session.Wrap(), displayname);
        }

        public void ClearCache()
        {
            WriteLock(Cache.Clear);
        }

        // ReSharper disable once InconsistentNaming
        public virtual T GetByID(S id)
        {
            return ReadLock(() => GetByIDUnsafe(id));
        }

        public T GetByID(ISession session, S id)
        {
            return GetByID(id);
        }

        public T GetByID(ISessionWrapper session, S id)
        {
            return GetByID(id);
        }

        public virtual IReadOnlyList<T> GetAll()
        {
            return ReadLock(GetAllUnsafe);
        }

        public IReadOnlyList<T> GetAll(int maxLimit)
        {
            return ReadLock(() => GetAllUnsafe(maxLimit));
        }

        public IReadOnlyList<T> GetAll(ISession session)
        {
            return GetAll();
        }

        public IReadOnlyList<T> GetAll(ISessionWrapper session)
        {
            return GetAll();
        }

        public virtual void Delete(S id)
        {
            Delete(GetByID(id));
        }

        public virtual void Delete(T cr)
        {
            if (cr == null) return;
            BeginDeleteCallback?.Invoke(cr);
            lock (GlobalDBLock)
            {
                DeleteFromDatabaseUnsafe(cr);
            }

            DeleteFromCache(cr);
            EndDeleteCallback?.Invoke(cr);
        }

        protected void DeleteFromCache(T cr)
        {
            WriteLock(() => DeleteFromCacheUnsafe(cr));
        }

        protected void UpdateCache(T cr)
        {
            WriteLock(() => UpdateCacheUnsafe(cr));
        }

        public virtual void Delete(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (var cr in objs) BeginDeleteCallback?.Invoke(cr);
            lock (GlobalDBLock)
            {
                DeleteFromDatabaseUnsafe(objs);
            }

            WriteLock(
                () =>
                {
                    foreach (var cr in objs) DeleteFromCacheUnsafe(cr);
                }
            );

            foreach (var cr in objs) EndDeleteCallback?.Invoke(cr);
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
            lock (GlobalDBLock)
            {
                DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                session.Delete(cr);
            }
            WriteLock(() => DeleteFromCacheUnsafe(cr));
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void DeleteWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (GlobalDBLock)
            {
                foreach (var cr in objs)
                {
                    DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                    session.Delete(cr);
                }
            }

            WriteLock(
                () =>
                {
                    foreach (var cr in objs) DeleteFromCacheUnsafe(cr);
                }
            );
        }

        public virtual void Save(T obj)
        {
            BeginSaveCallback?.Invoke(obj);
            lock (GlobalDBLock)
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                using var transaction = session.BeginTransaction();
                SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                session.SaveOrUpdate(obj);
                transaction.Commit();
            }

            WriteLock(() => UpdateCacheUnsafe(obj));

            EndSaveCallback?.Invoke(obj);
        }

        public virtual void Save(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;

            foreach (var obj in objs) BeginSaveCallback?.Invoke(obj);

            lock (GlobalDBLock)
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                using var transaction = session.BeginTransaction();
                foreach (var obj in objs)
                {
                    session.SaveOrUpdate(obj);
                    SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                }

                transaction.Commit();
            }

            WriteLock(
                () =>
                {
                    foreach (var obj in objs) UpdateCacheUnsafe(obj);
                }
            );

            foreach (var obj in objs)
            {
                EndSaveCallback?.Invoke(obj);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISessionWrapper session, T obj)
        {
            lock (GlobalDBLock)
            {
                if (Equals(SelectKey(obj), default(S)))
                    session.Insert(obj);
                else
                    session.Update(obj);
            }

            SaveWithOpenTransactionCallback?.Invoke(session, obj);
            WriteLock(() => UpdateCacheUnsafe(obj));
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, T obj)
        {
            lock (GlobalDBLock)
            {
                session.SaveOrUpdate(obj);
            }

            SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);

            WriteLock(() => UpdateCacheUnsafe(obj));
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void SaveWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;

            lock (GlobalDBLock)
            {
                foreach (var obj in objs) session.SaveOrUpdate(obj);
            }

            foreach (var obj in objs)
            {
                SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
            }

            foreach (var obj in objs)
            {
                WriteLock(() => UpdateCacheUnsafe(obj));
            }
        }

        protected void ReadLock(Action action)
        {
            _lock.EnterReadLock();
            try
            {
                action.Invoke();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        protected T5 ReadLock<T5>(Func<T5> action)
        {
            _lock.EnterReadLock();
            try
            {
                return action.Invoke();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        protected void WriteLock(Action action)
        {
            _lock.EnterWriteLock();
            try
            {
                action.Invoke();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        protected T5 WriteLock<T5>(Func<T5> action)
        {
            _lock.EnterWriteLock();
            try
            {
                return action.Invoke();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

#region Unsafe
        public virtual void ClearCacheUnsafe()
        {
            Cache.Clear();
        }

        protected virtual T GetByIDUnsafe(S id)
        {
            return Cache.Get(id);
        }

        protected virtual IReadOnlyList<T> GetAllUnsafe()
        {
            return Cache.Values.ToList();
        }

        protected virtual IReadOnlyList<T> GetAllUnsafe(int maxLimit)
        {
            return Cache.Values.Take(maxLimit).ToList();
        }

        protected virtual void UpdateCacheUnsafe(T cr)
        {
            Cache.Update(cr);
        }

        protected virtual void DeleteFromCacheUnsafe(T cr)
        {
            Cache.Remove(cr);
        }

        private void DeleteFromDatabaseUnsafe(T cr)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using var transaction = session.BeginTransaction();
            DeleteWithOpenTransactionCallback?.Invoke(session, cr);
            session.Delete(cr);
            transaction.Commit();
        }

        private void DeleteFromDatabaseUnsafe(IReadOnlyCollection<T> objs)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using var transaction = session.BeginTransaction();

            foreach (var cr in objs)
            {
                DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                session.Delete(cr);
            }

            transaction.Commit();
        }
#endregion
#region abstract
        public abstract void PopulateIndexes();
        public abstract void RegenerateDb();

        public virtual void PostProcess()
        {
        }

        protected abstract S SelectKey(T entity);
#endregion
    }
}
