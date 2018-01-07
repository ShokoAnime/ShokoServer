using System;
using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;

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



        internal AtomicBatchUpdate(BaseRepository<T, TS, TT> repo, List<TS> originalsKeys)
        {
            _originalsKeys = originalsKeys;
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
                using (_repo.CacheLock.ReaderLock())
                {
                    _repo.Table.RemoveRange(listed);
                    _repo.Context.SaveChanges();
                    if (_repo.IsCached)
                        listed.ForEach(_repo.Cache.Remove);
                }

                foreach (T e in listed)
                    _repo.EndDelete(e, savedobjects[e], pars);
            }

            if (References.Count > 0)
            {
                savedobjects = new Dictionary<T, object>();
                foreach (KeyValuePair<T, T> t in References)
                {
                    savedobjects[t.Key] = _repo.BeginSave(t.Key, t.Value, pars);
                }
                using (_repo.CacheLock.ReaderLock())
                {
                    foreach (KeyValuePair<T, T> t in References)
                        _repo.Table.Attach(t.Key);
                    _repo.Context.SaveChanges();
                    if (_repo.IsCached)
                    {
                        Release();
                        References.ForEach(a => _repo.Cache.Update(a.Key));
                    }
                }
                foreach (KeyValuePair<T, T> t in References)
                {
                    _repo.EndSave(t.Key, t.Value, savedobjects[t.Key], pars);
                }
            }
            return References.Keys.ToList();
        }

        public void Dispose()
        {
        }

        private void Release()
        {

        }


    }
}