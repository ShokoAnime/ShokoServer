using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_VoteRepository : BaseDirectRepository<AniDB_Vote, int>
    {
        private AniDB_VoteRepository()
        {
            EndSaveCallback = (cr) =>
            {
                if (cr.VoteType == (int) AniDBVoteType.Anime || cr.VoteType == (int) AniDBVoteType.AnimeTemp)
                {
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                }
            };
            EndDeleteCallback = (cr) =>
            {
                if (cr.VoteType == (int)AniDBVoteType.Anime || cr.VoteType == (int)AniDBVoteType.AnimeTemp)
                {
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                }

            };
        }

        public static AniDB_VoteRepository Create()
        {
            return new AniDB_VoteRepository();
        }
        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, animeID);
            }
        }

        public AniDB_Vote GetByAnimeID(ISession session, int animeID)
        {
            var votes = session
                .CreateCriteria(typeof(AniDB_Vote))
                .Add(Restrictions.Eq("EntityID", animeID))
                .List<AniDB_Vote>();

            foreach (AniDB_Vote vt in votes)
            {
                if (vt.VoteType == (int) AniDBVoteType.Anime || vt.VoteType == (int) AniDBVoteType.AnimeTemp)
                    return vt;
            }

            return null;
        }

        public Dictionary<int, AniDB_Vote> GetByAnimeIDs(ISessionWrapper session, IReadOnlyCollection<int> animeIDs)
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