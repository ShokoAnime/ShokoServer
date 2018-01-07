using System;
using System.Collections.Generic;

namespace Shoko.Server.Repositories
{
    public interface IAtomic<T,TT> : IBaseAtomic<T, TT>
    {
        T Entity { get; }
        T Original { get; }
    }

    public interface IBaseAtomic<T,TT> : IDisposable
    {
        bool IsUpdate { get; }
        T Commit(TT pars = default(TT));
    }

    public interface IBatchAtomic<T, TT> : IAtomicList<T, TT> 
    {
        T Process(Func<T> find, Action<T> populate);
       
    }
    public interface IAtomicList<T, TT> : IBaseAtomic<List<T>, TT>
    {
        List<T> EntityList { get; }
        T GetOriginal(T obj);
    }

}
