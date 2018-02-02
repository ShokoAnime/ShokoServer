using System;
using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories
{
    public class AtomicBatchUpdate<T,TS,TT> : IBatchAtomic<T, TT> where T : class, new()
    {
        private readonly List<TS> _originalsKeys;

        private readonly BaseRepository<T, TS, TT> _repo;
        public List<T> EntityList => References.Keys.ToList();
        public T GetOriginal(T obj)
        {
            return References.ContainsKey(obj) ? References[obj] : null;
        }

        private Dictionary<T, T> References { get; } = new Dictionary<T, T>();



        internal AtomicBatchUpdate(BaseRepository<T, TS, TT> repo, List<TS> originalsKeys=null)
        {
            _originalsKeys = originalsKeys ?? new List<TS>();
            _repo = repo;
        }
        //The first parameter, have the find function, this function might return an item from the db, or null in case it dosnt exists.
        //The second paramater contains the fill function, this is the update or the new data for the item.
        public T Process(Func<T> find, Action<T> populate)
        {
            T entity = find();
            T original = entity;
            if (entity == null)
            {
                entity=new T();
                _repo.Context.SetLocalKey(new T(), _repo.GetNextAutoGen);
            }
            else
            {
                entity = entity.DeepClone();
            }
            populate(entity);
            TS key = _repo.SelectKey(entity);
            if (_originalsKeys.Contains(key))
                _originalsKeys.Remove(key);
            References.Add(entity,original);
            return entity;
        }

        public bool IsUpdate => false;

        public List<T> Commit(TT pars = default(TT))
        {
            Dictionary<T, object> savedobjects = new Dictionary<T, object>();
            if (_originalsKeys.Count > 0)
            {
                List<T> listed = _repo.GetMany(_originalsKeys);
                foreach (T e in listed)
                    savedobjects[e] = _repo.BeginDelete(e, pars);
                using (_repo.RepoLock.ReaderLock())
                {
                    _repo.Table.RemoveRange(listed);
                    _repo.Context.SaveChanges();
                    if (_repo.IsCached)
                        listed.ForEach(_repo.Cache.Remove);
                }

                foreach (T e in listed)
                    _repo.EndDelete(e, savedobjects[e], pars);
            }
            List<T> returns = new List<T>();

            if (References.Count > 0)
            {
                Dictionary<T, object> savedObjects = new Dictionary<T, object>();
                foreach (T t in EntityList)
                {
                    savedObjects[t] = _repo.BeginSave(t, References[t], pars);
                }
                var updates = References.Where(a => a.Value != null).ToList();
                var creates = References.Where(a => a.Value == null).ToList();
                using (_repo.RepoLock.WriterLock())
                {
                    foreach (KeyValuePair<T, T> r in updates)
                    {

                        r.Key.DeepCloneTo(r.Value); //Tried to be 100% atomic and failed miserably, so is 99%. 
                                                    //If we replace Original with Entity in cache (updating with 'this' as the model to update, will not get the changes).
                                                    //So this is the best effort
                        returns.Add(r.Value);
                    }
                    foreach (KeyValuePair<T, T> r in creates)
                    {
                        _repo.Table.Add(r.Key);
                        returns.Add(r.Key);
                    }
                    if (_repo.IsCached)
                        returns.ForEach(_repo.Cache.Update);
                }
                _repo.Context.SaveChanges();
                foreach (T t in returns)
                {
                    _repo.EndSave(t, savedObjects[t], pars);
                }
            }
            Release();

            return returns;
        }

        public void Dispose()
        {
        }

        private void Release()
        {
            References.Clear();
            _originalsKeys.Clear();
        }


    }
}