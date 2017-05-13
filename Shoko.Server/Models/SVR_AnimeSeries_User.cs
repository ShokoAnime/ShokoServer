using System.Collections.Generic;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_AnimeSeries_User : AnimeSeries_User
    {
        public SVR_AnimeSeries_User()
        {
        }

        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

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
                PlexContractBlob = CompressionHelper.SerializeObject(value, out int outsize, true);
                PlexContractSize = outsize;
                PlexContractVersion = PLEXCONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _plexcontract = null;
        }

        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(SVR_AnimeSeries_User oldcontract,
            SVR_AnimeSeries_User newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();

            if (oldcontract == null ||
                oldcontract.UnwatchedEpisodeCount > 0 != newcontract.UnwatchedEpisodeCount > 0)
                h.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
            if (oldcontract == null || oldcontract.WatchedDate != newcontract.WatchedDate)
                h.Add(GroupFilterConditionType.EpisodeWatchedDate);
            if (oldcontract == null || oldcontract.WatchedEpisodeCount > 0 != newcontract.WatchedEpisodeCount > 0)
                h.Add(GroupFilterConditionType.HasWatchedEpisodes);
            return h;
        }

        public void UpdateGroupFilter(HashSet<GroupFilterConditionType> types)
        {
            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
            SVR_JMMUser usr = RepoFactory.JMMUser.GetByID(JMMUserID);
            if (ser != null && usr != null)
                ser.UpdateGroupFilters(types, usr);
        }

        public void DeleteFromFilters()
        {
            foreach (SVR_GroupFilter gf in RepoFactory.GroupFilter.GetAll())
            {
                bool change = false;
                if (gf.SeriesIds.ContainsKey(JMMUserID))
                {
                    if (gf.SeriesIds[JMMUserID].Contains(AnimeSeriesID))
                    {
                        gf.SeriesIds[JMMUserID].Remove(AnimeSeriesID);
                        change = true;
                    }
                }
                if (change)
                    RepoFactory.GroupFilter.Save(gf);
            }
        }


        public SVR_AnimeSeries_User(int userID, int seriesID)
        {
            JMMUserID = userID;
            AnimeSeriesID = seriesID;
            UnwatchedEpisodeCount = 0;
            WatchedEpisodeCount = 0;
            WatchedDate = null;
            PlayedCount = 0;
            WatchedCount = 0;
            StoppedCount = 0;
        }
    }
}