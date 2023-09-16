using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server.Repositories.Cached;

public class GroupFilterRepository : BaseCachedRepository<SVR_GroupFilter, int>
{
    private PocoIndex<int, SVR_GroupFilter, int> Parents;

    private BiDictionaryManyToMany<int, GroupFilterConditionType> Types;

    private readonly ChangeTracker<int> Changes = new();

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

    public override void RegenerateDb() { }


    public override void PostProcess() { }

    public void CleanUpEmptyDirectoryFilters()
    {
        var toremove = GetAll().Where(a => (a.FilterType & (int)GroupFilterType.Directory) != 0)
            .Where(gf => gf.GroupsIdsVersion == SVR_GroupFilter.GROUPFILTER_VERSION &&
                         gf.SeriesIdsVersion == SVR_GroupFilter.SERIEFILTER_VERSION && gf.GroupsIds.Count == 0 &&
                         string.IsNullOrEmpty(gf.GroupsIdsString) && gf.SeriesIds.Count == 0 &&
                         string.IsNullOrEmpty(gf.SeriesIdsString)).ToList();
        if (toremove.Count > 0)
        {
            Delete(toremove);
        }
    }

    public void CreateOrVerifyLockedFilters() { }

    public override void Save(SVR_GroupFilter obj)
    {
        Save(obj, false);
    }

    public void Save(SVR_GroupFilter obj, bool onlyconditions)
    {
        WriteLock(
            () =>
            {
                if (!onlyconditions)
                {
                    obj.UpdateEntityReferenceStrings();
                }

                var resaveConditions = obj.GroupFilterID == 0;
                obj.GroupConditions = JsonConvert.SerializeObject(obj._conditions);
                obj.GroupConditionsVersion = SVR_GroupFilter.GROUPCONDITIONS_VERSION;
                base.Save(obj);
                if (resaveConditions)
                {
                    obj.Conditions.ForEach(a => a.GroupFilterID = obj.GroupFilterID);
                    Save(obj, true);
                }
            }
        );
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
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (groupFilters == null)
        {
            throw new ArgumentNullException(nameof(groupFilters));
        }

        foreach (var groupFilter in groupFilters)
        {
            session.Update(groupFilter);
            UpdateCache(groupFilter);
            Changes.AddOrUpdate(groupFilter.GroupFilterID);
        }
    }

    public List<SVR_GroupFilter> GetByParentID(int parentid)
    {
        return ReadLock(() => Parents.GetMultiple(parentid));
    }

