using System.Collections.Generic;


namespace Shoko.Server.RepositoriesV2
{
    public interface IRepository<T,S,TT>
    {
        T GetByID(S id);
        List<T> GetAll();
        List<T> GetMany(IEnumerable<S> ids);
        void Delete(S id, TT pars);
        void Delete(T obj, TT pars);
        void Delete(IEnumerable<T> objs, TT pars);
        IAtomic<T, TT> BeginAdd(TT pars);
        IAtomic<T,TT> BeginUpdate(T obj);
        IAtomic<T,TT> BeginAddOrUpdate(S id);
        IAtomicList<T, TT> BeginUpdate(IEnumerable<T> objs);
        IAtomicList<T, TT> BeginUpdate(IEnumerable<S> ids);
        void PopulateCache();
        void SwitchCache(bool cache);
    }
}
