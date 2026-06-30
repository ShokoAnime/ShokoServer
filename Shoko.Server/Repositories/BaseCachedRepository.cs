using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.Databases;
using Shoko.Server.Exceptions;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CS0618
namespace Shoko.Server.Repositories;

// ReSharper disable once InconsistentNaming
public abstract class BaseCachedRepository<T, S> : BaseRepository, ICachedRepository, IRepository<T, S>
    where T : class, new()
{
    protected readonly DatabaseFactory _databaseFactory;

    // A hack to not have to pass the system service to every cached repository.
    protected SystemService SystemService => field ??= ISystemService.StaticServices.GetRequiredService<SystemService>();

    public PocoCache<S, T> Cache;

    public Action<T> BeginDeleteCallback { get; set; }

    public Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }

    public Action<T> EndDeleteCallback { get; set; }

    public Action<T> BeginSaveCallback { get; set; }

    public Action<ISessionWrapper, T> SaveWithOpenTransactionCallback { get; set; }

    public Action<T> EndSaveCallback { get; set; }

    protected BaseCachedRepository(DatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public virtual void Populate(ISessionWrapper session, bool displayName = true, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        if (displayName)
        {
            SystemService.StartupMessage = $"Database Cache - Caching  - {typeof(T).Name}...";
        }

        // This is only called from main thread, so we don't need to lock
        var settings = ISettingsProvider.Instance.GetSettings();
        Cache = new PocoCache<S, T>(session.CreateCriteria(typeof(T)).SetTimeout(settings.CachingDatabaseTimeout).List<T>(), SelectKey);
        PopulateIndexes();
    }

    public virtual void Populate(bool displayName = true, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        using var session = _databaseFactory.SessionFactory.OpenSession();
        Populate(session.Wrap(), displayName, cancellationToken);
    }

    public void ClearCache()
    {
        Cache.Clear();
    }

    // ReSharper disable once InconsistentNaming
    public virtual T GetByID(S id)
    {
        if (Equals(default(S), id)) throw new InvalidStateException($"Trying to lookup a {typeof(T).Name} by an ID of 0");
        return GetByIDUnsafe(id);
    }

    public T GetByID(ISession session, S id)
    {
        return GetByID(id);
    }

    public T GetByID(ISessionWrapper session, S id)
    {
        return GetByID(id);
    }

    public virtual IReadOnlyList<T> GetAll()
    {
        return GetAllUnsafe();
    }

    public IReadOnlyList<T> GetAll(int maxLimit)
    {
        return GetAllUnsafe(maxLimit);
    }

    public IReadOnlyList<T> GetAll(ISession session)
    {
        return GetAll();
    }

    public IReadOnlyList<T> GetAll(ISessionWrapper session)
    {
        return GetAll();
    }

    public virtual void Delete(S id)
    {
        Delete(GetByID(id));
    }

    public virtual void Delete(T cr)
    {
        if (cr == null)
        {
            return;
        }

        if (BeginDeleteCallback != null) BeginDeleteCallback(cr);
        DeleteFromDatabaseUnsafe(cr);
        DeleteFromCacheUnsafe(cr);
        if (EndDeleteCallback != null) EndDeleteCallback(cr);
    }

    protected void DeleteFromCache(T cr)
    {
        DeleteFromCacheUnsafe(cr);
    }

    protected void UpdateCache(T cr)
    {
        UpdateCacheUnsafe(cr);
    }

    public virtual void Delete(IReadOnlyCollection<T> objs)
    {
        if (objs.Count == 0)
        {
            return;
        }

        foreach (var cr in objs)
        {
            if (BeginDeleteCallback != null) BeginDeleteCallback(cr);
        }

        DeleteFromDatabaseUnsafe(objs);

        foreach (var cr in objs)
        {
            DeleteFromCacheUnsafe(cr);
        }

        foreach (var cr in objs)
        {
            if (EndDeleteCallback != null) EndDeleteCallback(cr);
        }
    }

    //This function do not run the BeginDeleteCallback and the EndDeleteCallback
    public virtual void DeleteWithOpenTransaction(ISession session, T cr)
    {
        if (cr == null)
        {
            return;
        }

        if (DeleteWithOpenTransactionCallback != null) DeleteWithOpenTransactionCallback(session, cr);
        session.Delete(cr);
        DeleteFromCacheUnsafe(cr);
    }

    //This function do not run the BeginDeleteCallback and the EndDeleteCallback
    public void DeleteWithOpenTransaction(ISession session, IReadOnlyList<T> objs)
    {
        if (objs.Count == 0) return;

        foreach (var cr in objs)
        {
            if (DeleteWithOpenTransactionCallback != null) DeleteWithOpenTransactionCallback(session, cr);
            session.Delete(cr);
        }

        foreach (var obj in objs)
            DeleteFromCacheUnsafe(obj);
    }

    public virtual void Save(T obj)
    {
        if (BeginSaveCallback != null) BeginSaveCallback(obj);

        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        session.SaveOrUpdate(obj);
        if (SaveWithOpenTransactionCallback != null) SaveWithOpenTransactionCallback(session.Wrap(), obj);
        transaction.Commit();

        UpdateCacheUnsafe(obj);
        if (EndSaveCallback != null) EndSaveCallback(obj);
    }

    public virtual void Save(IReadOnlyCollection<T> objs)
    {
        if (objs.Count == 0)
        {
            return;
        }

        foreach (var obj in objs)
        {
            if (BeginSaveCallback != null) BeginSaveCallback(obj);
        }

        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        var wrapper = session.Wrap();
        foreach (var obj in objs)
        {
            session.SaveOrUpdate(obj);
            if (SaveWithOpenTransactionCallback != null) SaveWithOpenTransactionCallback(wrapper, obj);
        }
        transaction.Commit();

        foreach (var obj in objs)
        {
            UpdateCacheUnsafe(obj);
        }

        foreach (var obj in objs)
        {
            if (EndSaveCallback != null) EndSaveCallback(obj);
        }
    }

    //This function do not run the BeginDeleteCallback and the EndDeleteCallback
    public virtual void SaveWithOpenTransaction(ISessionWrapper session, T obj)
    {
        if (Equals(SelectKey(obj), default(S)))
        {
            session.Insert(obj);
        }
        else
        {
            session.Update(obj);
        }

        if (SaveWithOpenTransactionCallback != null) SaveWithOpenTransactionCallback(session, obj);
        UpdateCacheUnsafe(obj);
    }

    //This function do not run the BeginDeleteCallback and the EndDeleteCallback
    public virtual void SaveWithOpenTransaction(ISession session, T obj)
    {
        session.SaveOrUpdate(obj);
        if (SaveWithOpenTransactionCallback != null) SaveWithOpenTransactionCallback(session.Wrap(), obj);
        UpdateCacheUnsafe(obj);
    }

    //This function do not run the BeginDeleteCallback and the EndDeleteCallback
    public void SaveWithOpenTransaction(ISession session, List<T> objs)
    {
        if (objs.Count == 0)
        {
            return;
        }

        foreach (var obj in objs)
        {
            session.SaveOrUpdate(obj);
        }

        foreach (var obj in objs)
        {
            if (SaveWithOpenTransactionCallback != null) SaveWithOpenTransactionCallback(session.Wrap(), obj);
        }

        foreach (var obj in objs)
        {
            UpdateCacheUnsafe(obj);
        }
    }

    #region Unsafe

    public virtual void ClearCacheUnsafe()
    {
        Cache.Clear();
    }

    protected virtual T GetByIDUnsafe(S id)
    {
        return Cache.Get(id);
    }

    protected virtual IReadOnlyList<T> GetAllUnsafe()
    {
        return Cache.GetAll();
    }

    protected virtual IReadOnlyList<T> GetAllUnsafe(int maxLimit)
    {
        return Cache.GetAll().Take(maxLimit).ToList();
    }

    protected virtual void UpdateCacheUnsafe(T cr)
    {
        Cache.Update(cr);
    }

    protected virtual void DeleteFromCacheUnsafe(T cr)
    {
        Cache.Remove(cr);
    }

    private void DeleteFromDatabaseUnsafe(T cr)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();
        if (DeleteWithOpenTransactionCallback != null) DeleteWithOpenTransactionCallback(session, cr);
        session.Delete(cr);
        transaction.Commit();
    }

    private void DeleteFromDatabaseUnsafe(IReadOnlyCollection<T> objs)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        foreach (var cr in objs)
        {
            if (DeleteWithOpenTransactionCallback != null) DeleteWithOpenTransactionCallback(session, cr);
            session.Delete(cr);
        }

        transaction.Commit();
    }

    #endregion

    #region abstract

    public virtual void PopulateIndexes() { }

    public virtual void RegenerateDb() { }

    public virtual void PostProcess() { }

    protected abstract S SelectKey(T entity);

    #endregion
}
