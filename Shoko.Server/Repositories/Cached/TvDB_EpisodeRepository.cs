using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

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

        public TvDB_Episode GetByTvDBID(int id)
        {
            return ReadLock(() => EpisodeIDs.GetOne(id));
        }

        public List<TvDB_Episode> GetBySeriesID(int seriesID)
        {
            return ReadLock(() => SeriesIDs.GetMultiple(seriesID));
        }

        /// <summary>
        /// Returns a set of all tvdb seasons in a series
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns>distinct list of integers</returns>
        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            return GetBySeriesID(seriesID).Select(xref => xref.SeasonNumber).Distinct().ToList();
        }

        /// <summary>
        /// Returns the last TvDB Season Number, or -1 if unable
        /// </summary>
        /// <param name="seriesID">The TvDB series ID</param>
        /// <returns>The last TvDB Season Number, or -1 if unable</returns>
        public int GetLastSeasonForSeries(int seriesID)
        {
            var seriesIDs = GetBySeriesID(seriesID);
            if (seriesIDs.Count == 0) return -1;
            return seriesIDs.Max(xref => xref.SeasonNumber);
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
            return GetBySeriesID(seriesID).FirstOrDefault(xref => xref.SeasonNumber == seasonNumber &&
                                                                          xref.EpisodeNumber == epNumber);
        }

        /// <summary>
        /// Returns the Number of Episodes in a Season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns>int</returns>
        public int GetNumberOfEpisodesForSeason(int seriesID, int seasonNumber)
        {
            return GetBySeriesID(seriesID).Count(xref => xref.SeasonNumber == seasonNumber);
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
