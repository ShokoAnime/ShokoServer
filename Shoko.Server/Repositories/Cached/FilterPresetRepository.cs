using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Functions;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Logic;
using Shoko.Server.Filters.Logic.DateTimes;
using Shoko.Server.Filters.Selectors;
using Shoko.Server.Filters.SortingSelectors;
using Shoko.Server.Filters.User;
using Shoko.Server.Models;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server.Repositories.Cached;

public class FilterPresetRepository : BaseCachedRepository<FilterPreset, int>
{
    private PocoIndex<int, FilterPreset, int> Parents;
    private readonly ChangeTracker<int> Changes = new();

    public FilterPresetRepository()
    {
        EndSaveCallback = obj =>
        {
            Changes.AddOrUpdate(obj.FilterPresetID);
        };
        EndDeleteCallback = obj =>
        {
            Changes.Remove(obj.FilterPresetID);
        };
    }

    protected override int SelectKey(FilterPreset entity)
    {
        return entity.FilterPresetID;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        Parents = Cache.CreateIndex(a => a.ParentFilterPresetID ?? 0);
    }

    public override void RegenerateDb() { }


    public override void PostProcess()
    {
        const string t = "FilterPreset";

        // Clean up. This will populate empty conditions and remove duplicate filters
        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
            t,
            " " + Resources.GroupFilter_Cleanup);
        var all = GetAll();
        var set = new HashSet<FilterPreset>(all);
        var notin = all.Except(set).ToList();
        Delete(notin);
    }

    public void CleanUpEmptyDirectoryFilters()
    {
        var evaluator = Utils.ServiceContainer.GetRequiredService<FilterEvaluator>();
        var filters = GetAll().Where(a => (a.FilterType & GroupFilterType.Directory) != 0)
            .Where(gf => gf.Expression is not null && !gf.Expression.UserDependent).ToList();
        var toRemove = evaluator.BatchEvaluateFilters(filters, null, true).Where(a => !a.Value.Any()).Select(a => a.Key).ToList();
        if (toRemove.Count <= 0) return;

        Delete(toRemove);
    }

    public void CreateOrVerifyLockedFilters()
    {
        const string t = "FilterPreset";

        var lockedGFs = GetLockedGroupFilters();

        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, t,
            " " + Resources.Filter_CreateContinueWatching);

        if (!lockedGFs.Any(a => a.Name == Constants.GroupFilterName.ContinueWatching))
        {
            var gf = new FilterPreset
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
            var gf = new FilterPreset
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
            var gf = new FilterPreset
            {
                Name = Constants.GroupFilterName.Tags,
                FilterType = (GroupFilterType.Directory | GroupFilterType.Tag),
                Locked = true
            };
            Save(gf);
        }

        if (!lockedGFs.Any(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Year)))
        {
            var gf = new FilterPreset
            {
                Name = Constants.GroupFilterName.Years,
                FilterType = (GroupFilterType.Directory | GroupFilterType.Year),
                Locked = true
            };
            Save(gf);
        }

        if (!lockedGFs.Any(a => a.FilterType == (GroupFilterType.Directory | GroupFilterType.Season)))
        {
            var gf = new FilterPreset
            {
                Name = Constants.GroupFilterName.Seasons,
                FilterType = (GroupFilterType.Directory | GroupFilterType.Season),
                Locked = true
            };
            Save(gf);
        }

        CreateOrVerifyDirectoryFilters(true);
    }

    public void CreateOrVerifyDirectoryFilters(bool frominit = false, ISet<string> tags = null,
        ISet<int> airdate = null, ISet<(int Year, AnimeSeason Season)> seasons = null)
    {
        const string t = "FilterPreset";

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

                var yf = new FilterPreset
                {
                    ParentFilterPresetID = tagsdirec.FilterPresetID,
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
                var grps = RepoFactory.AnimeSeries.GetAll().Select(a => a.GetAnime());

                allyears = new HashSet<int>();
                foreach (var anime in grps)
                {
                    var endyear = anime.EndYear;
                    var startyear = anime.BeginYear;
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

                var yf = new FilterPreset
                {
                    ParentFilterPresetID = yearsdirec.FilterPresetID,
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
            ISet<(int Year, AnimeSeason Season)> allseasons;
            if (seasons == null)
            {
                var grps = RepoFactory.AnimeSeries.GetAll().ToList();

                allseasons = new SortedSet<(int Year, AnimeSeason Season)>();
                foreach (var ser in grps)
                {
                    var seriesSeasons = ser?.GetAnime()?.GetSeasons().ToList();
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

                var yf = new FilterPreset
                {
                    ParentFilterPresetID = seasonsdirectory.FilterPresetID,
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
        var gf = new FilterPreset
        {
            Name = Constants.GroupFilterName.Favorites,
            FilterType = GroupFilterType.UserDefined,
            Expression = new IsFavoriteExpression(),
            SortingExpression = new NameSortingSelector()
        };
        Save(gf);

        // Missing Episodes
        gf = new FilterPreset
        {
            Name = Constants.GroupFilterName.MissingEpisodes,
            FilterType = GroupFilterType.UserDefined,
            Expression = new HasMissingEpisodesCollectingExpression(),
            SortingExpression = new MissingEpisodeCollectingCountSortingSelector{ Descending = true}
        };
        Save(gf);


        // Newly Added Series
        gf = new FilterPreset
        {
            Name = Constants.GroupFilterName.NewlyAddedSeries,
            FilterType = GroupFilterType.UserDefined,
            Expression = new DateGreaterThanEqualsExpression
            {
                Left = new DateAddFunction(new LastAddedDateSelector(), TimeSpan.FromDays(10)),
                Right = new TodayFunction()
            },
            SortingExpression = new LastAddedDateSortingSelector { Descending = true}
        };
        Save(gf);

        // Newly Airing Series
        gf = new FilterPreset
        {
            Name = Constants.GroupFilterName.NewlyAiringSeries,
            FilterType = GroupFilterType.UserDefined,
            Expression = new DateGreaterThanEqualsExpression(new DateAddFunction(new LastAirDateSelector(), TimeSpan.FromDays(30)), new TodayFunction()),
            SortingExpression = new LastAirDateSortingSelector { Descending = true }
        };
        Save(gf);

        // Votes Needed
        gf = new FilterPreset
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
        gf = new FilterPreset
        {
            Name = Constants.GroupFilterName.RecentlyWatched,
            FilterType = GroupFilterType.UserDefined,
            Expression = new AndExpression(new HasWatchedEpisodesExpression(), new 
                DateGreaterThanEqualsExpression(new DateAddFunction(new LastWatchedDateSelector(), TimeSpan.FromDays(10)), new TodayFunction())),
            SortingExpression = new LastWatchedDateSortingSelector
            {
                Descending = true
            }
        };
        Save(gf);

        // TvDB/MovieDB Link Missing
        gf = new FilterPreset
        {
            Name = Constants.GroupFilterName.MissingLinks,
            ApplyAtSeriesLevel = true,
            FilterType = GroupFilterType.UserDefined,
            Expression = new OrExpression(new MissingTvDBLinkExpression(), new MissingTMDbLinkExpression()),
            SortingExpression = new NameSortingSelector()
        };
        Save(gf);
    }

    public override void Save(FilterPreset obj)
    {
        WriteLock(() => { base.Save(obj); });
    }

    public override void Save(IReadOnlyCollection<FilterPreset> objs)
    {
        foreach (var obj in objs)
        {
            Save(obj);
        }
    }

    public override void Delete(IReadOnlyCollection<FilterPreset> objs)
    {
        foreach (var cr in objs)
        {
            base.Delete(cr);
        }
    }

    public List<FilterPreset> GetByParentID(int parentid)
    {
        return ReadLock(() => Parents.GetMultiple(parentid));
    }

    public FilterPreset GetTopLevelFilter(int filterID)
    {
        var parent = GetByID(filterID);
        if (parent == null || parent.ParentFilterPresetID is null or 0)
            return parent;

        while (true)
        {
            if (parent.ParentFilterPresetID is null or 0) return parent;
            var next = GetByID(parent.ParentFilterPresetID.Value);
            // should never happen, but it's not completely impossible
            if (next == null) return parent;
            parent = next;
        }
    }

    public List<FilterPreset> GetTopLevel()
    {
        return GetByParentID(0);
    }

    public List<FilterPreset> GetLockedGroupFilters()
    {
        return ReadLock(() => Cache.Values.Where(a => a.Locked).ToList());
    }

    public List<FilterPreset> GetTimeDependentFilters()
    {
        return ReadLock(() => GetAll().Where(a => a.Expression.TimeDependent).ToList());
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }
}
