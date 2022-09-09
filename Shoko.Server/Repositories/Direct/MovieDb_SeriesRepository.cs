using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class MovieDb_SeriesRepository : BaseDirectRepository<MovieDB_Series, int>
    {
        public MovieDB_Series GetByOnlineID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session.Wrap(), id);
            }
        }

        public MovieDB_Series GetByOnlineID(ISessionWrapper session, int id)
        {
            MovieDB_Series cr = session
                .CreateCriteria(typeof(MovieDB_Series))
                .Add(Restrictions.Eq("SeriesId", id))
                .UniqueResult<MovieDB_Series>();
            return cr;
        }

        public Dictionary<int, (CrossRef_AniDB, MovieDB_Series)> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return new Dictionary<int, (CrossRef_AniDB, MovieDB_Series)>();
            }
            ILookup<int, CrossRef_AniDB> lk=RepoFactory.CrossRef_AniDB.GetByAniDBIDs(animeIds, Shoko.Models.Constants.Providers.MovieDBSeries);
            List<int> seriesid = lk.SelectMany(a => a).Select(a => int.Parse(a.ProviderID)).ToList();
            Dictionary<int, MovieDB_Series> cr = session
                .CreateCriteria(typeof(MovieDB_Series))
                .Add(Restrictions.In("SeriesId", seriesid))
                .List<MovieDB_Series>().ToDictionary(a=>a.SeriesID,a=>a);
            Dictionary<int, (CrossRef_AniDB, MovieDB_Series)> dic = new Dictionary<int, (CrossRef_AniDB, MovieDB_Series)>();
            foreach (IGrouping<int, CrossRef_AniDB> g in lk)
            {
                if (g.Any())
                {
                    CrossRef_AniDB kr = g.ElementAt(0);
                    int movieid = int.Parse(kr.ProviderID);
                    if (cr.ContainsKey(movieid))
                    {
                        dic.Add(g.Key, (kr, cr[movieid]));
                    }
                }
            }

            return dic;
        }
    }
}
