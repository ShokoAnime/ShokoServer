using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
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
        private static PocoIndex<int, GroupFilter, int> Parents;

        public static void InitCache()
	    {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            

            GroupFilterRepository repo =new GroupFilterRepository();
	        List<GroupFilter> filters = repo.InternalGetAll();
	        Cache = new PocoCache<int, GroupFilter>(filters, a => a.GroupFilterID);
            Parents = Cache.CreateIndex(a => a.ParentGroupFilterID ?? 0);
            foreach (GroupFilter g in Cache.Values.ToList())
	        {
	            if (g.GroupFilterID!=0 && g.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION)
	            {
	                repo.Save(g,false,null);
	            }
	        }
	    }

	    public static void InitCacheSecondPart()
	    {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
	        CreateOrVerifyLockedFilters();
        }
        public static void CreateOrVerifyLockedFilters()
        {
            GroupFilterRepository repFilters = new GroupFilterRepository();
            GroupFilterConditionRepository repGFC = new GroupFilterConditionRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {

                List<GroupFilter> lockedGFs = repFilters.GetLockedGroupFilters(session);
                //Continue Watching
                // check if it already exists
                if (lockedGFs != null && !lockedGFs.Any(a => a.FilterType == (int)GroupFilterType.ContinueWatching))
                {
                    //Fixing Filter
                    foreach (GroupFilter gfTemp in lockedGFs)
                    {
                        if (
                            gfTemp.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                                StringComparison.InvariantCultureIgnoreCase) &&
                            gfTemp.FilterType != (int)GroupFilterType.ContinueWatching)
                        {
                            gfTemp.FilterType = (int)GroupFilterType.ContinueWatching;
                            repFilters.Save(gfTemp, true, null);
                            break;
                        }
                    }
                }
                else
                {
                    GroupFilter gf = new GroupFilter();
                    gf.GroupFilterName = Constants.GroupFilterName.ContinueWatching;
                    gf.Locked = 1;
                    gf.SortingCriteria = "4;2"; // by last watched episode desc
                    gf.ApplyToSeries = 0;
                    gf.BaseCondition = 1; // all
                    gf.FilterType = (int)GroupFilterType.ContinueWatching;
                    gf.IsVisibleInClients = 1;
                    repFilters.Save(gf, false, null); //Get ID

                    GroupFilterCondition gfc = new GroupFilterCondition();
                    gfc.ConditionType = (int)GroupFilterConditionType.HasWatchedEpisodes;
                    gfc.ConditionOperator = (int)GroupFilterOperator.Include;
                    gfc.ConditionParameter = "";
                    gfc.GroupFilterID = gf.GroupFilterID;
                    repGFC.Save(gfc);

                    gfc = new GroupFilterCondition();
                    gfc.ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes;
                    gfc.ConditionOperator = (int)GroupFilterOperator.Include;
                    gfc.ConditionParameter = "";
                    gfc.GroupFilterID = gf.GroupFilterID;
                    repGFC.Save(gfc);
                    //Re Save to recalc Group Filter
                    repFilters.Save(gf, true, null);
                }
                //Create All filter
                GroupFilter allfilter = lockedGFs.FirstOrDefault(a => a.FilterType == (int)GroupFilterType.All);
                if (allfilter == null)
                {
                    GroupFilter gf = new GroupFilter { GroupFilterName = "All", Locked = 1, IsVisibleInClients = 1, FilterType = (int)GroupFilterType.All, BaseCondition = 1, SortingCriteria = "5;1" };
                    repFilters.Save(gf, true, null);
                }
                GroupFilter tagsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Tag));
                if (tagsdirec == null)
                {
                    tagsdirec = new GroupFilter { GroupFilterName = "Tags", IsVisibleInClients = 1, FilterType = (int)(GroupFilterType.Directory | GroupFilterType.Tag), BaseCondition = 1, Locked = 1, SortingCriteria = "13;1" };
                    repFilters.Save(tagsdirec, true, null);
                }
                GroupFilter yearsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Year));
                if (yearsdirec == null)
                {
                    yearsdirec = new GroupFilter { GroupFilterName = "Years", FilterType = (int)(GroupFilterType.Directory | GroupFilterType.Year), BaseCondition = 1, Locked = 1, SortingCriteria = "13;1" };
                    repFilters.Save(yearsdirec, true, null);
                }
                AniDB_TagRepository tagsrepo = new AniDB_TagRepository();
                AnimeGroupRepository grouprepo = new AnimeGroupRepository();
                List<string> alltags = tagsrepo.GetAll(session).Select(a => a.TagName).ToList();
                List<string> notin = alltags.Where(a => !lockedGFs.Any(b => b.FilterType == (int)GroupFilterType.Tag && b.GroupFilterName == a)).ToList();
                foreach (string s in notin)
                {
                    GroupFilter yf = new GroupFilter { ParentGroupFilterID = tagsdirec.GroupFilterID, IsVisibleInClients = 1, GroupFilterName = s, BaseCondition = 1, Locked = 1, SortingCriteria = "5;1", FilterType = (int)GroupFilterType.Tag };
                    repFilters.Save(yf, false, null); //Get ID
                    GroupFilterCondition gfc = new GroupFilterCondition();
                    gfc.ConditionType = (int)GroupFilterConditionType.Tag;
                    gfc.ConditionOperator = (int)GroupFilterOperator.Include;
                    gfc.ConditionParameter = s;
                    gfc.GroupFilterID = yf.GroupFilterID;
                    repGFC.Save(gfc);
                    repFilters.Save(yf, true, null);
                }
                List<Contract_AnimeGroup> grps = grouprepo.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
                if (grps.Any(a => a.Stat_AirDate_Min.HasValue && a.Stat_AirDate_Max.HasValue))
                {
                    DateTime maxtime = grps.Where(a => a.Stat_AirDate_Max.HasValue).Max(a => a.Stat_AirDate_Max.Value);
                    DateTime mintime = grps.Where(a => a.Stat_AirDate_Min.HasValue).Min(a => a.Stat_AirDate_Min.Value);
                    List<string> allyears = Enumerable.Range(mintime.Year, maxtime.Year - mintime.Year + 1).Select(a => a.ToString()).ToList();
                    notin = allyears.Where(a => !lockedGFs.Any(b => b.FilterType == (int)GroupFilterType.Year && b.GroupFilterName == a)).ToList();
                    foreach (string s in notin)
                    {
                        GroupFilter yf = new GroupFilter { ParentGroupFilterID = yearsdirec.GroupFilterID, IsVisibleInClients = 1, GroupFilterName = s, BaseCondition = 1, Locked = 1, SortingCriteria = "5;1", FilterType = (int)GroupFilterType.Year };
                        repFilters.Save(yf, false, null); //Get ID
                        GroupFilterCondition gfc = new GroupFilterCondition();
                        gfc.ConditionType = (int)GroupFilterConditionType.Year;
                        gfc.ConditionOperator = (int)GroupFilterOperator.Include;
                        gfc.ConditionParameter = s;
                        gfc.GroupFilterID = yf.GroupFilterID;
                        repGFC.Save(gfc);
                        repFilters.Save(yf, true, null);
                    }
                }
            }

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
		    if (updategroupfilter)
		    {
                logger.Trace("Updating group filter stats by groupfilter from GroupFilterRepository.Save: {0}", obj.GroupFilterID);
                obj.UpdateGroupFilterUser(user);
		    }
            obj.GroupsIdsString = Newtonsoft.Json.JsonConvert.SerializeObject(obj._groupsId.ToDictionary(a => a.Key, a => a.Value.ToList()));
            obj.GroupsIdsVersion = GroupFilter.GROUPFILTER_VERSION;
            using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}
            Cache.Update(obj);

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

        public List<GroupFilter> GetByParentID(int parentid)
        {
            return Parents.GetMultiple(parentid);
        }

	    public List<GroupFilter> GetTopLevel()
	    {
            return Parents.GetMultiple(0);
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
