using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;

namespace Shoko.Server.RepositoriesV2
{
    public class AtomicUpdateList<T,TS, TT> : IAtomicList<T,TT> where T : class
    {
        private readonly BaseRepository<T,TS,TT> _repo;
        public List<T> UpdatableList { get; }
        public List<T> Original => References.Values.ToList();

        private Dictionary<T,T> References { get; }

         
        internal AtomicUpdateList(BaseRepository<T, TS, TT> repo, List<T> objs)
        {
            _repo = repo;
            References = new Dictionary<T, T>();
            UpdatableList = new List<T>();
            foreach (T t in objs)
            {
                T n = t.DeepClone();
                UpdatableList.Add(n);
                References.Add(n, t);
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


        public bool IsUpdate => true;

        public void Commit(TT pars = default(TT))
        {
            Dictionary<T, object> savedObjects=new Dictionary<T, object>();
            foreach (T t in UpdatableList)
            {
                savedObjects[t]=_repo.BeginSave(t, GetOriginal(t),pars);
            }
            using (_repo.CacheLock.ReaderLock())
            {
                foreach (T obj in UpdatableList)
                    _repo.Table.Attach(obj);
                _repo.Context.SaveChanges();
                if (_repo.IsCached)
                {
                    Release();
                    UpdatableList.ForEach(_repo.Cache.Update);
                }
            }
            foreach (T t in UpdatableList)
            {
                _repo.EndSave(t, GetOriginal(t), savedObjects[t], pars);
            }
        }


        private void Release()
        {

        }
    }
}
