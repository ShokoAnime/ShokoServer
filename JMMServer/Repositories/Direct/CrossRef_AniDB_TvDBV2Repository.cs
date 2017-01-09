using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_TvDBV2Repository : BaseDirectRepository<SVR_CrossRef_AniDB_TvDBV2, int>
    {
        private CrossRef_AniDB_TvDBV2Repository()
        {
            
        }

        public static CrossRef_AniDB_TvDBV2Repository Create()
        {
            return new CrossRef_AniDB_TvDBV2Repository();
        }
        public List<SVR_CrossRef_AniDB_TvDBV2> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<SVR_CrossRef_AniDB_TvDBV2> GetByAnimeID(ISessionWrapper session, int id)
        {
            var xrefs = session
                .CreateCriteria(typeof(SVR_CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Asc("AniDBStartEpisodeType"))
                .AddOrder(Order.Asc("AniDBStartEpisodeNumber"))
                .List<SVR_CrossRef_AniDB_TvDBV2>();

            return new List<SVR_CrossRef_AniDB_TvDBV2>(xrefs);
        }

        public ILookup<int, SVR_CrossRef_AniDB_TvDBV2> GetByAnimeIDs(ISessionWrapper session, IReadOnlyCollection<int> animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Count == 0)
            {
                return EmptyLookup<int, SVR_CrossRef_AniDB_TvDBV2>.Instance;
            }

            var xrefs = session
                .CreateCriteria(typeof(SVR_CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.InG("AnimeID", animeIds))
                .AddOrder(Order.Asc("AniDBStartEpisodeType"))
                .AddOrder(Order.Asc("AniDBStartEpisodeNumber"))
                .List<SVR_CrossRef_AniDB_TvDBV2>()
                .ToLookup(cr => cr.AnimeID);

            return xrefs;
        }

        public List<SVR_CrossRef_AniDB_TvDBV2> GetByAnimeIDEpTypeEpNumber(ISession session, int id, int aniEpType,
            int aniEpisodeNumber)
        {
            var xrefs = session
                .CreateCriteria(typeof(SVR_CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.Eq("AnimeID", id))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Le("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .List<SVR_CrossRef_AniDB_TvDBV2>();

            return new List<SVR_CrossRef_AniDB_TvDBV2>(xrefs);
        }

        public SVR_CrossRef_AniDB_TvDBV2 GetByTvDBID(ISession session, int id, int season, int episodeNumber, int animeID,
            int aniEpType, int aniEpisodeNumber)
        {
            SVR_CrossRef_AniDB_TvDBV2 cr = session
                .CreateCriteria(typeof(SVR_CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.Eq("TvDBID", id))
                .Add(Restrictions.Eq("TvDBSeasonNumber", season))
                .Add(Restrictions.Eq("TvDBStartEpisodeNumber", episodeNumber))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .UniqueResult<SVR_CrossRef_AniDB_TvDBV2>();
            return cr;
        }

        public SVR_CrossRef_AniDB_TvDBV2 GetByTvDBID(int id, int season, int episodeNumber, int animeID, int aniEpType,
            int aniEpisodeNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByTvDBID(session, id, season, episodeNumber, animeID, aniEpType, aniEpisodeNumber);
            }
        }
     
    }
}