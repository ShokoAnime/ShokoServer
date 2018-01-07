using Force.DeepCloner;

namespace Shoko.Server.Repositories
{
    public class AtomicUpdate<T,TS,TT> : IAtomic<T,TT> where T : class, new()
    {
        private readonly BaseRepository<T,TS,TT> _repo;
        public T Entity { get; }
        public T Original { get; }

        internal AtomicUpdate(BaseRepository<T, TS,TT> repo, T obj=null, bool isupdate=true)
        {
            _repo = repo;
            Original = obj;
            if (obj == null)
            {
                Entity = new T();
                IsUpdate = false;
            }
            else if (!isupdate)
            {
                Entity = obj;
                IsUpdate = false;
            }
            else
            {
                Entity = obj.DeepClone();
                IsUpdate = true;
            }
        }
        public void Dispose()
        {
            Release();
        }


        public bool IsUpdate { get;  }

        public T Commit(TT pars=default(TT)) 
        // Pars are the extra parameters send to the save and delete callbacks, in this way we can forward behaviors to the callbacks
        {
            object obj=_repo.BeginSave(Entity,Original, pars);
            using (_repo.CacheLock.ReaderLock())
            {
                _repo.Table.Attach(Entity);
                _repo.Context.SaveChanges();
                if (_repo.IsCached)
                {
                    Release();
                    _repo.Cache.Update(Entity);
                }
            }
            _repo.EndSave(Entity,Original, obj, pars);
            return Entity;
        }

       

        private void Release()
        {

        }

    }
}
