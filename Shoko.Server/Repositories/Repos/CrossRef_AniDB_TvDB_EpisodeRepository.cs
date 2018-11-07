using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_TvDB_EpisodeRepository : BaseRepository<CrossRef_AniDB_TvDB_Episode, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int> EpisodeIDs;

        internal override int SelectKey(CrossRef_AniDB_TvDB_Episode entity) => entity.CrossRef_AniDB_TvDB_EpisodeID;


        internal override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int>(Cache, a => Repo.Instance.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int>(Cache, a => a.AniDBEpisodeID);
        }

        internal override void ClearIndexes()
        {
            AnimeIDs = null;
            EpisodeIDs = null;
        }


        public List<CrossRef_AniDB_TvDB_Episode> GetByAniDBEpisodeID(int id)
        {
            // TODO Change this when multiple AniDB <=> TvDB Episode mappings
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return EpisodeIDs.GetMultiple(id).ToList();
                return Table.Where(a => a.AniDBEpisodeID==id).ToList();
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeIDs.GetMultiple(id);

                return Table
                    .Join(Repo.Instance.AniDB_Episode.GetAll(), a => a.AniDBEpisodeID, b => b.AniDB_EpisodeID, (xref, ae) => new { xref, ae })
                    .Where(jn => jn.ae.AnimeID == id).Select(jn => jn.xref).ToList();
            }
        }

        internal CrossRef_AniDB_TvDB_Episode GetByAniDBAndTvDBEpisodeIDs(int anidbID, int tvdbID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)  return EpisodeIDs.GetMultiple(anidbID).FirstOrDefault(a => a.TvDBEpisodeID == tvdbID);
                return Where(a => a.AniDBEpisodeID == anidbID).FirstOrDefault(a => a.TvDBEpisodeID == tvdbID);
            }
        }

        internal void DeleteAllUnverifiedLinksForAnime(int animeID)
        {
            using (RepoLock.WriterLock())
            {
                FindAndDelete(() => GetByAnimeID(animeID).Where(a => a.MatchRating != MatchRating.UserVerified)
                            .ToList());
            }
        }

        internal void DeleteAllUnverifiedLinks()
        {
            using (RepoLock.WriterLock())
                FindAndDelete(() => Where(s => s.MatchRating == MatchRating.UserVerified).ToList());
        }
    }
}