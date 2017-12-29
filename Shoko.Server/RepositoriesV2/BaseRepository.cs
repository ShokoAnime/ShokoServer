using System;
using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;
using Microsoft.EntityFrameworkCore;
using Nito.AsyncEx;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;

namespace Shoko.Server.RepositoriesV2
{
    public abstract class BaseRepository<T, TS> : BaseRepository<T, TS, object> where T : class
    {
    }

    public abstract class BaseRepository<T,TS,TT> : IRepository<T, TS, TT> where T : class 
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


        public List<T> GetMany(IEnumerable<TS> ids)
        {
            using (CacheLock.ReaderLock())
            {
                List<TS> ls = ids.ToList();
                return IsCached ? Cache.GetMany(ls).ToList() : Table.Where(a => ls.Contains(SelectKey(a))).ToList();
            }
        }
        public List<T> GetAll()
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Cache.Values.ToList() : Table.ToList();
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
            BeginDeleteCallback?.Invoke(obj,pars);
            using (CacheLock.ReaderLock())
            {
                Table.Remove(obj);
                Context.SaveChanges();
                if (IsCached)
                    Cache.Remove(obj);
            }
            EndDeleteCallback?.Invoke(obj,pars);
        }

  
        public void Delete(IEnumerable<T> objs, TT pars = default(TT))
        {
            if (objs == null)
                throw new ArgumentException("Unable to add a null value");
            List<T> listed = objs.ToList();
            if (listed.Count == 0)
                return;
            if (BeginDeleteCallback != null)
            {
                foreach (T e in listed)
                    BeginDeleteCallback(e, pars);
            }
            using (CacheLock.ReaderLock())
            {
                Table.RemoveRange(listed);
                Context.SaveChanges();
                if (IsCached)
                    listed.ForEach(Cache.Remove);
            }
            if (EndDeleteCallback != null)
            {
                foreach (T e in listed)
                    EndDeleteCallback(e, pars);
            }
        }
        public void Add(T obj,TT pars=default(TT))
        {
            if (obj == null)
                throw new ArgumentException("Unable to add a null value");
            BeginSaveCallback?.Invoke(obj,pars);
            using (CacheLock.ReaderLock())
            {
                Table.Add(obj);
                Context.SaveChanges();
                if (IsCached)
                    Cache.Update(obj);
            }
            EndSaveCallback?.Invoke(obj,pars);
        }
  
        public void Add(IEnumerable<T> objs, TT pars = default(TT))
        {
            if (objs == null)
                throw new ArgumentException("Unable to add a null value");
            List<T> listed = objs.ToList();
            if (listed.Count == 0)
                return;
            if (BeginSaveCallback != null)
            {
                foreach (T n in listed)
                    BeginSaveCallback(n, pars);
            }
            using (CacheLock.ReaderLock())
            {
                Table.AddRange(listed);
                Context.SaveChanges();
                if (IsCached)
                    listed.ForEach(Cache.Update);
            }
            if (EndSaveCallback != null)
            {
                foreach (T n in listed)
                    EndSaveCallback(n, pars);
            }
        }

        public IAtomic<T,TT> BeginAtomicUpdate(T obj)
        {
            using (CacheLock.ReaderLock())
                return new AtomicUpdate<T,TS,TT>(this, obj.DeepClone());
        }

        public IAtomic<T,TT> BeginAtomicUpdate(TS key)
        {
            using (CacheLock.ReaderLock())
            {
                T obj = InternalGetByID(key);
                if (obj == null)
                    return null;
                return new AtomicUpdate<T, TS,TT>(this, obj.DeepClone());
            }
        }



        public IAtomic<List<T>,TT> BeginAtomicBatchUpdate(IEnumerable<T> objs)
        {
            if (objs == null)
                throw new ArgumentException("Unable to add a null value");
            using (CacheLock.ReaderLock())
            {
                List<T> newobjs = new List<T>();
                foreach (T obj in objs)
                    newobjs.Add(obj.DeepClone());
                if (newobjs.Count == 0)
                    return null;
                return new AtomicUpdateList<T, TS,TT>(this, newobjs);
            }
        }

        public AtomicUpdateList<T, TS, TT> BeginAtomicBatchUpdate(IEnumerable<TS> ids)
        {
            if (ids == null)
                throw new ArgumentException("Unable to add a null value");
            using (CacheLock.ReaderLock())
                return new AtomicUpdateList<T, TS, TT>(this, GetMany(ids));
        }

        internal void BatchAction(IEnumerable<T> items, int batchSize, Action<T, T> peritemaction,Action<IAtomic<List<T>, TT>> perbatchaction)
        {
            foreach (T[] batch in items.Batch(batchSize))
            {
                using (IAtomic<List<T>, TT> update = BeginAtomicBatchUpdate(batch))
                {
                    foreach(T t in update.UpdateAble)
                    {
                        peritemaction(t,update.G)
                    }
                    action(update);
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
        public void SwitchCache(bool cache)
        {
            if (IsCached!=cache)
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

        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values.Where(predicate);
                return Table.Where(predicate);
            }
        }
        internal abstract void PopulateIndexes();

        internal abstract void ClearIndexes();

        internal virtual void RegenerateDb(IProgress<RegenerateProgress> progress, int batchsize=10)
        {
           
        }

        internal virtual void PostProcess()
        {
           
        }

    
        internal Action<T, TT> EndSaveCallback { get; set; }
        internal Action<T, TT> BeginDeleteCallback { get; set; }
        internal Action<T, TT> EndDeleteCallback { get; set; }
        internal Action<T, TT> BeginSaveCallback { get; set; }
    }
}
