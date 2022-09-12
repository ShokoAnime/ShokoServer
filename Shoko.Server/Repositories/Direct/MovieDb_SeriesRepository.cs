using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Providers;
using Shoko.Server.Repositories.NHibernate;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Repositories.Direct
{
    public class MovieDb_SeriesRepository : BaseDirectRepository<MovieDB_Series, int>, IEpisodeGenericRepo
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
            ILookup<int, CrossRef_AniDB> lk=RepoFactory.CrossRef_AniDB.GetByAniDBIDs(animeIds, Shoko.Models.Constants.Providers.MovieDB, MediaType.TvShow);
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

        private TvGroupCollection GetGroupFromId(string id)
        {
            List<GenericEpisode> episodes = new List<GenericEpisode>();
            MovieDB_Series series = GetByOnlineID(int.Parse(id));
            if (series?.Blob == null)
                return null;
            TvShow show = series.ToTvShow();
            if (show == null)
                return null;
            return show.GetBestGroup();
        }

        public List<GenericEpisode> GetByProviderID(string providerId)
        {
            TvGroupCollection group = GetGroupFromId(providerId);
            if (group == null)
                return new List<GenericEpisode>();
            return group.Groups.OrderBy(a => a.Order).SelectMany(a => a.Episodes).Select(a => new GenericEpisode(a)).ToList();
        }

        
        public int GetNumberOfEpisodesForSeason(string providerId, int season)
        {
            TvGroupCollection group = GetGroupFromId(providerId);
            if (group == null)
                return 0;
            return group.Groups.SelectMany(a => a.Episodes).Count(a => a.SeasonNumber == season);
        }

        public int GetLastSeasonForSeries(string providerId)
        {
            TvGroupCollection group = GetGroupFromId(providerId);
            if (group == null)
                return 0;
            return group.Groups.SelectMany(a => a.Episodes).Max(a => a.SeasonNumber);
        }

        public GenericEpisode GetByEpisodeProviderID(string episodeproviderId)
        {
            string[] parts = episodeproviderId.Split('_');
            if (parts.Length != 2)
                parts = episodeproviderId.Split('#');
            if (parts.Length < 2)
                return null;
            TvGroupCollection group = GetGroupFromId(parts[0]);
            if (group == null)
                return null;
            TvGroupEpisode ep = null;
            if (parts.Length==2)
                ep= group.Groups.SelectMany(a => a.Episodes).FirstOrDefault(a => a.Id == int.Parse(parts[1]));
            else if (parts.Length == 3)
                ep = group.Groups.SelectMany(a => a.Episodes).FirstOrDefault(a => a.SeasonNumber == int.Parse(parts[1]) & a.EpisodeNumber == int.Parse(parts[2]));
            if (ep == null)
                return null;
            return new GenericEpisode(ep);
        }


        public GenericEpisode GetByProviderIdSeasonAnEpNumber(string providerId, int season, int epNumber)
        {
            TvGroupCollection group = GetGroupFromId(providerId);
            if (group == null)
                return null;
            TvGroupEpisode ep = group.Groups.SelectMany(a => a.Episodes).FirstOrDefault(a => a.SeasonNumber == season & a.EpisodeNumber == epNumber);
            if (ep == null)
                return null;
            return new GenericEpisode(ep);
        }
    }
}
