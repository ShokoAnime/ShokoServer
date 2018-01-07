using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
        internal AsyncReaderWriterLock CacheLock=new AsyncReaderWriterLock();
        
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

        public IQueryable<T> Where(Expression<Func<T, bool>> predicate)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values.Where(predicate.Compile()).AsQueryable();
                return Table.Where(predicate);
            }
        }

        public IQueryable<T> WhereAll()
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values.AsQueryable();
                return Table;
            }
        }
        public IQueryable<T> WhereMany(IEnumerable<TS> ids)
        {
            using (CacheLock.ReaderLock())
            {
                List<TS> ls = ids.ToList();
                return IsCached ? Cache.GetMany(ls).AsQueryable() : Table.Where(a => ls.Contains(SelectKey(a)));
            }
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

        internal virtual void EndSave(T entity, T original_entity, object returnFromBeginSave, TT parameters)
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
}
