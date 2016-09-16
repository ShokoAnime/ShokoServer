using System;
using System.Collections.Generic;
using JMMContracts.PlexAndKodi;
using JMMServer.LZ4;
using JMMServer.PlexAndKodi;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.NHibernate;
using NLog;

namespace JMMServer.Entities
{
    public class AnimeGroup_User
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        #region DB Columns

        public int AnimeGroup_UserID { get; private set; }
        public int JMMUserID { get; set; }
        public int AnimeGroupID { get; set; }
        public int IsFave { get; set; }
        public int UnwatchedEpisodeCount { get; set; }
        public int WatchedEpisodeCount { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }

        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

        #endregion

        public const int PLEXCONTRACT_VERSION = 5;


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

        public AnimeGroup_User()
        {
        }

        public AnimeGroup_User(int userID, int groupID)
        {
            JMMUserID = userID;
            AnimeGroupID = groupID;
            IsFave = 0;
            UnwatchedEpisodeCount = 0;
            WatchedEpisodeCount = 0;
            WatchedDate = null;
            PlayedCount = 0;
            WatchedCount = 0;
            StoppedCount = 0;
        }

        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(AnimeGroup_User oldcontract,
            AnimeGroup_User newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();

            if (oldcontract == null ||
                oldcontract.UnwatchedEpisodeCount > 0 != newcontract.UnwatchedEpisodeCount > 0)
                h.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
            if (oldcontract == null || oldcontract.IsFave != newcontract.IsFave)
                h.Add(GroupFilterConditionType.Favourite);
            if (oldcontract == null || oldcontract.WatchedDate != newcontract.WatchedDate)
                h.Add(GroupFilterConditionType.EpisodeWatchedDate);
            if (oldcontract == null || oldcontract.WatchedEpisodeCount > 0 != newcontract.WatchedEpisodeCount > 0)
                h.Add(GroupFilterConditionType.HasWatchedEpisodes);
            return h;
        }


        public void UpdateGroupFilter(HashSet<GroupFilterConditionType> types)
        {
            AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
            JMMUser usr = RepoFactory.JMMUser.GetByID(JMMUserID);
            if (grp != null && usr != null)
                grp.UpdateGroupFilters(types, usr);
        }

        public void DeleteFromFilters()
        {
            foreach (GroupFilter gf in RepoFactory.GroupFilter.GetAll())
            {
                bool change = false;
                if (gf.GroupsIds.ContainsKey(JMMUserID))
                {
                    if (gf.GroupsIds[JMMUserID].Contains(AnimeGroupID))
                    {
                        gf.GroupsIds[JMMUserID].Remove(AnimeGroupID);
                        change = true;
                    }
                }
                if (change)
                    RepoFactory.GroupFilter.Save(gf);
            }
        }

        public void UpdatePlexKodiContracts()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();
                AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
                if (grp == null)
                    return;
                List<AnimeSeries> series = grp.GetAllSeries(sessionWrapper);
                PlexContract = Helper.GenerateFromAnimeGroup(sessionWrapper, grp, JMMUserID, series);
            }
        }

        public bool HasUnwatchedFiles => UnwatchedEpisodeCount > 0;

        public bool AllFilesWatched => UnwatchedEpisodeCount == 0;

        public bool AnyFilesWatched => WatchedEpisodeCount > 0;
    }
}