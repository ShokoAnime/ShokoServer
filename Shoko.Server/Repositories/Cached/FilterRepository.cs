using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.Filters;
using Shoko.Server.Models.Filters.Functions;
using Shoko.Server.Models.Filters.Info;
using Shoko.Server.Models.Filters.Logic;
using Shoko.Server.Models.Filters.Logic.DateTimes;
using Shoko.Server.Models.Filters.Selectors;
using Shoko.Server.Models.Filters.SortingSelectors;
using Shoko.Server.Models.Filters.User;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server.Repositories.Cached;

public class FilterRepository : BaseCachedRepository<Filter, int>
{
    private PocoIndex<int, Filter, int> Parents;
    private readonly ChangeTracker<int> Changes = new();

    public FilterRepository()
    {
        EndSaveCallback = obj =>
        {
            Changes.AddOrUpdate(obj.FilterID);
        };
        EndDeleteCallback = obj =>
        {
            Changes.Remove(obj.FilterID);
        };
    }

    protected override int SelectKey(Filter entity)
    {
        return entity.FilterID;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        Parents = Cache.CreateIndex(a => a.ParentFilterID ?? 0);
    }

    public override void RegenerateDb() { }


    public override void PostProcess()
    {
        const string t = "Filter";

        // Clean up. This will populate empty conditions and remove duplicate filters
        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
            t,
            " " + Resources.GroupFilter_Cleanup);
        var all = GetAll();
        var set = new HashSet<Filter>(all);
        var notin = all.Except(set).ToList();
        Delete(notin);
    }

    public void CleanUpEmptyDirectoryFilters()
    {
        var toremove = GetAll().Where(a => (a.FilterType & GroupFilterType.Directory) != 0)
            .Where(gf => // TODO evaluate
                         false).ToList();
        if (toremove.Count > 0)
        {
            Delete(toremove);
        }
    }

    public void CreateOrVerifyLockedFilters()
    {
        const string t = "GroupFilter";

        var lockedGFs = GetLockedGroupFilters();

        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, t,
            " " + Resources.Filter_CreateContinueWatching);

        if (!lockedGFs.Any(a => a.Name == Constants.GroupFilterName.ContinueWatching))
        {
            var gf = new Filter
            {
                Name = Constants.GroupFilterName.ContinueWatching,
                Locked = true,
                ApplyAtSeriesLevel = false,
                FilterType = GroupFilterType.None,
                Expression = new AndExpression{ Left = new HasWatchedEpisodesExpression(), Right = new HasUnwatchedEpisodesExpression() },
                SortingExpression = new LastWatchedDateSortingSelector { Descending = true }
            };
            Save(gf);
        }

        //Create All filter
        if (!lockedGFs.Any(a => a.Name == Constants.GroupFilterName.All))
        {
            var gf = new Filter
            {
                Name = Constants.GroupFilterName.All,
                Locked = true,
                FilterType = GroupFilterType.All,
                SortingExpression = new NameSortingSelector()
            };
            Save(gf);
        }

        if (!lockedGFs.Any(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Tag)))
        {
            var gf = new Filter
            {
                Name = Constants.GroupFilterName.Tags,
                FilterType = (GroupFilterType.Directory | GroupFilterType.Tag),
                Locked = true
            };
            Save(gf);
        }

        if (!lockedGFs.Any(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Year)))
        {
            var gf = new Filter
            {
                Name = Constants.GroupFilterName.Years,
                FilterType = (GroupFilterType.Directory | GroupFilterType.Year),
                Locked = true
            };
            Save(gf);
        }

        if (!lockedGFs.Any(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Season)))
        {
            var gf = new Filter
            {
                Name = Constants.GroupFilterName.Seasons,
                FilterType = (GroupFilterType.Directory | GroupFilterType.Season),
                Locked = true
            };
            Save(gf);
        }

        CreateOrVerifyDirectoryFilters(true);
    }

    public void CreateOrVerifyDirectoryFilters(bool frominit = false, HashSet<string> tags = null,
        HashSet<int> airdate = null, SortedSet<(int Year, AnimeSeason Season)> seasons = null)
    {
        const string t = "GroupFilter";

        var lockedGFs = GetLockedGroupFilters();


        var tagsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Tag));
        if (tagsdirec != null)
        {
            HashSet<string> alltags;
            if (tags == null)
            {
                alltags = new HashSet<string>(
                    RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')),
                    StringComparer.InvariantCultureIgnoreCase);
            }
            else
            {
                alltags = new HashSet<string>(tags, StringComparer.InvariantCultureIgnoreCase);
            }

            var existingTags =
                new HashSet<string>(
                    lockedGFs.Where(a => a.FilterType == GroupFilterType.Tag).Select(a => a.Expression).Cast<HasTagExpression>().Select(a => a.Parameter),
                    StringComparer.InvariantCultureIgnoreCase);
            alltags.ExceptWith(existingTags);

            var max = alltags.Count;
            var cnt = 0;
            //AniDB Tags are in english so we use en-us culture
            var tinfo = new CultureInfo("en-US", false).TextInfo;
            foreach (var s in alltags)
            {
                cnt++;
                if (frominit)
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, t,
                        Resources.Filter_CreatingTag + " " +
                        Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
                }

                var yf = new Filter
                {
                    ParentFilterID = tagsdirec.FilterID,
                    FilterType = GroupFilterType.Tag,
                    ApplyAtSeriesLevel = true,
                    Name = tinfo.ToTitleCase(s),
                    Locked = true,
                    Expression = new HasTagExpression { Parameter = s },
                    SortingExpression = new NameSortingSelector()
                };
                Save(yf);
            }
        }

        var yearsdirec = lockedGFs.FirstOrDefault(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Year));
        if (yearsdirec != null)
        {
            HashSet<int> allyears;
            if (airdate == null || airdate.Count == 0)
            {
                var grps = RepoFactory.AnimeSeries.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();

                allyears = new HashSet<int>();
                foreach (var ser in grps)
                {
                    var endyear = ser.AniDBAnime.AniDBAnime.EndYear;
                    var startyear = ser.AniDBAnime.AniDBAnime.BeginYear;
                    if (endyear <= 0) endyear = DateTime.Today.Year;
                    if (endyear < startyear || endyear - startyear + 1 >= int.MaxValue) endyear = startyear;
                    if (startyear != 0) allyears.UnionWith(Enumerable.Range(startyear, endyear - startyear + 1).Select(a => a));
                }
            }
            else
            {
                allyears = new HashSet<int>(airdate.Select(a => a));
            }

            var notin = new HashSet<int>(lockedGFs.Where(a => a.FilterType == GroupFilterType.Year).Select(a => a.Expression).Cast<InYearExpression>()
                .Select(a => a.Parameter));
            allyears.ExceptWith(notin);
            var max = allyears.Count;
            var cnt = 0;
            foreach (var s in allyears)
            {
                cnt++;
                if (frominit)
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, t,
                        Resources.Filter_CreatingYear + " " +
                        Resources.Filter_Filter + " " + cnt + "/" + max + " - " + s);
                }

                var yf = new Filter
                {
                    ParentFilterID = yearsdirec.FilterID,
                    Name = s.ToString(),
                    FilterType = GroupFilterType.Year,
                    Locked = true,
                    ApplyAtSeriesLevel = true,
                    Expression = new InYearExpression { Parameter = s },
                    SortingExpression = new NameSortingSelector()
                };
                Save(yf);
            }
        }

        var seasonsdirectory = lockedGFs.FirstOrDefault(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Season));
        if (seasonsdirectory != null)
        {
            SortedSet<(int Year, AnimeSeason Season)> allseasons;
            if (seasons == null)
            {
                var grps = RepoFactory.AnimeSeries.GetAll().ToList();

                allseasons = new SortedSet<(int Year, AnimeSeason Season)>();
                foreach (var ser in grps)
                {
                    var seriesSeasons = ser?.Contract?.AniDBAnime?.AniDBAnime?.GetSeasons().ToList();
                    if ((seriesSeasons?.Count ?? 0) == 0) ser?.UpdateContract();
                    if ((seriesSeasons?.Count ?? 0) == 0) continue;
                    allseasons.UnionWith(seriesSeasons);
                }
            }
            else
            {
                allseasons = seasons;
            }

            var notin = new HashSet<(int Year, AnimeSeason Season)>(lockedGFs.Where(a => a.FilterType == GroupFilterType.Season).Select(a => a.Expression)
                .Cast<InSeasonExpression>().Select(a => (a.Year, a.Season)));
            allseasons.ExceptWith(notin);

            var max = allseasons.Count;
            var cnt = 0;
            foreach (var season in allseasons)
            {
                cnt++;
                if (frominit)
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, t,
                        Resources.Filter_CreatingSeason + " " +
                        Resources.Filter_Filter + " " + cnt + "/" + max + " - " + season);
                }

                var yf = new Filter
                {
                    ParentFilterID = seasonsdirectory.FilterID,
                    Name = season.Season + " " + season.Year,
                    Locked = true,
                    FilterType = GroupFilterType.Season,
                    ApplyAtSeriesLevel = true,
                    Expression = new InSeasonExpression { Season = season.Season, Year = season.Year },
                    SortingExpression = new NameSortingSelector()
                };
                Save(yf);
            }
        }

        CleanUpEmptyDirectoryFilters();
    }
    
    public void CreateInitialFilters()
    {
        // group filters
        // Do to DatabaseFixes, some filters may be made, namely directory filters
        // All, Continue Watching, Years, Seasons, Tags... 6 seems to be enough to tell for now
        // We can't just check the existence of anything specific, as the user can delete most of these
        if (GetTopLevel().Count > 6) return;

        // Favorites
        var gf = new Filter
        {
            Name = Constants.GroupFilterName.Favorites,
            FilterType = GroupFilterType.UserDefined,
            Expression = new IsFavoriteExpression(),
            SortingExpression = new NameSortingSelector()
        };
        Save(gf);

        // Missing Episodes
        gf = new Filter
        {
            Name = Constants.GroupFilterName.MissingEpisodes,
            FilterType = GroupFilterType.UserDefined,
            Expression = new HasMissingEpisodesCollectingExpression(),
            SortingExpression = new MissingEpisodeCollectingCountSortingSelector{ Descending = true}
        };
        Save(gf);


        // Newly Added Series
        gf = new Filter
        {
            Name = Constants.GroupFilterName.NewlyAddedSeries,
            FilterType = GroupFilterType.UserDefined,
            Expression = new GreaterThanEqualExpression
            {
                Left = new DateAddFunction { Selector = new LastAddedDateSelector(), Parameter = TimeSpan.FromDays(10) },
                Right = new TodayFunction()
            },
            SortingExpression = new LastAddedDateSortingSelector { Descending = true}
        };
        Save(gf);

        // Newly Airing Series
        gf = new Filter
        {
            Name = Constants.GroupFilterName.NewlyAiringSeries,
            FilterType = GroupFilterType.UserDefined,
            Expression = new GreaterThanEqualExpression
            {
                Left = new DateAddFunction { Selector = new LastAirDateSelector(), Parameter = TimeSpan.FromDays(30) },
                Right = new TodayFunction()
            },
            SortingExpression = new LastAirDateSortingSelector { Descending = true}
        };
        Save(gf);

        // Votes Needed
        gf = new Filter
        {
            Name = Constants.GroupFilterName.MissingVotes,
            ApplyAtSeriesLevel = true,
            FilterType = GroupFilterType.UserDefined,
            Expression = new AndExpression
            {
                // all watched and none missing
                Left = new AndExpression
                {
                    // all watched, aka no episodes unwatched
                    Left = new NotExpression
                    {
                        Left = new HasUnwatchedEpisodesExpression()
                    },
                    // no missing episodes
                    Right = new NotExpression
                    {
                        Left = new HasMissingEpisodesExpression()
                    }
                },
                // does not have votes
                Right = new NotExpression
                {
                    Left = new HasUserVotesExpression()
                }
            },
            SortingExpression = new NameSortingSelector()
        };
        Save(gf);

        // Recently Watched
        gf = new Filter
        {
            Name = Constants.GroupFilterName.RecentlyWatched,
            FilterType = GroupFilterType.UserDefined,
            Expression = new AndExpression
            {
                Left = new HasWatchedEpisodesExpression(),
                Right = new GreaterThanEqualExpression
                {
                    Left = new DateAddFunction { Selector = new LastWatchedDateSelector(), Parameter = TimeSpan.FromDays(10) },
                    Right = new TodayFunction()
                },
            },
            SortingExpression = new LastWatchedDateSortingSelector
            {
                Descending = true
            }
        };
        Save(gf);

        // TvDB/MovieDB Link Missing
        gf = new Filter
        {
            Name = Constants.GroupFilterName.MissingLinks,
            ApplyAtSeriesLevel = true,
            FilterType = GroupFilterType.UserDefined,
            Expression = new OrExpression
            {
                Left = new MissingTvDBLinkExpression(),
                Right = new MissingTMDbLinkExpression()
            },
            SortingExpression = new NameSortingSelector()
        };
        Save(gf);
    }

    public override void Save(Filter obj)
    {
        WriteLock(() => { base.Save(obj); });
    }

    public override void Save(IReadOnlyCollection<Filter> objs)
    {
        foreach (var obj in objs)
        {
            Save(obj);
        }
    }

    public override void Delete(IReadOnlyCollection<Filter> objs)
    {
        foreach (var cr in objs)
        {
            base.Delete(cr);
        }
    }

    /// <summary>
    /// Updates a batch of <see cref="Filter"/>s.
    /// </summary>
    /// <remarks>
    /// This method ONLY updates existing <see cref="Filter"/>s. It will not insert any that don't already exist.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="groupFilters">The batch of <see cref="Filter"/>s to update.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="groupFilters"/> is <c>null</c>.</exception>
    public void BatchUpdate(ISessionWrapper session, IEnumerable<Filter> groupFilters)
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
            Changes.AddOrUpdate(groupFilter.FilterID);
        }
    }

    public List<Filter> GetByParentID(int parentid)
    {
        return ReadLock(() => Parents.GetMultiple(parentid));
    }

    public List<Filter> GetTopLevel()
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

        var tagsdirec = GetAll(session).FirstOrDefault(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Tag));

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
                    if (user?.GetHideCategories().FindInEnumerable(seriesTags) ?? false) continue;

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
            session.CreateQuery($"DELETE FROM {nameof(Filter)} WHERE FilterType = {(int)GroupFilterType.Tag};")
                .ExecuteUpdate();
            trans.Commit();
        });
    }

    private void CreateAllTagFilters(ISessionWrapper session, Filter tagsdirec,
        Dictionary<int, ILookup<string, int>> lookup)
    {
        if (tagsdirec == null)
        {
            return;
        }

        var alltags = new HashSet<string>(
            RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')),
            StringComparer.InvariantCultureIgnoreCase);
        var toAdd = new List<Filter>(alltags.Count);

        var users = RepoFactory.JMMUser.GetAll().ToList();

        //AniDB Tags are in english so we use en-us culture
        var tinfo = new CultureInfo("en-US", false).TextInfo;
        foreach (var s in alltags)
        {
            // this is creating new tag filters, so locking isn't completely necessary.
            // Ideally, we would create a blank filter to ensure an ID exists, but then it would be empty,
            // and would have data inconsistencies, anyway
            var yf = new Filter
            {
                ParentFilterID = tagsdirec.FilterID,
                Hidden = false,
                ApplyAtSeriesLevel = true,
                Name = tinfo.ToTitleCase(s),
                Locked = true,
                FilterType = GroupFilterType.Tag,
                Expression = new HasTagExpression { Parameter = s },
                SortingExpression = new NameSortingSelector()
            };

            Lock(() =>
            {
                using var trans = session.BeginTransaction();
                // get an ID
                session.Insert(yf);
                trans.Commit();
            });

            toAdd.Add(yf);
        }

        Populate(session, false);
    }

    public List<Filter> GetLockedGroupFilters()
    {
        return ReadLock(() => Cache.Values.Where(a => a.Locked).ToList());
    }

    public List<Filter> GetTimeDependentFilters()
    {
        return ReadLock(
            () =>
            {
                return GetAll().Where(a => a.Expression.TimeDependent).ToList();
            }
        );
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }
}
