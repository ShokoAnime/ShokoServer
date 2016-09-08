using System;
using System.Data;
using NHibernate;

namespace JMMServer.Repositories.NHibernate
{
    internal class SessionWrapper : ISessionWrapper
    {
        private readonly ISession _session;

        public SessionWrapper(ISession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            _session = session;
        }

        public ICriteria CreateCriteria(Type type)
        {
            return _session.CreateCriteria(type);
        }

        public ICriteria CreateCriteria<T>() where T : class
        {
            return _session.CreateCriteria<T>();
        }

        public IQuery CreateQuery(string query)
        {
            return _session.CreateQuery(query);
        }

        public ISQLQuery CreateSQLQuery(string query)
        {
            return _session.CreateSQLQuery(query);
        }

        public TObj Get<TObj>(object id)
        {
            return _session.Get<TObj>(id);
        }

        public IDbConnection Connection
        {
            get { return _session.Connection; }
        }
    }
}