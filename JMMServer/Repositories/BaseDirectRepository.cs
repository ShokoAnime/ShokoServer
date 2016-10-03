using System;
using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Repositories.NHibernate;
using NHibernate;
// ReSharper disable InconsistentNaming

namespace JMMServer.Repositories
{
    public class BaseDirectRepository<T, S> : IRepository<T, S> where T : class
    {

        public Action<T> BeginDeleteCallback { get; set; }
        public Action<ISession, T> DeleteWithOpenTransactionCallback { get; set; }
        public Action<T> EndDeleteCallback { get; set; }
        public Action<T> BeginSaveCallback { get; set; }
        public Action<ISession, T> SaveWithOpenTransactionCallback { get; set; }
        public Action<T> EndSaveCallback { get; set; }

        public virtual T GetByID(S id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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

        public virtual void Delete(List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach(T obj in objs)
                BeginDeleteCallback?.Invoke(obj);
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
            foreach (T obj in objs)
                EndDeleteCallback?.Invoke(obj);
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, S id)
        {
            DeleteWithOpenTransaction(session, GetByID(id));
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, T cr)
        {
            if (cr != null)
            {
                session.Delete(cr);
            }
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void DeleteWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T cr in objs)
            {
                DeleteWithOpenTransactionCallback?.Invoke(session, cr);
                session.Delete(cr);
            }
        }

        public virtual void Save(T obj)
        {
            BeginSaveCallback?.Invoke(obj);
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    SaveWithOpenTransactionCallback?.Invoke(session, obj);
                    transaction.Commit();
                }
            }
            EndSaveCallback?.Invoke(obj);
        }

        public virtual void Save(List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach(T obj in objs)
                BeginSaveCallback?.Invoke(obj);
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                using (var transaction = session.BeginTransaction())
                {
                    foreach (T obj in objs)
                    {
                        session.SaveOrUpdate(obj);
                        SaveWithOpenTransactionCallback?.Invoke(session, obj);
                    }
                    transaction.Commit();   
                }
            }
            foreach(T obj in objs)
                EndSaveCallback?.Invoke(obj);
        }



        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, T obj)
        {
            session.SaveOrUpdate(obj);
        }
        //This function do not run the BeginDeleteCallback and the EndDeleteCallback
        public virtual void SaveWithOpenTransaction(ISession session, List<T> objs)
        {
            if (objs.Count == 0)
                return;
            foreach (T obj in objs)
            {
                session.SaveOrUpdate(obj);
                SaveWithOpenTransactionCallback?.Invoke(session, obj);
            }
        }
    }
}
