using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDB_TvDBRepository : BaseCachedRepository<CrossRef_AniDB_TvDB, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB, int> TvDBIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB, int> AnimeIDs;

        public override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, CrossRef_AniDB_TvDB, int>(Cache, a => a.TvDBID);
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB, int>(Cache, a => a.AniDBID);
        }

        private CrossRef_AniDB_TvDBRepository()
        {
            EndSaveCallback +=
                (db) => TvDBLinkingHelper.GenerateTvDBEpisodeMatches(db.AniDBID);
        }

        public static CrossRef_AniDB_TvDBRepository Create()
        {
            var repo = new CrossRef_AniDB_TvDBRepository();
            Repo.CachedRepositories.Add(repo);
            return repo;
        }

        public List<CrossRef_AniDB_TvDB> GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return AnimeIDs.GetMultiple(id);
            }
        }

        public List<CrossRef_AniDB_TvDB> GetByTvDBID(int id)
        {
            lock (Cache)
            {
                return TvDBIDs.GetMultiple(id);
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

            lock (Cache)
            {
                return animeIds.SelectMany(id => AnimeIDs.GetMultiple(id))
                    .ToLookup(xref => xref.AniDBID);
            }
        }

        public CrossRef_AniDB_TvDB GetByAniDBAndTvDBID(int animeID, int tvdbID)
        {
            lock (Cache)
            {
                return TvDBIDs.GetMultiple(tvdbID).FirstOrDefault(xref => xref.AniDBID == animeID);
            }
        }

        public List<SVR_AnimeSeries> GetSeriesWithoutLinks()
        {
            return Repo.AnimeSeries.GetAll().Where(a =>
            {
                var anime = a.GetAnime();
                if (anime == null) return false;
                if (anime.Restricted > 0) return false;
                if (anime.AnimeType == (int) AnimeType.Movie) return false;
                return !GetByAnimeID(a.AniDB_ID).Any();
            }).ToList();
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB_TvDB entity)
        {
            return entity.CrossRef_AniDB_TvDBID;
        }

        public List<CrossRef_AniDB_TvDBV2> GetV2LinksFromAnime(int animeID)
        {
            List<(AniDB_Episode AniDB, TvDB_Episode TvDB)> eplinks = Repo.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID)
                .ToLookup(a => Repo.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID),
                    b => Repo.TvDB_Episode.GetByTvDBID(b.TvDBEpisodeID))
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
                TvDBTitle = Repo.TvDB_Series.GetByTvDBID(a.TvDBSeries)?.SeriesName
            }).ToList();
        }
    }
}
