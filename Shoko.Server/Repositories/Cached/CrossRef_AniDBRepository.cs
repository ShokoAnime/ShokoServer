using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDBRepository : BaseCachedRepository<CrossRef_AniDB, int>
    {
        private PocoIndex<int, CrossRef_AniDB, int, string> AnimeProviderIDs;
        private PocoIndex<int, CrossRef_AniDB, string, string> ProviderIDs;
        private PocoIndex<int, CrossRef_AniDB, int> AnimeIDs;
        public override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB, int>(Cache, a => a.AniDBID);
            AnimeProviderIDs = new PocoIndex<int, CrossRef_AniDB, int, string>(Cache, a => a.AniDBID, a => a.Provider);
            ProviderIDs = new PocoIndex<int, CrossRef_AniDB, string, string>(Cache, a => a.ProviderID, a => a.Provider);
        }

        public List<CrossRef_AniDB> GetByAniDB(int id, string provider = null)
        {
            lock (Cache)
            {
                if (provider == null) 
                    return AnimeIDs.GetMultiple(id);
                return AnimeProviderIDs.GetMultiple(id, provider);
            }
        }
        public List<CrossRef_AniDB> GetByProviderID(string providerId, string provider)
        {
            lock (Cache)
            {
                return ProviderIDs.GetMultiple(providerId, provider);
            }
        }
        public ILookup<int, CrossRef_AniDB> GetByAniDBIDs(IReadOnlyCollection<int> aniDbIds, string provider = null)
        {
            if (aniDbIds == null)
                throw new ArgumentNullException(nameof(aniDbIds));

            if (aniDbIds.Count == 0)
            {
                return EmptyLookup<int, CrossRef_AniDB>.Instance;
            }

            lock (Cache)
            {
                if (provider==null)
                    return aniDbIds.SelectMany(id => AnimeIDs.GetMultiple(id))
                        .ToLookup(xref => xref.AniDBID);
                else
                    return aniDbIds.SelectMany(id => AnimeProviderIDs.GetMultiple(id, provider))
                        .ToLookup(xref => xref.AniDBID);
            }
        }

        public List<SVR_AnimeSeries> GetSeriesWithoutLinks(string provider, MediaType media)
        {
            return RepoFactory.AnimeSeries.GetAll().Where(a =>
            {
                var anime = a.GetAnime();
                if (anime == null) return false;
                if (anime.Restricted > 0) return false;
                if (anime.AnimeType == (int) AnimeType.Movie && media==MediaType.TvShow) return false;
                if (anime.AnimeType != (int)AnimeType.Movie && media == MediaType.Movie) return false;
                return !GetByAniDB(a.AniDB_ID, provider).Any();
            }).ToList();
        }
        public CrossRef_AniDB GetByAniDBAndProviderID(int animeID, string providerId, string provider)
        {
            lock (Cache)
            {
                return ProviderIDs.GetMultiple(providerId, provider).FirstOrDefault(xref => xref.AniDBID == animeID);
            }
        }
        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB entity)
        {
            return entity.CrossRef_AniDBID;
        }
        
        public List<CrossRef_AniDB_EpisodeMap> GetMapLinksFromAnime(int animeID)
        {
            List<CrossRef_AniDB_EpisodeMap> ls = new List<CrossRef_AniDB_EpisodeMap>();
            ls.AddRange(GetMapLinksFromAnime(animeID, Shoko.Models.Constants.Providers.TvDB));
            ls.AddRange(GetMapLinksFromAnime(animeID, Shoko.Models.Constants.Providers.Trakt));
            return ls;
        }

        public List<(int AniDBEpisodeID, string ProviderEpisodeID, bool overRide)> GetMapLinksFromAnimePlain(int animeID, string provider)
        {
            var overrides = RepoFactory.CrossRef_AniDB_Episode_Override.GetByAnimeID(animeID, provider);
            var normals = RepoFactory.CrossRef_AniDB_Episode.GetByAnimeID(animeID, provider);
            List<(int AniDBEpisodeID, string ProviderEpisodeID, bool overRide)> ls = new List<(int AniDBEpisodeID, string ProviderEpisodeID, bool overRide)>();
            foreach (CrossRef_AniDB_Episode epo in normals)
            {
                CrossRef_AniDB_Episode_Override ov = overrides.FirstOrDefault(a => a.AniDBEpisodeID == epo.AniDBEpisodeID);
                if (ov != null)
                {
                    ls.Add((ov.AniDBEpisodeID,ov.ProviderEpisodeID, true));
                    overrides.Remove(ov);
                }
                else
                {
                    ls.Add((epo.AniDBEpisodeID,epo.ProviderEpisodeID, false));
                }
            }
            foreach(CrossRef_AniDB_Episode_Override ov in overrides)
                ls.Add((ov.AniDBEpisodeID,ov.ProviderEpisodeID, true));
            return ls;
        }

        private CrossRef_AniDB_EpisodeMap ConvertToMap((AniDB_Episode AniDB, GenericEpisode Episode) b)
        {
            return new CrossRef_AniDB_EpisodeMap
            {
                Provider = b.Episode.Provider,
                AnimeID = b.AniDB.AnimeID,
                AniDBEpisodeID = b.AniDB.EpisodeID,
                ProviderEpisodeID = b.Episode.ProviderEpisodeId,
                AniDBStartEpisodeType = b.AniDB.EpisodeType,
                AniDBStartEpisodeNumber = b.AniDB.EpisodeNumber,
                ProviderID = b.Episode.ProviderId,
                ProviderSeasonNumber = b.Episode.SeasonNumber,
                ProviderEpisodeNumber = b.Episode.EpisodeNumber,
                Title = b.Episode.Title
            };
        }
        public List<CrossRef_AniDB_EpisodeMap> GetMapLinksFromAnime(int animeID, string provider)
        {
            IEpisodeGenericRepo repo = GenericEpisode.RepoFromProvider(provider);
            List<(int AniDBEpisodeID, string ProviderEpisodeID, bool overRide)> ls = GetMapLinksFromAnimePlain(animeID, provider);




            List<(AniDB_Episode AniDB, GenericEpisode Episode)> eplinks = ls.ToLookup(a=> RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID),b=>repo.GetByEpisodeProviderID(b.ProviderEpisodeID))
                .Select(a => (AniDB: a.Key, Episode: a.FirstOrDefault())).Where(a => a.AniDB != null && a.Episode != null)
                .OrderBy(a => a.AniDB.EpisodeType).ThenBy(a => a.AniDB.EpisodeNumber).ToList();

            List<CrossRef_AniDB_EpisodeMap> result = new List<CrossRef_AniDB_EpisodeMap>();


            for (int i = 0; i < eplinks.Count; i++)
            {
                // Cases:
                // - first ep
                // - new type/season
                // - the next episode is not a simple increment

                var b = eplinks[i];

                if (i == 0)
                {
                    if (b.AniDB == null || b.Episode == null) return new List<CrossRef_AniDB_EpisodeMap>();
                    result.Add(ConvertToMap(b));
                    continue;
                }

                var a = eplinks[i - 1];

                if (a.AniDB.EpisodeType != b.AniDB.EpisodeType || b.Episode.SeasonNumber != a.Episode.SeasonNumber)
                {
                    result.Add(ConvertToMap(b));
                    continue;
                }

                if (b.AniDB.EpisodeNumber - a.AniDB.EpisodeNumber != 1 ||
                    b.Episode.EpisodeNumber - a.Episode.EpisodeNumber != 1)
                {
                    result.Add(ConvertToMap(b));
                }
            }

            return result;
        }

    }
}
