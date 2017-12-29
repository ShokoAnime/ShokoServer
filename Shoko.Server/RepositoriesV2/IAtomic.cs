using System;
using System.Collections.Generic;


namespace Shoko.Server.RepositoriesV2
{
    public interface IAtomic<T,TT> : IBaseAtomic<TT>
    {
        T Updatable { get; }
        T Original { get; }
    }

    public interface IBaseAtomic<T> : IDisposable
    {
        bool IsUpdate { get; }
        void Commit(T pars = default(T));
    }
    public interface IAtomicList<T, TT> : IBaseAtomic<TT>
    {
        List<T> UpdatableList { get; }
        T GetOriginal(T obj);
    }
}
