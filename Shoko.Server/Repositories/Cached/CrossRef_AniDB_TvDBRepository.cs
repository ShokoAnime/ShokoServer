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
            return RepoFactory.AnimeSeries.GetAll().Where(a =>
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


           
            var overrides = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAnimeID(animeID);
            var normals = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID);
            List<(int anidb_episode, int tvdb_episode)> ls=new List<(int anidb_episode, int tvdb_episode)>();
            foreach (CrossRef_AniDB_TvDB_Episode epo in normals)
            {
                CrossRef_AniDB_TvDB_Episode_Override ov = overrides.FirstOrDefault(a => a.AniDBEpisodeID == epo.AniDBEpisodeID);
                if (ov != null)
                {
                    ls.Add((ov.AniDBEpisodeID,ov.TvDBEpisodeID));
                    overrides.Remove(ov);
                }
                else
                {
                    ls.Add((epo.AniDBEpisodeID,epo.TvDBEpisodeID));
                }
            }
            foreach(CrossRef_AniDB_TvDB_Episode_Override ov in overrides)
                ls.Add((ov.AniDBEpisodeID,ov.TvDBEpisodeID));

            List<(AniDB_Episode AniDB, TvDB_Episode TvDB)> eplinks = ls.ToLookup(a=> RepoFactory.AniDB_Episode.GetByEpisodeID(a.anidb_episode),b=>RepoFactory.TvDB_Episode.GetByTvDBID(b.tvdb_episode))
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
                TvDBTitle = RepoFactory.TvDB_Series.GetByTvDBID(a.TvDBSeries)?.SeriesName
            }).ToList();
        }
    }
}
