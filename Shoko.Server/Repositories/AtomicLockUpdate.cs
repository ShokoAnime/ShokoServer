using System;
using Force.DeepCloner;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories
{
    public class AtomicLockUpdate<T, TS, TT> : IAtomic<T, TT> where T : class, new()
    {
        private readonly BaseRepository<T, TS, TT> _repo;
        public T Entity { get; private set; }
        public T Original { get; private set; }
        private IDisposable _lock;

        internal AtomicLockUpdate(BaseRepository<T, TS, TT> repo, Func<T> function, Func<T> default_function = null)
        {
            _repo = repo;
            _lock = _repo.RepoLock.WriterLock();
            Original = function();
            if (Original == null)
            {
                Entity = default_function != null ? default_function() : new T();
                IsUpdate = false;
            }
            else
            {
                Entity = Original.DeepClone();
                IsUpdate = true;
            }
        }
        public void Dispose()
        {
            Release();
        }


        public bool IsUpdate { get; }

        public T Commit(TT pars = default(TT))
        // Pars are the extra parameters send to the save and delete callbacks, in this way we can forward behaviors to the callbacks
        {
            object obj = _repo.BeginSave(Entity, Original, pars);
            T ret;
            ShokoContext ctx = _repo.Provider.GetContext();
            if (Original == null)
            {
                ret = Entity;
                ctx.Add(Entity);
            }
            else
            {
                ret = Original;
                Entity.DeepCloneTo(Original); //Tried to be 100% atomic and failed miserably, so is 99%. 
                                                //If we replace Original with Entity in cache (updating with 'this' as the model to update will not get the changes).
                                                //So this is the best effort
                ctx.Attach(Original);
                ctx.Update(Original);
            }
            if (_repo.IsCached)
                _repo.Cache.Update(ret);
            ctx.SaveChanges();
            _repo.EndSave(ret, obj, pars);
            return ret;
        }



        public void Release()
        {
            if (_lock != null)
            {
                _lock.Dispose();
                _lock = null;
            }
            Entity = null;
            Original = null;
        }

    }
}
