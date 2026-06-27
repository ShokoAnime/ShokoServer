using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories;

public class BaseDirectRepository<T, S> : BaseRepository, IDirectRepository, IRepository<T, S> where T : class
{
    protected readonly DatabaseFactory _databaseFactory;

    public BaseDirectRepository(DatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public Action<T> BeginDeleteCallback { get; set; }
    public Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }
    public Action<T> EndDeleteCallback { get; set; }
    public Action<T> BeginSaveCallback { get; set; }
    public Action<ISessionWrapper, T> SaveWithOpenTransactionCallback { get; set; }
    public Action<T> EndSaveCallback { get; set; }

    public virtual T GetByID(S id)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session.Get<T>(id);
    }

    public virtual T GetByID(ISession session, S id)
    {
        return session.Get<T>(id);
    }

    public virtual T GetByID(ISessionWrapper session, S id)
    {
        return session.Get<T>(id);
    }

    public virtual IReadOnlyList<T> GetAll()
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session.CreateCriteria(typeof(T)).List<T>().ToList();
    }

    public virtual IReadOnlyList<T> GetAll(ISession session)
    {
        return session.CreateCriteria(typeof(T)).List<T>().ToList();
    }

    public virtual IReadOnlyList<T> GetAll(ISessionWrapper session)
    {
        return session.CreateCriteria(typeof(T)).List<T>().ToList();
    }


    public virtual void Delete(S id)
    {
        Delete(GetByID(id));
    }

    public virtual void Delete(T cr)
    {
        if (cr == null) return;

        BeginDeleteCallback?.Invoke(cr);
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        DeleteWithOpenTransactionCallback?.Invoke(session, cr);
        session.Delete(cr);
        transaction.Commit();
        EndDeleteCallback?.Invoke(cr);
    }

    public void Delete(IReadOnlyCollection<T> objs)
    {
        if (objs.Count == 0) return;

        foreach (var obj in objs)
        {
            BeginDeleteCallback?.Invoke(obj);
        }

        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        foreach (var cr in objs)
        {
            DeleteWithOpenTransactionCallback?.Invoke(session, cr);
            session.Delete(cr);
        }

        transaction.Commit();

        foreach (var obj in objs)
        {
            EndDeleteCallback?.Invoke(obj);
        }
    }

    public virtual void Save(T obj)
    {
        BeginSaveCallback?.Invoke(obj);
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        session.SaveOrUpdate(obj);
        SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
        transaction.Commit();
        EndSaveCallback?.Invoke(obj);
    }

    public void Save(IReadOnlyCollection<T> objs)
    {
        if (objs.Count == 0) return;

        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        foreach (var obj in objs)
        {
            BeginSaveCallback?.Invoke(obj);
            session.SaveOrUpdate(obj);
            SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
            EndSaveCallback?.Invoke(obj);
        }

        transaction.Commit();
    }
}
