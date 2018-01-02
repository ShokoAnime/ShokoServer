using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Shoko.Server.Databases;


namespace Shoko.Server.RepositoriesV2
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
        IAtomic<T,TT> BeginUpdate(T obj);
        IAtomic<T,TT> BeginAddOrUpdate(S id);
        IAtomicList<T, TT> BeginUpdate(IEnumerable<T> objs);
        IAtomicList<T, TT> BeginUpdate(IEnumerable<S> ids);

    }
    public interface IRepository
    {
        void Init(IProgress<RegenerateProgress> progress, int batchSize);
        string Name { get; }
        void SwitchCache(bool cache);

    }

    public interface IRepository<T> : IRepository where T: class
    {
        void SetContext(ShokoContext db, DbSet<T> table);
    }
}
