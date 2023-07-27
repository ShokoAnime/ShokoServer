﻿using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using NHibernate;

namespace Shoko.Server.Repositories.NHibernate;

[DebuggerStepThrough]
internal class StatelessSessionWrapper : ISessionWrapper
{
    private readonly IStatelessSession _session;

    public StatelessSessionWrapper(IStatelessSession session)
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

    public IDbConnection Connection => _session.Connection;

    public void Dispose()
    {
        _session?.Dispose();
    }
}
