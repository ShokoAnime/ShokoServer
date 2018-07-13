using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeEpisode_UserRepository : BaseRepository<SVR_AnimeEpisode_User, int>
    {
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Series;
        private PocoIndex<int, SVR_AnimeEpisode_User, int, int> UsersEpisodes;
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Users;
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Episodes;
        private PocoIndex<int, SVR_AnimeEpisode_User, int, int> UsersSeries;


        internal override object BeginSave(SVR_AnimeEpisode_User entity, SVR_AnimeEpisode_User original_entity, object parameters)
        {
            UpdateContract(entity);
            return null;
        }

        internal override int SelectKey(SVR_AnimeEpisode_User entity) => entity.AnimeEpisode_UserID;



        internal override void PopulateIndexes()
        {
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersEpisodes = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeEpisodeID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Episodes = Cache.CreateIndex(a => a.AnimeEpisodeID);
            UsersSeries = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeSeriesID);
        }

        internal override void ClearIndexes()
        {
            Series = null;
            UsersEpisodes = null;
            Users = null;
            Episodes = null;
            UsersSeries = null;
        }

        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            List<SVR_AnimeEpisode_User> sers = Where(a => a.ContractVersion < SVR_AnimeEpisode_User.CONTRACT_VERSION || a.AnimeEpisode_UserID == 0).ToList();
            if (sers.Count == 0)
                return;
            InitProgress regen = new InitProgress();
            regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, typeof(AnimeEpisode_User).Name, " Regen");
            regen.Step = 0;
            regen.Total = sers.Count;
            BatchAction(sers, batchSize, (ser, original) =>
            {
                //Empty change (update contract is called on save);
                regen.Step++;
                progress.Report(regen);
            });
            regen.Step = regen.Total;
            progress.Report(regen);
        }

        public List<SVR_AnimeEpisode_User> GetBySeriesID(int seriesid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Series.GetMultiple(seriesid);
                return Table.Where(a => a.AnimeSeriesID == seriesid).ToList();
            }
        }

        public SVR_AnimeEpisode_User GetByUserIDAndEpisodeID(int userid, int epid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UsersEpisodes.GetOne(userid, epid);
                return Table.FirstOrDefault(a => a.JMMUserID == userid && a.AnimeEpisodeID == epid);
            }
        }


        public List<SVR_AnimeEpisode_User> GetByUserID(int userid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Users.GetMultiple(userid);
                return Table.Where(a => a.JMMUserID==userid).ToList();
            }
        }

        public List<SVR_AnimeEpisode_User> GetMostRecentlyWatched(int userid, int maxresults = 100)
        {
            return GetByUserID(userid).Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate)
                .Take(maxresults).ToList();
        }


        public SVR_AnimeEpisode_User GetLastWatchedEpisode()
        {
            return Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate).FirstOrDefault();
        }

        public SVR_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int seriesid, int userid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UsersSeries.GetMultiple(userid, seriesid).Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate).FirstOrDefault();
                return Table.Where(a => a.JMMUserID == userid && a.AnimeSeriesID==seriesid).Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate).FirstOrDefault();
            }
        }

        public List<SVR_AnimeEpisode_User> GetByEpisodeID(int epid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Episodes.GetMultiple(epid);
                return Table.Where(a => a.AnimeEpisodeID==epid).ToList();
            }
        }

        public List<SVR_AnimeEpisode_User> GetByUserIDAndSeriesID(int userid, int seriesid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UsersSeries.GetMultiple(userid, seriesid);
                return Table.Where(a => a.JMMUserID==userid && a.AnimeSeriesID==seriesid).ToList();
            }
        }


        public void UpdateContract(SVR_AnimeEpisode_User aeu)
        {
            CL_AnimeEpisode_User caep = aeu.Contract ?? new CL_AnimeEpisode_User();
            SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(aeu.AnimeEpisodeID);
            if (ep == null)
                return;
            AniDB_Episode aniEp = ep.AniDB_Episode;
            caep.AniDB_EpisodeID = ep.AniDB_EpisodeID;
            caep.AnimeEpisodeID = ep.AnimeEpisodeID;
            caep.AnimeSeriesID = ep.AnimeSeriesID;
            caep.DateTimeUpdated = ep.DateTimeUpdated;
            caep.PlayedCount = aeu.PlayedCount;
            caep.StoppedCount = aeu.StoppedCount;
            caep.WatchedCount = aeu.WatchedCount;
            caep.WatchedDate = aeu.WatchedDate;
            var englishTitle = Repo.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, "EN")
                .FirstOrDefault()?.Title;
            var romajiTitle = Repo.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, "X-JAT")
                .FirstOrDefault()?.Title;
            caep.AniDB_EnglishName = englishTitle;
            caep.AniDB_RomajiName = romajiTitle;
            caep.EpisodeNameEnglish = englishTitle;
            caep.EpisodeNameRomaji = romajiTitle;
            if (aniEp != null)
            {
                caep.AniDB_AirDate = aniEp.GetAirDateAsDate();
                caep.AniDB_LengthSeconds = aniEp.LengthSeconds;
                caep.AniDB_Rating = aniEp.Rating;
                caep.AniDB_Votes = aniEp.Votes;

                caep.EpisodeNumber = aniEp.EpisodeNumber;
                caep.Description = aniEp.Description;
                caep.EpisodeType = aniEp.EpisodeType;
            }

            /*
            //TODO if this is needed, calculating it in here will not affect performance
            caep.ReleaseGroups = new List<CL_AniDB_GroupStatus>();
            */
            aeu.Contract = caep;
        }
    }
}