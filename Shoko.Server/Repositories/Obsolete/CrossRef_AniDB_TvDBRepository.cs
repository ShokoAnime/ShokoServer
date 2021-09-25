using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SqlServer.Management.Sdk.Differencing.SPI;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers;

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

    }
}
