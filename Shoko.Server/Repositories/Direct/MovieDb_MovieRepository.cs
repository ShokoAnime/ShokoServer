using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class MovieDb_MovieRepository : BaseDirectRepository<MovieDB_Movie, int>
    {
        public MovieDB_Movie GetByOnlineID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByOnlineID(session.Wrap(), id);
            }
        }

        public MovieDB_Movie GetByOnlineID(ISessionWrapper session, int id)
        {
            MovieDB_Movie cr = session
                .CreateCriteria(typeof(MovieDB_Movie))
                .Add(Restrictions.Eq("MovieId", id))
                .UniqueResult<MovieDB_Movie>();
            return cr;
        }

        public Dictionary<int, (CrossRef_AniDB, MovieDB_Movie)> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return new Dictionary<int, (CrossRef_AniDB, MovieDB_Movie)>();
            }
            ILookup<int, CrossRef_AniDB> lk=RepoFactory.CrossRef_AniDB.GetByAniDBIDs(animeIds, Shoko.Models.Constants.Providers.MovieDB);
            List<int> movieids = lk.SelectMany(a => a).Select(a => int.Parse(a.ProviderID)).ToList();
           Dictionary<int, MovieDB_Movie> cr = session
                .CreateCriteria(typeof(MovieDB_Movie))
                .Add(Restrictions.In("MovieId", movieids))
                .List<MovieDB_Movie>().ToDictionary(a=>a.MovieId,a=>a);
            Dictionary<int, (CrossRef_AniDB, MovieDB_Movie)> dic = new Dictionary<int, (CrossRef_AniDB, MovieDB_Movie)>();
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