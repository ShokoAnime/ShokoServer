using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeEpisodeRepository : BaseRepository<SVR_AnimeEpisode, int>
    {
        private PocoIndex<int, SVR_AnimeEpisode, int> Series;
        private PocoIndex<int, SVR_AnimeEpisode, int> EpisodeIDs;

        internal override object BeginSave(SVR_AnimeEpisode entity, SVR_AnimeEpisode original_entity, object parameters)
        {
            UpdatePlexContract(entity);
            return null;
        }

        internal override void EndDelete(SVR_AnimeEpisode entity, object returnFromBeginDelete, object parameters)
        {
            Repo.AnimeEpisode_User.Delete(entity.AnimeEpisodeID);
        }

        internal override int SelectKey(SVR_AnimeEpisode entity)
        {
            return entity.AnimeEpisodeID;
        }

        internal override void PopulateIndexes()
        {
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            EpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
        }

        internal override void ClearIndexes()
        {
            Series = null;
            EpisodeIDs = null;
        }


        private void UpdatePlexContract(SVR_AnimeEpisode e)
        {
            e.PlexContract = Helper.GenerateVideoFromAnimeEpisode(e);
        }




        public List<SVR_AnimeEpisode> GetBySeriesID(int seriesid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Series.GetMultiple(seriesid);
                return Table.Where(a => a.AnimeSeriesID==seriesid).ToList();
            }

        }
        public List<int> GetAniDBEpisodesIdBySeriesIDs(IEnumerable<int> seriesids)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return seriesids.SelectMany(a=>Series.GetMultiple(a)).Select(a=>a.AniDB_EpisodeID).Distinct().ToList();
                return Table.Where(a => seriesids.Contains(a.AnimeSeriesID)).Select(a=>a.AniDB_EpisodeID).Distinct().ToList();
            }

        }

        public List<SVR_AnimeEpisode> GetByAniDBEpisodeID(int epid)
        {
            //AniDB_Episode may not unique for the series, Example with Toriko Episode 1 and One Piece 492, same AniDBEpisodeID in two shows.


            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return EpisodeIDs.GetMultiple(epid);
                return Table.Where(a => a.AniDB_EpisodeID == epid).ToList();
            }

        }
        public List<SVR_AnimeEpisode> GetByAniDBEpisodeIDs(IEnumerable<int> epids)
        {
            
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return epids.Select(a=>EpisodeIDs.GetMultiple(a)).SelectMany(a=>a).ToList();
                return Table.Where(a => epids.Contains(a.AniDB_EpisodeID)).ToList();
            }

        }

        /// <summary>
        /// Get the AnimeEpisode 
        /// </summary>
        /// <param name="name">The filename of the anime to search for.</param>
        /// <returns>the AnimeEpisode given the file information</returns>
        public SVR_AnimeEpisode GetByFilename(string name)
        {
            return Repo.VideoLocal_Place.Where(v => name.Equals(v.FilePath.Split(Path.DirectorySeparatorChar).LastOrDefault(),StringComparison.InvariantCultureIgnoreCase)).Select(a => a.VideoLocal.GetAnimeEpisodes())
                .FirstOrDefault()?.FirstOrDefault();
        }


        /// <summary>
        /// Get all the AnimeEpisode records associate with an AniDB_File record
        /// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
        /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
        /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public List<SVR_AnimeEpisode> GetByHash(string hash)
        {
            return GetByAniDBEpisodeIDs(Repo.CrossRef_File_Episode.GetByHash(hash).Select(a => a.EpisodeID).ToList());
        }
        //TODO DBRefactor
        public List<SVR_AnimeEpisode> GetEpisodesWithMultipleFiles(bool ignoreVariations)
        {

            List<string> hashes= ignoreVariations ? Repo.VideoLocal.Where(a=>a.IsVariation==0 && !string.IsNullOrEmpty(a.Hash)).Select(a=>a.Hash).ToList() : Repo.VideoLocal.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
            return GetByAniDBEpisodeIDs(Repo.CrossRef_File_Episode.GetMultiEpIdByHashes(hashes));
        }

        public List<SVR_AnimeEpisode> GetUnwatchedEpisodes(int seriesid, int userid)
        {
            List<int> eps =
                Repo.AnimeEpisode_User.GetByUserIDAndSeriesID(userid, seriesid).Where(a => a.WatchedDate.HasValue)
                    .Select(a => a.AnimeEpisodeID)
                    .ToList();
            return GetBySeriesID(seriesid).Where(a => !eps.Contains(a.AnimeEpisodeID)).ToList();
        }

        public List<SVR_AnimeEpisode> GetMostRecentlyAdded(int seriesID)
        {
            return GetBySeriesID(seriesID).OrderByDescending(a => a.DateTimeCreated).ToList();
        }
    }
}