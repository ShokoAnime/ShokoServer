using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_VoteRepository
    {
        public void Save(AniDB_Vote obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
            if (obj.VoteType == (int)AniDBVoteType.Anime || obj.VoteType == (int)AniDBVoteType.AnimeTemp)
            {
                StatsCache.Instance.UpdateUsingAnime(obj.EntityID);
                StatsCache.Instance.UpdateAnimeContract(obj.EntityID);
            }
        }

        public AniDB_Vote GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Vote>(id);
            }
        }

        public List<AniDB_Vote> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Vote))
                    .List<AniDB_Vote>();

                return new List<AniDB_Vote>(objs);
                ;
            }
        }

        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var cr = session
                    .CreateCriteria(typeof(AniDB_Vote))
                    .Add(Restrictions.Eq("EntityID", entID))
                    .Add(Restrictions.Eq("VoteType", (int)voteType))
                    .UniqueResult<AniDB_Vote>();

                return cr;
            }
        }

        public List<AniDB_Vote> GetByEntity(int entID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var votes = session
                    .CreateCriteria(typeof(AniDB_Vote))
                    .Add(Restrictions.Eq("EntityID", entID))
                    .List<AniDB_Vote>();

                return new List<AniDB_Vote>(votes);
            }
        }

        public AniDB_Vote GetByAnimeID(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var votes = session
                    .CreateCriteria(typeof(AniDB_Vote))
                    .Add(Restrictions.Eq("EntityID", animeID))
                    .List<AniDB_Vote>();

                var tempList = new List<AniDB_Vote>(votes);
                var retList = new List<AniDB_Vote>();

                foreach (var vt in tempList)
                {
                    if (vt.VoteType == (int)AniDBVoteType.Anime || vt.VoteType == (int)AniDBVoteType.AnimeTemp)
                        return vt;
                }

                return null;
            }
        }

        public AniDB_Vote GetByAnimeID(ISession session, int animeID)
        {
            var votes = session
                .CreateCriteria(typeof(AniDB_Vote))
                .Add(Restrictions.Eq("EntityID", animeID))
                .List<AniDB_Vote>();

            var tempList = new List<AniDB_Vote>(votes);
            var retList = new List<AniDB_Vote>();

            foreach (var vt in tempList)
            {
                if (vt.VoteType == (int)AniDBVoteType.Anime || vt.VoteType == (int)AniDBVoteType.AnimeTemp)
                    return vt;
            }

            return null;
        }

        public void Delete(int id)
        {
            int? animeID = null;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
                    if (cr != null)
                    {
                        if (cr.VoteType == (int)AniDBVoteType.Anime || cr.VoteType == (int)AniDBVoteType.AnimeTemp)
                            animeID = cr.EntityID;

                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
            if (animeID.HasValue)
            {
                StatsCache.Instance.UpdateUsingAnime(animeID.Value);
                StatsCache.Instance.UpdateAnimeContract(animeID.Value);
            }
        }
    }
}