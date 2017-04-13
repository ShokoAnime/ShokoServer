using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using NHibernate.Criterion;
using NHibernate.Util;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class TvDB_EpisodeRepository : BaseDirectRepository<TvDB_Episode, int>
    {
        private TvDB_EpisodeRepository()
        {
        }

        public static TvDB_EpisodeRepository Create()
        {
            return new TvDB_EpisodeRepository();
        }

        public TvDB_Episode GetByTvDBID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                TvDB_Episode cr = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .Add(Restrictions.Eq("Id", id))
                    .UniqueResult<TvDB_Episode>();
                return cr;
            }
        }

        public List<TvDB_Episode> GetBySeriesID(int seriesID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session.Wrap(), seriesID);
            }
        }

        public List<TvDB_Episode> GetBySeriesID(ISessionWrapper session, int seriesID)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_Episode))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .List<TvDB_Episode>();

            return objs.ToList();
        }

        /// <summary>
        /// Returns a set of all tvdb seasons in a series
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns>distinct list of integers
        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetSeasonNumbersForSeries(session, seriesID);
            }
        }

        /// <summary>
        /// Returns a set of all tvdb seasons in a series
        /// </summary>
        /// <param name="session"></param>
        /// <param name="seriesID"></param>
        /// <returns>distinct list of integers</returns>
        public List<int> GetSeasonNumbersForSeries(ISession session, int seriesID)
        {
            /*var objs = session
                .CreateCriteria(typeof(TvDB_Episode))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .AddOrder(Order.Asc("SeasonNumber"))
                .List<TvDB_Episode>();*/
            var objs = session
                .CreateSQLQuery(
                    "select distinct SeasonNumber from TvDB_Episode\nwhere SeriesID = :sid\nOrder By SeasonNumber")
                .SetInt32("sid", seriesID).List<int>();

            return objs.ToList();
        }

        /// <summary>
        /// Returns the last TvDB Season Number, or -1 if unable
        /// </summary>
        /// <param name="seriesID">The TvDB series ID</param>
        /// <returns>The last TvDB Season Number, or -1 if unable</returns>
        public int getLastSeasonForSeries(int seriesID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return getLastSeasonForSeries(session, seriesID);
            }
        }

        /// <summary>
        /// Returns the last TvDB Season Number, or -1 if unable
        /// </summary>
        /// <param name="session"></param>
        /// <param name="seriesID">The TvDB series ID</param>
        /// <returns>The last TvDB Season Number, or -1 if unable</returns>
        public int getLastSeasonForSeries(ISession session, int seriesID)
        {
            // normally bad practice, but we are running a max(), so there will only ever be 0 or 1 result
            var result = session
                .CreateSQLQuery(
                    "select max(SeasonNumber) from TvDB_Episode\nwhere SeriesID = :sid")
                .SetInt32("sid", seriesID).UniqueResult();
            return (int)(result ?? -1);
        }

        /// <summary>
        /// Gets all episodes for a series and season
        /// </summary>
        /// <param name="seriesID">AnimeSeries ID</param>
        /// <param name="seasonNumber">TvDB season number</param>
        /// <returns>List of TvDB_Episodes</returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumber(int seriesID, int seasonNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesIDAndSeasonNumber(session, seriesID, seasonNumber);
            }
        }

        /// <summary>
        /// Gets all episodes for a series and season
        /// </summary>
        /// <param name="session"></param>
        /// <param name="seriesID">AnimeSeries ID</param>
        /// <param name="seasonNumber">TvDB season number</param>
        /// <returns>List of TvDB_Episodes</returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumber(ISession session, int seriesID, int seasonNumber)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_Episode))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .Add(Restrictions.Eq("SeasonNumber", seasonNumber))
                .List<TvDB_Episode>();
            return objs.ToList();
        }

        /// <summary>
        /// Gets a unique episode by series, season, and tvdb episode number
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <param name="epNumber"></param>
        /// <returns></returns>
        public TvDB_Episode GetBySeriesIDSeasonNumberAndEpisode(int seriesID, int seasonNumber, int epNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetBySeriesIDSeasonNumberAndEpisode(session, seriesID, seasonNumber, epNumber);
            }
        }

        /// <summary>
        /// Gets a unique episode by series, season, and tvdb episode number
        /// </summary>
        /// <param name="session"></param>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <param name="epNumber"></param>
        /// <returns></returns>
        public TvDB_Episode GetBySeriesIDSeasonNumberAndEpisode(ISession session, int seriesID, int seasonNumber, int epNumber)
        {
            var objs = session
                .CreateCriteria(typeof(TvDB_Episode))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .Add(Restrictions.Eq("SeasonNumber", seasonNumber))
                .Add(Restrictions.Eq("EpisodeNumber", epNumber))
                .UniqueResult<TvDB_Episode>();
            return objs;
        }

        /// <summary>
        /// Returns the Number of Episodes in a Season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns>int</returns>
        public int GetNumberOfEpisodesForSeason(int seriesID, int seasonNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetNumberOfEpisodesForSeason(session, seriesID, seasonNumber);
            }
        }

        /// <summary>
        /// Returns the Number of Episodes in a Season
        /// </summary>
        /// <param name="session"></param>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns>int</returns>
        public int GetNumberOfEpisodesForSeason(ISession session, int seriesID, int seasonNumber)
        {
            return session.CreateCriteria(typeof(int))
                .Add(Restrictions.Eq("SeriesID", seriesID))
                .Add(Restrictions.Eq("SeasonNumber", seasonNumber))
                .SetProjection(Projections.Count(Projections.Id()))
                .UniqueResult<int>();
        }

        /// <summary>
        /// Returns a sorted list of all episodes in a season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns></returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumberSorted(int seriesID, int seasonNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(TvDB_Episode))
                    .Add(Restrictions.Eq("SeriesID", seriesID))
                    .Add(Restrictions.Eq("SeasonNumber", seasonNumber))
                    .AddOrder(Order.Asc("EpisodeNumber"))
                    .List<TvDB_Episode>();

                return new List<TvDB_Episode>(objs);
            }
        }
    }
}