using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using JMMContracts;
using JMMServer.Entities;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class GroupFilterRepository : BaseCachedRepository<GroupFilter, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, GroupFilter, int> Parents;

        private BiDictionaryManyToMany<int, GroupFilterConditionType> Types;

        private ChangeTracker<int> Changes=new ChangeTracker<int>();

        public List<GroupFilter> PostProcessFilters { get; set; }=new List<GroupFilter>();

        public GroupFilterRepository()
        {
            EndSaveCallback = (obj) =>
                            {
                                Types[obj.GroupFilterID] = obj.Types;
                                Changes.AddOrUpdate(obj.GroupFilterID);
                            };
            EndDeleteCallback = (obj) =>
                            {
                                Types.Remove(obj.GroupFilterID);
                                Changes.Remove(obj.GroupFilterID);
                            };
        }


        public override void PopulateIndexes()
        {
            Changes.AddOrUpdateRange(Cache.Keys);
            Parents = Cache.CreateIndex(a => a.ParentGroupFilterID ?? 0);
            Types = new BiDictionaryManyToMany<int, GroupFilterConditionType>(Cache.Values.ToDictionary(a => a.GroupFilterID, a => a.Types));
        }

        public override void RegenerateDb()
        {
            PostProcessFilters = new List<GroupFilter>();
            foreach (GroupFilter g in Cache.Values.ToList())
            {
                if (g.GroupFilterID != 0 && g.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION ||
                    g.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION)
                {
                    if (g.GroupConditionsVersion == 0)
                    {
                        g.Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(g.GroupFilterID);
                    }
                    Save(g, true);
                    PostProcessFilters.Add(g);
                }
            }
        }



        public void PostProcess()
        {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            foreach (GroupFilter g in Cache.Values.ToList())
            {
                if (g.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION ||
                    g.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION ||
                    g.SeriesIdsVersion < GroupFilter.SERIEFILTER_VERSION)
                {
                    if (!PostProcessFilters.Contains(g))
                        PostProcessFilters.Add(g);
                }
            }
            int max = PostProcessFilters.Count;
            int cnt = 0;
            foreach (GroupFilter gf in PostProcessFilters)
            {
                cnt++;
                ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                    Properties.Resources.Filter_Recalc + " " + gf.GroupFilterName + " - " + cnt + "/" + max);
                if (gf.GroupsIdsVersion < GroupFilter.GROUPFILTER_VERSION ||
                    gf.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION)
                    gf.EvaluateAnimeGroups();
                if (gf.SeriesIdsVersion < GroupFilter.SERIEFILTER_VERSION ||
                    gf.GroupConditionsVersion < GroupFilter.GROUPCONDITIONS_VERSION)
                    gf.EvaluateAnimeSeries();
                Save(gf);
            }
            PostProcessFilters = null;
        }


        //TODO Cleanup function for Empty Tags and Empty Years

        

        public void CreateOrVerifyLockedFilters()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                string t = "GroupFilter";

                List<GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
                //Continue Watching
                // check if it already exists

                ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, " " + Properties.Resources.Filter_CreateContinueWatching);

                GroupFilter cwatching =
                    lockedGFs.FirstOrDefault(
                        a =>
                            a.FilterType == (int)GroupFilterType.ContinueWatching);
                if (cwatching != null && cwatching.FilterType != (int) GroupFilterType.ContinueWatching)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, " " + Properties.Resources.Filter_CreateContinueWatching);
                    cwatching.FilterType = (int) GroupFilterType.ContinueWatching;
                    Save(cwatching);
                }
                else if (cwatching == null)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, " " + Properties.Resources.Filter_CreateContinueWatching);
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
                    Save(gf); //Get ID
                }
                //Create All filter
                GroupFilter allfilter = lockedGFs.FirstOrDefault(a => a.FilterType == (int) GroupFilterType.All);
                if (allfilter == null)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, " " + Properties.Resources.Filter_CreateAll);
                    GroupFilter gf = new GroupFilter
                    {
                        GroupFilterName = Properties.Resources.Filter_All,
                        Locked = 1,
                        InvisibleInClients = 0,
                        FilterType = (int) GroupFilterType.All,
                        BaseCondition = 1,
                        SortingCriteria = "5;1"
                    };
                    gf.EvaluateAnimeGroups();
                    gf.EvaluateAnimeSeries();
                    Save(gf);
                }
                GroupFilter tagsdirec =
                    lockedGFs.FirstOrDefault(
                        a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));
                if (tagsdirec == null)
                {
                    tagsdirec = new GroupFilter
                    {
                        GroupFilterName = Properties.Resources.Filter_Tags,
                        InvisibleInClients = 0,
                        FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Tag),
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "13;1"
                    };
                    Save(tagsdirec);
                }
                GroupFilter yearsdirec =
                    lockedGFs.FirstOrDefault(
                        a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Year));
                if (yearsdirec == null)
                {
                    yearsdirec = new GroupFilter
                    {
                        GroupFilterName = Properties.Resources.Filter_Years,
                        InvisibleInClients = 0,
                        FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Year),
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "13;1"
                    };
                    Save(yearsdirec);
                }
            }
            CreateOrVerifyTagsAndYearsFilters(true);
        }
        public void CreateOrVerifyTagsAndYearsFilters(bool frominit = false, HashSet<string> tags = null, DateTime? airdate = null)
        {

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                string t = "GroupFilter";

                List<GroupFilter> lockedGFs = GetLockedGroupFilters();
         

                GroupFilter tagsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Tag));
                if (tagsdirec != null)
                {
                    HashSet<string> alltags;
                    if (tags == null)
                        alltags = new HashSet<string>(RepoFactory.AniDB_Tag.GetAll().Select(a => a.TagName).Distinct(StringComparer.InvariantCultureIgnoreCase),StringComparer.InvariantCultureIgnoreCase);
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
                            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                                Properties.Resources.Filter_CreatingTag + " '" + s + "'" + Properties.Resources.Filter_Filter + cnt + "/" + max);
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
                        Save(yf);
                    }
                }
                GroupFilter yearsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Year));
                if (yearsdirec != null)
                {

                    HashSet<string> allyears;
                    if (airdate == null)
                    {
                        List<Contract_AnimeGroup> grps =
                            RepoFactory.AnimeGroup.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
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
                            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                                Properties.Resources.Filter_CreatingYear + " '" + s + "'  " + Properties.Resources.Filter_Filter + cnt + "/" + max);
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
                        Save(yf);
                    }
                }
            }
        }

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(List<GroupFilter> objs) { throw new NotSupportedException(); }

        public override void Save(GroupFilter obj)
        {
            Save(obj, false);
        }

        public void Save(GroupFilter obj, bool onlyconditions)
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
                base.Save(obj);
            }
        }


        public List<GroupFilter> GetByParentID(int parentid)
        {
            return Parents.GetMultiple(parentid);
        }

        public List<GroupFilter> GetTopLevel()
        {
            return Parents.GetMultiple(0);
        }



        public List<GroupFilter> GetLockedGroupFilters()
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

        public ChangeTracker<int> GetChangeTracker()
        {
            return Changes;
        }
    }
}