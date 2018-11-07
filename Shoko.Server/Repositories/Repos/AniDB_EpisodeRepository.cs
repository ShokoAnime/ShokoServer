using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
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

        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            /*List<AniDB_Episode> episodes = Where(episode => episode.EnglishName.Contains('`') || episode.RomajiName.Contains('`')).ToList();
            InitProgress regen = new InitProgress();
            regen.Title = "Fixing Episode Titles";
            regen.Step = 0;
            regen.Total = episodes.Count;
            progress.Report(regen);
            /* this isn't in the repo anymore.
            BatchAction(episodes, batchSize, (episode, original) =>
            {
                episode.EnglishName = episode.EnglishName.Replace('`', '\'');
                episode.RomajiName = episode.RomajiName.Replace('`', '\'');
                regen.Step++;
                progress.Report(regen);
            });* /
            regen.Step = regen.Total;
            progress.Report(regen);*/
        }


        public AniDB_Episode GetByEpisodeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return EpisodesIds.GetOne(id);
                return Table.FirstOrDefault(a => a.EpisodeID == id);
            }
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetAniDBEpisodesIdByAnimeIds(IEnumerable<int> animeids)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return animeids.SelectMany(a => Animes.GetMultiple(a)).Select(a => a.AniDB_EpisodeID).Distinct().ToList();
                return Table.Where(a => animeids.Contains(a.AnimeID)).Select(a => a.AniDB_EpisodeID).Distinct().ToList();
            }

        }
        public List<int> GetAniDBEpisodesIdByAnimeId(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_EpisodeID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a=>a.AniDB_EpisodeID).ToList();
            }
        }
        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeid).Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == EpisodeType.Episode).ToList();
                return Table.Where(a=>a.AnimeID==animeid && a.EpisodeNumber==epnumber && a.EpisodeType==(int)EpisodeType.Episode).ToList();
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeid).Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == epType).ToList();
                return Table.Where(a => a.AnimeID == animeid && a.EpisodeNumber == epnumber && a.EpisodeType == (int)epType).ToList();
            }
        }

        public List<AniDB_Episode> GetEpisodesWithMultipleFiles()
        {
            List<int> ids = Repo.Instance.CrossRef_File_Episode.GetEpisodesIdsWithMultipleFiles();
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ids.Select(a => EpisodesIds.GetOne(a)).ToList();
                return Table.Where(a => ids.Contains(a.EpisodeID)).ToList();
            }
        }

        public Dictionary<int, List<int>> GetGroupByAnimeIDEpisodes()
        {
            using (RepoLock.ReaderLock())
            {
                return WhereAll().GroupBy(a => a.AnimeID).ToDictionary(a => a.Key, a => a.Select(b => b.AniDB_EpisodeID).ToList());
            }
        }
    }
}