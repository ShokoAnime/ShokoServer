using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Shoko.Models.Server;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached
{
    public class GroupFilterRepository : BaseCachedRepository<SVR_GroupFilter, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_GroupFilter, int> Parents;

        private BiDictionaryManyToMany<int, GroupFilterConditionType> Types;

        private ChangeTracker<int> Changes = new ChangeTracker<int>();

        public List<SVR_GroupFilter> PostProcessFilters { get; set; } = new List<SVR_GroupFilter>();

        private GroupFilterRepository()
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

        public static GroupFilterRepository Create()
        {
            return new GroupFilterRepository();
        }

        protected override int SelectKey(SVR_GroupFilter entity)
        {
            return entity.GroupFilterID;
        }

        public override void PopulateIndexes()
        {
            Changes.AddOrUpdateRange(Cache.Keys);
            Parents = Cache.CreateIndex(a => a.ParentGroupFilterID ?? 0);
            Types =
                new BiDictionaryManyToMany<int, GroupFilterConditionType>(
                    Cache.Values.ToDictionary(a => a.GroupFilterID, a => a.Types));
            PostProcessFilters = new List<SVR_GroupFilter>();
        }

        public override void RegenerateDb()
        {
            foreach (SVR_GroupFilter g in Cache.Values.Where(g =>
                g.GroupFilterID != 0 && g.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                g.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION).ToList())
            {
                if (g.GroupConditionsVersion == 0)
                    g.Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(g.GroupFilterID);
                Save(g, true);
                PostProcessFilters.Add(g);
            }
        }


        public override void PostProcess()
        {
            string t = "GroupFilter";
            ServerState.Instance.CurrentSetupStatus = string.Format(Commons.Properties.Resources.Database_Validating,
                t, string.Empty);
            foreach (SVR_GroupFilter g in Cache.Values.ToList())
                if (g.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                    g.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION ||
                    g.SeriesIdsVersion < SVR_GroupFilter.SERIEFILTER_VERSION)
                    if (!PostProcessFilters.Contains(g))
                        PostProcessFilters.Add(g);
            int max = PostProcessFilters.Count;
            int cnt = 0;
            foreach (SVR_GroupFilter gf in PostProcessFilters)
            {
                cnt++;
                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Validating, t,
                    Commons.Properties.Resources.Filter_Recalc + " " + cnt + "/" + max + " - " +
                    gf.GroupFilterName);
                if (gf.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                    gf.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION ||
                    gf.SeriesIdsVersion < SVR_GroupFilter.SERIEFILTER_VERSION ||
                    gf.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION)
                    gf.CalculateGroupsAndSeries();
                Save(gf);
            }

            // Clean up. This will populate empty conditions and remove duplicate filters
            ServerState.Instance.CurrentSetupStatus = string.Format(Commons.Properties.Resources.Database_Validating,
                t,
                " " + Commons.Properties.Resources.GroupFilter_Cleanup);
            IReadOnlyList<SVR_GroupFilter> all = GetAll();
            HashSet<SVR_GroupFilter> set = new HashSet<SVR_GroupFilter>(all);
            List<SVR_GroupFilter> notin = all.Except(set).ToList();
            Delete(notin);

            // Remove orphaned group filter conditions
            List<GroupFilterCondition> toremove = RepoFactory.GroupFilterCondition.GetAll().ToList()
                .Where(condition => RepoFactory.GroupFilter.GetByID(condition.GroupFilterID) == null).ToList();
            RepoFactory.GroupFilterCondition.Delete(toremove);

            PostProcessFilters = null;
        }

        public void CleanUpEmptyDirectoryFilters()
        {
            List<SVR_GroupFilter> toremove = GetAll()
                .Where(a => (a.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory).Where(
                    gf => gf.GroupsIds.Count == 0 && string.IsNullOrEmpty(gf.GroupsIdsString) &&
                          gf.SeriesIds.Count == 0 && string.IsNullOrEmpty(gf.SeriesIdsString)).ToList();
            Delete(toremove);
        }

        public void CreateOrVerifyLockedFilters()
        {
            string t = "GroupFilter";

            List<SVR_GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
            //Continue Watching
            // check if it already exists

            // TODO Replace with a "Validating Default Filters"
            ServerState.Instance.CurrentSetupStatus = string.Format(
                Commons.Properties.Resources.Database_Validating, t,
                " " + Commons.Properties.Resources.Filter_CreateContinueWatching);

            SVR_GroupFilter cwatching =
                lockedGFs.FirstOrDefault(
                    a =>
                        a.FilterType == (int) GroupFilterType.ContinueWatching);
            if (cwatching != null && cwatching.FilterType != (int) GroupFilterType.ContinueWatching)
            {
                cwatching.FilterType = (int) GroupFilterType.ContinueWatching;
                Save(cwatching);
            }
            else if (cwatching == null)
            {
                SVR_GroupFilter gf = new SVR_GroupFilter
                {
                    GroupFilterName = Constants.GroupFilterName.ContinueWatching,
                    Locked = 1,
                    SortingCriteria = "4;2", // by last watched episode desc
                    ApplyToSeries = 0,
                    BaseCondition = 1, // all
                    FilterType = (int) GroupFilterType.ContinueWatching,
                    InvisibleInClients = 0,
                    Conditions = new List<GroupFilterCondition>()
                };
                GroupFilterCondition gfc = new GroupFilterCondition
                {
                    ConditionType = (int) GroupFilterConditionType.HasWatchedEpisodes,
                    ConditionOperator = (int) GroupFilterOperator.Include,
                    ConditionParameter = string.Empty,
                    GroupFilterID = gf.GroupFilterID
                };
                gf.Conditions.Add(gfc);
                gfc = new GroupFilterCondition
                {
                    ConditionType = (int) GroupFilterConditionType.HasUnwatchedEpisodes,
                    ConditionOperator = (int) GroupFilterOperator.Include,
                    ConditionParameter = string.Empty,
                    GroupFilterID = gf.GroupFilterID
                };
                gf.Conditions.Add(gfc);
                gf.CalculateGroupsAndSeries();
                Save(gf); //Get ID
            }
            //Create All filter
            SVR_GroupFilter allfilter = lockedGFs.FirstOrDefault(a => a.FilterType == (int) GroupFilterType.All);
            if (allfilter == null)
            {
                SVR_GroupFilter gf = new SVR_GroupFilter
                {
                    GroupFilterName = Commons.Properties.Resources.Filter_All,
                    Locked = 1,
                    InvisibleInClients = 0,
                    FilterType = (int) GroupFilterType.All,
                    BaseCondition = 1,
                    SortingCriteria = "5;1"
                };
                gf.CalculateGroupsAndSeries();
                Save(gf);
            }
            SVR_GroupFilter tagsdirec =
                lockedGFs.FirstOrDefault(
                    a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));
            if (tagsdirec == null)
            {
                tagsdirec = new SVR_GroupFilter
                {
                    GroupFilterName = Commons.Properties.Resources.Filter_Tags,
                    InvisibleInClients = 0,
                    FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Tag),
                    BaseCondition = 1,
                    Locked = 1,
                    SortingCriteria = "13;1"
                };
                Save(tagsdirec);
            }
            SVR_GroupFilter yearsdirec =
                lockedGFs.FirstOrDefault(
                    a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Year));
            if (yearsdirec == null)
            {
                yearsdirec = new SVR_GroupFilter
                {
                    GroupFilterName = Commons.Properties.Resources.Filter_Years,
                    InvisibleInClients = 0,
                    FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Year),
                    BaseCondition = 1,
                    Locked = 1,
                    SortingCriteria = "13;1"
                };
                Save(yearsdirec);
            }
            SVR_GroupFilter seasonsdirec =
                lockedGFs.FirstOrDefault(
                    a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Season));
            if (seasonsdirec == null)
            {
                seasonsdirec = new SVR_GroupFilter
                {
                    GroupFilterName = Commons.Properties.Resources.Filter_Seasons,
                    InvisibleInClients = 0,
                    FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Season),
                    BaseCondition = 1,
                    Locked = 1,
                    SortingCriteria = "13;1"
                };
                Save(seasonsdirec);
            }
            CreateOrVerifyDirectoryFilters(true);
        }

        public void CreateOrVerifyDirectoryFilters(bool frominit = false, HashSet<string> tags = null,
            HashSet<int> airdate = null, SortedSet<string> season = null)
        {
            const string t = "GroupFilter";

            List<SVR_GroupFilter> lockedGFs = GetLockedGroupFilters();


            SVR_GroupFilter tagsdirec = lockedGFs.FirstOrDefault(
                a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));
            if (tagsdirec != null)
            {
                HashSet<string> alltags;
                if (tags == null)
                    alltags = new HashSet<string>(
                        RepoFactory.AniDB_Tag.GetAll()
                            .Select(a => a.TagName)
                            .Distinct(StringComparer.InvariantCultureIgnoreCase),
                        StringComparer.InvariantCultureIgnoreCase);
                else
                    alltags = new HashSet<string>(tags.Distinct(StringComparer.InvariantCultureIgnoreCase),
                        StringComparer.InvariantCultureIgnoreCase);
                HashSet<string> notin =
                    new HashSet<string>(
                        lockedGFs.Where(a => a.FilterType == (int) GroupFilterType.Tag)
                            .Select(a => a.Conditions.FirstOrDefault()?.ConditionParameter),
                        StringComparer.InvariantCultureIgnoreCase);
                alltags.ExceptWith(notin);

                int max = alltags.Count;
                int cnt = 0;
                //AniDB Tags are in english so we use en-us culture
                TextInfo tinfo = new CultureInfo("en-US", false).TextInfo;
                foreach (string s in alltags)
                {
                    cnt++;
                    if (frominit)
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Validating, t,
                            Commons.Properties.Resources.Filter_CreatingTag + " " +
                            Commons.Properties.Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
                    SVR_GroupFilter yf = new SVR_GroupFilter
                    {
                        ParentGroupFilterID = tagsdirec.GroupFilterID,
                        InvisibleInClients = 0,
                        GroupFilterName = tinfo.ToTitleCase(s.Replace("`", "'")),
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "5;1",
                        FilterType = (int) GroupFilterType.Tag
                    };
                    GroupFilterCondition gfc = new GroupFilterCondition
                    {
                        ConditionType = (int) GroupFilterConditionType.Tag,
                        ConditionOperator = (int) GroupFilterOperator.In,
                        ConditionParameter = s,
                        GroupFilterID = yf.GroupFilterID
                    };
                    yf.Conditions.Add(gfc);
                    yf.CalculateGroupsAndSeries();
                    Save(yf);
                }
            }
            SVR_GroupFilter yearsdirec = lockedGFs.FirstOrDefault(
                a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Year));
            if (yearsdirec != null)
            {
                HashSet<string> allyears;
                if (airdate == null || airdate.Count == 0)
                {
                    List<CL_AnimeSeries_User> grps =
                        RepoFactory.AnimeSeries.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();

                    allyears = new HashSet<string>(StringComparer.Ordinal);
                    foreach (CL_AnimeSeries_User ser in grps)
                    {
                        int endyear = ser.AniDBAnime.AniDBAnime.EndYear;
                        int startyear = ser.AniDBAnime.AniDBAnime.BeginYear;
                        if (endyear == 0) endyear = DateTime.Today.Year;
                        if (startyear != 0)
                            allyears.UnionWith(Enumerable.Range(startyear,
                                    endyear - startyear + 1)
                                .Select(a => a.ToString()));
                    }
                }
                else
                {
                    allyears = new HashSet<string>(airdate.Select(a => a.ToString()), StringComparer.Ordinal);
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
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Validating, t,
                            Commons.Properties.Resources.Filter_CreatingYear + " " +
                            Commons.Properties.Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
                    SVR_GroupFilter yf = new SVR_GroupFilter
                    {
                        ParentGroupFilterID = yearsdirec.GroupFilterID,
                        InvisibleInClients = 0,
                        GroupFilterName = s,
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "5;1",
                        FilterType = (int) GroupFilterType.Year,
                        ApplyToSeries = 1
                    };
                    GroupFilterCondition gfc = new GroupFilterCondition
                    {
                        ConditionType = (int) GroupFilterConditionType.Year,
                        ConditionOperator = (int) GroupFilterOperator.Include,
                        ConditionParameter = s,
                        GroupFilterID = yf.GroupFilterID
                    };
                    yf.Conditions.Add(gfc);
                    yf.CalculateGroupsAndSeries();
                    Save(yf);
                }
            }
            SVR_GroupFilter seasonsdirectory = lockedGFs.FirstOrDefault(
                a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Season));
            if (seasonsdirectory != null)
            {
                SortedSet<string> allseasons;
                if (season == null)
                {
                    List<SVR_AnimeSeries> grps =
                        RepoFactory.AnimeSeries.GetAll().ToList();

                    allseasons = new SortedSet<string>(new SeasonComparator());
                    foreach (SVR_AnimeSeries ser in grps)
                    {
                        if ((ser.Contract?.AniDBAnime?.Stat_AllSeasons.Count ?? 0) == 0)
                        {
                            ser.UpdateContract();
                        }
                        if ((ser.Contract?.AniDBAnime?.Stat_AllSeasons.Count ?? 0) == 0) continue;
                        allseasons.UnionWith(ser.Contract.AniDBAnime.Stat_AllSeasons);
                    }
                }
                else
                {
                    allseasons = season;
                }
                HashSet<string> notin =
                    new HashSet<string>(
                        lockedGFs.Where(a => a.FilterType == (int) GroupFilterType.Season)
                            .Select(a => a.Conditions.FirstOrDefault()?.ConditionParameter),
                        StringComparer.InvariantCultureIgnoreCase);
                allseasons.ExceptWith(notin);
                int max = allseasons.Count;
                int cnt = 0;
                foreach (string s in allseasons)
                {
                    cnt++;
                    if (frominit)
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Validating, t,
                            Commons.Properties.Resources.Filter_CreatingSeason + " " +
                            Commons.Properties.Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
                    SVR_GroupFilter yf = new SVR_GroupFilter
                    {
                        ParentGroupFilterID = seasonsdirectory.GroupFilterID,
                        InvisibleInClients = 0,
                        GroupFilterName = s,
                        BaseCondition = 1,
                        Locked = 1,
                        SortingCriteria = "5;1",
                        FilterType = (int) GroupFilterType.Season,
                        ApplyToSeries = 1
                    };
                    GroupFilterCondition gfc = new GroupFilterCondition
                    {
                        ConditionType = (int) GroupFilterConditionType.Season,
                        ConditionOperator = (int) GroupFilterOperator.In,
                        ConditionParameter = s,
                        GroupFilterID = yf.GroupFilterID
                    };
                    yf.Conditions.Add(gfc);
                    yf.CalculateGroupsAndSeries();
                    Save(yf);
                }
            }
            CleanUpEmptyDirectoryFilters();
        }

        public override void Save(SVR_GroupFilter obj)
        {
            Save(obj, false);
        }

        public void Save(SVR_GroupFilter obj, bool onlyconditions)
        {
            lock (obj)
            {
                if (!onlyconditions)
                    obj.UpdateEntityReferenceStrings();
                bool resaveConditions = obj.GroupFilterID == 0;
                obj.GroupConditions = Newtonsoft.Json.JsonConvert.SerializeObject(obj._conditions);
                obj.GroupConditionsVersion = SVR_GroupFilter.GROUPCONDITIONS_VERSION;
                base.Save(obj);
                if (resaveConditions)
                {
                    obj.Conditions.ForEach(a => a.GroupFilterID = obj.GroupFilterID);
                    Save(obj, true);
                }
            }
        }

        /// <summary>
        /// Updates a batch of <see cref="SVR_GroupFilter"/>s.
        /// </summary>
        /// <remarks>
        /// This method ONLY updates existing <see cref="SVR_GroupFilter"/>s. It will not insert any that don't already exist.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="groupFilters">The batch of <see cref="SVR_GroupFilter"/>s to update.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="groupFilters"/> is <c>null</c>.</exception>
        public void BatchUpdate(ISessionWrapper session, IEnumerable<SVR_GroupFilter> groupFilters)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groupFilters == null)
                throw new ArgumentNullException(nameof(groupFilters));

            lock (globalDBLock)
            {
                lock (Cache)
                {
                    foreach (SVR_GroupFilter groupFilter in groupFilters)
                        lock (groupFilter)
                        {
                            session.Update(groupFilter);
                            Cache.Update(groupFilter);
                        }
                }
            }
        }

        public List<SVR_GroupFilter> GetByParentID(int parentid)
        {
            lock (Cache)
            {
                return Parents.GetMultiple(parentid);
            }
        }

        public List<SVR_GroupFilter> GetTopLevel()
        {
            lock (Cache)
            {
                return Parents.GetMultiple(0);
            }
        }

        /// <summary>
        /// Calculates what groups should belong to tag related group filters.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <returns>A <see cref="ILookup{TKey,TElement}"/> that maps group filter ID to anime group IDs.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public ILookup<int, int> CalculateAnimeGroupsPerTagGroupFilter(ISessionWrapper session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            lock (globalDBLock)
            {
                var groupsByFilter = session.CreateSQLQuery(@"
                SELECT DISTINCT grpFilter.GroupFilterID, grp.AnimeGroupID
                    FROM AnimeGroup grp
                        INNER JOIN AnimeSeries series
                            ON series.AnimeGroupID = grp.AnimeGroupID
                        INNER JOIN AniDB_Anime_Tag anidbTag
                            ON anidbTag.AnimeID = series.AniDB_ID
                        INNER JOIN AniDB_Tag tag
                            ON tag.TagID = anidbTag.TagID
                        INNER JOIN GroupFilter grpFilter
                            ON grpFilter.GroupFilterName = tag.TagName
                                AND grpFilter.FilterType = :tagType
                    ORDER BY grpFilter.GroupFilterID, grp.AnimeGroupID")
                    .AddScalar("GroupFilterID", NHibernateUtil.Int32)
                    .AddScalar("AnimeGroupID", NHibernateUtil.Int32)
                    .SetInt32("tagType", (int) GroupFilterType.Tag)
                    .List<object[]>()
                    .ToLookup(r => (int) r[0], r => (int) r[1]);

                return groupsByFilter;
            }
        }

        public List<SVR_GroupFilter> GetLockedGroupFilters()
        {
            lock (Cache)
            {
                return Cache.Values.Where(a => a.Locked == 1).ToList();
            }
        }

        public List<SVR_GroupFilter> GetWithConditionTypesAndAll(HashSet<GroupFilterConditionType> types)
        {
            lock (Cache)
            {
                HashSet<int> filters = new HashSet<int>(Cache.Values
                    .Where(a => a.FilterType == (int) GroupFilterType.All)
                    .Select(a => a.GroupFilterID));
                foreach (GroupFilterConditionType t in types)
                    filters.UnionWith(Types.FindInverse(t));

                return filters.Select(a => Cache.Get(a)).ToList();
            }
        }

        public List<SVR_GroupFilter> GetWithConditionsTypes(HashSet<GroupFilterConditionType> types)
        {
            lock (Cache)
            {
                HashSet<int> filters = new HashSet<int>();
                foreach (GroupFilterConditionType t in types)
                    filters.UnionWith(Types.FindInverse(t));
                return filters.Select(a => Cache.Get(a)).ToList();
            }
        }

        public ChangeTracker<int> GetChangeTracker()
        {
            return Changes;
        }
    }
}