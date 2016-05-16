using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
	public class GroupFilterRepository
	{
        private static Logger logger = LogManager.GetCurrentClassLogger();

	    private static PocoCache<int, GroupFilter> Cache;

	    public static void InitCache()
	    {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            

            GroupFilterRepository repo =new GroupFilterRepository();
	        List<GroupFilter> filters = repo.InternalGetAll();
	        filters.Add(new GroupFilter {GroupFilterID = -999, GroupFilterName = "All"});
	        Cache = new PocoCache<int, GroupFilter>(filters, a => a.GroupFilterID);
	        foreach (GroupFilter g in Cache.Values.ToList())
	        {
	            if (g.GroupFilterID!=0 && g.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION)
	            {
	                repo.Save(g,false,null);
	            }
	        }
	    }

	    public static void CreateFakeAllFilter()
	    {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            GroupFilterRepository repo = new GroupFilterRepository();
	        repo.GetByID(-999).UpdateGroupFilterUser(null); // All Filter
	    }
        private List<GroupFilter> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var gfs = session
                    .CreateCriteria(typeof(GroupFilter))
                    .List<GroupFilter>();
                return new List<GroupFilter>(gfs);
            }
        }
     


        public void Save(GroupFilter obj, bool updategroupfilter, JMMUser user)
        {
            if (obj.GroupFilterID == -999)
                return;
		    if (updategroupfilter)
		    {
                logger.Trace("Updating group filter stats by groupfilter from GroupFilterRepository.Save: {0}", obj.GroupFilterID);
                obj.UpdateGroupFilterUser(user);
		    }
            obj.GroupsIdsString = Newtonsoft.Json.JsonConvert.SerializeObject(obj._groupsId.ToDictionary(a => a.Key, a => a.Value.ToList()));
            obj.GroupsIdsVersion = GroupFilter.GROUPFILTER_VERSION;
            Cache.Update(obj);
            using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}

		}
        
        public GroupFilter GetByID(int id)
        {
            return Cache.Get(id);
		}

		public GroupFilter GetByID(ISession session, int id)
		{
            return GetByID(id);
		}

		public List<GroupFilter> GetAll()
		{
		    return Cache.Values.ToList();
		}

		public List<GroupFilter> GetAll(ISession session)
		{
		    return GetAll();
		}

		public List<GroupFilter> GetLockedGroupFilters(ISession session)
		{
		    return Cache.Values.Where(a => a.Locked == 1).ToList();
		}

		public void Delete(int id)
        {
            if (id == -999)
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					GroupFilter cr = GetByID(id);
					if (cr != null)
					{
                        Cache.Remove(cr);
						session.Delete(cr); 
						transaction.Commit();
					}
				}
			}
		}
	}
}
