using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Shoko.Server.Repositories
{
    public interface IRepository<T,S,TT> : IRepository<T> where T : class
    {
        T GetByID(S id);
        List<T> GetAll();
        List<T> GetMany(IEnumerable<S> ids);
        IAtomic<T, TT> BeginAdd();
        IAtomic<T, TT> BeginAdd(T obj);
        IAtomicList<T, TT> BeginAdd(IEnumerable<T> objs);
        IAtomic<T, TT> BeginAddOrUpdate(Func<T> find_function, Func<T> create_function = null);
        //This methods applies a lock on the repository
        //The find_function is called inside the lock, the lock is mantained, till the IAtomic is commited or released.
        //So, it mantain atomicity, on Find, Update, Commit.
        bool FindAndDelete(Func<T> find_function, TT pars);
        bool FindAndDelete(Func<IEnumerable<T>> find_function, TT pars = default(TT));
        IBatchAtomic<T,TT> BeginBatchUpdate(Func<List<T>> find_original_items_function, bool delete_not_updated);
        T Touch(Func<T> find_function, TT pars);
        List<T> Touch(Func<IEnumerable<T>> find_function, TT pars);
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
        void SetContext(ShokoContextProvider db, DbSet<T> table);
    }
}
