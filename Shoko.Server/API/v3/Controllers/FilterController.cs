using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class FilterController : BaseController
{
    private static readonly IMemoryCache PreviewCache = new MemoryCache(new MemoryCacheOptions() {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    internal const string FilterNotFound = "No Filter entry for the given filterID";

    private readonly FilterFactory _factory;
    private readonly SeriesFactory _seriesFactory;
    private readonly FilterEvaluator _filterEvaluator;
    private static Filter.FilterExpressionHelp[] _expressionTypes;
    private static Filter.SortingCriteriaHelp[] _sortingTypes;

    #region Existing Filters

    /// <summary>
    /// Get all <see cref="Filter"/>s except the live filter.
    /// </summary>
    /// <param name="includeEmpty">Include empty filters.</param>
    /// <param name="showHidden">Show hidden filters.</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="withConditions">Include conditions and sort criteria in the response.</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<ListResult<Filter>> GetAllFilters([FromQuery] bool includeEmpty = false,
        [FromQuery] bool showHidden = false, [FromQuery] [Range(0, 100)] int pageSize = 10,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool withConditions = false)
    {
        var user = User;
        
        return _filterEvaluator.BatchEvaluateFilters(RepoFactory.FilterPreset.GetTopLevel(), user.JMMUserID, true)
            .Where(kv =>
            {
                var filter = kv.Key;
                if (!showHidden && filter.Hidden)
                    return false;

                if (includeEmpty || (filter.IsDirectory() ? RepoFactory.FilterPreset.GetByParentID(filter.FilterPresetID).Count > 0 : kv.Value.Any()))
                    return true;

                return false;
            })
            .Select(a => a.Key)
            .OrderBy(filter => filter.Name)
            .ToListResult(filter => _factory.GetFilter(filter, withConditions), page, pageSize);
    }

    /// <summary>
    /// Add a new group filter. Requires admin.
    /// </summary>
    /// <param name="body"></param>
    /// <returns>The newly added filter.</returns>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<Filter> AddNewFilter(Filter.Input.CreateOrUpdateFilterBody body)
    {
        try
        {
            var filterPreset = new FilterPreset
            {
                FilterType = GroupFilterType.UserDefined
            };
            var filter = _factory.MergeWithExisting(body, filterPreset, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            return filter;
        }
        catch (ArgumentException e)
        {
            return ValidationProblem(e.Message, "Expression");
        }
    }

    /// <summary>
    /// Lists the available expressions.
    /// </summary>
    /// <remarks>
    /// The word "Filterable" is used a lot. It is a generic word for a series or group, depending on what the filter is set to apply to.  
    /// Expression: The identifier used to create the expression. eg. And, Not, HasTag.  
    /// Type: Parameters have a type, and this is the type that needs to match.  
    /// Left, Right, Parameter, and SecondParameter show what type the expression supports as parameters.  
    /// Left and Right are Expressions or Selectors. Parameters are constants.
    /// </remarks>
    [HttpGet("Expressions")]
    public ActionResult<Filter.FilterExpressionHelp[]> GetExpressions()
    {
        // get all classes that derive from FilterExpression, but not SortingExpression
        _expressionTypes ??= AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => a != typeof(FilterExpression) && !a.IsGenericType && typeof(FilterExpression).IsAssignableFrom(a) && !typeof(SortingExpression).IsAssignableFrom(a))
            .OrderBy(a => a.FullName).Select(a =>
        {
            var expression = (FilterExpression)Activator.CreateInstance(a);
            if (expression == null) return null;
            Filter.FilterExpressionHelp.FilterExpressionParameterType? left = expression switch
            {
                IWithExpressionParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Expression,
                IWithDateSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.DateSelector,
                IWithNumberSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.NumberSelector,
                IWithStringSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.StringSelector,
                _ => null
            };
            Filter.FilterExpressionHelp.FilterExpressionParameterType? right = expression switch
            {
                IWithSecondExpressionParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Expression,
                IWithSecondDateSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.DateSelector,
                IWithSecondNumberSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.NumberSelector,
                IWithSecondStringSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.StringSelector,
                _ => null
            };
            Filter.FilterExpressionHelp.FilterExpressionParameterType? parameter = expression switch
            {
                IWithDateParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Date,
                IWithNumberParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Number,
                IWithStringParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.String,
                IWithTimeSpanParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.TimeSpan,
                _ => null
            };
            Filter.FilterExpressionHelp.FilterExpressionParameterType? secondParameter = expression switch
            {
                IWithSecondStringParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.String,
                _ => null
            };
            var type = expression switch
            {
                FilterExpression<bool> => Filter.FilterExpressionHelp.FilterExpressionParameterType.Expression,
                FilterExpression<DateTime?> => Filter.FilterExpressionHelp.FilterExpressionParameterType.DateSelector,
                FilterExpression<double> => Filter.FilterExpressionHelp.FilterExpressionParameterType.NumberSelector,
                FilterExpression<string> => Filter.FilterExpressionHelp.FilterExpressionParameterType.StringSelector,
                _ => throw new Exception($"Expression {a.Name} is not a handled type for Filter Expression Help")
            };
            return new Filter.FilterExpressionHelp
            {
                Expression = a.Name.Replace("Expression", "").Replace("Function", ""),
                Description = expression.HelpDescription,
                PossibleParameters = expression.HelpPossibleParameters,
                PossibleSecondParameters = expression.HelpPossibleSecondParameters,
                Left = left,
                Right = right,
                Parameter = parameter,
                SecondParameter = secondParameter,
                Type = type
            };
        }).Where(a => a != null).ToArray();
        return _expressionTypes;
    }

    /// <summary>
    /// Lists the available sorting expressions. These are basically selectors that the filter system uses to sort.
    /// </summary>
    /// <remarks>
    /// The word "Filterable" is used a lot. It is a generic word for a series or group, depending on what the filter is set to apply to.  
    /// Type: The identifier used to create the expression. eg. AddedDate.  
    /// IsInverted: Whether the sorting should be in descending order.  
    /// Next: If the expression returns equal values, it defers to the next expression to sort more predictably.  
    /// For example, MissingEpisodeCount,Descending -> AirDate, Descending would have thing with the most missing episodes, then the last aired first.
    /// </remarks>
    [HttpGet("SortingCriteria")]
    public ActionResult<Filter.SortingCriteriaHelp[]> GetSortingCriteria()
    {
        // get all classes that derive from FilterExpression, but not SortingExpression
        _sortingTypes ??= AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
                a != typeof(FilterExpression) && !a.IsAbstract && !a.IsGenericType && typeof(SortingExpression).IsAssignableFrom(a)).OrderBy(a => a.FullName)
            .Select(a =>
            {
                var criteria = (SortingExpression)Activator.CreateInstance(a);
                if (criteria == null) return null;
                return new Filter.SortingCriteriaHelp
                {
                    Type = a.Name.Replace("SortingSelector", ""), Description = criteria.HelpDescription
                };
            }).Where(a => a != null).ToArray();
        return _sortingTypes;
    }

    /// <summary>
    /// Get the <see cref="Filter"/> for the given <paramref name="filterID"/>.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="withConditions">Include conditions and sort criteria in the response.</param>
    /// <returns>The filter</returns>
    [HttpGet("{filterID}")]
    public ActionResult<Filter> GetFilter([FromRoute] int filterID, [FromQuery] bool withConditions = false)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        return _factory.GetFilter(filterPreset, withConditions);
    }

    /// <summary>
    /// Edit an existing filter using a JSON patch document to do a partial
    /// update. Requires admin.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="document">JSON patch document for the partial update.</param>
    /// <returns>The updated filter.</returns>
    [Authorize("admin")]
    [HttpPatch("{filterID}")]
    public ActionResult<Filter> PatchFilter([FromRoute] int filterID, JsonPatchDocument<Filter.Input.CreateOrUpdateFilterBody> document)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        try
        {
            var body = _factory.CreateOrUpdateFilterBody(filterPreset);
            document.ApplyTo(body, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var filter = _factory.MergeWithExisting(body, filterPreset, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            return filter;
        }
        catch (ArgumentException e)
        {
            return ValidationProblem(e.Message, "Expression");
        }
    }

    /// <summary>
    /// Edit an existing filter using a raw object. Requires admin.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="body">The full document for the changes to be made to the filter.</param>
    /// <returns>The updated filter.</returns>
    [Authorize("admin")]
    [HttpPut("{filterID}")]
    public ActionResult<Filter> PutFilter([FromRoute] int filterID, Filter.Input.CreateOrUpdateFilterBody body)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        try
        {
            var filter = _factory.MergeWithExisting(body, filterPreset, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            return filter;
        }
        catch (ArgumentException e)
        {
            return ValidationProblem(e.Message, "Expression");
        }
    }

    /// <summary>
    /// Removes an existing filter. Requires admin.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpDelete("{filterID}")]
    public ActionResult DeleteFilter(int filterID)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        RepoFactory.FilterPreset.Delete(filterPreset);

        return NoContent();
    }

    #endregion


    #region Preview/On-the-fly Filter

    [NonAction]
    internal static FilterPreset GetDefaultFilterForUser(SVR_JMMUser user)
    {

        var filterPreset = new FilterPreset
        {
            FilterType = GroupFilterType.UserDefined,
            Name = "Live Filtering",
        };

        return filterPreset;
    }

    [NonAction]
    internal static FilterPreset GetPreviewFilterForUser(SVR_JMMUser user)
    {
        var userId = user.JMMUserID;
        var key = $"User={userId}";
        if (!PreviewCache.TryGetValue(key, out FilterPreset filterPreset))
            filterPreset = PreviewCache.Set(key, GetDefaultFilterForUser(user), new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromHours(1) });

        return filterPreset;
    }

    [NonAction]
    internal static bool ResetPreviewFilterForUser(SVR_JMMUser user)
    {
        var userId = user.JMMUserID;
        var key = $"User={userId}";
        if (!PreviewCache.TryGetValue(key, out FilterPreset _))
            return false;

        PreviewCache.Remove(key);

        return true;
    }

    /// <summary>
    /// Get the live filter for the current user.
    /// </summary>
    /// <returns>The live filter.</returns>
    [HttpGet("Preview")]
    public ActionResult<Filter.Input.CreateOrUpdateFilterBody> GetPreviewFilter()
    {
        var filterPreset = GetPreviewFilterForUser(User);
        return _factory.CreateOrUpdateFilterBody(filterPreset);
    }

    /// <summary>
    /// Edit the live filter for the current user using a JSON patch document to
    /// do a partial update.
    /// </summary>
    /// <param name="document">JSON patch document for the partial update.</param>
    /// <returns>The updated live filter.</returns>
    [HttpPatch("Preview")]
    public ActionResult<Filter.Input.CreateOrUpdateFilterBody> PatchPreviewFilter([FromBody] JsonPatchDocument<Filter.Input.CreateOrUpdateFilterBody> document)
    {
        var filterPreset = GetPreviewFilterForUser(User);

        var body = _factory.CreateOrUpdateFilterBody(filterPreset);
        document.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _factory.MergeWithExisting(body, filterPreset, ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return _factory.CreateOrUpdateFilterBody(filterPreset);
    }

    /// <summary>
    /// Edit the live filter for the current user using a raw object.
    /// </summary>
    /// <param name="body">The full document for the changes to be made to the filter.</param>
    /// <returns>The updated live filter.</returns>
    [HttpPut("Preview")]
    public ActionResult<Filter.Input.CreateOrUpdateFilterBody> PutPreviewFilter([FromBody] Filter.Input.CreateOrUpdateFilterBody body)
    {
        var filterPreset = GetPreviewFilterForUser(User);
        _factory.MergeWithExisting(body, filterPreset, ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return _factory.CreateOrUpdateFilterBody(filterPreset);
    }

    /// <summary>
    /// Resets the live filter for the current user.
    /// </summary>
    /// <returns>Void.</returns>
    [HttpDelete("Preview")]
    public ActionResult RemovePreviewFilter()
    {
        ResetPreviewFilterForUser(User);
        return NoContent();
    }

    /// <summary>
    /// Get a paginated list of all the top-level <see cref="Group"/>s for the live filter.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="orderByName">Ignore the group filter sort critaria and always order the returned list by name.</param>
    /// <returns></returns>
    [HttpGet("Preview/Group")]
    public ActionResult<ListResult<Group>> GetPreviewFilteredGroups(
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeEmpty = false, [FromQuery] bool randomImages = false, [FromQuery] bool orderByName = false)
    {
        // Directories should only contain sub-filters, not groups and series.
        var filterPreset = GetPreviewFilterForUser(User);
        if (filterPreset.IsDirectory())
            return new ListResult<Group>();

        // Fast path when user is not in the filter.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID);
        if (!results.Any()) return new ListResult<Group>();

        var groups = results
            .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key))
            .Where(group =>
            {
                // not top level groups
                if (group == null || group.AnimeGroupParentID.HasValue)
                    return false;

                return includeEmpty || group.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            });
        return groups
            .ToListResult(group => new Group(HttpContext, group, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a dictionary with the count for each starting character in each of
    /// the top-level group's name with the live filter applied.
    /// </summary>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpGet("Preview/Group/Letters")]
    public ActionResult<Dictionary<char, int>> GetPreviewGroupNameLettersInFilter([FromQuery] bool includeEmpty = false)
    {
        // Directories should only contain sub-filters, not groups and series.
        var user = User;
        var filterPreset = GetPreviewFilterForUser(user);
        if (filterPreset.IsDirectory())
            return new Dictionary<char, int>();

        // Fast path when user is not in the filter
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new Dictionary<char, int>();

        return results
            .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key))
            .Where(group =>
            {
                if (group is not { AnimeGroupParentID: null })
                    return false;

                return includeEmpty || group.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            })
            .GroupBy(group => group.GetSortName()[0])
            .OrderBy(groupList => groupList.Key)
            .ToDictionary(groupList => groupList.Key, groupList => groupList.Count());
    }

    /// <summary>
    /// Get a paginated list of all the <see cref="Series"/> within the live filter.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/>.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpGet("Preview/Series")]
    public ActionResult<ListResult<Series>> GetPreviewSeriesInFilteredGroup(
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeMissing = false)
    {
        // Directories should only contain sub-filters, not groups and series.
        var user = User;
        var filterPreset = GetPreviewFilterForUser(user);
        if (filterPreset.IsDirectory())
            return new ListResult<Series>();

        // Return early if every series will be filtered out.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new ListResult<Series>();

        // We don't need separate logic for ApplyAtSeriesLevel, as the FilterEvaluator handles that
        return results.SelectMany(a => a.Select(id => RepoFactory.AnimeSeries.GetByID(id)))
            .Where(series => series != null && (includeMissing || series.GetVideoLocals().Count > 0))
            .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
            .ToListResult(series => _seriesFactory.GetSeries(series, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a list of all the sub-<see cref="Group"/>s belonging to the <see cref="Group"/> with the given <paramref name="groupID"/> and which are present within the live filter.
    /// </summary>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("Preview/Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetPreviewFilteredSubGroups([FromRoute] int groupID,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeEmpty = false)
    {
        var user = User;
        var filterPreset = GetPreviewFilterForUser(user);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory())
            return new List<Group>();

        // Just return early because the every group will be filtered out.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new List<Group>();
        
        // Subgroups are weird. We'll take the group, build a set of all subgroup IDs, and use that to determine if a group should be included
        // This should maintain the order of results, but have every group in the tree for those results
        var orderedGroups = results.SelectMany(a => RepoFactory.AnimeGroup.GetByID(a.Key).TopLevelAnimeGroup.GetAllChildGroups().Select(b => b.AnimeGroupID)).ToArray();
        var groups = orderedGroups.ToHashSet();
        
        return group.GetChildGroups()
            .Where(subGroup =>
            {
                if (subGroup == null)
                    return false;

                if (!user.AllowedGroup(subGroup))
                    return false;

                if (!includeEmpty && !subGroup.GetAllSeries()
                        .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0)))
                    return false;

                return groups.Contains(subGroup.AnimeGroupID);
            })
            .OrderBy(a => Array.IndexOf(orderedGroups, a.AnimeGroupID))
            .Select(g => new Group(HttpContext, g, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the live filter.
    /// </summary>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// /// <returns></returns>
    [HttpGet("Preview/Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetPreviewSeriesInFilteredGroup([FromRoute] int groupID,
        [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = false,
        [FromQuery] bool randomImages = false, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var user = User;
        var filterPreset = GetPreviewFilterForUser(user);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory())
            return new List<Series>();

        if (!filterPreset.ApplyAtSeriesLevel)
            return (recursive ? group.GetAllSeries() : group.GetSeries())
                .Where(a => user.AllowedSeries(a))
                .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
                .Select(series => _seriesFactory.GetSeries(series, randomImages, includeDataFrom))
                .Where(series => series.Size > 0 || includeMissing)
                .ToList();

        // Just return early because the every series will be filtered out.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new List<Series>();

        var seriesIDs = recursive
            ? group.GetAllChildGroups().SelectMany(a => results.FirstOrDefault(b => b.Key == a.AnimeGroupID))
            : results.FirstOrDefault(a => a.Key == groupID);

        var series = seriesIDs?.Select(a => RepoFactory.AnimeSeries.GetByID(a)).Where(a => a.GetVideoLocals().Any() || includeMissing) ??
                     Array.Empty<SVR_AnimeSeries>();

        return series
            .Select(a => _seriesFactory.GetSeries(a, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    public FilterController(ISettingsProvider settingsProvider, FilterFactory factory, SeriesFactory seriesFactory, FilterEvaluator filterEvaluator) : base(settingsProvider)
    {
        _factory = factory;
        _seriesFactory = seriesFactory;
        _filterEvaluator = filterEvaluator;
    }
}
