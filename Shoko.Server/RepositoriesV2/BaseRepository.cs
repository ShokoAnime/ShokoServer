using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                throw new ArgumentException("Unable to add a null value");
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
        private int GetNextAutoGen(PropertyInfo prop)
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
            AtomicUpdate<T, TS, TT> upd = new AtomicUpdate<T, TS, TT>(this, null);
            Context.SetLocalKey(upd.Updatable,GetNextAutoGen);
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
                n.SetValue(update.Updatable,id);
            }

            return update;
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
        
        internal void BatchAction(IEnumerable<T> items, int batchSize, Action<T, T> peritemaction, TT pars=default(TT))
        {
            foreach (T[] batch in items.Batch(batchSize))
            {
                using (IAtomicList<T, TT> update = BeginUpdate(batch))
                {
                    foreach(T t in update.UpdatableList)
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

        public virtual void Init(IProgress<RegenerateProgress> progress, int batchSize)
        {
            
        }

        public string Name => Table.GetName();
    }
}
