using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentNHibernate.Utils;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class GroupFilterRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static PocoCache<int, GroupFilter> Cache;
        private static PocoIndex<int, GroupFilter, int> Parents;

        private static BiDictionaryManyToMany<int, GroupFilterConditionType> Types;


        public static List<GroupFilter> InitCache()
        {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);

            GroupFilterConditionRepository repConds = new GroupFilterConditionRepository();

            GroupFilterRepository repo = new GroupFilterRepository();
            List<GroupFilter> filters = repo.InternalGetAll();
            Cache = new PocoCache<int, GroupFilter>(filters, a => a.GroupFilterID);
            Parents = Cache.CreateIndex(a => a.ParentGroupFilterID ?? 0);
            Types =
                new BiDictionaryManyToMany<int, GroupFilterConditionType>(Cache.Values.ToDictionary(
                    a => a.GroupFilterID,
                    a => a.Types));
            List<GroupFilter> recalc = new List<GroupFilter>();
            foreach (GroupFilter g in Cache.Values.ToList())
            {
                if (g.GroupFilterID != 0 && g.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION ||
                    g.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION)
                {
                    if (g.GroupConditionsVersion == 0)
                    {
                        g.Conditions = repConds.GetByGroupFilterID(g.GroupFilterID);
                    }
                    repo.Save(g, true);
                    recalc.Add(g);
                }
            }
            return recalc;
        }

        public static void InitCacheSecondPart(List<GroupFilter> recalc)
        {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            GroupFilterRepository repo = new GroupFilterRepository();
            foreach (GroupFilter g in Cache.Values.ToList())
            {
                if (g.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION ||
                    g.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION ||
                    g.SeriesIdsVersion < GroupFilter.SERIEFILTER_VERSION)
                {
                    if (!recalc.Contains(g))
                        recalc.Add(g);
                }
            }
            int max = recalc.Count;
            int cnt = 0;
            foreach (GroupFilter gf in recalc)
            {
                cnt++;
                ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                    " Recalc Filter " + gf.GroupFilterName + " - " + cnt + "/" + max);
                if (gf.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION ||
                    gf.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION)
                    gf.EvaluateAnimeGroups();
                if (gf.SeriesIdsVersion < GroupFilter.SERIEFILTER_VERSION ||
                    gf.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION)
                    gf.EvaluateAnimeSeries();
                repo.Save(gf);
            }
        }


        //TODO Cleanup function for Empty Tags and Empty Years

        

        public static void CreateOrVerifyLockedFilters()
        {
            GroupFilterRepository repFilters = new GroupFilterRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                string t = "GroupFilter";

                List<GroupFilter> lockedGFs = repFilters.GetLockedGroupFilters(session);
                //Continue Watching
                // check if it already exists

                ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " Creating Continue Watching filter");

                GroupFilter cwatching =
                    lockedGFs.FirstOrDefault(
                        a =>
                            a.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                                StringComparison.InvariantCultureIgnoreCase));
                if (cwatching != null && cwatching.FilterType != (int) GroupFilterType.ContinueWatching)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " Creating Continue Watching filter");
                    cwatching.FilterType = (int) GroupFilterType.ContinueWatching;
                    repFilters.Save(cwatching);
                }
                else if (cwatching == null)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " Creating Continue Watching filter");
                    GroupFilter gf = new GroupFilter();
                    gf.GroupFilterName = Constants.GroupFilterName.ContinueWatching;
                    gf.Locked = 1;
                    gf.SortingCriteria = "4;2"; // by last watched episode desc
                    gf.ApplyToSeries = 0;
                    gf.BaseCondition = 1; // all
                    gf.FilterType = (int) GroupFilterType.ContinueWatching;
                    gf.InvisibleInClients = 0;
                    gf.Conditions = new List<GroupFilterCondition>();

                    GroupFilterCondition gfc = new GroupFilterCondition();
                    gfc.ConditionType = (int) GroupFilterConditionType.HasWatchedEpisodes;
                    gfc.ConditionOperator = (int) GroupFilterOperator.Include;
                    gfc.ConditionParameter = "";
                    gfc.GroupFilterID = gf.GroupFilterID;
                    gf.Conditions.Add(gfc);
                    gfc = new GroupFilterCondition();
                    gfc.ConditionType = (int) GroupFilterConditionType.HasUnwatchedEpisodes;
                    gfc.ConditionOperator = (int) GroupFilterOperator.Include;
                    gfc.ConditionParameter = "";
                    gfc.GroupFilterID = gf.GroupFilterID;
                    gf.Conditions.Add(gfc);
                    gf.EvaluateAnimeGroups();
                    gf.EvaluateAnimeSeries();
                    repFilters.Save(gf); //Get ID
                }
                //Create All filter
                GroupFilter allfilter = lockedGFs.FirstOrDefault(a => a.FilterType == (int) GroupFilterType.All);
                if (allfilter == null)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " Creating All filter");
                    GroupFilter gf = new GroupFilter
                    {
                        GroupFilterName = "All",
                        Locked = 1,
                        InvisibleInClients = 0,
                        FilterType = (int) GroupFilterType.All,
                        BaseCondition = 1,
                        SortingCriteria = "5;1"
                    };
                    gf.EvaluateAnimeGroups();
                    gf.EvaluateAnimeSeries();
                    repFilters.Save(gf);
                }
                GroupFilter tagsdirec =
                    lockedGFs.FirstOrDefault(
                        a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));
                if (tagsdirec == null)
                {
                    tagsdirec = new GroupFilter
                    {
                        GroupFilterName = "Tags",
                        InvisibleInClients = 0,
                        FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Tag),
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "13;1"
                    };
                    repFilters.Save(tagsdirec);
                }
                GroupFilter yearsdirec =
                    lockedGFs.FirstOrDefault(
                        a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Year));
                if (yearsdirec == null)
                {
                    yearsdirec = new GroupFilter
                    {
                        GroupFilterName = "Years",
                        InvisibleInClients = 0,
                        FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Year),
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "13;1"
                    };
                    repFilters.Save(yearsdirec);
                }
            }
            CreateOrVerifyTagsAndYearsFilters(true);
        }
        public static void CreateOrVerifyTagsAndYearsFilters(bool frominit = false, HashSet<string> tags = null, DateTime? airdate = null)
        {
            GroupFilterRepository repFilters = new GroupFilterRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                string t = "GroupFilter";

                List<GroupFilter> lockedGFs = repFilters.GetLockedGroupFilters(session);
                AniDB_TagRepository tagsrepo = new AniDB_TagRepository();
                AnimeGroupRepository grouprepo = new AnimeGroupRepository();
                GroupFilter tagsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Tag));
                if (tagsdirec != null)
                {
                    HashSet<string> alltags;
                    if (tags == null)
                        alltags = new HashSet<string>(tagsrepo.GetAll(session).Select(a => a.TagName).Distinct(StringComparer.InvariantCultureIgnoreCase),StringComparer.InvariantCultureIgnoreCase);
                    else
                        alltags = new HashSet<string>(tags.Distinct(StringComparer.InvariantCultureIgnoreCase),StringComparer.InvariantCultureIgnoreCase);
                    HashSet<string> notin = new HashSet<string>(lockedGFs.Where(a => a.FilterType == (int) GroupFilterType.Tag).Select(a => a.Conditions.FirstOrDefault()?.ConditionParameter),StringComparer.InvariantCultureIgnoreCase);
                    alltags.ExceptWith(notin);

                    int max = alltags.Count;
                    int cnt = 0;
                    //AniDB Tags are in english so we use en-us culture
                    TextInfo tinfo = new CultureInfo("en-US", false).TextInfo;
                    foreach (string s in alltags)
                    {
                        cnt++;
                        if (frominit)
                            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                                " Creating Tag '" + s + "' filter " + cnt + "/" + max);
                        GroupFilter yf = new GroupFilter
                        {
                            ParentGroupFilterID = tagsdirec.GroupFilterID,
                            InvisibleInClients = 0,
                            GroupFilterName = tinfo.ToTitleCase(s.Replace("`","'")),
                            BaseCondition = 1,
                            Locked = 1,
                            SortingCriteria = "5;1",
                            FilterType = (int) GroupFilterType.Tag
                        };
                        GroupFilterCondition gfc = new GroupFilterCondition();
                        gfc.ConditionType = (int) GroupFilterConditionType.Tag;
                        gfc.ConditionOperator = (int) GroupFilterOperator.Include;
                        gfc.ConditionParameter = s;
                        gfc.GroupFilterID = yf.GroupFilterID;
                        yf.Conditions.Add(gfc);
                        yf.EvaluateAnimeGroups();
                        yf.EvaluateAnimeSeries();
                        repFilters.Save(yf);
                    }
                }
                GroupFilter yearsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Year));
                if (yearsdirec != null)
                {

                    HashSet<string> allyears;
                    if (airdate == null)
                    {
                        List<Contract_AnimeGroup> grps =
                            grouprepo.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
                        if (grps.Any(a => a.Stat_AirDate_Min.HasValue && a.Stat_AirDate_Max.HasValue))
                        {
                            DateTime maxtime =
                                grps.Where(a => a.Stat_AirDate_Max.HasValue).Max(a => a.Stat_AirDate_Max.Value);
                            DateTime mintime =
                                grps.Where(a => a.Stat_AirDate_Min.HasValue).Min(a => a.Stat_AirDate_Min.Value);
                            allyears =
                                new HashSet<string>(
                                    Enumerable.Range(mintime.Year, maxtime.Year - mintime.Year + 1)
                                        .Select(a => a.ToString()), StringComparer.InvariantCultureIgnoreCase);
                        }
                        else
                        {
                            allyears = new HashSet<string>();
                        }
                    }
                    else
                    {
                        allyears = new HashSet<string>(new string[] {airdate.Value.Year.ToString()});
                    }
                    HashSet<string> notin =
                        new HashSet<string>(
                            lockedGFs.Where(a => a.FilterType == (int) GroupFilterType.Year)
                                .Select(a => a.Conditions.FirstOrDefault()?.ConditionParameter),
                            StringComparer.InvariantCultureIgnoreCase);
                    allyears.ExceptWith(notin);
                    int max = allyears.Count;
                    int cnt = 0;
                    foreach (string s in allyears)
                    {
                        cnt++;
                        if (frominit)
                            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                                " Creating Year '" + s + "' filter " + cnt + "/" + max);
                        GroupFilter yf = new GroupFilter
                        {
                            ParentGroupFilterID = yearsdirec.GroupFilterID,
                            InvisibleInClients = 0,
                            GroupFilterName = s,
                            BaseCondition = 1,
                            Locked = 1,
                            SortingCriteria = "5;1",
                            FilterType = (int) GroupFilterType.Year
                        };
                        GroupFilterCondition gfc = new GroupFilterCondition();
                        gfc.ConditionType = (int) GroupFilterConditionType.Year;
                        gfc.ConditionOperator = (int) GroupFilterOperator.Include;
                        gfc.ConditionParameter = s;
                        gfc.GroupFilterID = yf.GroupFilterID;
                        yf.Conditions.Add(gfc);
                        yf.EvaluateAnimeGroups();
                        yf.EvaluateAnimeSeries();
                        repFilters.Save(yf);
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


        public void Save(GroupFilter obj, bool onlyconditions = false)
        {
            lock (obj)
            {
                if (!onlyconditions)
                {
                    obj.GroupsIdsString =
                        Newtonsoft.Json.JsonConvert.SerializeObject(obj.GroupsIds.ToDictionary(a => a.Key,
                            a => a.Value.ToList()));
                    obj.GroupsIdsVersion = GroupFilter.GROUPFILTER_VERSION;
                    obj.SeriesIdsString =
                        Newtonsoft.Json.JsonConvert.SerializeObject(obj.SeriesIds.ToDictionary(a => a.Key,
                            a => a.Value.ToList()));
                    obj.SeriesIdsVersion = GroupFilter.SERIEFILTER_VERSION;
                }
                obj.GroupConditions = Newtonsoft.Json.JsonConvert.SerializeObject(obj._conditions);
                obj.GroupConditionsVersion = GroupFilter.GROUPCONDITIONS_VERSION;
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
                Types[obj.GroupFilterID] = obj.Types;
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

        public List<GroupFilter> GetWithConditionTypesAndAll(HashSet<GroupFilterConditionType> types)
        {
            HashSet<int> filters = new HashSet<int>(Cache.Values.Where(a => a.FilterType == (int)GroupFilterType.All).Select(a=>a.GroupFilterID));
            foreach (GroupFilterConditionType t in types)
            {
                filters.UnionWith(Types.FindInverse(t));
            }
            
            return filters.Select(a => Cache.Get(a)).ToList();
        }

        public List<GroupFilter> GetWithConditionsTypes(HashSet<GroupFilterConditionType> types)
        {
            HashSet<int> filters = new HashSet<int>();
            foreach (GroupFilterConditionType t in types)
            {
                filters.UnionWith(Types.FindInverse(t));
            }
            return filters.Select(a => Cache.Get(a)).ToList();
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
                        Types.Remove(cr.GroupFilterID);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}