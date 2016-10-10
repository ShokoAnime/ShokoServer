using System;
using System.Data;
using NHibernate;

namespace JMMServer.Repositories.NHibernate
{
    internal class StatelessSessionWrapper : ISessionWrapper
    {
        private readonly IStatelessSession _session;

        public StatelessSessionWrapper(IStatelessSession session)
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

        public ITransaction BeginTransaction()
        {
            return _session.BeginTransaction();
        }

        public void Insert(object entity)
        {
            _session.Insert(entity);
        }

        public void Update(object entity)
        {
            _session.Update(entity);
        }

        public void Delete(object entity)
        {
            _session.Delete(entity);
        }

        public IDbConnection Connection
        {
            get { return _session.Connection; }
        }
    }
}