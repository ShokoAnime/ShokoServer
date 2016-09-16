using System;
using System.Collections.Generic;
using JMMServer.Repositories.NHibernate;
using NHibernate;
// ReSharper disable InconsistentNaming

namespace JMMServer.Repositories
{
    public class BaseDirectRepository<T, S> : IRepository<T, S> where T : class
    {
        public Action<ISession, T> DeleteCallback { get; set; }
        public Action<ISession, T> SaveCallback { get; set; }
        public Action<T> AfterCommitCallback { get; set; }

        public virtual T GetByID(S id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<T>(id);
            }
        }

        public virtual T GetByID(ISession session, S id)
        {
            return session.Get<T>(id);
        }

        public virtual T GetByID(ISessionWrapper session, S id)
        {
            return session.Get<T>(id);
        }

        public virtual List<T> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return new List<T>(session.CreateCriteria(typeof(T)).List<T>());
            }
        }

        public virtual List<T> GetAll(ISession session)
        {
            return new List<T>(session.CreateCriteria(typeof(T)).List<T>());
        }

        public virtual List<T> GetAll(ISessionWrapper session)
        {
            return new List<T>(session.CreateCriteria(typeof(T)).List<T>());
        }


        public virtual void Delete(S id)
        {
            Delete(GetByID(id));
        }

        public virtual void Delete(T cr)
        {
            if (cr != null)
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        DeleteCallback?.Invoke(session, cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }

        public virtual void Delete(List<T> objs)
        {
            if (objs.Count == 0)
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T cr in objs)
                    {
                        DeleteCallback?.Invoke(session, cr);
                        session.Delete(cr);
                    }
                    transaction.Commit();
                }
            }
        }

        public virtual void Delete(ISession session, S id)
        {
            Delete(session, GetByID(id));
        }

        public virtual void Delete(ISession session, T cr)
        {
            if (cr != null)
            {
                session.Delete(cr);
            }
        }

        public virtual void Delete(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs)
            {
                DeleteCallback?.Invoke(session, cr);
                session.Delete(cr);
            }
        }

        public virtual void Save(T obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    SaveCallback?.Invoke(session, obj);
                    transaction.Commit();
                    AfterCommitCallback?.Invoke(obj);
                }
            }
        }

        public virtual void Save(List<T> objs)
        {
            if (objs.Count == 0)
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T obj in objs)
                    {
                        session.SaveOrUpdate(obj);
                        SaveCallback?.Invoke(session, obj);
                    }
                    transaction.Commit();
                    if (AfterCommitCallback != null)
                    {
                        foreach (T obj in objs)
                            AfterCommitCallback?.Invoke(obj);
                    }
                }
            }
        }
        //This two do not have after commit, parent should handle that after commit.
        public virtual void Save(ISession session, T obj)
        {
            session.SaveOrUpdate(obj);
        }
        //This two do not have after commit, parent should handle that after commit.
        public virtual void Save(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T obj in objs)
            {
                session.SaveOrUpdate(obj);
                SaveCallback?.Invoke(session, obj);
            }
        }
    }
}
