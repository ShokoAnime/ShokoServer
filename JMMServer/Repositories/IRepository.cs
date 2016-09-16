using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMServer.Repositories.NHibernate;
using NHibernate;
// ReSharper disable InconsistentNaming

namespace JMMServer.Repositories
{
    public interface IRepository<T,S>
    {
        T GetByID(S id);
        T GetByID(ISession session, S id);
        T GetByID(ISessionWrapper session, S id);
        List<T> GetAll();
        List<T> GetAll(ISession session);
        List<T> GetAll(ISessionWrapper session);
        void Delete(S id);
        void Delete(T cr);
        void Delete(List<T> objs);
        void Save(T obj);
        void Save(List<T> objs);

        Action<T> BeginDeleteCallback { get; set; }
        Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }
        Action<T> EndDeleteCallback { get; set; }
        Action<T> BeginSaveCallback { get; set; }
        Action<ISession, T> SaveWithOpenTransactionCallback { get; set; }
        Action<T> EndSaveCallback { get; set; }
    }
}
