using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AnimeSeries_UserRepository : BaseRepository<SVR_AnimeSeries_User, int>
    {
        
        private PocoIndex<int, SVR_AnimeSeries_User, int> Users;
        private PocoIndex<int, SVR_AnimeSeries_User, int> Series;
        private PocoIndex<int, SVR_AnimeSeries_User, int, int> UsersSeries;
        private readonly Dictionary<int, ChangeTracker<int>> Changes = new Dictionary<int, ChangeTracker<int>>();


        internal override object BeginSave(SVR_AnimeSeries_User entity, SVR_AnimeSeries_User original_entity, object parameters)
        {
            UpdatePlexKodiContracts(entity);
            return SVR_AnimeSeries_User.GetConditionTypesChanged(original_entity, entity);
        }

        internal override void EndSave(SVR_AnimeSeries_User entity, SVR_AnimeSeries_User original_entity, object returnFromBeginSave,
            object parameters)
        {
            HashSet<GroupFilterConditionType> types = (HashSet<GroupFilterConditionType>)returnFromBeginSave;
            lock (Changes)
            {
                if (!Changes.ContainsKey(entity.JMMUserID))
                    Changes[entity.JMMUserID] = new ChangeTracker<int>();
                Changes[entity.JMMUserID].AddOrUpdate(entity.AnimeSeriesID);
            }
            entity.UpdateGroupFilter(types);
        }

        internal override void EndDelete(SVR_AnimeSeries_User entity, object returnFromBeginDelete, object parameters)
        {
            lock (Changes)
            {
                if (!Changes.ContainsKey(entity.JMMUserID))
                    Changes[entity.JMMUserID] = new ChangeTracker<int>();
                Changes[entity.JMMUserID].Remove(entity.AnimeSeriesID);
            }
            entity.DeleteFromFilters();
        }

        internal override int SelectKey(SVR_AnimeSeries_User entity)
        {
            return entity.AnimeSeries_UserID;
        }

        internal override void PopulateIndexes()
        {
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersSeries = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeSeriesID);
        }

        internal override void ClearIndexes()
        {
            Users = null;
            Series = null;
            UsersSeries = null;
        }



        private void UpdatePlexKodiContracts(SVR_AnimeSeries_User ugrp)
        {
            SVR_AnimeSeries ser = Repo.AnimeSeries.GetByID(ugrp.AnimeSeriesID);
            CL_AnimeSeries_User con = ser?.GetUserContract(ugrp.JMMUserID);
            if (con == null)
                return;
            ugrp.PlexContract = Helper.GenerateFromSeries(con, ser, ser.GetAnime(), ugrp.JMMUserID);
        }


        public SVR_AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return UsersSeries.GetOne(userid, seriesid);
                return Table.FirstOrDefault(a => a.JMMUserID == userid && a.AnimeSeriesID == seriesid);
            }
        }

        public List<SVR_AnimeSeries_User> GetByUserID(int userid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Users.GetMultiple(userid);
                return Table.Where(a => a.JMMUserID==userid).ToList();
            }
        }

        public List<SVR_AnimeSeries_User> GetBySeriesID(int seriesid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Series.GetMultiple(seriesid);
                return Table.Where(a => a.AnimeSeriesID==seriesid).ToList();
            }
        }


        public List<SVR_AnimeSeries_User> GetMostRecentlyWatched(int userID)
        {
            return
                GetByUserID(userID)
                    .Where(a => a.UnwatchedEpisodeCount > 0)
                    .OrderByDescending(a => a.WatchedDate)
                    .ToList();
        }


        public ChangeTracker<int> GetChangeTracker(int userid)
        {
            lock (Changes)
            {
                if (Changes.ContainsKey(userid))
                    return Changes[userid];
            }
            return new ChangeTracker<int>();
        }
    }
}