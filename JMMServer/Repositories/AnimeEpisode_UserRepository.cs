using System.Collections.Generic;
using System.Linq;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AnimeEpisode_UserRepository
    {
        private static PocoCache<int, AnimeEpisode_User> Cache;
        private static PocoIndex<int, AnimeEpisode_User, int> Series;
        private static PocoIndex<int, AnimeEpisode_User, int, int> UsersEpisodes;
        private static PocoIndex<int, AnimeEpisode_User, int> Users;
        private static PocoIndex<int, AnimeEpisode_User, int> Episodes;
        private static PocoIndex<int, AnimeEpisode_User, int, int> UsersSeries;


        public static void InitCache()
        {
            string t = "AnimeEpisodes_User";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AnimeEpisode_UserRepository repo = new AnimeEpisode_UserRepository();
            Cache = new PocoCache<int, AnimeEpisode_User>(repo.InternalGetAll(), a => a.AnimeEpisode_UserID);
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersEpisodes = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeEpisodeID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Episodes = Cache.CreateIndex(a => a.AnimeEpisodeID);
            UsersSeries = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeSeriesID);
            int cnt = 0;
            List<AnimeEpisode_User> sers =
                Cache.Values.Where(a => a.ContractVersion < AnimeEpisode_User.CONTRACT_VERSION).ToList();
            int max = sers.Count;
            foreach (AnimeEpisode_User g in sers)
            {
                repo.Save(g);
                cnt++;
                if (cnt%10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                " DbRegen - " + max + "/" + max);
        }

        private List<AnimeEpisode_User> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var grps = session
                    .CreateCriteria(typeof(AnimeEpisode_User))
                    .List<AnimeEpisode_User>();

                return new List<AnimeEpisode_User>(grps);
            }
        }

        public void Save(AnimeEpisode_User obj)
        {
            lock (obj)
            {
                if (obj.AnimeEpisode_UserID == 0)
                {
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        using (var transaction = session.BeginTransaction())
                        {
                            session.SaveOrUpdate(obj);
                            transaction.Commit();
                        }
                    }

                }
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    UpdateContract(session, obj);
                    // populate the database
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                }
                Cache.Update(obj);
            }
        }

        public AnimeEpisode_User GetByID(int id)
        {
            return Cache.Get(id);
        }

        public List<AnimeEpisode_User> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<AnimeEpisode_User> GetBySeriesID(int seriesid)
        {
            return Series.GetMultiple(seriesid);
        }

        public AnimeEpisode_User GetByUserIDAndEpisodeID(int userid, int epid)
        {
            return UsersEpisodes.GetOne(userid, epid);
        }

        public AnimeEpisode_User GetByUserIDAndEpisodeID(ISession session, int userid, int epid)
        {
            return GetByUserIDAndEpisodeID(userid, epid);
        }

        public List<AnimeEpisode_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }

        public List<AnimeEpisode_User> GetMostRecentlyWatched(int userid, int maxresults = 100)
        {
            return
                GetByUserID(userid)
                    .Where(a => a.WatchedCount > 0)
                    .OrderByDescending(a => a.WatchedDate)
                    .Take(maxresults)
                    .ToList();
        }

        public List<AnimeEpisode_User> GetMostRecentlyWatched(ISession session, int userid, int maxresults = 100)
        {
            return GetMostRecentlyWatched(userid, maxresults);
        }

        public AnimeEpisode_User GetLastWatchedEpisode()
        {
            return Cache.Values.Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate).FirstOrDefault();
        }

        public AnimeEpisode_User GetLastWatchedEpisodeForSeries(int seriesid, int userid)
        {
            return
                UsersSeries.GetMultiple(userid, seriesid)
                    .Where(a => a.WatchedCount > 0)
                    .OrderByDescending(a => a.WatchedDate)
                    .FirstOrDefault();
        }

        public List<AnimeEpisode_User> GetByEpisodeID(int epid)
        {
            return Episodes.GetMultiple(epid);
        }

        public List<AnimeEpisode_User> GetByUserIDAndSeriesID(int userid, int seriesid)
        {
            return UsersSeries.GetMultiple(userid, seriesid);
        }

        public List<AnimeEpisode_User> GetByUserIDAndSeriesID(ISession session, int userid, int seriesid)
        {
            return GetByUserIDAndSeriesID(userid, seriesid);
        }


        public void UpdateContract(ISession session, AnimeEpisode_User aeu)
        {
            AnimeEpisodeRepository aerepo = new AnimeEpisodeRepository();
            Contract_AnimeEpisode caep = aeu.Contract ?? new Contract_AnimeEpisode();
            AnimeEpisode ep = aerepo.GetByID(aeu.AnimeEpisodeID);
            if (ep == null)
                return;
            AniDB_Episode aniEp = ep.AniDB_Episode;
            caep.AniDB_EpisodeID = ep.AniDB_EpisodeID;
            caep.AnimeEpisodeID = ep.AnimeEpisodeID;
            caep.AnimeSeriesID = ep.AnimeSeriesID;
            caep.DateTimeUpdated = ep.DateTimeUpdated;
            caep.IsWatched = aeu.WatchedCount > 0 ? 1 : 0;
            caep.PlayedCount = aeu.PlayedCount;
            caep.StoppedCount = aeu.StoppedCount;
            caep.WatchedCount = aeu.WatchedCount;
            caep.WatchedDate = aeu.WatchedDate;
            if (aniEp != null)
            {
                caep.AniDB_AirDate = aniEp.AirDateAsDate;
                caep.AniDB_EnglishName = aniEp.EnglishName;
                caep.AniDB_LengthSeconds = aniEp.LengthSeconds;
                caep.AniDB_Rating = aniEp.Rating;
                caep.AniDB_RomajiName = aniEp.RomajiName;
                caep.AniDB_Votes = aniEp.Votes;

                caep.EpisodeNumber = aniEp.EpisodeNumber;
                caep.EpisodeNameRomaji = aniEp.RomajiName;
                caep.EpisodeNameEnglish = aniEp.EnglishName;
                caep.EpisodeType = aniEp.EpisodeType;
            }


            //TODO if this is needed, calculating it in here will not affect performance
            caep.ReleaseGroups = new List<Contract_AniDBReleaseGroup>();

            aeu.Contract = caep;
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AnimeEpisode_User cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}