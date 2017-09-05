using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached
{
    public class TvDB_EpisodeRepository : BaseCachedRepository<TvDB_Episode, int>
    {
        private PocoIndex<int, TvDB_Episode, int> SeriesIDs;
        private PocoIndex<int, TvDB_Episode, int> EpisodeIDs;

        public override void PopulateIndexes()
        {
            SeriesIDs = new PocoIndex<int, TvDB_Episode, int>(Cache, a => a.SeriesID);
            EpisodeIDs = new PocoIndex<int, TvDB_Episode, int>(Cache, a => a.Id);
        }

        private TvDB_EpisodeRepository()
        {
        }

        public static TvDB_EpisodeRepository Create()
        {
            return new TvDB_EpisodeRepository();
        }

        public TvDB_Episode GetByTvDBID(int id)
        {
            return EpisodeIDs.GetOne(id);
        }

        public List<TvDB_Episode> GetBySeriesID(int seriesID)
        {
            return SeriesIDs.GetMultiple(seriesID);
        }

        /// <summary>
        /// Returns a set of all tvdb seasons in a series
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns>distinct list of integers</returns>
        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            return SeriesIDs.GetMultiple(seriesID).Select(xref => xref.SeasonNumber).Distinct().ToList();
        }

        /// <summary>
        /// Returns the last TvDB Season Number, or -1 if unable
        /// </summary>
        /// <param name="seriesID">The TvDB series ID</param>
        /// <returns>The last TvDB Season Number, or -1 if unable</returns>
        public int getLastSeasonForSeries(int seriesID)
        {
            if (SeriesIDs.GetMultiple(seriesID).Count == 0) return -1;
            return SeriesIDs.GetMultiple(seriesID).Max(xref => xref.SeasonNumber);
        }

        /// <summary>
        /// Gets all episodes for a series and season
        /// </summary>
        /// <param name="seriesID">AnimeSeries ID</param>
        /// <param name="seasonNumber">TvDB season number</param>
        /// <returns>List of TvDB_Episodes</returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumber(int seriesID, int seasonNumber)
        {
            return SeriesIDs.GetMultiple(seriesID).Where(xref => xref.SeasonNumber == seasonNumber).ToList();
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
            return SeriesIDs.GetMultiple(seriesID).FirstOrDefault(xref => xref.SeasonNumber == seasonNumber &&
                                                       xref.EpisodeNumber == epNumber);
        }

        public TvDB_Episode GetBySeriesIDAndDate(int seriesID, DateTime date)
        {
            return SeriesIDs.GetMultiple(seriesID)
                .FirstOrDefault(a => a.AirDate != null && Math.Abs((a.AirDate.Value - date).TotalDays) < 1.5D);
        }

        /// <summary>
        /// Returns the Number of Episodes in a Season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns>int</returns>
        public int GetNumberOfEpisodesForSeason(int seriesID, int seasonNumber)
        {
            return SeriesIDs.GetMultiple(seriesID).Count(xref => xref.SeasonNumber == seasonNumber);
        }

        /// <summary>
        /// Returns a sorted list of all episodes in a season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns></returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumberSorted(int seriesID, int seasonNumber)
        {
            return Cache.Values.Where(xref => xref.SeriesID == seriesID && xref.SeasonNumber == seasonNumber)
                .OrderBy(xref => xref.EpisodeNumber).ToList();
        }

        public ILookup<int, TvDB_Episode> GetByAnimeIDs(ISessionWrapper session,
            int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return new List<TvDB_Episode>().ToLookup(a => a.Id, a => a);
            }

            var tvDBEpisodeByAnime = session.CreateSQLQuery(@"
                SELECT {tvxref.*}, {tvep.*}
                    FROM CrossRef_AniDB_TvDBV2 tvxref
                        INNER JOIN TvDB_Episode tvep
                            ON tvxref.TvDBID = tvep.SeriesID
                    WHERE tvxref.AnimeID IN (:animeIds)")
                .AddEntity("tvxref", typeof(CrossRef_AniDB_TvDBV2))
                .AddEntity("tvep", typeof(TvDB_Episode))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>().ToLookup(r => ((CrossRef_AniDB_TvDBV2)r[0]).AnimeID, r => (TvDB_Episode)r[1]);

            return tvDBEpisodeByAnime;
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(TvDB_Episode entity)
        {
            return entity.TvDB_EpisodeID;
        }
    }
}