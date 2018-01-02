using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;


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

        internal override int SelectKey(AniDB_Episode entity) => entity.AniDB_EpisodeID;

        public override void Init(IProgress<RegenerateProgress> progress, int batchSize)
        {
            List<AniDB_Episode> episodes = Where(episode => episode.EnglishName.Contains('`') || episode.RomajiName.Contains('`')).ToList();
            RegenerateProgress regen = new RegenerateProgress();
            regen.Title = "Fixing Episode Titles";
            regen.Step = 0;
            regen.Total = episodes.Count;
            progress.Report(regen);
            BatchAction(episodes, batchSize, (episode, original) =>
            {
                episode.EnglishName = episode.EnglishName.Replace('`', '\'');
                episode.RomajiName = episode.RomajiName.Replace('`', '\'');
                regen.Step++;
                progress.Report(regen);
            });
            regen.Step = regen.Total;
            progress.Report(regen);
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
                Repo.CrossRef_File_Episode.GetAll()
                    .GroupBy(a => a.EpisodeID)
                    .Where(a => a.Count() > 1)
                    .Select(a => GetByEpisodeID(a.Key))
                    .ToList();
        }
    }
}