using System;
using System.Collections.Generic;
using JMMContracts.PlexAndKodi;
using JMMServer.LZ4;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
    public class AnimeSeries_User
    {
        public int AnimeSeries_UserID { get; private set; }
        public int JMMUserID { get; set; }
        public int AnimeSeriesID { get; set; }

        public int UnwatchedEpisodeCount { get; set; }
        public int WatchedEpisodeCount { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int PlayedCount { get; set; }
        public int WatchedCount { get; set; }
        public int StoppedCount { get; set; }


        public int PlexContractVersion { get; set; }
        public byte[] PlexContractBlob { get; set; }
        public int PlexContractSize { get; set; }

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

        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(AnimeSeries_User oldcontract,
            AnimeSeries_User newcontract)
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
            AnimeSeriesRepository repo = new AnimeSeriesRepository();
            JMMUserRepository repouser = new JMMUserRepository();
            AnimeSeries ser = repo.GetByID(AnimeSeriesID);
            JMMUser usr = repouser.GetByID(JMMUserID);
            if (ser != null && usr != null)
                ser.UpdateGroupFilters(types, usr);
        }

        public void DeleteFromFilters()
        {
            GroupFilterRepository repo = new GroupFilterRepository();
            foreach (GroupFilter gf in repo.GetAll())
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
                    repo.Save(gf);
            }
        }

        public AnimeSeries_User()
        {
        }

        public AnimeSeries_User(int userID, int seriesID)
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