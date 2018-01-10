using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories
{
    public interface IRepository<T,S,TT> : IRepository<T> where T : class
    {
        T GetByID(S id);
        List<T> GetAll();
        List<T> GetMany(IEnumerable<S> ids);
        void Delete(S id, TT pars);
        void Delete(T obj, TT pars);
        void Delete(IEnumerable<T> objs, TT pars);
        IAtomic<T, TT> BeginAdd();
        IAtomic<T, TT> BeginAdd(T obj);
        IAtomicList<T, TT> BeginAdd(IEnumerable<T> objs);
        IAtomic<T,TT> BeginAddOrUpdate(S id);

        IAtomic<T, TT> BeginAddOrUpdateWithLock(Func<T> find_function); //This method applies a lock on the repository
                                                                        //The find_function is called inside the lock, the lock is mantained, till the IAtomic is commited or released.
                                                                        //So, it mantain atomicity, on Find, Update, Commit.
                                                                       
        IAtomic<T, TT> BeginUpdate(T obj);
        IAtomicList<T, TT> BeginUpdate(IEnumerable<T> objs);
        IAtomicList<T, TT> BeginUpdate(IEnumerable<S> ids);
        IBatchAtomic<T,TT> BeginBatchUpdate(IEnumerable<S> ids);

    }
    public interface IRepository
    {
        void PreInit(IProgress<InitProgress> progress, int batchSize);
        void PostInit(IProgress<InitProgress> progress, int batchSize);
        string Name { get; }
        void SwitchCache(bool cache);

    }

    public interface IRepository<T> : IRepository where T: class
    {
        void SetContext(ShokoContext db, DbSet<T> table);
    }
}
