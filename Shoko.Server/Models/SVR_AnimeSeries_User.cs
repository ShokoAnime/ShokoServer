using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using System;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.Models
{
    public class SVR_AnimeSeries_User : AnimeSeries_User
    {
        private DateTime _lastPlexRegen = DateTime.MinValue;
        private Video _plexContract;

        public SVR_AnimeSeries_User()
        {
        }

        private DateTime _lastPlexRegen = DateTime.MinValue;
        private Video _plexContract = null;

        public virtual Video PlexContract
        {
            get
            {
                if (_plexContract == null || _lastPlexRegen.Add(TimeSpan.FromMinutes(10)) > DateTime.Now)
                {
                    _lastPlexRegen = DateTime.Now;
                    var series = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
                    return _plexContract = Helper.GenerateFromSeries(series.GetUserContract(JMMUserID), series,
                        series.GetAnime(), JMMUserID);
                }
                return _plexContract;
            }
            set
            {
                _plexContract = value;
                _lastPlexRegen = DateTime.Now;
            }
        }

        public void CollectContractMemory()
        {
            _plexContract = null;
        }

        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(SVR_AnimeSeries_User oldcontract, SVR_AnimeSeries_User newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();

            if (oldcontract == null || oldcontract.UnwatchedEpisodeCount > 0 != newcontract.UnwatchedEpisodeCount > 0)
                h.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
            if (oldcontract == null || oldcontract.WatchedDate != newcontract.WatchedDate)
                h.Add(GroupFilterConditionType.EpisodeWatchedDate);
            if (oldcontract == null || oldcontract.WatchedEpisodeCount > 0 != newcontract.WatchedEpisodeCount > 0)
                h.Add(GroupFilterConditionType.HasWatchedEpisodes);
            return h;
        }

        public void UpdateGroupFilter(HashSet<GroupFilterConditionType> types)
        {
            SVR_AnimeSeries ser = Repo.AnimeSeries.GetByID(AnimeSeriesID);
            SVR_JMMUser usr = Repo.JMMUser.GetByID(JMMUserID);
            if (ser != null && usr != null)
                ser.UpdateGroupFilters(types, usr);
        }

        public void DeleteFromFilters()
        {
            using (var upd = Repo.GroupFilter.BeginBatchUpdate(() => Repo.GroupFilter.GetAll()))
            {
                foreach (SVR_GroupFilter gf in upd)
                {
                    if (gf.SeriesIds.ContainsKey(JMMUserID))
                    {
                        if (gf.SeriesIds[JMMUserID].Contains(AnimeSeriesID))
                        {
                            gf.SeriesIds[JMMUserID].Remove(AnimeSeriesID);
                            upd.Update(gf);
                        }
                    }
                }
                upd.Commit();
            }

        }
    }
}