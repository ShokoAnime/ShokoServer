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
        void Release();
    }

    public interface IBatchAtomic<T, TT>  : IEnumerable<T>, IDisposable
    {
        void Update(T item);
        T Create();
        List<T> Commit(TT pars = default(TT));
        T FindOrCreate(Func<T, bool> predicate);

    }
    public interface IAtomicList<T, TT> : IBaseAtomic<List<T>, TT>
    {
        List<T> EntityList { get; }
        T GetOriginal(T obj);
    }

    public static class AtomicExtensions
    {
        public static bool IsNew<T, TT>(this IAtomic<T, TT> at)
        {
            return at.Original == null;
        }
        public static bool IsUpdate<T, TT>(this IAtomic<T, TT> at)
        {
            return at.Original != null;
        }
    }
}
