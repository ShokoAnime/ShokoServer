using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeGroup_UserRepository : BaseRepository<SVR_AnimeGroup_User, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AnimeGroup_User, int> Groups;
        private PocoIndex<int, SVR_AnimeGroup_User, int> Users;
        private PocoIndex<int, SVR_AnimeGroup_User, int, int> UsersGroups;
        private readonly Dictionary<int, ChangeTracker<int>> Changes = new Dictionary<int, ChangeTracker<int>>();



        internal override object BeginSave(SVR_AnimeGroup_User entity, SVR_AnimeGroup_User original_entity, object parameters)
        {
            entity.UpdatePlexKodiContracts_RA();
            return SVR_AnimeGroup_User.GetConditionTypesChanged(original_entity, entity);
        }

        internal override void EndSave(SVR_AnimeGroup_User entity, object returnFromBeginSave,
            object parameters)
        {
            HashSet<GroupFilterConditionType> types = (HashSet<GroupFilterConditionType>)returnFromBeginSave;
            lock (Changes)
            {
                if (!Changes.ContainsKey(entity.JMMUserID))
                    Changes[entity.JMMUserID] = new ChangeTracker<int>();
                Changes[entity.JMMUserID].AddOrUpdate(entity.AnimeGroupID);
            }
            entity.UpdateGroupFilter(types);
        }

        internal override void EndDelete(SVR_AnimeGroup_User entity, object returnFromBeginDelete, object parameters)
        {
            lock (Changes)
            {
                if (!Changes.ContainsKey(entity.JMMUserID))
                    Changes[entity.JMMUserID] = new ChangeTracker<int>();
                Changes[entity.JMMUserID].Remove(entity.AnimeGroupID);
            }
            logger.Trace("Updating group filter stats by animegroup from AnimeGroup_UserRepository.Delete: {0}", entity.AnimeGroupID);
            entity.DeleteFromFilters();
        }

        internal override int SelectKey(SVR_AnimeGroup_User entity) => entity.AnimeGroup_UserID;

        internal override void PopulateIndexes()
        {
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            UsersGroups = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeGroupID);


        }

        internal override void ClearIndexes()
        {
            Groups = null;
            Users = null;
            UsersGroups = null;
        }

        public override void PostInit(IProgress<InitProgress> progress, int batchSize)
        {
            lock (Changes)
            {
                foreach (int n in WhereAll().Select(a => a.JMMUserID).Distinct())
                {
                    Changes[n] = new ChangeTracker<int>();
                    Changes[n].AddOrUpdateRange(GetByUserID(n).Select(a => a.AnimeGroupID));
                }
            }
        }
        /*
        //TODO RefactorDB

        /// <summary>
        /// Inserts a batch of <see cref="SVR_AnimeGroup_User"/> into the database.
        /// </summary>
        /// <remarks>
        /// <para>This method should NOT be used for updating existing entities.</para>
        /// <para>It is up to the caller of this method to manage transactions, etc.</para>
        /// <para>Group Filters, etc. will not be updated by this method.</para>
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="groupUsers">The batch of <see cref="SVR_AnimeGroup_User"/> to insert into the database.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="groupUsers"/> is <c>null</c>.</exception>
        public void InsertBatch(ISessionWrapper session, IEnumerable<SVR_AnimeGroup_User> groupUsers)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groupUsers == null)
                throw new ArgumentNullException(nameof(groupUsers));

            foreach (SVR_AnimeGroup_User groupUser in groupUsers)
            {
                lock (globalDBLock)
                {
                    session.Insert(groupUser);
                    lock (Cache)
                    {
                        Cache.Update(groupUser);
                    }
                }

                lock (Changes)
                {
                    if (!Changes.TryGetValue(groupUser.JMMUserID, out ChangeTracker<int> changeTracker))
                    {
                        changeTracker = new ChangeTracker<int>();
                        Changes[groupUser.JMMUserID] = changeTracker;
                    }
                    changeTracker.AddOrUpdate(groupUser.AnimeGroupID);
                }
            }
        }

        /// <summary>
        /// Inserts a batch of <see cref="SVR_AnimeGroup_User"/> into the database.
        /// </summary>
        /// <remarks>
        /// <para>It is up to the caller of this method to manage transactions, etc.</para>
        /// <para>Group Filters, etc. will not be updated by this method.</para>
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="groupUsers">The batch of <see cref="SVR_AnimeGroup_User"/> to insert into the database.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="groupUsers"/> is <c>null</c>.</exception>
        public void UpdateBatch(ISessionWrapper session, IEnumerable<SVR_AnimeGroup_User> groupUsers)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groupUsers == null)
                throw new ArgumentNullException(nameof(groupUsers));

            foreach (SVR_AnimeGroup_User groupUser in groupUsers)
            {
                lock (globalDBLock)
                {
                    session.Update(groupUser);
                    lock (Cache)
                    {
                        Cache.Update(groupUser);
                    }
                }

                lock (Changes)
                {
                    if (!Changes.TryGetValue(groupUser.JMMUserID, out ChangeTracker<int> changeTracker))
                    {
                        changeTracker = new ChangeTracker<int>();
                        Changes[groupUser.JMMUserID] = changeTracker;
                    }

                    changeTracker.AddOrUpdate(groupUser.AnimeGroupID);
                }
            }
        }

        /// <summary>
        /// Deletes all AnimeGroup_User records.
        /// </summary>
        /// <remarks>
        /// This method also makes sure that the cache is cleared.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public void DeleteAll(ISessionWrapper session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            // First, get all of the current user/groups so that we can inform the change tracker that they have been removed later
            var usrGrpMap = GetAll()
                .GroupBy(g => g.JMMUserID, g => g.AnimeGroupID);

            lock (globalDBLock)
            {
                // Then, actually delete the AnimeGroup_Users
                session.CreateQuery("delete SVR_AnimeGroup_User agu").ExecuteUpdate();
            }

            // Now, update the change trackers with all removed records
            foreach (var grp in usrGrpMap)
            {
                int jmmUserId = grp.Key;

                lock (Changes)
                {
                    if (!Changes.TryGetValue(jmmUserId, out ChangeTracker<int> changeTracker))
                    {
                        changeTracker = new ChangeTracker<int>();
                        Changes[jmmUserId] = changeTracker;
                    }

                    changeTracker.RemoveRange(grp);
                }
            }

            // Finally, we need to clear the cache so that it is in sync with the database
            ClearCache();
        }
        */
        public SVR_AnimeGroup_User GetByUserAndGroupID(int userid, int groupid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return UsersGroups.GetOne(userid, groupid);
                return Table.FirstOrDefault(a => a.JMMUserID==userid && a.AnimeGroupID==groupid);
            }

        }

        public List<SVR_AnimeGroup_User> GetByUserID(int userid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Users.GetMultiple(userid);
                return Table.Where(a => a.JMMUserID==userid).ToList();
            }
        }

        public List<SVR_AnimeGroup_User> GetByGroupID(int groupid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Groups.GetMultiple(groupid);
                return Table.Where(a => a.AnimeGroupID==groupid).ToList();
            }

        }
        public List<SVR_AnimeGroup_User> GetByGroupsID(IEnumerable<int> groupsid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return groupsid.SelectMany(a => Groups.GetMultiple(a)).ToList();
                return Table.Where(a => groupsid.Contains(a.AnimeGroupID)).ToList();
            }

        }
        public void KillEmAll()
        {
            using (RepoLock.ReaderLock())
            {

                List<SVR_AnimeGroup_User> grps;
                if (IsCached)
                {
                    grps = Cache.Values.ToList();
                    Cache = null;
                    ClearIndexes();
                }
                else
                {
                    grps = Table.ToList();
                }
                ShokoContext ctx = Provider.GetContext();
                ctx.AttachRange(grps);
                ctx.RemoveRange(grps);
                ctx.SaveChanges();
            }
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