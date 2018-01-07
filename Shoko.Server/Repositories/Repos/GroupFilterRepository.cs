using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class GroupFilterRepository : BaseRepository<SVR_GroupFilter, int, bool>
    {
       
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_GroupFilter, int> Parents;

        private BiDictionaryManyToMany<int, GroupFilterConditionType> Types;

        private readonly Repositories.ChangeTracker<int> Changes = new Repositories.ChangeTracker<int>();

        public List<SVR_GroupFilter> PostProcessFilters { get; set; } = new List<SVR_GroupFilter>();
        internal override object BeginSave(SVR_GroupFilter entity, SVR_GroupFilter original_entity, bool onlyconditions)
        {
            if (!onlyconditions)
                entity.UpdateEntityReferenceStrings();
            entity.GroupConditions = Newtonsoft.Json.JsonConvert.SerializeObject(entity._conditions);
            entity.GroupConditionsVersion = SVR_GroupFilter.GROUPCONDITIONS_VERSION;
            return null;
        }

        internal override void EndSave(SVR_GroupFilter entity, SVR_GroupFilter original_entity, object returnFromBeginSave, bool onlyconditions)
        {
            lock (Types)
            {
                lock (Changes)
                {
                    Types[entity.GroupFilterID] = entity.Types;
                    Changes.AddOrUpdate(entity.GroupFilterID);
                }
            }
        }

        internal override void EndDelete(SVR_GroupFilter entity, object returnFromBeginDelete, bool onlyconditions)
        {
            lock (Types)
            {
                lock (Changes)
                {
                    Types.Remove(entity.GroupFilterID);
                    Changes.Remove(entity.GroupFilterID);
                }
            }
        }

        internal override int SelectKey(SVR_GroupFilter entity) => entity.GroupFilterID;

        internal override void PopulateIndexes()
        {
            Parents = Cache.CreateIndex(a => a.ParentGroupFilterID ?? 0);
            Types = new BiDictionaryManyToMany<int, GroupFilterConditionType>(Cache.Values.ToDictionary(a => a.GroupFilterID, a => a.Types));

        }
        internal override void ClearIndexes()
        {
            Parents = null;
            Types = null;
        }

        //Since groupfilters need to know The ConditionTypes and the db is unable to filter subcollections (not no sql), GroupFilter needs to be always cached.
        internal override RepositoryCache Supports() => RepositoryCache.SupportsCache;

        public override void PostInit(IProgress<InitProgress> progress, int batchSize)
        {
            string t = typeof(GroupFilter).Name;
            InitProgress regen = new InitProgress();
            regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, t, string.Empty);
            regen.Step = 0;
            regen.Total = 0;
            progress.Report(regen);

            foreach (SVR_GroupFilter g in Where(a => a.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                                                     a.GroupConditionsVersion <
                                                     SVR_GroupFilter.GROUPCONDITIONS_VERSION ||
                                                     a.SeriesIdsVersion < SVR_GroupFilter.SERIEFILTER_VERSION))
            {
                if (!PostProcessFilters.Contains(g))
                    PostProcessFilters.Add(g);
            }
            regen.Total = PostProcessFilters.Count;
            regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, t, Commons.Properties.Resources.Filter_Recalc);
            BatchAction(PostProcessFilters, batchSize, (gf, original) =>
            {
                regen.Step++;
                if (gf.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                    gf.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION ||
                    gf.SeriesIdsVersion < SVR_GroupFilter.SERIEFILTER_VERSION ||
                    gf.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION)
                    gf.CalculateGroupsAndSeries();

                progress.Report(regen);
            }, false);
            regen.Total = PostProcessFilters.Count;
            regen.Title = string.Format(Commons.Properties.Resources.Database_Validating,t," " + Commons.Properties.Resources.GroupFilter_Cleanup);

            // Clean up. This will populate empty conditions and remove duplicate filters
            IReadOnlyList<SVR_GroupFilter> all = GetAll();
            HashSet<SVR_GroupFilter> set = new HashSet<SVR_GroupFilter>(all);
            List<SVR_GroupFilter> notin = all.Except(set).ToList();
            Delete(notin);
            PostProcessFilters = null;
            lock (Changes)
            {
                Changes.AddOrUpdateRange(Cache.Keys);
            }
        }



        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            

            string t = typeof(GroupFilter).Name;
            PostProcessFilters = new List<SVR_GroupFilter>();
            InitProgress regen = new InitProgress();
            List<SVR_GroupFilter> filters= Where(a => a.GroupFilterID != 0 && a.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                                                      a.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION).ToList();
            if (filters.Count == 0)
                return;
            regen.Step = 0;
            regen.Total = filters.Count;
            regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, t, " Regen");
            progress.Report(regen);
            BatchAction(filters, batchSize, (gf, original) =>
            {
                if (gf.GroupConditionsVersion == 0)
                    gf.Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(gf.GroupFilterID);
                PostProcessFilters.Add(gf);
                progress.Report(regen);
            }, true);
        }




        public void CleanUpEmptyDirectoryFilters()
        {
            List<SVR_GroupFilter> toremove = GetAll()
                .Where(a => (a.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory).Where(
                    gf => gf.GroupsIds.Count == 0 && string.IsNullOrEmpty(gf.GroupsIdsString) &&
                          gf.SeriesIds.Count == 0 && string.IsNullOrEmpty(gf.SeriesIdsString)).ToList();
            Delete(toremove);
        }

        public void CreateOrVerifyLockedFilters(IProgress<InitProgress> progress, int batchSize)
        {
            string t = typeof(GroupFilter).Name;
            List<SVR_GroupFilter> lockedGFs = GetLockedGroupFilters();
            //Continue Watching
            // check if it already exists

            // TODO Replace with a "Validating Default Filters"
            InitProgress regen = new InitProgress();
            regen.Step = 0;
            regen.Total = 0;
            regen.Title = string.Format(
                Commons.Properties.Resources.Database_Validating, t,
                " " + Commons.Properties.Resources.Filter_CreateContinueWatching);
            progress.Report(regen);


            SVR_GroupFilter cwatching =lockedGFs.FirstOrDefault(
                    a =>
                        a.FilterType == (int) GroupFilterType.ContinueWatching);
            if (cwatching != null && cwatching.FilterType != (int) GroupFilterType.ContinueWatching)
            {
                using (var b = BeginUpdate(cwatching))
                {
                    b.Entity.FilterType = (int)GroupFilterType.ContinueWatching;
                    b.Commit();
                }
            }
            else if (cwatching == null)
            {
                using (var b = BeginAdd())
                {
                    b.Entity.GroupFilterName = Constants.GroupFilterName.ContinueWatching;
                    b.Entity.Locked = 1;
                    b.Entity.SortingCriteria = "4;2"; // by last watched episode desc
                    b.Entity.ApplyToSeries = 0;
                    b.Entity.BaseCondition = 1; // all
                    b.Entity.FilterType = (int) GroupFilterType.ContinueWatching;
                    b.Entity.InvisibleInClients = 0;
                    b.Entity.Conditions = new List<GroupFilterCondition>();
                    GroupFilterCondition gfc = new GroupFilterCondition
                    {
                        ConditionType = (int)GroupFilterConditionType.HasWatchedEpisodes,
                        ConditionOperator = (int)GroupFilterOperator.Include,
                        ConditionParameter = string.Empty,
                        GroupFilterID = b.Entity.GroupFilterID
                    };
                    b.Entity.Conditions.Add(gfc);
                    gfc = new GroupFilterCondition
                    {
                        ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes,
                        ConditionOperator = (int)GroupFilterOperator.Include,
                        ConditionParameter = string.Empty,
                        GroupFilterID = b.Entity.GroupFilterID
                    };
                    b.Entity.Conditions.Add(gfc);
                    b.Entity.CalculateGroupsAndSeries();
                    b.Commit();
                }
            }
            //Create All filter
            SVR_GroupFilter allfilter = lockedGFs.FirstOrDefault(a => a.FilterType == (int) GroupFilterType.All);
            if (allfilter == null)
            {
                using (var b = BeginAdd())
                {
                    b.Entity.GroupFilterName = Commons.Properties.Resources.Filter_All;
                    b.Entity.Locked = 1;
                    b.Entity.InvisibleInClients = 0;
                    b.Entity.FilterType = (int) GroupFilterType.All;
                    b.Entity.BaseCondition = 1;
                    b.Entity.SortingCriteria = "5;1";
                    b.Entity.CalculateGroupsAndSeries();
                    b.Commit();
                }
            }
            SVR_GroupFilter tagsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));
            if (tagsdirec == null)
            {
                using (var b = BeginAdd())
                {
                    b.Entity.GroupFilterName = Commons.Properties.Resources.Filter_Tags;
                    b.Entity.InvisibleInClients = 0;
                    b.Entity.FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Tag);
                    b.Entity.BaseCondition = 1;
                    b.Entity.Locked = 1;
                    b.Entity.SortingCriteria = "13;1";
                    b.Commit();
                }
            }
            SVR_GroupFilter yearsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Year));
            if (yearsdirec == null)
            {
                using (var b = BeginAdd())
                {
                    b.Entity.GroupFilterName = Commons.Properties.Resources.Filter_Years;
                    b.Entity.InvisibleInClients = 0;
                    b.Entity.FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Year);
                    b.Entity.BaseCondition = 1;
                    b.Entity.Locked = 1;
                    b.Entity.SortingCriteria = "13;1";
                    b.Commit();
                }
            }
            SVR_GroupFilter seasonsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Season));
            if (seasonsdirec == null)
            {
                using (var b = BeginAdd())
                {
                    b.Entity.GroupFilterName = Commons.Properties.Resources.Filter_Seasons;
                    b.Entity.InvisibleInClients = 0;
                    b.Entity.FilterType = (int) (GroupFilterType.Directory | GroupFilterType.Season);
                    b.Entity.BaseCondition = 1;
                    b.Entity.Locked = 1;
                    b.Entity.SortingCriteria = "13;1";
                    b.Commit();
                }
            }
            CreateOrVerifyDirectoryFilters(progress, true);
        }

        public void CreateOrVerifyDirectoryFilters(IProgress<InitProgress> progress, bool frominit = false, HashSet<string> tags = null,
            HashSet<int> airdate = null, SortedSet<string> season = null)
        {
            const string t = "GroupFilter";

            List<SVR_GroupFilter> lockedGFs = GetLockedGroupFilters();
            SVR_GroupFilter tagsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));
            if (tagsdirec != null)
            {
                HashSet<string> alltags;
                if (tags == null)
                    alltags = new HashSet<string>(
                        Enumerable.Select<AniDB_Tag, string>(Repo.AniDB_Tag.GetAll(), a => a.TagName)
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
                InitProgress regen = new InitProgress();
                if (frominit)
                {
                    
                    regen.Step = 0;
                    regen.Total = alltags.Count;
                    regen.Title = string.Format(
                        Commons.Properties.Resources.Database_Validating, t,
                        Commons.Properties.Resources.Filter_CreatingTag + " " +
                        Commons.Properties.Resources.Filter_Filter);
                    progress.Report(regen);
                }

                //AniDB Tags are in english so we use en-us culture
                TextInfo tinfo = new CultureInfo("en-US", false).TextInfo;
                foreach (string s in alltags)
                {
                    using (var b = BeginAdd())
                    {
                        b.Entity.ParentGroupFilterID = tagsdirec.GroupFilterID;
                        b.Entity.InvisibleInClients = 0;
                        b.Entity.GroupFilterName = tinfo.ToTitleCase(s.Replace("`", "'"));
                        b.Entity.BaseCondition = 1;
                        b.Entity.Locked = 1;
                        b.Entity.SortingCriteria = "5;1";
                        b.Entity.FilterType = (int) GroupFilterType.Tag;
                        GroupFilterCondition gfc = new GroupFilterCondition
                        {
                            ConditionType = (int)GroupFilterConditionType.Tag,
                            ConditionOperator = (int)GroupFilterOperator.In,
                            ConditionParameter = s,
                            GroupFilterID = b.Entity.GroupFilterID
                        };
                        b.Entity.Conditions.Add(gfc);
                        b.Entity.CalculateGroupsAndSeries();
                        b.Commit();
                    }
                    if (frominit)
                    {
                        regen.Step++;
                        progress.Report(regen);
                    }
                }
            }
            SVR_GroupFilter yearsdirec = lockedGFs.FirstOrDefault(
                a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Year));
            if (yearsdirec != null)
            {
                HashSet<string> allyears;
                if (airdate == null || airdate.Count == 0)
                {
                    List<CL_AnimeSeries_User> grps = Enumerable.Select<SVR_AnimeSeries, CL_AnimeSeries_User>(Repo.AnimeSeries.GetAll(), a => a.Contract).Where(a => a != null).ToList();

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

                InitProgress regen = new InitProgress();
                if (frominit)
                {

                    regen.Step = 0;
                    regen.Total = allyears.Count;
                    regen.Title = string.Format(
                        Commons.Properties.Resources.Database_Validating, t,
                        Commons.Properties.Resources.Filter_CreatingYear + " " +
                        Commons.Properties.Resources.Filter_Filter);
                    progress.Report(regen);
                }

                foreach (string s in allyears)
                {
                    using (var b = BeginAdd())
                    {
                        b.Entity.ParentGroupFilterID = yearsdirec.GroupFilterID;
                        b.Entity.InvisibleInClients = 0;
                        b.Entity.GroupFilterName = s;
                        b.Entity.BaseCondition = 1;
                        b.Entity.Locked = 1;
                        b.Entity.SortingCriteria = "5;1";
                        b.Entity.FilterType = (int) GroupFilterType.Year;
                        b.Entity.ApplyToSeries = 1;
                        GroupFilterCondition gfc = new GroupFilterCondition
                        {
                            ConditionType = (int)GroupFilterConditionType.Year,
                            ConditionOperator = (int)GroupFilterOperator.Include,
                            ConditionParameter = s,
                            GroupFilterID = b.Entity.GroupFilterID
                        };
                        b.Entity.Conditions.Add(gfc);
                        b.Entity.CalculateGroupsAndSeries();
                        b.Commit();
                    }
                    if (frominit)
                    {
                        regen.Step++;
                        progress.Report(regen);
                    }
                }
            }
            SVR_GroupFilter seasonsdirectory = lockedGFs.FirstOrDefault(a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Season));
            if (seasonsdirectory != null)
            {
                SortedSet<string> allseasons;
                if (season == null)
                {
                    List<SVR_AnimeSeries> grps = Enumerable.ToList<SVR_AnimeSeries>(Repo.AnimeSeries.GetAll());

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


                InitProgress regen = new InitProgress();
                if (frominit)
                {

                    regen.Step = 0;
                    regen.Total = allseasons.Count;
                    regen.Title = string.Format(string.Format(
                        Commons.Properties.Resources.Database_Validating, t,
                        Commons.Properties.Resources.Filter_CreatingSeason + " " +
                        Commons.Properties.Resources.Filter_Filter));
                    progress.Report(regen);
                }

                foreach (string s in allseasons)
                {
                    using (var b = BeginAdd())
                    {
                        b.Entity.ParentGroupFilterID = seasonsdirectory.GroupFilterID;
                        b.Entity.InvisibleInClients = 0;
                        b.Entity.GroupFilterName = s;
                        b.Entity.BaseCondition = 1;
                        b.Entity.Locked = 1;
                        b.Entity.SortingCriteria = "5;1";
                        b.Entity.FilterType = (int) GroupFilterType.Season;
                        b.Entity.ApplyToSeries = 1;
                        GroupFilterCondition gfc = new GroupFilterCondition
                        {
                            ConditionType = (int)GroupFilterConditionType.Season,
                            ConditionOperator = (int)GroupFilterOperator.In,
                            ConditionParameter = s,
                            GroupFilterID = b.Entity.GroupFilterID
                        };
                        b.Entity.Conditions.Add(gfc);
                        b.Entity.CalculateGroupsAndSeries();
                        b.Commit();
                    }
                    if (frominit)
                    {
                        regen.Step++;
                        regen.Title = string.Format(string.Format(
                            Commons.Properties.Resources.Database_Validating, t,
                            Commons.Properties.Resources.Filter_CreatingSeason + " " +
                            Commons.Properties.Resources.Filter_Filter + " " + s));
                       progress.Report(regen);
                    }

                 
                }
            }
            CleanUpEmptyDirectoryFilters();
        }

        /*
        //TODO DBRefactor
    
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
        */
        public List<SVR_GroupFilter> GetByParentID(int parentid)
        {
            return Parents.GetMultiple(parentid);
        }

        public List<SVR_GroupFilter> GetTopLevel()
        {
            return Parents.GetMultiple(0);
        }

        /// <summary>
        /// Calculates what groups should belong to tag related group filters.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <returns>A <see cref="ILookup{TKey,TElement}"/> that maps group filter ID to anime group IDs.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public Dictionary<int, List<int>> CalculateAnimeGroupsPerTagGroupFilter()
        {
            Dictionary<string, List<int>> alltags = Queryable.GroupBy<AniDB_Tag, string>(Repo.AniDB_Tag.WhereAll(), a => a.TagName).ToDictionary(a => a.Key, a => a.Select(b => b.TagID).ToList());
            Dictionary<int, List<int>> allanimetags = Queryable.GroupBy<AniDB_Anime_Tag, int>(Repo.AniDB_Anime_Tag.WhereAll(), a => a.TagID).ToDictionary(a => a.Key, a => a.Select(b => b.AnimeID).ToList());
            Dictionary<int, List<int>> allgroups = Queryable.GroupBy<SVR_AnimeSeries, int>(Repo.AnimeSeries.WhereAll(), a=>a.AniDB_ID).ToDictionary(a=>a.Key,a=>a.Select(b=>b.AnimeGroupID).ToList());
            Dictionary<int, string> filters = WhereAll().Where(a => a.FilterType == (int) GroupFilterType.Tag).ToDictionary(a => a.GroupFilterID, a => a.GroupFilterName);
            return filters.ToDictionary(a => a.Key, a => alltags.SafeGetList(a.Value).SelectMany(b => allanimetags.SafeGetList(b).SelectMany(c => allgroups.SafeGetList(c))).ToList());
        }

        public List<SVR_GroupFilter> GetLockedGroupFilters()
        {
            return GetAll().Where(a => a.Locked == 1).ToList();
        }

        public List<SVR_GroupFilter> GetWithConditionTypesAndAll(HashSet<GroupFilterConditionType> types)
        {
            HashSet<int> filters = new HashSet<int>(GetAll().Where(a => a.FilterType == (int) GroupFilterType.All).Select(a => a.GroupFilterID));
            foreach (GroupFilterConditionType t in types)
                filters.UnionWith(Types.FindInverse(t));
            return filters.Select(a => Cache.Get(a)).ToList();
        }

        public List<SVR_GroupFilter> GetWithConditionsTypes(HashSet<GroupFilterConditionType> types)
        {
            HashSet<int> filters = new HashSet<int>();
            foreach (GroupFilterConditionType t in types)
                filters.UnionWith(Types.FindInverse(t));
            return filters.Select(a => Cache.Get(a)).ToList();
        }

        public Repositories.ChangeTracker<int> GetChangeTracker()
        {

            //This lock ensures.....?
            lock (Changes)
            {
                return Changes;
            }
        }
    }
}