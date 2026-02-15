using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NHibernate;

namespace Shoko.Server.Repositories.NHibernate;

[DebuggerStepThrough]
internal class SessionWrapper : ISessionWrapper
{
    private readonly ISession _session;

    public SessionWrapper(ISession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
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

    public IQueryOver<T, T> QueryOver<T>() where T : class
    {
        return _session.QueryOver<T>();
    }

    public IQueryable<T> Query<T>() where T : class
    {
        return _session.Query<T>();
    }

    public TObj Get<TObj>(object id)
    {
        return _session.Get<TObj>(id);
    }

    public Task<TObj> GetAsync<TObj>(object id)
    {
        return _session.GetAsync<TObj>(id);
    }

    public ITransaction BeginTransaction()
    {
        return _session.BeginTransaction();
    }

    public void Insert(object entity)
    {
        _session.Save(entity);
    }

    public Task InsertAsync(object entity)
    {
        return _session.SaveAsync(entity);
    }

    public void Update(object entity)
    {
        _session.Update(entity);
    }

    public Task UpdateAsync(object entity)
    {
        return _session.UpdateAsync(entity);
    }

    public void Delete(object entity)
    {
        _session.Delete(entity);
    }

    public Task DeleteAsync(object entity)
    {
        return _session.DeleteAsync(entity);
    }

    public IDbConnection Connection => _session.Connection;

    public void Dispose()
    {
        _session?.Dispose();
    }
}
