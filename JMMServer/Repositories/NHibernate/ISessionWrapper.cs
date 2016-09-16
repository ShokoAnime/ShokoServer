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

        IDbConnection Connection { get; }


    }
}