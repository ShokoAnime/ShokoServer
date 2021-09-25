using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached
{
    public class TvDB_SeriesRepository : BaseCachedRepository<TvDB_Series, int>
    {
        private PocoIndex<int, TvDB_Series, int> TvDBIDs;

        public override void PopulateIndexes()
        {
            TvDBIDs = new PocoIndex<int, TvDB_Series, int>(Cache, a => a.SeriesID);
        }

        public TvDB_Series GetByTvDBID(int id)
        {
            lock (Cache)
            {
                return TvDBIDs.GetOne(id);
            }
        }
        public Dictionary<int, TvDB_Series> GetByTvDBIDs(IReadOnlyCollection<int> tvdbIds)
        {
            if (tvdbIds == null)
                throw new ArgumentNullException(nameof(tvdbIds));

            if (tvdbIds.Count == 0)
            {
                return new Dictionary<int, TvDB_Series>();
            }

            lock (Cache)
            {
                return tvdbIds.Select(id => TvDBIDs.GetOne(id)).Where(a=>a!=null).ToDictionary(a=>a.SeriesID,a=>a);
            }
        }

        public ILookup<int, (CrossRef_AniDB, TvDB_Series)> GetByAnimeIDs(int[] animeIds)
        {
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, (CrossRef_AniDB, TvDB_Series)>.Instance;
            }

            lock (globalDBLock)
            {
                ILookup<int, CrossRef_AniDB> lk=RepoFactory.CrossRef_AniDB.GetByAniDBIDs(animeIds, Shoko.Models.Constants.Providers.TvDB);
                Dictionary<int, TvDB_Series> sr = GetByTvDBIDs(lk.SelectMany(a => a).Select(a => int.Parse(a.ProviderID)).Distinct().ToList());
                Dictionary<int, List<(CrossRef_AniDB, TvDB_Series)>> dic = new Dictionary<int, List<(CrossRef_AniDB, TvDB_Series)>>();
                foreach (IGrouping<int, CrossRef_AniDB> g in lk)
                {
                    List<(CrossRef_AniDB, TvDB_Series)> ls = new List<(CrossRef_AniDB, TvDB_Series)>();
                    foreach (CrossRef_AniDB k in g)
                    {
                        int s = int.Parse(k.ProviderID);
                        if (dic.ContainsKey(s))
                            ls.Add((k,sr[s]));
                    }
                    if (ls.Count>0)
                        dic.Add(g.Key, ls);
                }

                return dic.SelectMany(a=>a.Value.Select(x=>new { Key = a.Key, Value = x})).ToLookup(a=>a.Key, a=>a.Value);

            }
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(TvDB_Series entity)
        {
            return entity.TvDB_SeriesID;
        }
    }
}
