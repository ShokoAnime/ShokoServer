using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_AnimeRepository
    {
        public void Save(AniDB_Anime obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Save(session, obj);
            }
        }

        public void Save(ISession session, AniDB_Anime obj)
        {
            if (obj.AniDB_AnimeID == 0)
            {
                obj.Contract = null;
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }

            obj.UpdateContractDetailed(session);
            // populate the database

            using (var transaction = session.BeginTransaction())
            {
                session.SaveOrUpdate(obj);
                transaction.Commit();
            }
        }

        public static void InitCache()
        {
            string t = "AniDB_Anime";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AniDB_AnimeRepository repo = new AniDB_AnimeRepository();
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                List<AniDB_Anime> ls =
                    repo.GetAll(session).Where(a => a.ContractVersion < AniDB_Anime.CONTRACT_VERSION).ToList();
                int max = ls.Count;
                int cnt = 0;
                foreach (AniDB_Anime a in ls)
                {
                    repo.Save(session, a);
                    cnt++;
                    if (cnt%10 == 0)
                    {
                        ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                            " DbRegen - " + cnt + "/" + max);
                    }
                }
                ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                    " DbRegen - " + max + "/" + max);
            }
        }

        public AniDB_Anime GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Anime>(id);
            }
        }

        public AniDB_Anime GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Anime cr = session
                    .CreateCriteria(typeof(AniDB_Anime))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .UniqueResult<AniDB_Anime>();
                return cr;
            }
        }

        public AniDB_Anime GetByAnimeID(ISession session, int id)
        {
            AniDB_Anime cr = session
                .CreateCriteria(typeof(AniDB_Anime))
                .Add(Restrictions.Eq("AnimeID", id))
                .UniqueResult<AniDB_Anime>();
            return cr;
        }

        public List<AniDB_Anime> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAll(session);
            }
        }

        public List<AniDB_Anime> GetAll(ISession session)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Anime))
                .List<AniDB_Anime>();

            return new List<AniDB_Anime>(objs);
        }

        public List<AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetForDate(session, startDate, endDate);
            }
        }

        public List<AniDB_Anime> GetForDate(ISession session, DateTime startDate, DateTime endDate)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Anime))
                .Add(Restrictions.Ge("AirDate", startDate))
                .Add(Restrictions.Le("AirDate", endDate))
                .AddOrder(Order.Desc("AirDate"))
                .List<AniDB_Anime>();

            return new List<AniDB_Anime>(objs);
        }

        public List<AniDB_Anime> SearchByName(ISession session, string queryText)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Anime))
                .Add(Restrictions.InsensitiveLike("AllTitles", queryText, MatchMode.Anywhere))
                .List<AniDB_Anime>();

            return new List<AniDB_Anime>(objs);
        }

        public List<AniDB_Anime> SearchByName(string queryText)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime))
                    .Add(Restrictions.InsensitiveLike("AllTitles", queryText, MatchMode.Anywhere))
                    .List<AniDB_Anime>();

                return new List<AniDB_Anime>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Anime cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }

        public List<AniDB_Anime> SearchByTag(string queryText)
        {
            
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime))
                    .Add(Restrictions.InsensitiveLike("AllTags", queryText, MatchMode.Anywhere))
                    .List<AniDB_Anime>();

                return new List<AniDB_Anime>(objs);
            }
        }
    }
}