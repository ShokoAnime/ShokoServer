using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDB_EpisodeRepository : BaseCachedRepository<CrossRef_AniDB_Episode, int>
    {
        private PocoIndex<int, CrossRef_AniDB_Episode, int> AllAnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_Episode, int, string> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_Episode, int, string> EpisodeIDs;
            
        public override void PopulateIndexes()
        {
            AllAnimeIDs = new PocoIndex<int, CrossRef_AniDB_Episode, int>(Cache,
                a => RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1);
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_Episode, int, string>(Cache,
                a => RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1,a=>a.Provider);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_Episode, int, string>(Cache, a => a.AniDBEpisodeID, a=>a.Provider);
        }

        public CrossRef_AniDB_Episode GetByAniDBAndProviderEpisodeIDs(int anidbID, string providerEpisodeID, string provider)
        {
            lock (Cache)
            {
                return EpisodeIDs.GetMultiple(anidbID, provider).FirstOrDefault(a => a.ProviderEpisodeID == providerEpisodeID);
            }
        }

        public List<CrossRef_AniDB_Episode> GetByAniDBEpisodeID(int id, string provider)
        {
            lock (Cache)
            {
                return EpisodeIDs.GetMultiple(id, provider);
            }
        }

        public List<CrossRef_AniDB_Episode> GetByAnimeID(int id, string provider)
        {
            lock (Cache)
            {
                return AnimeIDs.GetMultiple(id, provider);
            }
        }
        public List<CrossRef_AniDB_Episode> GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return AllAnimeIDs.GetMultiple(id);
            }
        }
        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB_Episode entity)
        {
            return entity.CrossRef_AniDB_EpisodeID;
        }

        public void DeleteAllUnverifiedLinksForAnime(int AnimeID, string provider=null)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        List<CrossRef_AniDB_Episode> toRemove;
                        if (string.IsNullOrEmpty(provider))
                            toRemove = GetByAnimeID(AnimeID).Where(a => a.MatchRating != MatchRating.UserVerified).ToList();
                        else
                            toRemove = GetByAnimeID(AnimeID, provider).Where(a => a.MatchRating != MatchRating.UserVerified).ToList();
                        if (toRemove.Count <= 0) return;
                        foreach (var episode in toRemove) Cache.Remove(episode);

                        using (var transaction = session.BeginTransaction())
                        {
                            // I'm aware that this is stupid, but it's MySQL's fault
                            // see https://stackoverflow.com/questions/45494/mysql-error-1093-cant-specify-target-table-for-update-in-from-clause
                            try
                            {
                                session.CreateSQLQuery(@"SET optimizer_switch = 'derived_merge=off';").ExecuteUpdate();
                            }
                            catch
                            {
                                // ignore
                            }

                            if (string.IsNullOrEmpty(provider))
                            {
                                session.CreateSQLQuery(
                                    @"DELETE FROM CrossRef_AniDB_Episode
WHERE CrossRef_AniDB_Episode.MatchRating != :rating AND CrossRef_AniDB_Episode.CrossRef_AniDB_EpisodeID IN (
  SELECT CrossRef_AniDB_EpisodeID FROM (
    SELECT CrossRef_AniDB_EpisodeID
    FROM CrossRef_AniDB_Episode
    INNER JOIN AniDB_Episode ON AniDB_Episode.EpisodeID = CrossRef_AniDB_Episode.AniDBEpisodeID
    WHERE AniDB_Episode.AnimeID = :animeid
  ) x
);"
                                ).SetInt32("animeid", AnimeID).SetInt32("rating", (int)MatchRating.UserVerified).ExecuteUpdate();
                            }
                            else
                            {
                                session.CreateSQLQuery(
                                    @"DELETE FROM CrossRef_AniDB_Episode
WHERE CrossRef_AniDB_Episode.MatchRating != :rating AND CrossRef_AniDB_Episode.CrossRef_AniDB_EpisodeID IN (
  SELECT CrossRef_AniDB_EpisodeID FROM (
    SELECT CrossRef_AniDB_EpisodeID
    FROM CrossRef_AniDB_Episode
    INNER JOIN AniDB_Episode ON AniDB_Episode.EpisodeID = CrossRef_AniDB_Episode.AniDBEpisodeID
    WHERE AniDB_Episode.AnimeID = :animeid &&  CrossRef_AniDB_Episode.Provider = :provider
  ) x
);"
                                ).SetInt32("animeid", AnimeID).SetString("provider", provider).SetInt32("rating", (int)MatchRating.UserVerified).ExecuteUpdate();
                            }

                            try
                            {
                                session.CreateSQLQuery(@"SET optimizer_switch = 'derived_merge=on';").ExecuteUpdate();
                            }
                            catch
                            {
                                // ignore
                            }

                            transaction.Commit();
                        }
                    }
                }
            }
        }

        public void DeleteAllUnverifiedLinks(string provider=null)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        using (var transaction = session.BeginTransaction())
                        {
                            List<CrossRef_AniDB_Episode> toRemove;
                            if (string.IsNullOrEmpty(provider))
                                toRemove = GetAll().Where(a => a.MatchRating != MatchRating.UserVerified)
                                    .ToList();
                            else
                                toRemove = GetAll().Where(a => a.MatchRating != MatchRating.UserVerified && a.Provider==provider)
                                    .ToList();

                            foreach (var episode in toRemove) Cache.Remove(episode);
                            if (string.IsNullOrEmpty(provider))
                            {
                                session.CreateSQLQuery("DELETE FROM CrossRef_AniDB_Episode WHERE MatchRating != :rating;").SetInt32("rating", (int)MatchRating.UserVerified).ExecuteUpdate();
                            }
                            else
                            {
                                session.CreateSQLQuery("DELETE FROM CrossRef_AniDB_Episode WHERE MatchRating != :rating AND Provider= :provider;").SetInt32("rating", (int)MatchRating.UserVerified).SetString("provider", provider).ExecuteUpdate();
                            }
                            transaction.Commit();
                        }
                    }
                }
            }
        }
    }
}
