using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AniDB_EpisodeRepository : BaseRepository<AniDB_Episode, int>
    {
        private PocoIndex<int, AniDB_Episode, int> EpisodesIds;
        private PocoIndex<int, AniDB_Episode, int> Animes;

        internal override void PopulateIndexes()
        {
            EpisodesIds = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.EpisodeID);
            Animes = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            EpisodesIds = null;
        }

        private AniDB_EpisodeRepository()
        {
        }



        internal override int SelectKey(AniDB_Episode entity) => entity.AniDB_EpisodeID;

        internal override void RegenerateDb(IProgress<RegenerateProgress> progress)
        {
            List<AniDB_Episode> episodes;
            using (CacheLock.ReaderLock())
            {
                episodes = IsCached ? Cache.Values.Where(episode => episode.EnglishName.Contains('`') || episode.RomajiName.Contains('`')).ToList() : Table.Where((episode => episode.EnglishName.Contains('`') || episode.RomajiName.Contains('`'))).ToList();
            }
            using (IAtomic<List<AniDB_Episode>,object> update = BeginAtomicBatchUpdate(episodes))
            {
                RegenerateProgress regen = new RegenerateProgress();
                regen.Title = "Fixing Episode Titles";
                regen.Step = 0;
                regen.Total = update.Updatable.Count;
                foreach (AniDB_Episode episode in update.Updatable)
                {
                    episode.EnglishName = episode.EnglishName.Replace('`', '\'');
                    episode.RomajiName = episode.RomajiName.Replace('`', '\'');
                    regen.Step++;
                    progress.Report(regen);
                }
                update.Commit();
                regen.Step = regen.Total;
                progress.Report(regen);
            }
        }


        public AniDB_Episode GetByEpisodeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return EpisodesIds.GetOne(id);
                return Table.FirstOrDefault(a => a.EpisodeID == id);
            }
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeid).Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == EpisodeType.Episode).ToList();
                return Table.Where(a=>a.AnimeID==animeid && a.EpisodeNumber==epnumber && a.EpisodeType==(int)EpisodeType.Episode).ToList();
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeid).Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == epType).ToList();
                return Table.Where(a => a.AnimeID == animeid && a.EpisodeNumber == epnumber && a.EpisodeType == (int)epType).ToList();
            }
        }

        public List<AniDB_Episode> GetEpisodesWithMultipleFiles()
        {
            return
                RepoFactory.CrossRef_File_Episode.GetAll()
                    .GroupBy(a => a.EpisodeID)
                    .Where(a => a.Count() > 1)
                    .Select(a => GetByEpisodeID(a.Key))
                    .ToList();
        }
    }
}