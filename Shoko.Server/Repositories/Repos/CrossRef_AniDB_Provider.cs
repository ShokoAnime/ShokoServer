using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_ProviderRepository : BaseRepository<SVR_CrossRef_AniDB_Provider, int>
    {
        private PocoIndex<int, SVR_CrossRef_AniDB_Provider, int> Animes;
        private PocoIndex<int, SVR_CrossRef_AniDB_Provider, CrossRefType> CrossTypes;
        private PocoIndex<int, SVR_CrossRef_AniDB_Provider, CrossRefType, string> CrossRefs;
        internal override int SelectKey(SVR_CrossRef_AniDB_Provider entity) => entity.CrossRef_AniDB_ProviderID;

        internal override object BeginSave(SVR_CrossRef_AniDB_Provider entity, SVR_CrossRef_AniDB_Provider original_entity, object parameters)
        {
            entity.EpisodesList.Persist();
            entity.EpisodesListOverride.Persist();
            return null;
        }
        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, SVR_CrossRef_AniDB_Provider, int>(Cache, a => a.AnimeID);
            CrossTypes = new PocoIndex<int, SVR_CrossRef_AniDB_Provider, CrossRefType>(Cache, a => a.CrossRefType);
            CrossRefs = new PocoIndex<int, SVR_CrossRef_AniDB_Provider, CrossRefType, string>(Cache, a => a.CrossRefType, a => a.CrossRefID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            CrossTypes = null;
            CrossRefs = null;
        }

        public List<SVR_CrossRef_AniDB_Provider> GetByAnimeIDAndType(int animeID, CrossRefType xrefType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID).Where(a=>a.CrossRefType==xrefType).ToList();
                return Table.Where(a => a.AnimeID == animeID && a.CrossRefType == xrefType).ToList();
            }
        }


        public Dictionary<int, List<SVR_CrossRef_AniDB_Provider>> GetByAnimeIDsAndTypes(IEnumerable<int> animeIds, params CrossRefType[] xrefTypes)
        {
            if (xrefTypes == null || xrefTypes.Length == 0 || animeIds == null)
                return new Dictionary<int, List<SVR_CrossRef_AniDB_Provider>>();
            List<CrossRefType> types = xrefTypes.ToList();
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return animeIds.ToDictionary(a=>a,a => Animes.GetMultiple(a).Where(b => types.Contains(b.CrossRefType)).ToList());
                return Table.Where(a => animeIds.Contains(a.AnimeID) && types.Contains(a.CrossRefType)).GroupBy(a=>a.AnimeID).ToDictionary(a=>a.Key,a=>a.ToList());
            }
        }

        /// <summary>
        /// Gets other cross references by anime ID.
        /// </summary>
        /// <param name="animeIds">An optional list of anime IDs whose cross references are to be retrieved.
        /// Can be <c>null</c> to get cross references for ALL anime.</param>
        /// <param name="xrefTypes">The types of cross references to find.</param>
        /// <returns>A <see cref="ILookup{TKey,TElement}"/> that maps anime ID to their associated other cross references.</returns>
        public ILookup<int, SVR_CrossRef_AniDB_Provider> GetByAnimeIDsAndType(IReadOnlyCollection<int> animeIds,
            params CrossRefType[] xrefTypes)
        {
            if (xrefTypes == null || xrefTypes.Length == 0 || animeIds?.Count == 0)
            {
                return EmptyLookup<int, SVR_CrossRef_AniDB_Provider>.Instance;
            }

            using (RepoLock.ReaderLock())
            {

                if (IsCached)
                    return GetAll()
                    .Where(a => xrefTypes.Any(s => s == a.CrossRefType))
                    .Where(a => animeIds?.Contains(a.AnimeID) != false)
                    .ToLookup(s => s.AnimeID);

                return Table
                    .Where(a => xrefTypes.Any(s => s == a.CrossRefType))
                    .Where(a => animeIds == null || animeIds.Contains(a.AnimeID))
                    .ToLookup(s => s.AnimeID);
            }
        }

        public List<SVR_CrossRef_AniDB_Provider> GetByType(CrossRefType xrefType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CrossTypes.GetMultiple(xrefType);
                return Table.Where(a => a.CrossRefType == xrefType).ToList();
            }
        }

        public List<SVR_CrossRef_AniDB_Provider> GetByAnimeID(int animeID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID);
                return Table.Where(a => a.AnimeID == animeID).ToList();
            }
        }
        public List<SVR_CrossRef_AniDB_Provider> GetByProvider(CrossRefType type, string providerid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CrossRefs.GetMultiple(type, providerid);
                return Table.Where(a => a.CrossRefType==type && a.CrossRefID==providerid).ToList();
            }
        }
        public List<SVR_CrossRef_AniDB_Provider> GetByAnimeIdAndProvider(CrossRefType type, int animeid, string providerid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return CrossRefs.GetMultiple(type, providerid).Where(a=>a.AnimeID==animeid).ToList();
                return Table.Where(a => a.CrossRefType == type && a.CrossRefID == providerid && a.AnimeID==animeid).ToList();
            }
        }
        public List<SVR_AnimeSeries> GetSeriesWithoutLinks(CrossRefType type)
        {
            return Repo.Instance.AnimeSeries.GetAll().Where(a =>
            {
                var anime = a.GetAnime();
                if (anime == null) return false;
                if (anime.Restricted > 0) return false;
                return !GetByAnimeIDAndType(a.AniDB_ID,type).Any();
            }).ToList();
        }
        

        public List<CrossRef_AniDB_TvDBV2> GetTvDBV2LinksFromAnime(int animeID)
        {
            List<(AniDB_Episode AniDB, TvDB_Episode TvDB)> eplinks = Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIDAndType(animeID, CrossRefType.TvDB).
                SelectMany(a => a.GetEpisodesWithOverrides()).
                Select(a => (AniDB: Repo.Instance.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID),
                    TvDB: Repo.Instance.TvDB_Episode.GetByID(int.Parse(a.ProviderEpisodeID)))).
                Where(a => a.AniDB != null && a.TvDB != null).
                OrderBy(a => a.AniDB.EpisodeType).
                ThenBy(a => a.AniDB.EpisodeNumber).ToList();

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
        public List<CrossRef_AniDB_TraktV2> GetTraktV2LinksFromAnime(int animeID)
        {
            List<(AniDB_Episode AniDB, Trakt_Episode Trakt)> eplinks = Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIDAndType(animeID, CrossRefType.TraktTV).
                SelectMany(a => a.GetEpisodesWithOverrides()).
                Select(a => (AniDB: Repo.Instance.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID),
                    Trakt: Repo.Instance.Trakt_Episode.GetByReference(a.ProviderEpisodeID))).
                Where(a => a.AniDB != null && a.Trakt != null).
                OrderBy(a => a.AniDB.EpisodeType).
                ThenBy(a => a.AniDB.EpisodeNumber).ToList();

            List<(int EpisodeType, int EpisodeNumber, int TraktSeries, int TraktSeason, int TraktNumber)> output =
                new List<(int EpisodeType, int EpisodeNumber, int TraktSeries, int TraktSeason, int TraktNumber)>();

            for (int i = 0; i < eplinks.Count; i++)
            {
                // Cases:
                // - first ep
                // - new type/season
                // - the next episode is not a simple increment

                var b = eplinks[i];

                if (i == 0)
                {
                    if (b.AniDB == null || b.Trakt == null) return new List<CrossRef_AniDB_TraktV2>();
                    output.Add((b.AniDB.EpisodeType, b.AniDB.EpisodeNumber, b.Trakt.Trakt_ShowID, b.Trakt.Season,
                        b.Trakt.EpisodeNumber));
                    continue;
                }

                var a = eplinks[i - 1];

                if (a.AniDB.EpisodeType != b.AniDB.EpisodeType || b.Trakt.Season != a.Trakt.Season)
                {
                    output.Add((b.AniDB.EpisodeType, b.AniDB.EpisodeNumber, b.Trakt.Trakt_ShowID, b.Trakt.Season,
                        b.Trakt.EpisodeNumber));
                    continue;
                }

                if (b.AniDB.EpisodeNumber - a.AniDB.EpisodeNumber != 1 ||
                    b.Trakt.EpisodeNumber - a.Trakt.EpisodeNumber != 1)
                {
                    output.Add((b.AniDB.EpisodeType, b.AniDB.EpisodeNumber, b.Trakt.Trakt_ShowID, b.Trakt.Season,
                        b.Trakt.EpisodeNumber));
                }
            }

            return output.Select(a => new CrossRef_AniDB_TraktV2
            {
                AnimeID = animeID,
                AniDBStartEpisodeType = a.EpisodeType,
                AniDBStartEpisodeNumber = a.EpisodeNumber,
                TraktID = a.TraktSeries.ToString(),
                TraktSeasonNumber = a.TraktSeason,
                TraktStartEpisodeNumber = a.TraktNumber,
                TraktTitle = Repo.Instance.Trakt_Show.GetByTraktSlug(a.TraktSeries.ToString())?.Title
            }).ToList();
        }
    }
}