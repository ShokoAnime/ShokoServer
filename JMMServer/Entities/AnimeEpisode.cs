using System;
using System.Collections.Generic;
using AniDBAPI;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.LZ4;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using Stream = JMMContracts.PlexAndKodi.Stream;

namespace JMMServer.Entities
{
    public class AnimeEpisode
    {
        public int AnimeEpisodeID { get; private set; }
        public int AnimeSeriesID { get; set; }
        public int AniDB_EpisodeID { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }


        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

        public const int PLEXCONTRACT_VERSION = 4;


        private Video _plexcontract = null;


        public virtual Video PlexContract
        {
            get
            {
                if ((_plexcontract == null) && (PlexContractBlob != null) && (PlexContractBlob.Length > 0) &&
                    (PlexContractSize > 0))
                    _plexcontract = CompressionHelper.DeserializeObject<Video>(PlexContractBlob, PlexContractSize);
                return _plexcontract;
            }
            set
            {
                _plexcontract = value;
                int outsize;
                PlexContractBlob = CompressionHelper.SerializeObject(value, out outsize, true);
                PlexContractSize = outsize;
                PlexContractVersion = PLEXCONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _plexcontract = null;
        }


        public enEpisodeType EpisodeTypeEnum
        {
            get { return (enEpisodeType) AniDB_Episode.EpisodeType; }
        }

        public AniDB_Episode AniDB_Episode
        {
            get
            {
                AniDB_EpisodeRepository repEps = new AniDB_EpisodeRepository();
                return repEps.GetByEpisodeID(this.AniDB_EpisodeID);
            }
        }

        public void Populate(AniDB_Episode anidbEp)
        {
            this.AniDB_EpisodeID = anidbEp.EpisodeID;
            this.DateTimeUpdated = DateTime.Now;
            this.DateTimeCreated = DateTime.Now;
        }

        public AnimeEpisode_User GetUserRecord(int userID)
        {
            AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
            return repEpUser.GetByUserIDAndEpisodeID(userID, this.AnimeEpisodeID);
        }

        public AnimeEpisode_User GetUserRecord(ISession session, int userID)
        {
            AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
            return repEpUser.GetByUserIDAndEpisodeID(userID, this.AnimeEpisodeID);
        }


        /// <summary>
        /// Gets the AnimeSeries this episode belongs to
        /// </summary>
        public AnimeSeries GetAnimeSeries()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAnimeSeries(session.Wrap());
            }
        }

        public AnimeSeries GetAnimeSeries(ISessionWrapper session)
        {
            AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
            return repSeries.GetByID(this.AnimeSeriesID);
        }

        public List<VideoLocal> GetVideoLocals()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetVideoLocals(session);
            }
        }

        public List<VideoLocal> GetVideoLocals(ISession session)
        {
            VideoLocalRepository repVidLocals = new VideoLocalRepository();
            return repVidLocals.GetByAniDBEpisodeID(AniDB_EpisodeID);
        }

        public List<CrossRef_File_Episode> FileCrossRefs
        {
            get
            {
                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();
                return repCrossRefs.GetByEpisodeID(AniDB_EpisodeID);
            }
        }

        public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
        {
            AnimeEpisode_UserRepository repEpisodeUsers = new AnimeEpisode_UserRepository();
            AnimeEpisode_User epUserRecord = this.GetUserRecord(userID);

            if (watched)
            {
                // lets check if an update is actually required
                if (epUserRecord != null)
                {
                    if (epUserRecord.WatchedDate.HasValue && watchedDate.HasValue &&
                        epUserRecord.WatchedDate.Value.Equals(watchedDate.Value))
                    {
                        // this will happen when we are adding a new file for an episode where we already had another file
                        // and the file/episode was watched already
                        return;
                    }
                }

                if (epUserRecord == null)
                {
                    epUserRecord = new AnimeEpisode_User();
                    epUserRecord.PlayedCount = 0;
                    epUserRecord.StoppedCount = 0;
                    epUserRecord.WatchedCount = 0;
                }
                epUserRecord.AnimeEpisodeID = this.AnimeEpisodeID;
                epUserRecord.AnimeSeriesID = this.AnimeSeriesID;
                epUserRecord.JMMUserID = userID;
                epUserRecord.WatchedCount++;

                if (watchedDate.HasValue)
                {
                    if (updateWatchedDate)
                        epUserRecord.WatchedDate = watchedDate.Value;
                }

                if (!epUserRecord.WatchedDate.HasValue) epUserRecord.WatchedDate = DateTime.Now;

                repEpisodeUsers.Save(epUserRecord);
            }
            else
            {
                if (epUserRecord != null)
                    repEpisodeUsers.Delete(epUserRecord.AnimeEpisode_UserID);
            }
        }


        public List<Contract_VideoDetailed> GetVideoDetailedContracts(int userID)
        {
            VideoLocalRepository repVids = new VideoLocalRepository();
            List<Contract_VideoDetailed> contracts = new List<Contract_VideoDetailed>();

            // get all the cross refs
            foreach (CrossRef_File_Episode xref in FileCrossRefs)
            {
                VideoLocal v=repVids.GetByHash(xref.Hash);
                if (v != null)
                    contracts.Add(v.ToContractDetailed(userID));
            }


            return contracts;
        }

        private static object _lock = new object();

        public Contract_AnimeEpisode GetUserContract(int userid)
        {
            lock(_lock) //Make it atomic on creation
            { 
                AnimeEpisode_User rr = GetUserRecord(userid);
                if (rr != null)
                    return rr.Contract;
                rr = new AnimeEpisode_User();
                rr.PlayedCount = 0;
                rr.StoppedCount = 0;
                rr.WatchedCount = 0;
                rr.AnimeEpisodeID = this.AnimeEpisodeID;
                rr.AnimeSeriesID = this.AnimeSeriesID;
                rr.JMMUserID = userid;
                rr.WatchedDate = null;
                AnimeEpisode_UserRepository repo = new AnimeEpisode_UserRepository();
                repo.Save(rr);
                return rr.Contract;
            }
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID,
            bool syncTrakt)
        {
            ToggleWatchedStatus(watched, updateOnline, watchedDate, true, true, userID, syncTrakt);
        }

        public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
            bool updateStatsCache, int userID, bool syncTrakt)
        {
            foreach (VideoLocal vid in GetVideoLocals())
            {
                vid.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, updateStatsCache, userID,
                    syncTrakt, true);
            }
        }
    }
}