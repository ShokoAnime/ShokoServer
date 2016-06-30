using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AnimeGroup_UserRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        private static PocoCache<int, AnimeGroup_User> Cache;
        private static PocoIndex<int, AnimeGroup_User, int> Groups;
        private static PocoIndex<int, AnimeGroup_User, int> Users;
        private static PocoIndex<int, AnimeGroup_User, int, int> UsersGroups;

        private static Dictionary<int, ChangeTracker<int>> Changes = new Dictionary<int, ChangeTracker<int>>();
        public static void InitCache()
        {
            string t = "AnimeGroups_User";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AnimeGroup_UserRepository repo = new AnimeGroup_UserRepository();
            Cache = new PocoCache<int, AnimeGroup_User>(repo.InternalGetAll(), a => a.AnimeGroup_UserID);
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            UsersGroups = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeGroupID);
            foreach (int n in Cache.Values.Select(a => a.JMMUserID).Distinct())
            {
                Changes[n]=new ChangeTracker<int>();
                Changes[n].AddOrUpdateRange(Users.GetMultiple(n).Select(a=>a.AnimeGroupID));
            }

            int cnt = 0;
            List<AnimeGroup_User> grps =
                Cache.Values.Where(a => a.PlexContractVersion < AnimeGroup_User.PLEXCONTRACT_VERSION).ToList();
            int max = grps.Count;
            foreach (AnimeGroup_User g in grps)
            {
                repo.Save(g);
                cnt++;
                if (cnt%10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                " DbRegen - " + max + "/" + max);
        }


        private List<AnimeGroup_User> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var grps = session
                    .CreateCriteria(typeof(AnimeGroup_User))
                    .List<AnimeGroup_User>();

                return new List<AnimeGroup_User>(grps);
            }
        }


        public void Save(AnimeGroup_User obj)
        {
            lock (obj)
            {
                obj.UpdatePlexKodiContracts();
                //Get The previous AnimeGroup_User from db for comparasion;
                AnimeGroup_User old;
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    old = session.Get<AnimeGroup_User>(obj.AnimeGroup_UserID);
                }
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    HashSet<GroupFilterConditionType> types = AnimeGroup_User.GetConditionTypesChanged(old, obj);                    
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                    Cache.Update(obj);
                    if (!Changes.ContainsKey(obj.JMMUserID))
                        Changes[obj.JMMUserID] = new ChangeTracker<int>();
                    Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeGroupID);
                    obj.UpdateGroupFilter(types);
                }
            }
        }


        public AnimeGroup_User GetByID(int id)
        {
            return Cache.Get(id);
        }

        public AnimeGroup_User GetByUserAndGroupID(int userid, int groupid)
        {
            return UsersGroups.GetOne(userid, groupid);
        }

        public AnimeGroup_User GetByUserAndGroupID(ISession session, int userid, int groupid)
        {
            return GetByUserAndGroupID(userid, groupid);
        }

        public List<AnimeGroup_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }

        public List<AnimeGroup_User> GetByUserID(ISession session, int userid)
        {
            return GetByUserID(userid);
        }

        public List<AnimeGroup_User> GetByGroupID(int groupid)
        {
            return Groups.GetMultiple(groupid);
        }

        public List<AnimeGroup_User> GetAll()
        {
            return Cache.Values.ToList();
        }

        public static ChangeTracker<int> GetChangeTracker(int userid)
        {
            if (Changes.ContainsKey(userid))
                return Changes[userid];
            return new ChangeTracker<int>();
        }
        public void Delete(int id)
        {
            AnimeGroup_User cr = null;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                        Cache.Remove(cr);
                        if (!Changes.ContainsKey(cr.JMMUserID))
                            Changes[cr.JMMUserID] = new ChangeTracker<int>();
                        Changes[cr.JMMUserID].Remove(cr.AnimeGroupID);
                        logger.Trace(
                            "Updating group filter stats by animegroup from AnimeGroup_UserRepository.Delete: {0}",
                            cr.AnimeGroupID);
                        cr.DeleteFromFilters();
                    }
                }
            }
        }
    }
}