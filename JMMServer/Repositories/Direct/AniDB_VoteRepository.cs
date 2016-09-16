using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_VoteRepository : BaseDirectRepository<AniDB_Vote, int>
    {
        public AniDB_VoteRepository()
        {
            AfterCommitCallback = (obj) =>
            {
                if (obj.VoteType == (int) AniDBVoteType.Anime || obj.VoteType == (int) AniDBVoteType.AnimeTemp)
                {
                    AniDB_Anime.UpdateStatsByAnimeID(obj.EntityID);
                }
            };
            DeleteCallback = (ses, cr) =>
            {
                if (cr.VoteType == (int)AniDBVoteType.Anime || cr.VoteType == (int)AniDBVoteType.AnimeTemp)
                {
                    AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                }

            };
        }
        
        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Vote cr = session
                    .CreateCriteria(typeof(AniDB_Vote))
                    .Add(Restrictions.Eq("EntityID", entID))
                    .Add(Restrictions.Eq("VoteType", (int) voteType))
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

                List<AniDB_Vote> tempList = new List<AniDB_Vote>(votes);
                List<AniDB_Vote> retList = new List<AniDB_Vote>();

                foreach (AniDB_Vote vt in tempList)
                {
                    if (vt.VoteType == (int) AniDBVoteType.Anime || vt.VoteType == (int) AniDBVoteType.AnimeTemp)
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

            List<AniDB_Vote> tempList = new List<AniDB_Vote>(votes);
            List<AniDB_Vote> retList = new List<AniDB_Vote>();

            foreach (AniDB_Vote vt in tempList)
            {
                if (vt.VoteType == (int) AniDBVoteType.Anime || vt.VoteType == (int) AniDBVoteType.AnimeTemp)
                    return vt;
            }

            return null;
        }

        public Dictionary<int, AniDB_Vote> GetByAnimeIDs(ISessionWrapper session, ICollection<int> animeIDs)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIDs == null)
                throw new ArgumentNullException(nameof(animeIDs));

            var votesByAnime = session
                .CreateCriteria<AniDB_Vote>()
                .Add(Restrictions.InG(nameof(AniDB_Vote.EntityID), animeIDs))
                .Add(Restrictions.In(nameof(AniDB_Vote.VoteType), new[] { (int)AniDBVoteType.Anime, (int)AniDBVoteType.AnimeTemp }))
                .List<AniDB_Vote>()
                .GroupBy(v => v.EntityID)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault());

            return votesByAnime;
        }
    }
}