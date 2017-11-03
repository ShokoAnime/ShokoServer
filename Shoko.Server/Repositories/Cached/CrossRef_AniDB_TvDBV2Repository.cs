using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDB_TvDBV2Repository : BaseCachedRepository<CrossRef_AniDB_TvDBV2, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDBV2, int> TvDBIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDBV2, int> AnimeIDs;

        public override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, CrossRef_AniDB_TvDBV2, int>(Cache, a => a.TvDBID);
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDBV2, int>(Cache, a => a.AnimeID);
        }

        private CrossRef_AniDB_TvDBV2Repository()
        {
        }

        public static CrossRef_AniDB_TvDBV2Repository Create()
        {
            return new CrossRef_AniDB_TvDBV2Repository();
        }

        public List<CrossRef_AniDB_TvDBV2> GetByAnimeID(int id)
        {
            return AnimeIDs.GetMultiple(id).OrderBy(xref => xref.AniDBStartEpisodeType)
                .ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList();
        }

        public List<CrossRef_AniDB_TvDBV2> GetByTvDBID(int id)
        {
            return TvDBIDs.GetMultiple(id).OrderBy(xref => xref.AniDBStartEpisodeType)
                .ThenBy(xref => xref.AniDBStartEpisodeNumber).ToList();
        }

        public ILookup<int, CrossRef_AniDB_TvDBV2> GetByAnimeIDs(IReadOnlyCollection<int> animeIds)
        {
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Count == 0)
            {
                return EmptyLookup<int, CrossRef_AniDB_TvDBV2>.Instance;
            }

            return animeIds.SelectMany(id => AnimeIDs.GetMultiple(id))
                .OrderBy(xref => xref.AniDBStartEpisodeType).ThenBy(xref => xref.AniDBStartEpisodeNumber)
                .ToLookup(xref => xref.AnimeID);
        }

        public List<CrossRef_AniDB_TvDBV2> GetByAnimeIDEpTypeEpNumber(int id, int aniEpType, int aniEpisodeNumber)
        {
            return AnimeIDs.GetMultiple(id)
                .Where(xref => xref.AniDBStartEpisodeType == aniEpType &&
                               xref.AniDBStartEpisodeNumber <= aniEpisodeNumber)
                .OrderByDescending(xref => xref.AniDBStartEpisodeNumber).ToList();
        }

        public CrossRef_AniDB_TvDBV2 GetByTvDBID(int id, int season, int episodeNumber, int animeID,
            int aniEpType, int aniEpisodeNumber)
        {
            return TvDBIDs.GetMultiple(id).FirstOrDefault(xref => xref.TvDBSeasonNumber == season &&
                                                                  xref.TvDBStartEpisodeNumber == episodeNumber &&
                                                                  xref.AnimeID == animeID &&
                                                                  xref.AniDBStartEpisodeType == aniEpType &&
                                                                  xref.AniDBStartEpisodeNumber == aniEpisodeNumber);
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB_TvDBV2 entity)
        {
            return entity.CrossRef_AniDB_TvDBV2ID;
        }
    }
}