using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Nito.AsyncEx;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories
{
    public abstract class BaseRepository<T, TS> : BaseRepository<T, TS, object> where T : class, new()
    {
    
    }

    public abstract class BaseRepository<T,TS,TT> : IRepository<T, TS, TT> where T : class, new()
    {
        //This Repo lacks locks on updates when cache is active, because the overegineering complexity.
        //So, if two threads wants to update the same entity at the same time, only the last one commited will get the changes through.
        //Optimistic locking write through :P

        internal bool IsCached;
        internal PocoCache<TS, T> Cache;
        internal DbSet<T> Table;
        internal ShokoContext Context;
        internal ReaderWriterLockSlim CacheLock=new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        
        public static TU Create<TU>(ShokoContext context,DbSet<T> table, bool cache) where TU : BaseRepository<T, TS,TT>,new()
        {
            TU repo = new TU();
            repo.Table = table;
            repo.Context = context;
            repo.SwitchCache(cache);
            return repo;
        }


        internal abstract TS SelectKey(T entity);

        public T GetByID(TS id)
        {
            using (CacheLock.ReaderLock())
                return IsCached ? Cache.Get(id) : Table.FirstOrDefault(a => SelectKey(a).Equals(id));
        }
        private T InternalGetByID(TS id)
        {
            return IsCached ? Cache.Get(id) : Table.FirstOrDefault(a => SelectKey(a).Equals(id));
        }


        public virtual List<T> GetMany(IEnumerable<TS> ids)
        {
            using (CacheLock.ReaderLock())
            {
                List<TS> ls = ids.ToList();
                return IsCached ? Cache.GetMany(ls).ToList() : Table.Where(a => ls.Contains(SelectKey(a))).ToList();
            }
        }
        public virtual List<T> GetAll()
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Cache.Values.ToList() : Table.ToList();
            }
        }
        public virtual List<TS> GetIds()
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Cache.Keys.ToList() : Table.Select(SelectKey).ToList();
            }
        }
        public void Delete(TS id, TT pars = default(TT))
        {
            Delete(InternalGetByID(id));
        }

        public void Delete(T obj, TT pars=default(TT))
        {
            if (obj == null)
                return;
            object ret=BeginDelete(obj,pars);
            using (CacheLock.ReaderLock())
            {
                Table.Remove(obj);
                Context.SaveChanges();
                if (IsCached)
                    Cache.Remove(obj);
            }
            EndDelete(obj,ret, pars);
        }

  
        public void Delete(IEnumerable<T> objs, TT pars = default(TT))
        {
            if (objs == null)
                return;
            List<T> listed = objs.ToList();
            if (listed.Count == 0)
                return;
            Dictionary<T,object> savedobjects=new Dictionary<T, object>();
            foreach (T e in listed)
                savedobjects[e]=BeginDelete(e, pars);
            using (CacheLock.ReaderLock())
            {
                Table.RemoveRange(listed);
                Context.SaveChanges();
                if (IsCached)
                    listed.ForEach(Cache.Remove);
            }
            foreach (T e in listed)
                EndDelete(e, savedobjects[e], pars);
        }



        private int IntAutoGen = -1;
        private readonly object IntAutoLock = new object();
        internal int GetNextAutoGen(PropertyInfo prop)
        {
            lock (IntAutoLock)
            {
                if (IntAutoGen == -1)
                {

                    IntAutoGen = (int) Table.Max(a=>prop.GetValue(a,null));
                }
                IntAutoGen++;
            }
            return IntAutoGen;
        }

        
        public IAtomic<T, TT> BeginAdd()
        {
            AtomicUpdate<T, TS, TT> upd = new AtomicUpdate<T, TS, TT>(this);
            Context.SetLocalKey(upd.Entity,GetNextAutoGen);
            return upd;
        }

        public IAtomic<T, TT> BeginAddOrUpdateWithLock(Func<T> find_function, T default_value=null)
        {
            return new AtomicLockUpdate<T,TS,TT>(this,find_function,default_value);
        }

        public IAtomic<T,TT> BeginUpdate(T obj)
        {
            using (CacheLock.ReaderLock())
                return new AtomicUpdate<T,TS,TT>(this, obj);
        }


        public IAtomic<T, TT> BeginAddOrUpdate(TS id)
        {
            IAtomic<T, TT> update = BeginUpdate(id);
            if (update == null)
            {
                update = BeginAdd();
                List<PropertyInfo> props = Context.GetPrimaries<T>();
                PropertyInfo n = props.FirstOrDefault(a => a.PropertyType == typeof(TS));
                n.SetValue(update.Entity,id);
            }

            return update;
        }
        public IAtomic<T, TT> BeginAddOrUpdate(Func<T> obj)
        {
            T entity = obj();
            if (entity != null)
                return BeginUpdate(entity);
            return BeginAdd();
        }
        public IAtomic<T, TT> BeginAdd(T obj)
        {
            if (obj != null)
                return new AtomicUpdate<T, TS, TT>(this, obj, false);
            return BeginAdd();
        }
        public IAtomic<T,TT> BeginUpdate(TS key)
        {
            using (CacheLock.ReaderLock())
            {
                T obj = InternalGetByID(key);
                if (obj == null)
                    return null;
                return new AtomicUpdate<T, TS,TT>(this, obj);
            }
        }

        public IAtomicList<T, TT> BeginAdd(IEnumerable<T> objs)
        {
            if (objs == null)
                throw new ArgumentException("Unable to add a null value");
            using (CacheLock.ReaderLock())
            {
                return new AtomicUpdateList<T, TS, TT>(this, objs.ToList(),false);
            }
        }


        public IAtomicList<T,TT> BeginUpdate(IEnumerable<T> objs)
        {
            if (objs == null)
                throw new ArgumentException("Unable to add a null value");
            using (CacheLock.ReaderLock())
            {
                return new AtomicUpdateList<T, TS,TT>(this, objs.ToList());
            }
        }

        public IAtomicList<T, TT> BeginUpdate(IEnumerable<TS> ids)
        {
            if (ids == null)
                throw new ArgumentException("Unable to add a null value");
            using (CacheLock.ReaderLock())
                return new AtomicUpdateList<T, TS, TT>(this, GetMany(ids));
        }

        //The idea of this method, is returning a class, capable of autodealing with an update of a collection, mantaining, which objects to delete, update or add.
        //The input is the original keys of the collection before the update, this is needed to calculate at the end, the items to delete from the collection
        public IBatchAtomic<T, TT> BeginBatchUpdate(IEnumerable<TS> originalIds)
        {
            return new AtomicBatchUpdate<T,TS,TT>(this, originalIds.ToList());
        }

        internal void BatchAction(IEnumerable<T> items, int batchSize, Action<T, T> peritemaction, TT pars=default(TT))
        {
            foreach (T[] batch in items.Batch(batchSize))
            {
                using (IAtomicList<T, TT> update = BeginUpdate(batch))
                {
                    foreach(T t in update.EntityList)
                        peritemaction(t, update.GetOriginal(t));
                    update.Commit(pars);
                }
            }
        }
        
        public virtual void PopulateCache()
        {
            Cache = new PocoCache<TS, T>(Table, SelectKey);
            PopulateIndexes();
        }
        internal virtual void ClearCache()
        {
            ClearIndexes();
            Cache = null;
            GC.Collect();
        }

        internal virtual RepositoryCache Supports()
        {
            return RepositoryCache.SupportsCache | RepositoryCache.SupportsDirectAccess;
        }
        public void SwitchCache(bool cache)
        {
            RepositoryCache supports = Supports();
            if (supports == (RepositoryCache.SupportsCache | RepositoryCache.SupportsDirectAccess))
            {
                if (IsCached != cache)
                {
                    using (CacheLock.WriterLock())
                    {
                        if (IsCached)
                        {
                            IsCached = false;
                            ClearCache();
                        }
                        else
                        {
                            PopulateCache();
                            IsCached = true;
                        }
                    }
                }
            }
            else if (supports==RepositoryCache.SupportsCache)
            {
                IsCached = true;
                PopulateCache();
            }
            else if (supports == RepositoryCache.SupportsDirectAccess)
            {
                IsCached = false;
                ClearCache();
            }
        }
        
        protected IQueryable<T> Where(Expression<Func<T, bool>> predicate)
        {
            if (IsCached)
                return Cache.Values.Where(predicate.Compile()).AsQueryable();
            return Table.Where(predicate);
        }

        protected IQueryable<T> WhereAll()
        {
            if (IsCached)
                return Cache.Values.AsQueryable();
            return Table;
        }
        protected IQueryable<T> WhereMany(IEnumerable<TS> ids)
        {
            List<TS> ls = ids.ToList();
            return IsCached ? Cache.GetMany(ls).AsQueryable() : Table.Where(a => ls.Contains(SelectKey(a)));
        }

        internal abstract void PopulateIndexes();

        internal abstract void ClearIndexes();



        internal virtual void PostProcess()
        {
           
        }

        internal virtual object BeginSave(T entity, T original_entity, TT parameters)
        {
            return null;
        }

        internal virtual void EndSave(T entity, object returnFromBeginSave, TT parameters)
        {

        }

        internal virtual object BeginDelete(T entity, TT parameters)
        {
            return null;
        }

        internal virtual void EndDelete(T entity, object returnFromBeginDelete, TT parameters)
        {
        }

        public void SetContext(ShokoContext db, DbSet<T> table)
        {
            Context = db;
            Table = table;
        }

        public virtual void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            
        }
        public virtual void PostInit(IProgress<InitProgress> progress, int batchSize)
        {

        }
        public string Name => Table.GetName();
    }
    public static class ReaderWriterLockSlimExtension
    {
        /// &lt;summary&gt;
        /// Obtains a Read lock on the ReaderWriterLockSlim object
        /// &lt;/summary&gt;
        /// &lt;param name="readerWriterLock"&gt;The reader writer lock.&lt;/param&gt;
        /// &lt;returns&gt;An IDisposable object that will release the lock on disposal&lt;/returns&gt;
        public static IDisposable ReaderLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Read);
        }

        /// &lt;summary&gt;
        /// Obtains an Upgradeable Read lock on the ReaderWriterLockSlim object
        /// &lt;/summary&gt;
        /// &lt;param name="readerWriterLock"&gt;The reader writer lock.&lt;/param&gt;
        /// &lt;returns&gt;An IDisposable object that will release the lock on disposal&lt;/returns&gt;
        public static IDisposable UpgradeableReaderLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.UpgradeableRead);
        }

        /// &lt;summary&gt;
        /// Obtains a Write Lock on the ReaderWriterLockSlim object
        /// &lt;/summary&gt;
        /// &lt;param name="readerWriterLock"&gt;The reader writer lock.&lt;/param&gt;
        /// &lt;returns&gt;An IDisposable object that will release the lock on disposal&lt;/returns&gt;
        public static IDisposable WriterLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new DisposableLockWrapper(readerWriterLock, LockType.Write);
        }
    }
    /// &lt;summary&gt;Type of lock operation&lt;/summary&gt;
    public enum LockType
    {
        /// &lt;summary&gt;A read lock, allowing multiple simultaneous reads&lt;/summary&gt;
        Read,

        /// &lt;summary&gt;An upgradeable read, allowing multiple simultaneous reads, but with the potential that ths may be upgraded to a Write lock &lt;/summary&gt;
        UpgradeableRead,

        /// &lt;summary&gt;A blocking Write lock&lt;/summary&gt;
        Write,

        Bypass
    }

    /// &lt;summary&gt;Wrapper for the ReaderWriterLockSlim which allows callers to dispose the object to remove the lock &lt;/summary&gt;
    public class DisposableLockWrapper : IDisposable
    {
        /// &lt;summary&gt;The lock object being wrapped&lt;/summary&gt;
        private readonly ReaderWriterLockSlim readerWriterLock;

        /// &lt;summary&gt;The lock type&lt;/summary&gt;
        private readonly LockType lockType;

        /// &lt;summary&gt;
        /// Initializes a new instance of the &lt;see cref="DisposableLockWrapper"/&gt; class.
        /// &lt;/summary&gt;
        /// &lt;param name="readerWriterLock"&gt;The reader writer lock.&lt;/param&gt;
        /// &lt;param name="lockType"&gt;Type of the lock.&lt;/param&gt;
        public DisposableLockWrapper(ReaderWriterLockSlim readerWriterLock, LockType lockType)
        {

            this.readerWriterLock = readerWriterLock;
            this.lockType = lockType;

            switch (this.lockType)
            {
                case LockType.Read:
                    this.readerWriterLock.EnterReadLock();
                    break;

                case LockType.UpgradeableRead:
                    this.readerWriterLock.EnterUpgradeableReadLock();
                    break;

                case LockType.Write:
                    this.readerWriterLock.EnterWriteLock();
                    break;
            }
        }

        /// &lt;summary&gt;
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// &lt;/summary&gt;
        /// &lt;filterpriority&gt;2&lt;/filterpriority&gt;
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// &lt;summary&gt;
        /// Releases unmanaged and - optionally - managed resources
        /// &lt;/summary&gt;
        /// &lt;param name="disposing"&gt;&lt;c&gt;true&lt;/c&gt; to release both managed and unmanaged resources; &lt;c&gt;false&lt;/c&gt; to release only unmanaged resources.&lt;/param&gt;
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed objects
                switch (this.lockType)
                {
                    case LockType.Read:
                        this.readerWriterLock.ExitReadLock();
                        break;

                    case LockType.UpgradeableRead:
                        this.readerWriterLock.ExitUpgradeableReadLock();
                        break;

                    case LockType.Write:
                        this.readerWriterLock.ExitWriteLock();
                        break;
                }
            }
        }
    }
}
