using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories;

public class BaseDirectRepository<T, S>(DatabaseFactory databaseFactory) : BaseRepository, IDirectRepository, IRepository<T, S> where T : class where S : notnull
{
    protected readonly DatabaseFactory _databaseFactory = databaseFactory;

    /// <summary>
    ///   Runs before an entity is deleted.
    /// </summary>
    protected virtual void OnBeginDelete(T obj)
    {
    }

    /// <summary>
    ///   Runs inside the delete transaction, before the entity itself is deleted.
    /// </summary>
    protected virtual void OnDeleteWithOpenTransaction(ISession session, T obj)
    {
    }

    /// <summary>
    ///   Runs after an entity is deleted.
    /// </summary>
    protected virtual void OnEndDelete(T obj)
    {
    }

    /// <summary>
    ///   Runs before an entity is saved.
    /// </summary>
    protected virtual void OnBeginSave(T obj)
    {
    }

    /// <summary>
    ///   Runs inside the save transaction, after the entity itself is saved.
    /// </summary>
    protected virtual void OnSaveWithOpenTransaction(ISessionWrapper session, T obj)
    {
    }

    /// <summary>
    ///   Runs after an entity is saved.
    /// </summary>
    protected virtual void OnEndSave(T obj)
    {
    }

    public virtual T? GetByID(S id)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session.Get<T>(id);
    }

    public virtual T? GetByID(ISession session, S id)
    {
        return session.Get<T>(id);
    }

    public virtual T? GetByID(ISessionWrapper session, S id)
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
        // Deliberately not null-guarded; overrides may throw on a missing entity, as they did before.
        Delete(GetByID(id)!);
    }

    public virtual void Delete(T cr)
    {
        if (cr == null) return;

        OnBeginDelete(cr);
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        OnDeleteWithOpenTransaction(session, cr);
        session.Delete(cr);
        transaction.Commit();
        OnEndDelete(cr);
    }

    public void Delete(IReadOnlyCollection<T> objs)
    {
        if (objs.Count == 0) return;

        foreach (var obj in objs)
        {
            OnBeginDelete(obj);
        }

        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        foreach (var cr in objs)
        {
            OnDeleteWithOpenTransaction(session, cr);
            session.Delete(cr);
        }

        transaction.Commit();

        foreach (var obj in objs)
        {
            OnEndDelete(obj);
        }
    }

    public virtual void Save(T obj)
    {
        OnBeginSave(obj);
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        session.SaveOrUpdate(obj);
        OnSaveWithOpenTransaction(session.Wrap(), obj);
        transaction.Commit();
        OnEndSave(obj);
    }

    public void Save(IReadOnlyCollection<T> objs)
    {
        if (objs.Count == 0) return;

        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        foreach (var obj in objs)
        {
            OnBeginSave(obj);
            session.SaveOrUpdate(obj);
            OnSaveWithOpenTransaction(session.Wrap(), obj);
            OnEndSave(obj);
        }

        transaction.Commit();
    }
}