    public List<SVR_GroupFilter> GetTopLevel()
    {
        return GetByParentID(0);
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
        {
            throw new ArgumentNullException(nameof(session));
        }

        var tagsdirec = GetAll(session).FirstOrDefault(
            a => a.FilterType == (int)(GroupFilterType.Directory | GroupFilterType.Tag));

        DropAllTagFilters(session);

        // user -> tag -> series
        var somethingDictionary =
            new ConcurrentDictionary<int, ConcurrentDictionary<string, HashSet<int>>>();
        var users = new List<SVR_JMMUser> { null };
        users.AddRange(RepoFactory.JMMUser.GetAll());
        var tags = RepoFactory.AniDB_Tag.GetAll().ToLookup(a => a?.TagName?.ToLowerInvariant());

        Parallel.ForEach(tags, tag =>
        {
            foreach (var series in tag.ToList().SelectMany(a => RepoFactory.AniDB_Anime_Tag.GetAnimeWithTag(a.TagID)))
            {
                var seriesTags = series.GetAnime()?.GetAllTags();
                foreach (var user in users)
                {
                    if (user?.GetHideCategories().FindInEnumerable(seriesTags) ?? false)
                    {
                        continue;
                    }

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
                                new HashSet<int> { series.AnimeSeriesID }, (oldTag, oldIDs) =>
                                {
                                    lock (oldIDs)
                                    {
                                        oldIDs.Add(series.AnimeSeriesID);
                                    }

                                    return oldIDs;
                                });
                        }
                    }
                    else
                    {
                        somethingDictionary.AddOrUpdate(user?.JMMUserID ?? 0, id =>
                            {
                                var newdict = new ConcurrentDictionary<string, HashSet<int>>();
                                newdict.AddOrUpdate(tag.Key, new HashSet<int> { series.AnimeSeriesID },
                                    (oldTag, oldIDs) =>
                                    {
                                        lock (oldIDs)
                                        {
                                            oldIDs.Add(series.AnimeSeriesID);
                                        }

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
                                        new HashSet<int> { series.AnimeSeriesID }, (oldTag, oldIDs) =>
                                        {
                                            lock (oldIDs)
                                            {
                                                oldIDs.Add(series.AnimeSeriesID);
                                            }

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
        ClearCache();
        Lock(() =>
        {
            using var trans = session.BeginTransaction();
            session.CreateQuery($"DELETE FROM {nameof(SVR_GroupFilter)} WHERE FilterType = {(int)GroupFilterType.Tag};")
                .ExecuteUpdate();
            trans.Commit();
        });
    }

    private void CreateAllTagFilters(ISessionWrapper session, SVR_GroupFilter tagsdirec,
        Dictionary<int, ILookup<string, int>> lookup)
    {
        if (tagsdirec == null)
        {
            return;
        }

        var alltags = new HashSet<string>(
            RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')),
            StringComparer.InvariantCultureIgnoreCase);
        var toAdd = new List<SVR_GroupFilter>(alltags.Count);

        var users = RepoFactory.JMMUser.GetAll().ToList();

        //AniDB Tags are in english so we use en-us culture
        var tinfo = new CultureInfo("en-US", false).TextInfo;
        foreach (var s in alltags)
        {
            // this is creating new tag filters, so locking isn't completely necessary.
            // Ideally, we would create a blank filter to ensure an ID exists, but then it would be empty,
            // and would have data inconsistencies, anyway
            var yf = new SVR_GroupFilter
            {
                ParentGroupFilterID = tagsdirec.GroupFilterID,
                InvisibleInClients = 0,
                ApplyToSeries = 1,
                GroupFilterName = tinfo.ToTitleCase(s),
                BaseCondition = 1,
                Locked = 1,
                SortingCriteria = "5;1",
                FilterType = (int)GroupFilterType.Tag
            };
            yf.SeriesIds[0] = lookup[0][s.ToLowerInvariant()].ToHashSet();
            yf.GroupsIds[0] = yf.SeriesIds[0]
                .Select(id => RepoFactory.AnimeSeries.GetByID(id).TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                .Where(id => id != -1).ToHashSet();
            foreach (var user in users)
            {
                var key = s.ToLowerInvariant();
                // this will happen if you only have porn or none the series you have are allowed by a user 
                if (!lookup.ContainsKey(user.JMMUserID) || !lookup[user.JMMUserID].Contains(key))
                {
                    continue;
                }

                yf.SeriesIds[user.JMMUserID] = lookup[user.JMMUserID][key].ToHashSet();
                yf.GroupsIds[user.JMMUserID] = yf.SeriesIds[user.JMMUserID]
                    .Select(id => RepoFactory.AnimeSeries.GetByID(id).TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                    .Where(id => id != -1).ToHashSet();
            }

            Lock(() =>
            {
                using var trans = session.BeginTransaction();
                // get an ID
                session.Insert(yf);
                trans.Commit();
            });

            var gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionParameter = s,
                GroupFilterID = yf.GroupFilterID
            };
            yf.Conditions.Add(gfc);
            yf.UpdateEntityReferenceStrings();
            yf.GroupConditions = JsonConvert.SerializeObject(yf._conditions);
            yf.GroupConditionsVersion = SVR_GroupFilter.GROUPCONDITIONS_VERSION;
            toAdd.Add(yf);
        }

        Populate(session, false);
        foreach (var filters in toAdd.Batch(50))
        {
            Lock(() =>
            {
                using var trans = session.BeginTransaction();
                BatchUpdate(session, filters);
                trans.Commit();
            });
        }
    }

    public List<SVR_GroupFilter> GetLockedGroupFilters()
    {
        return ReadLock(() => Cache.Values.Where(a => a.Locked == 1).ToList());
    }

    public List<SVR_GroupFilter> GetWithConditionTypesAndAll(HashSet<GroupFilterConditionType> types)
    {
        return ReadLock(
            () =>
            {
                var filters = new HashSet<int>(
                    Cache.Values
                        .Where(a => a.FilterType == (int)GroupFilterType.All)
                        .Select(a => a.GroupFilterID)
                );
                foreach (var t in types)
                {
                    filters.UnionWith(Types.FindInverse(t));
                }

                return filters.Select(a => Cache.Get(a)).ToList();
            }
        );
    }

    public List<SVR_GroupFilter> GetWithConditionsTypes(HashSet<GroupFilterConditionType> types)
    {
        return ReadLock(
            () =>
            {
                var filters = new HashSet<int>();
                foreach (var t in types)
                {
                    filters.UnionWith(Types.FindInverse(t));
                }

                return filters.Select(a => Cache.Get(a)).ToList();
            }
        );
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }
}
