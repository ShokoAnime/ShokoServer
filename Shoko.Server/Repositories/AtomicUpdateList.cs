using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;

namespace Shoko.Server.Repositories
{
    public class AtomicUpdateList<T,TS, TT> : IAtomicList<T,TT> where T : class,new()
    {
        private readonly BaseRepository<T,TS,TT> _repo;
        public List<T> EntityList => References.Keys.ToList();

        private Dictionary<T,T> References { get; }=new Dictionary<T, T>();

         
        internal AtomicUpdateList(BaseRepository<T, TS, TT> repo, List<T> objs, bool isUpdate=true)
        {
            _repo = repo;
            IsUpdate = isUpdate;
            if (isUpdate)
            {
                foreach (T t in objs)
                {
                    T n = t.DeepClone();
                    References.Add(n, t);
                }
            }
            else
            {
                foreach (T t in objs)
                {
                    References.Add(t, null);
                }
            }
        }



        public T GetOriginal(T obj)
        {
            if (References.ContainsKey(obj))
                return References[obj];
            return null;
        }





        public void Dispose()
        {
            Release();
        }


        public bool IsUpdate { get;  }

        public List<T> Commit(TT pars = default(TT))
        {
            Dictionary<T, object> savedObjects=new Dictionary<T, object>();
            foreach (T t in EntityList)
            {
                savedObjects[t]=_repo.BeginSave(t, GetOriginal(t),pars);
            }
            using (_repo.CacheLock.ReaderLock())
            {
                foreach (T obj in EntityList)
                    _repo.Table.Attach(obj);
                _repo.Context.SaveChanges();
                if (_repo.IsCached)
                {
                    Release();
                    EntityList.ForEach(_repo.Cache.Update);
                }
            }
            foreach (T t in EntityList)
            {
                _repo.EndSave(t, GetOriginal(t), savedObjects[t], pars);
            }

            return EntityList;
        }


        private void Release()
        {

        }
    }
}
