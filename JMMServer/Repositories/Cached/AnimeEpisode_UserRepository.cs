using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JMMContracts;
using JMMServer.Entities;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class AnimeEpisode_UserRepository : BaseCachedRepository<AnimeEpisode_User, int>
    {
        private PocoIndex<int, AnimeEpisode_User, int> Series;
        private PocoIndex<int, AnimeEpisode_User, int, int> UsersEpisodes;
        private PocoIndex<int, AnimeEpisode_User, int> Users;
        private PocoIndex<int, AnimeEpisode_User, int> Episodes;
        private PocoIndex<int, AnimeEpisode_User, int, int> UsersSeries;


        public override void PopulateIndexes()
        {
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersEpisodes = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeEpisodeID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Episodes = Cache.CreateIndex(a => a.AnimeEpisodeID);
            UsersSeries = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeSeriesID);
        }

        public override void RegenerateDb()
        {
            int cnt = 0;
            List<AnimeEpisode_User> sers =
                Cache.Values.Where(a => a.ContractVersion < AnimeEpisode_User.CONTRACT_VERSION || a.AnimeEpisode_UserID == 0).ToList();
            int max = sers.Count;
            foreach (AnimeEpisode_User g in sers)
            {
                Save(g);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeEpisode_User).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeEpisode_User).Name,
                " DbRegen - " + max + "/" + max);
        }



        public override void Save(List<AnimeEpisode_User> objs)
        {
            foreach(AnimeEpisode_User e in objs)
                Save(e);
        }

        public override void Save(AnimeEpisode_User obj)
        {
            lock (obj)
            {
                if (obj.AnimeEpisode_UserID == 0)
                {
                    base.Save(obj);

                }
                UpdateContract(obj);
                base.Save(obj);
            }
        }


        public List<AnimeEpisode_User> GetBySeriesID(int seriesid)
        {
            return Series.GetMultiple(seriesid);
        }

        public AnimeEpisode_User GetByUserIDAndEpisodeID(int userid, int epid)
        {
            return UsersEpisodes.GetOne(userid, epid);
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




        public void UpdateContract(AnimeEpisode_User aeu)
        {
            Contract_AnimeEpisode caep = aeu.Contract ?? new Contract_AnimeEpisode();
            AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(aeu.AnimeEpisodeID);
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

    }
}