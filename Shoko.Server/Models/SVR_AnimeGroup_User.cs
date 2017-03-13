using System.Collections.Generic;
using NLog;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.LZ4;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Models
{
    public class SVR_AnimeGroup_User : AnimeGroup_User
    {
        public SVR_AnimeGroup_User()
        {
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        #region DB Columns

        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

        #endregion

        public const int PLEXCONTRACT_VERSION = 6;


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


        public SVR_AnimeGroup_User(int userID, int groupID)
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

        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(SVR_AnimeGroup_User oldcontract,
            SVR_AnimeGroup_User newcontract)
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
            SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
            SVR_JMMUser usr = RepoFactory.JMMUser.GetByID(JMMUserID);
            if (grp != null && usr != null)
                grp.UpdateGroupFilters(types, usr);
        }

        public void DeleteFromFilters()
        {
            foreach (SVR_GroupFilter gf in RepoFactory.GroupFilter.GetAll())
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

        public void UpdatePlexKodiContracts(ISessionWrapper session = null)
        {
            SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
            if (grp == null)
                return;
            List<SVR_AnimeSeries> series = grp.GetAllSeries();
            PlexContract = Helper.GenerateFromAnimeGroup(grp, JMMUserID, series, session);
        }
    }
}