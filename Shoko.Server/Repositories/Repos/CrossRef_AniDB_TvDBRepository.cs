using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_TvDBRepository : BaseRepository<CrossRef_AniDB_TvDB, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB, int> TvDBIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB, int> AnimeIDs;

        internal override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, CrossRef_AniDB_TvDB, int>(Cache, a => a.TvDBID);
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB, int>(Cache, a => a.AniDBID);
        }

        internal override void EndSave(CrossRef_AniDB_TvDB entity, object returnFromBeginSave, object parameters)
        {
            base.EndSave(entity, returnFromBeginSave, parameters);
            TvDBLinkingHelper.GenerateTvDBEpisodeMatches(entity.AniDBID);
        }
        public List<CrossRef_AniDB_TvDB> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeIDs.GetMultiple(id);
                return Table.Where(a => a.AniDBID == id).ToList();
            }
        }

        public List<CrossRef_AniDB_TvDB> GetByTvDBID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return TvDBIDs.GetMultiple(id);
                return Table.Where(a => a.TvDBID == id).ToList();
            }
        }

        public ILookup<int, CrossRef_AniDB_TvDB> GetByAnimeIDs(IReadOnlyCollection<int> animeIds)
        {
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Count == 0)
            {
                return EmptyLookup<int, CrossRef_AniDB_TvDB>.Instance;
            }

            using (RepoLock.ReaderLock())
            {
                return animeIds.SelectMany(GetByAnimeID) //TODO: Test for the recursion locks.
                    .ToLookup(xref => xref.AniDBID);
            }
        }

        public CrossRef_AniDB_TvDB GetByAniDBAndTvDBID(int animeID, int tvdbID)
        {
            using (RepoLock.ReaderLock())
            {
                return TvDBIDs.GetMultiple(tvdbID).FirstOrDefault(xref => xref.AniDBID == animeID);
            }
        }

        public List<SVR_AnimeSeries> GetSeriesWithoutLinks()
        {
            return Repo.Instance.AnimeSeries.GetAll().Where(a =>
            {
                var anime = a.GetAnime();
                if (anime == null) return false;
                if (anime.Restricted > 0) return false;
                if (anime.AnimeType == (int) AnimeType.Movie) return false;
                return !GetByAnimeID(a.AniDB_ID).Any();
            }).ToList();
        }

        internal override int SelectKey(CrossRef_AniDB_TvDB entity)
        {
            return entity.CrossRef_AniDB_TvDBID;
        }

        public List<CrossRef_AniDB_TvDBV2> GetV2LinksFromAnime(int animeID)
        {
            List<(AniDB_Episode AniDB, TvDB_Episode TvDB)> eplinks = Repo.Instance.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID)
                .ToLookup(a => Repo.Instance.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID),
                    b => Repo.Instance.TvDB_Episode.GetByTvDBID(b.TvDBEpisodeID))
                .Select(a => (AniDB: a.Key, TvDB: a.FirstOrDefault())).Where(a => a.AniDB != null && a.TvDB != null)
                .OrderBy(a => a.AniDB.EpisodeType).ThenBy(a => a.AniDB.EpisodeNumber).ToList();

            List<(int EpisodeType, int EpisodeNumber, int TvDBSeries, int TvDBSeason, int TvDBNumber)> output =
                new List<(int EpisodeType, int EpisodeNumber, int TvDBSeries, int TvDBSeason, int TvDBNumber)>();

            for (int i = 0; i < eplinks.Count; i++)
            {
                // Cases:
                // - first ep
                // - new type/season
                // - the next episode is not a simple increment

                var b = eplinks[i];

                if (i == 0)
                {
                    if (b.AniDB == null || b.TvDB == null) return new List<CrossRef_AniDB_TvDBV2>();
                    output.Add((b.AniDB.EpisodeType, b.AniDB.EpisodeNumber, b.TvDB.SeriesID, b.TvDB.SeasonNumber,
                        b.TvDB.EpisodeNumber));
                    continue;
                }

                var a = eplinks[i - 1];

                if (a.AniDB.EpisodeType != b.AniDB.EpisodeType || b.TvDB.SeasonNumber != a.TvDB.SeasonNumber)
                {
                    output.Add((b.AniDB.EpisodeType, b.AniDB.EpisodeNumber, b.TvDB.SeriesID, b.TvDB.SeasonNumber,
                        b.TvDB.EpisodeNumber));
                    continue;
                }

                if (b.AniDB.EpisodeNumber - a.AniDB.EpisodeNumber != 1 ||
                    b.TvDB.EpisodeNumber - a.TvDB.EpisodeNumber != 1)
                {
                    output.Add((b.AniDB.EpisodeType, b.AniDB.EpisodeNumber, b.TvDB.SeriesID, b.TvDB.SeasonNumber,
                        b.TvDB.EpisodeNumber));
                }
            }

            return output.Select(a => new CrossRef_AniDB_TvDBV2
            {
                AnimeID = animeID,
                AniDBStartEpisodeType = a.EpisodeType,
                AniDBStartEpisodeNumber = a.EpisodeNumber,
                TvDBID = a.TvDBSeries,
                TvDBSeasonNumber = a.TvDBSeason,
                TvDBStartEpisodeNumber = a.TvDBNumber,
                TvDBTitle = Repo.Instance.TvDB_Series.GetByTvDBID(a.TvDBSeries)?.SeriesName
            }).ToList();
        }

        internal override void ClearIndexes()
        {
            TvDBIDs = null;
            AnimeIDs = null;
        }
    }
}
