using Force.DeepCloner;

namespace Shoko.Server.RepositoriesV2
{
    public class AtomicUpdate<T,S,TT> : IAtomic<T,TT> where T : class
    {
        private readonly BaseRepository<T,S,TT> _repo;
        public T Updatable { get; }
        public T Original { get; }

        internal AtomicUpdate(BaseRepository<T, S,TT> repo, T obj)
        {
            _repo = repo;
            Original = obj;
            if (obj == null)
            {
                Updatable = default(T);
                IsUpdate = false;
            }
            else
            {

                Updatable = obj.DeepClone();
                IsUpdate = true;
            }

        }
        public void Dispose()
        {
            Release();
        }


        public bool IsUpdate { get;  }

        public void Commit(TT pars=default(TT)) 
        // Pars are the extra parameters send to the save and delete callbacks, in this way we can forward behaviors to the callbacks
        {
            object obj=_repo.BeginSave(Updatable,Original, pars);
            using (_repo.CacheLock.ReaderLock())
            {
                _repo.Table.Attach(Updatable);
                _repo.Context.SaveChanges();
                if (_repo.IsCached)
                {
                    Release();
                    _repo.Cache.Update(Updatable);
                }
            }
            _repo.EndSave(Updatable,Original, obj, pars);
        }

       

        private void Release()
        {

        }

    }
}
