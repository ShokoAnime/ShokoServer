using System;
using System.Data;
using NHibernate;

namespace Shoko.Server.Repositories.NHibernate
{
    public interface ISessionWrapper
    {
        ICriteria CreateCriteria<T>() where T : class;

        ICriteria CreateCriteria(Type type);

        IQuery CreateQuery(string query);

        ISQLQuery CreateSQLQuery(string query);

        IQueryOver<T, T> QueryOver<T>() where T : class;

        TObj Get<TObj>(object id);

        ITransaction BeginTransaction();

        void Insert(object entity);

        void Update(object entity);

        void Delete(object entity);

        IDbConnection Connection { get; }
    }
}