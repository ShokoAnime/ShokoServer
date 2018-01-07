using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class TvDB_EpisodeRepository : BaseRepository<TvDB_Episode, int>
    {
        private PocoIndex<int, TvDB_Episode, int> SeriesIDs;
        private PocoIndex<int, TvDB_Episode, int> EpisodeIDs;

        internal override int SelectKey(TvDB_Episode entity) => entity.TvDB_EpisodeID;


        internal override void PopulateIndexes()
        {
            SeriesIDs = new PocoIndex<int, TvDB_Episode, int>(Cache, a => a.SeriesID);
            EpisodeIDs = new PocoIndex<int, TvDB_Episode, int>(Cache, a => a.Id);
        }

        internal override void ClearIndexes()
        {
            
        }


        public TvDB_Episode GetByTvDBID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return EpisodeIDs.GetOne(id);
                return Table.FirstOrDefault(a => a.Id == id);
            }
        }

        public List<TvDB_Episode> GetBySeriesID(int seriesID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID);
                return Table.Where(a => a.SeriesID == seriesID).ToList();
            }
        }

        /// <summary>
        /// Returns a set of all tvdb seasons in a series
        /// </summary>
        /// <param name="seriesID"></param>
        /// <returns>distinct list of integers</returns>
        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID).Select(xref => xref.SeasonNumber).Distinct().ToList();
                return Table.Where(a => a.SeriesID == seriesID).Select(xref => xref.SeasonNumber).Distinct().ToList();
            }
        }

        /// <summary>
        /// Returns the last TvDB Season Number, or -1 if unable
        /// </summary>
        /// <param name="seriesID">The TvDB series ID</param>
        /// <returns>The last TvDB Season Number, or -1 if unable</returns>
        public int GetLastSeasonForSeries(int seriesID)
        {
            using (CacheLock.ReaderLock())
            {
                List<int> max;
                if (IsCached)
                    max = SeriesIDs.GetMultiple(seriesID).Select(xref => xref.SeasonNumber).ToList();
                else
                   max = Table.Where(a => a.SeriesID == seriesID).Select(xref => xref.SeasonNumber).ToList();

                if (max.Count == 0) return -1;
                return max.Max();
            }
        }

        /// <summary>
        /// Gets all episodes for a series and season
        /// </summary>
        /// <param name="seriesID">AnimeSeries ID</param>
        /// <param name="seasonNumber">TvDB season number</param>
        /// <returns>List of TvDB_Episodes</returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumber(int seriesID, int seasonNumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID).Where(xref => xref.SeasonNumber == seasonNumber).ToList();
                return Table.Where(a => a.SeriesID == seriesID && a.SeasonNumber == seasonNumber).ToList();
            }

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

            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID).FirstOrDefault(xref => xref.SeasonNumber == seasonNumber && xref.EpisodeNumber == epNumber);
                return Table.FirstOrDefault(a => a.SeriesID == seriesID && a.SeasonNumber == seasonNumber && a.EpisodeNumber == epNumber);
            }
        }

        public TvDB_Episode GetBySeriesIDAndDate(int seriesID, DateTime date)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID).FirstOrDefault(a => a.SeasonNumber > 0 && a.AirDate != null && Math.Abs((a.AirDate.Value - date).TotalDays) <= 2D);
                return Table.FirstOrDefault(a => a.SeriesID == seriesID && a.SeasonNumber > 0 && a.AirDate != null && Math.Abs((a.AirDate.Value - date).TotalDays) <= 2D);
            }
        }

        /// <summary>
        /// Returns the Number of Episodes in a Season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns>int</returns>
        public int GetNumberOfEpisodesForSeason(int seriesID, int seasonNumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID).Count(xref => xref.SeasonNumber == seasonNumber);
                return Table.Count(a => a.SeriesID == seriesID && a.SeasonNumber == seasonNumber);
            }
        }

        /// <summary>
        /// Returns a sorted list of all episodes in a season
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="seasonNumber"></param>
        /// <returns></returns>
        public List<TvDB_Episode> GetBySeriesIDAndSeasonNumberSorted(int seriesID, int seasonNumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return SeriesIDs.GetMultiple(seriesID).Where(xref => xref.SeasonNumber == seasonNumber).OrderBy(xref => xref.EpisodeNumber).ToList();
                return Table.Where(a => a.SeriesID == seriesID && a.SeasonNumber == seasonNumber).OrderBy(xref => xref.EpisodeNumber).ToList();
            }
        }
    }
}