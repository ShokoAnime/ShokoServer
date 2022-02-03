using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server.Repositories.Cached
{
    public class GroupFilterRepository : BaseCachedRepository<SVR_GroupFilter, int>
    {
        private PocoIndex<int, SVR_GroupFilter, int> Parents;

        private BiDictionaryManyToMany<int, GroupFilterConditionType> Types;

        private readonly ChangeTracker<int> Changes = new ChangeTracker<int>();

        public List<SVR_GroupFilter> PostProcessFilters { get; set; }

        public GroupFilterRepository()
        {
            EndSaveCallback = obj =>
            {
                Types[obj.GroupFilterID] = obj.Types;
                Changes.AddOrUpdate(obj.GroupFilterID);
            };
            EndDeleteCallback = obj =>
            {
                Types.Remove(obj.GroupFilterID);
                Changes.Remove(obj.GroupFilterID);
            };
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
            const string t = "GroupFilter";
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
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
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, t,
                    Resources.Filter_Recalc + " " + cnt + "/" + max + " - " +
                    gf.GroupFilterName);
                if (gf.GroupsIdsVersion < SVR_GroupFilter.GROUPFILTER_VERSION ||
                    gf.GroupConditionsVersion < SVR_GroupFilter.GROUPCONDITIONS_VERSION ||
                    gf.SeriesIdsVersion < SVR_GroupFilter.SERIEFILTER_VERSION)
                    gf.CalculateGroupsAndSeries();
                Save(gf);
            }

            // Clean up. This will populate empty conditions and remove duplicate filters
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
                t,
                " " + Resources.GroupFilter_Cleanup);
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
            List<SVR_GroupFilter> toremove = GetAll().Where(a => (a.FilterType & (int) GroupFilterType.Directory) != 0)
                .Where(gf => gf.GroupsIdsVersion == SVR_GroupFilter.GROUPFILTER_VERSION &&
                             gf.SeriesIdsVersion == SVR_GroupFilter.SERIEFILTER_VERSION && gf.GroupsIds.Count == 0 &&
                             string.IsNullOrEmpty(gf.GroupsIdsString) && gf.SeriesIds.Count == 0 &&
                             string.IsNullOrEmpty(gf.SeriesIdsString)).ToList();
            if (toremove.Count > 0)
                Delete(toremove);
        }

        public void CreateOrVerifyLockedFilters()
        {
            const string t = "GroupFilter";

            List<SVR_GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();

            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, t,
                " " + Resources.Filter_CreateContinueWatching);

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
                    GroupFilterName = Resources.Filter_All,
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
                    GroupFilterName = Resources.Filter_Tags,
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
                    GroupFilterName = Resources.Filter_Years,
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
                    GroupFilterName = Resources.Filter_Seasons,
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
            HashSet<int> airdate = null, SortedSet<string> seasons = null)
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
                        RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')),
                        StringComparer.InvariantCultureIgnoreCase);
                else
                    alltags = new HashSet<string>(tags,
                        StringComparer.InvariantCultureIgnoreCase);
                HashSet<string> existingTags =
                    new HashSet<string>(
                        lockedGFs.Where(a => a.FilterType == (int) GroupFilterType.Tag)
                            .Select(a => a.Conditions.FirstOrDefault()?.ConditionParameter),
                        StringComparer.InvariantCultureIgnoreCase);
                alltags.ExceptWith(existingTags);

                int max = alltags.Count;
                int cnt = 0;
                //AniDB Tags are in english so we use en-us culture
                TextInfo tinfo = new CultureInfo("en-US", false).TextInfo;
                foreach (string s in alltags)
                {
                    cnt++;
                    if (frominit)
                        ServerState.Instance.ServerStartingStatus = string.Format(
                            Resources.Database_Validating, t,
                            Resources.Filter_CreatingTag + " " +
                            Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
                    SVR_GroupFilter yf = new SVR_GroupFilter
                    {
                        ParentGroupFilterID = tagsdirec.GroupFilterID,
                        InvisibleInClients = 0,
                        ApplyToSeries = 1,
                        GroupFilterName = tinfo.ToTitleCase(s),
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
                        if (endyear <= 0) endyear = DateTime.Today.Year;
                        if (endyear < startyear || endyear - startyear + 1 >= int.MaxValue) endyear = startyear;
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
                        ServerState.Instance.ServerStartingStatus = string.Format(
                            Resources.Database_Validating, t,
                            Resources.Filter_CreatingYear + " " +
                            Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
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
                if (seasons == null)
                {
                    List<SVR_AnimeSeries> grps =
                        RepoFactory.AnimeSeries.GetAll().ToList();

                    allseasons = new SortedSet<string>(new SeasonComparator());
                    foreach (SVR_AnimeSeries ser in grps)
                    {
                        if ((ser?.Contract?.AniDBAnime?.Stat_AllSeasons?.Count ?? 0) == 0) ser?.UpdateContract();
                        if ((ser?.Contract?.AniDBAnime?.Stat_AllSeasons?.Count ?? 0) == 0) continue;
                        allseasons.UnionWith(ser.Contract.AniDBAnime.Stat_AllSeasons);
                    }
                }
                else
                {
                    allseasons = seasons;
                }

                HashSet<string> notin =
                    new HashSet<string>(
                        lockedGFs.Where(a => a.FilterType == (int) GroupFilterType.Season)
                            .Select(a => a.Conditions.FirstOrDefault()?.ConditionParameter),
                        StringComparer.InvariantCultureIgnoreCase);
                allseasons.ExceptWith(notin);
                int max = allseasons.Count;
                int cnt = 0;
                foreach (string season in allseasons)
                {
                    cnt++;
                    if (frominit)
                        ServerState.Instance.ServerStartingStatus = string.Format(
                            Resources.Database_Validating, t,
                            Resources.Filter_CreatingSeason + " " +
                            Resources.Filter_Filter + " " + cnt + "/" + max + " - " + season);
                    SVR_GroupFilter yf = new SVR_GroupFilter
                    {
                        ParentGroupFilterID = seasonsdirectory.GroupFilterID,
                        InvisibleInClients = 0,
                        GroupFilterName = season,
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
                        ConditionParameter = season,
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
                obj.GroupConditions = JsonConvert.SerializeObject(obj._conditions);
                obj.GroupConditionsVersion = SVR_GroupFilter.GROUPCONDITIONS_VERSION;
                base.Save(obj);
                if (resaveConditions)
                {
                    obj.Conditions.ForEach(a => a.GroupFilterID = obj.GroupFilterID);
                    Save(obj, true);
                }
            }
        }

        public override void Save(IReadOnlyCollection<SVR_GroupFilter> objs)
        {
            foreach (var obj in objs)
            {
                Save(obj);
            }
        }

        public override void Delete(IReadOnlyCollection<SVR_GroupFilter> objs)
        {
            foreach (var cr in objs)
            {
                base.Delete(cr);
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
                    {
                        session.Update(groupFilter);
                        Cache.Update(groupFilter);
                        Changes.AddOrUpdate(groupFilter.GroupFilterID);
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
        public void CalculateAnimeSeriesPerTagGroupFilter(ISessionWrapper session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            SVR_GroupFilter tagsdirec = GetAll(session).FirstOrDefault(
                a => a.FilterType == (int) (GroupFilterType.Directory | GroupFilterType.Tag));

            DropAllTagFilters(session);

            // user -> tag -> series
            ConcurrentDictionary<int, ConcurrentDictionary<string, HashSet<int>>> somethingDictionary =
                new ConcurrentDictionary<int, ConcurrentDictionary<string, HashSet<int>>>();
            List<SVR_JMMUser> users = new List<SVR_JMMUser> {null};
            users.AddRange(RepoFactory.JMMUser.GetAll());
            var tags = RepoFactory.AniDB_Tag.GetAll().ToLookup(a => a?.TagName?.ToLowerInvariant());

            Parallel.ForEach(tags, tag =>
            {
                if (tag.Key == null) return;

                foreach (var series in tag.ToList().SelectMany(a => RepoFactory.AniDB_Anime_Tag.GetAnimeWithTag(a.TagID)))
                {
                    var seriesTags = series.GetAnime()?.GetAllTags();
                    foreach (var user in users)
                    {
                        
                        if (user?.GetHideCategories().FindInEnumerable(seriesTags) ?? false)
                            continue;

                        if (somethingDictionary.ContainsKey(user?.JMMUserID ?? 0))
                        {
                            if (somethingDictionary[user?.JMMUserID ?? 0].ContainsKey(tag.Key))
                            {
                                somethingDictionary[user?.JMMUserID ?? 0][tag.Key]
                                    .Add(series.AnimeSeriesID);
                            }
                            else
                            {
                                somethingDictionary[user?.JMMUserID ?? 0].AddOrUpdate(tag.Key,
                                    new HashSet<int> {series.AnimeSeriesID}, (oldTag, oldIDs) =>
                                    {
                                        lock (oldIDs) oldIDs.Add(series.AnimeSeriesID);
                                        return oldIDs;
                                    });
                            }
                        }
                        else
                        {
                            somethingDictionary.AddOrUpdate(user?.JMMUserID ?? 0, id => {
                                var newdict = new ConcurrentDictionary<string, HashSet<int>>();
                                    newdict.AddOrUpdate(tag.Key, new HashSet<int> {series.AnimeSeriesID},
                                        (oldTag, oldIDs) =>
                                        {
                                            lock (oldIDs) oldIDs.Add(series.AnimeSeriesID);
                                            return oldIDs;
                                        });
                                return newdict;
                            },
                            (i, value) =>
                            {
                                if (value.ContainsKey(tag.Key))
                                {
                                    value[tag.Key]
                                        .Add(series.AnimeSeriesID);
                                }
                                else
                                {
                                    value.AddOrUpdate(tag.Key,
                                        new HashSet<int> {series.AnimeSeriesID}, (oldTag, oldIDs) =>
                                        {
                                            lock (oldIDs) oldIDs.Add(series.AnimeSeriesID);
                                            return oldIDs;
                                        });
                                }

                                return value;
                            });
                        }
                    }
                }
            });

            var lookup = somethingDictionary.Keys.Where(a => somethingDictionary[a] != null).ToDictionary(key => key, key =>
                somethingDictionary[key].Where(a => a.Value != null)
                .SelectMany(p => p.Value.Select(a => Tuple.Create(p.Key, a)))
                .ToLookup(p => p.Item1, p => p.Item2));

            CreateAllTagFilters(session, tagsdirec, lookup);
        }

        private void DropAllTagFilters(ISessionWrapper session)
        {
            lock (globalDBLock)
            {
                lock (Cache)
                {
                    ClearCache();
                }

                using (ITransaction trans = session.BeginTransaction())
                {
                    session.CreateQuery("DELETE FROM " + nameof(SVR_GroupFilter) + " WHERE FilterType = " +
                                        (int) GroupFilterType.Tag + ";")
                        .ExecuteUpdate();
                    trans.Commit();
                }
            }
        }

        private void CreateAllTagFilters(ISessionWrapper session, SVR_GroupFilter tagsdirec, Dictionary<int, ILookup<string, int>> lookup)
        {
            if (tagsdirec == null) return;

            HashSet<string> alltags = new HashSet<string>(
                RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')),
                StringComparer.InvariantCultureIgnoreCase);
            List<SVR_GroupFilter> toAdd = new List<SVR_GroupFilter>(alltags.Count);

            var users = RepoFactory.JMMUser.GetAll().ToList();

            //AniDB Tags are in english so we use en-us culture
            TextInfo tinfo = new CultureInfo("en-US", false).TextInfo;
            foreach (string s in alltags)
            {
                SVR_GroupFilter yf = new SVR_GroupFilter
                {
                    ParentGroupFilterID = tagsdirec.GroupFilterID,
                    InvisibleInClients = 0,
                    ApplyToSeries = 1,
                    GroupFilterName = tinfo.ToTitleCase(s),
                    BaseCondition = 1,
                    Locked = 1,
                    SortingCriteria = "5;1",
                    FilterType = (int) GroupFilterType.Tag,
                };
                yf.SeriesIds[0] = lookup[0][s.ToLowerInvariant()].ToHashSet();
                yf.GroupsIds[0] = yf.SeriesIds[0]
                    .Select(id => RepoFactory.AnimeSeries.GetByID(id).TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                    .Where(id => id != -1).ToHashSet();
                foreach (var user in users)
                {
                    var key = s.ToLowerInvariant();
                    // this will happen if you only have porn or none the series you have are allowed by a user 
                    if (!lookup.ContainsKey(user.JMMUserID) || !lookup[user.JMMUserID].Contains(key)) continue;
                    yf.SeriesIds[user.JMMUserID] = lookup[user.JMMUserID][key].ToHashSet();
                    yf.GroupsIds[user.JMMUserID] = yf.SeriesIds[user.JMMUserID]
                        .Select(id => RepoFactory.AnimeSeries.GetByID(id).TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                        .Where(id => id != -1).ToHashSet();
                }

                using (var trans = session.BeginTransaction())
                {
                    // get an ID
                    session.Insert(yf);
                    trans.Commit();
                }

                GroupFilterCondition gfc = new GroupFilterCondition
                {
                    ConditionType = (int) GroupFilterConditionType.Tag,
                    ConditionOperator = (int) GroupFilterOperator.In,
                    ConditionParameter = s,
                    GroupFilterID = yf.GroupFilterID
                };
                yf.Conditions.Add(gfc);
                yf.UpdateEntityReferenceStrings();
                yf.GroupConditions = JsonConvert.SerializeObject(yf._conditions);
                yf.GroupConditionsVersion = SVR_GroupFilter.GROUPCONDITIONS_VERSION;
                toAdd.Add(yf);
            }

            lock (Cache)
            {
                Populate(session, false);
                lock (globalDBLock)
                {
                    foreach (var filters in toAdd.Batch(50))
                    {
                        using (ITransaction trans = session.BeginTransaction())
                        {
                            BatchUpdate(session, filters);
                            trans.Commit();
                        }
                    }
                }
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
