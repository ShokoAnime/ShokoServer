using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;

namespace Shoko.Server.RepositoriesV2
{
    public class AtomicUpdateList<T,TT> : IAtomic<List<T>,TT> where T : class
    {
        private readonly BaseRepository<T,TS,TT> _repo;
        public List<T> Updatable { get; }
        public List<T> Original => References.Values.ToList();

        private Dictionary<T,T> References { get; }

        internal AtomicUpdateList<TS>(BaseRepository<T,TS,TT> repo, List<T> objs)
        {
            _repo = repo;
            References=new Dictionary<T, T>();
            Updatable=new List<T>();
            foreach (T t in objs)
            {
                T n = t.DeepClone();
                Updatable.Add(n);
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

        
        public void Commit(TT pars=default(TT))
        {
            if (_repo.BeginSaveCallback != null)
            {
                foreach (T t in Updatable)
                {
                    _repo.BeginSaveCallback(t, pars);
                }
            }
            using (_repo.CacheLock.ReaderLock())
            {
                foreach (T obj in Updatable)
                    _repo.Table.Attach(obj);
                _repo.Context.SaveChanges();
                if (_repo.IsCached)
                {
                    Release();
                    Updatable.ForEach(_repo.Cache.Update);
                }
            }
            if (_repo.EndSaveCallback != null)
            {
                foreach (T t in Updatable)
                {
                    _repo.EndSaveCallback(t, pars);
                }
            }
        }


        private void Release()
        {

        }
    }
}
