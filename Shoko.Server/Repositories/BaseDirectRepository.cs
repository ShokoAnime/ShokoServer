using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories
{
    public class BaseDirectRepository<T, S> : IRepository<T, S> where T : class
    {
        protected readonly object globalDBLock = new object();
        public Action<T> BeginDeleteCallback { get; set; }
        public Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }
        public Action<T> EndDeleteCallback { get; set; }
        public Action<T> BeginSaveCallback { get; set; }
        public Action<ISessionWrapper, T> SaveWithOpenTransactionCallback { get; set; }
        public Action<T> EndSaveCallback { get; set; }

        public virtual T GetByID(S id)
        {
            lock (globalDBLock)
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    return session.Get<T>(id);
                }
            }
        }

        public virtual T GetByID(ISession session, S id)
        {
            lock (globalDBLock)
            {
                return session.Get<T>(id);
            }
        }

        public virtual T GetByID(ISessionWrapper session, S id)
        {
            lock (globalDBLock)
            {
                return session.Get<T>(id);
            }
        }

        public virtual IReadOnlyList<T> GetAll()
        {
            lock (globalDBLock)
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    return session.CreateCriteria(typeof(T)).List<T>().ToList();
                }
            }
        }

        public virtual IReadOnlyList<T> GetAll(ISession session)
        {
            lock (globalDBLock)
            {
                return session.CreateCriteria(typeof(T)).List<T>().ToList();
            }
        }

        public virtual IReadOnlyList<T> GetAll(ISessionWrapper session)
        {
            lock (globalDBLock)
            {
                return session.CreateCriteria(typeof(T)).List<T>().ToList();
            }
        }


        public virtual void Delete(S id)
        {
            Delete(GetByID(id));
        }

        public virtual void Delete(T cr)
        {
            if (cr == null) return;
            lock (globalDBLock)
            {
                BeginDeleteCallback?.Invoke(cr);
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
                EndDeleteCallback?.Invoke(cr);
            }
        }

        public void Delete(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (globalDBLock)
            {
                foreach (T obj in objs) BeginDeleteCallback?.Invoke(obj);
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (T cr in objs)
                        {
                            DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                            session.Delete(cr);
                        }
                        transaction.Commit();
                    }
                }
                foreach (T obj in objs) EndDeleteCallback?.Invoke(obj);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, S id)
        {
            DeleteWithOpenTransaction(session, GetByID(id));
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, T cr)
        {
            if (cr == null) return;
            lock (globalDBLock)
            {
                session.Delete(cr);
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void DeleteWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0) return;
            lock (globalDBLock)
            {
                foreach (T cr in objs)
                {
                    lock (cr)
                    {
                        DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                        session.Delete(cr);
                    }
                }
            }
        }

        public virtual void Save(T obj)
        {
            lock (globalDBLock)
            {
                lock (obj)
                {
                    BeginSaveCallback?.Invoke(obj);
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        using (var transaction = session.BeginTransaction())
                        {
                            session.SaveOrUpdate(obj);
                            SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                            transaction.Commit();
                        }
                    }
                    EndSaveCallback?.Invoke(obj);
                }
            }
        }

        public void Save(IReadOnlyCollection<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (globalDBLock)
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (T obj in objs)
                        {
                            lock (obj)
                            {
                                BeginSaveCallback?.Invoke(obj);
                                session.SaveOrUpdate(obj);
                                SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                                EndSaveCallback?.Invoke(obj);
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
        }


        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, T obj)
        {
            lock (globalDBLock)
            {
                lock (obj)
                {
                    session.SaveOrUpdate(obj);
                }
            }
        }

        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public void SaveWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            lock (globalDBLock)
            {
                foreach (T obj in objs)
                {
                    session.SaveOrUpdate(obj);
                    SaveWithOpenTransactionCallback?.Invoke(session.Wrap(), obj);
                }
            }
        }
    }
}