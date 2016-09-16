using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JMMServer.Entities;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class AnimeGroup_UserRepository : BaseCachedRepository<AnimeGroup_User, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, AnimeGroup_User, int> Groups;
        private PocoIndex<int, AnimeGroup_User, int> Users;
        private PocoIndex<int, AnimeGroup_User, int, int> UsersGroups;
        private Dictionary<int, ChangeTracker<int>> Changes = new Dictionary<int, ChangeTracker<int>>();


        public AnimeGroup_UserRepository()
        {
            DeleteCallback = (ses, cr) =>
            {
                if (!Changes.ContainsKey(cr.JMMUserID))
                    Changes[cr.JMMUserID] = new ChangeTracker<int>();
                Changes[cr.JMMUserID].Remove(cr.AnimeGroupID);
                logger.Trace("Updating group filter stats by animegroup from AnimeGroup_UserRepository.Delete: {0}", cr.AnimeGroupID);
                cr.DeleteFromFilters();
            };
        }



        public override void PopulateIndexes()
        {
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            UsersGroups = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeGroupID);

            foreach (int n in Cache.Values.Select(a => a.JMMUserID).Distinct())
            {
                Changes[n] = new ChangeTracker<int>();
                Changes[n].AddOrUpdateRange(Users.GetMultiple(n).Select(a => a.AnimeGroupID));
            }
        }

        public override void RegenerateDb()
        {
            int cnt = 0;
            List<AnimeGroup_User> grps =
                Cache.Values.Where(a => a.PlexContractVersion < AnimeGroup_User.PLEXCONTRACT_VERSION).ToList();
            int max = grps.Count;
            foreach (AnimeGroup_User g in grps)
            {
                Save(g);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeGroup_User).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeGroup_User).Name,
                " DbRegen - " + max + "/" + max);
        }

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(ISession session, AnimeGroup_User obj) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(List<AnimeGroup_User> objs) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(ISession session, List<AnimeGroup_User> objs) { throw new NotSupportedException(); }


        public override void Save(AnimeGroup_User obj)
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
                HashSet<GroupFilterConditionType> types = AnimeGroup_User.GetConditionTypesChanged(old, obj);
                base.Save(obj);
                if (!Changes.ContainsKey(obj.JMMUserID))
                    Changes[obj.JMMUserID] = new ChangeTracker<int>();
                Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeGroupID);
                obj.UpdateGroupFilter(types);
            }
        }



       

        public AnimeGroup_User GetByUserAndGroupID(int userid, int groupid)
        {
            return UsersGroups.GetOne(userid, groupid);
        }

        public List<AnimeGroup_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }



        public List<AnimeGroup_User> GetByGroupID(int groupid)
        {
            return Groups.GetMultiple(groupid);
        }



        public ChangeTracker<int> GetChangeTracker(int userid)
        {
            if (Changes.ContainsKey(userid))
                return Changes[userid];
            return new ChangeTracker<int>();
        }

    }
}