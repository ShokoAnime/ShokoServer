using System;
using System.Data;
using NHibernate;

namespace JMMServer.Repositories.NHibernate
{
    public interface ISessionWrapper
    {
        ICriteria CreateCriteria<T>() where T : class;

        ICriteria CreateCriteria(Type type);

        IQuery CreateQuery(string query);

        ISQLQuery CreateSQLQuery(string query);

        TObj Get<TObj>(object id);

        ITransaction BeginTransaction();

        void Insert(object entity);

        void Update(object entity);

        void Delete(object entity);

        IDbConnection Connection { get; }
    }
}