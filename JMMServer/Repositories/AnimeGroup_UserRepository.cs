using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.PlexAndKodi;
using NLog;
using NHibernate;
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

        public static void InitCache()
        {
            string t = "AnimeGroups_User";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AnimeGroup_UserRepository repo = new AnimeGroup_UserRepository();
            Cache = new PocoCache<int, AnimeGroup_User>(repo.InternalGetAll(), a=>a.AnimeGroup_UserID);
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            UsersGroups = Cache.CreateIndex(a => a.JMMUserID,a => a.AnimeGroupID);

            int cnt = 0;
            List<AnimeGroup_User> grps = Cache.Values.Where(a => a.PlexContractVersion < AnimeGroup_User.PLEXCONTRACT_VERSION).ToList();
            int max = grps.Count;
            foreach (AnimeGroup_User g in grps)
            {
                repo.Save(g);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " DbRegen - " + max + "/" + max);
        }


	    private List<AnimeGroup_User> InternalGetAll()
	    {
	        using (var session = JMMService.SessionFactory.OpenSession())
	        {
	            var grps = session
	                .CreateCriteria(typeof (AnimeGroup_User))
	                .List<AnimeGroup_User>();

	            return new List<AnimeGroup_User>(grps);
	        }
	    }

	    public void Save(AnimeGroup_User obj)
		{
            UpdatePlexKodiContracts(obj);
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}
			//logger.Trace("Updating group stats by group from AnimeGroup_UserRepository.Save: {0}", obj.AnimeGroupID);
			//StatsCache.Instance.UpdateUsingGroup(obj.AnimeGroupID);
            logger.Trace("Updating group filter stats by animegroup from AnimeGroup_UserRepository.Save: {0}", obj.AnimeGroupID);
            UpdateGroupFilter(obj);
            Cache.Update(obj);
		}

	    private void UpdateGroupFilter(AnimeGroup_User obj)
	    {
            AnimeGroupRepository repo = new AnimeGroupRepository();
            JMMUserRepository repouser = new JMMUserRepository();
            AnimeGroup grp = repo.GetByID(obj.AnimeGroupID);
            JMMUser usr = repouser.GetByID(obj.JMMUserID);
            if (grp != null && usr != null)
	            grp.UpdateGroupFilters(usr);
	    }

	    private void UpdatePlexKodiContracts(AnimeGroup_User ugrp)
	    {
	        using (var session = JMMService.SessionFactory.OpenSession())
	        {
                AnimeGroupRepository repo = new AnimeGroupRepository();
                AnimeGroup grp = repo.GetByID(ugrp.AnimeGroupID);
	            if (grp == null)
	                return;
                List<AnimeSeries> series = grp.GetAllSeries(session);
                ugrp.PlexContract = Helper.GenerateFromAnimeGroup(session, grp, ugrp.JMMUserID, series);
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
                        Cache.Remove(cr);
                        session.Delete(cr);
		                transaction.Commit();
		            }
		        }
		    }
		    if (cr != null)
		    {
		        logger.Trace("Updating group filter stats by animegroup from AnimeGroup_UserRepository.Delete: {0}", cr.AnimeGroupID);
                UpdateGroupFilter(cr);
		    }
		}

	    //if (cr != null)
			//{
			//	logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}", cr.AnimeGroupID);
			//	StatsCache.Instance.UpdateUsingGroup(cr.AnimeGroupID);
			//}

	}
}
