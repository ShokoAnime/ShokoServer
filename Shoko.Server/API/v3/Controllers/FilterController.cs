using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class FilterController : BaseController
{
    internal const string FilterNotFound = "No Filter entry for the given filterID";

    private readonly FilterFactory _factory;
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
        [FromQuery] bool showHidden = false, [FromQuery, Range(0, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery] bool withConditions = false)
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
            var filterPreset = _factory.GetFilterPreset(body, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            RepoFactory.FilterPreset.Save(filterPreset);
            return _factory.GetFilter(filterPreset, true);
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
    /// <param name="types">Optional. The Expression types to return</param>
    /// <param name="groups">Optional. The Expression groups to return</param>
    [HttpGet("Expressions")]
    public ActionResult<Filter.FilterExpressionHelp[]> GetExpressions([FromQuery] Filter.FilterExpressionHelp.FilterExpressionParameterType[] types = null,
        [FromQuery] FilterExpressionGroup[] groups = null)
    {
        types ??= [];
        groups ??= [];

        // get all classes that derive from FilterExpression, but not SortingExpression
        _expressionTypes ??= AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => a != typeof(FilterExpression) && !a.IsGenericType && typeof(FilterExpression).IsAssignableFrom(a) &&
                        !typeof(SortingExpression).IsAssignableFrom(a))
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
                    IWithStringSetSelectorParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.StringSetSelector,
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
                    FilterExpression<IReadOnlySet<string>> => Filter.FilterExpressionHelp.FilterExpressionParameterType.StringSetSelector,
                    _ => throw new Exception($"Expression {a.Name} is not a handled type for Filter Expression Help")
                };
                return new Filter.FilterExpressionHelp
                {
                    Expression = a.Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector").Trim(),
                    Name = expression.Name,
                    Group = expression.Group,
                    Description = expression.HelpDescription,
                    PossibleParameters = expression.HelpPossibleParameters,
                    PossibleSecondParameters = expression.HelpPossibleSecondParameters,
                    PossibleParameterPairs = expression.HelpPossibleParameterPairs,
                    Left = left,
                    Right = right,
                    Parameter = parameter,
                    SecondParameter = secondParameter,
                    Type = type
                };
            }).Where(a => a != null && (types.Length == 0 || types.Contains(a.Type)) && (groups.Length == 0 || groups.Contains(a.Group))).ToArray();
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
                    Type = a.Name.TrimEnd("SortingSelector").Trim(),
                    Name = criteria.Name,
                    Description = criteria.HelpDescription
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
    public ActionResult<Filter> GetFilter([FromRoute, Range(1, int.MaxValue)] int filterID, [FromQuery] bool withConditions = false)
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
    public ActionResult<Filter> PatchFilter([FromRoute, Range(1, int.MaxValue)] int filterID, JsonPatchDocument<Filter.Input.CreateOrUpdateFilterBody> document)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        try
        {
            var body = _factory.GetPostModel(filterPreset);
            document.ApplyTo(body, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            filterPreset = _factory.GetFilterPreset(body, ModelState, filterPreset);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            RepoFactory.FilterPreset.Save(filterPreset);
            return _factory.GetFilter(filterPreset, true);
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
    public ActionResult<Filter> PutFilter([FromRoute, Range(1, int.MaxValue)] int filterID, Filter.Input.CreateOrUpdateFilterBody body)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        try
        {
            filterPreset = _factory.GetFilterPreset(body, ModelState, filterPreset);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            RepoFactory.FilterPreset.Save(filterPreset);
            return _factory.GetFilter(filterPreset, true);
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
    public ActionResult DeleteFilter([FromRoute, Range(1, int.MaxValue)] int filterID)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterNotFound);

        RepoFactory.FilterPreset.Delete(filterPreset);

        return NoContent();
    }

    #endregion


    #region Preview/On-the-fly Filter

    /// <summary>
    /// Get a paginated list of all the top-level <see cref="Group"/>s for the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <param name="orderByName">Ignore the group filter sort criteria and always order the returned list by name.</param>
    /// <returns></returns>
    [HttpPost("Preview/Group")]
    public ActionResult<ListResult<Group>> GetPreviewFilteredGroups([FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromQuery, Range(0, 100)] int pageSize = 50, [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeEmpty = false, [FromQuery] bool randomImages = false, [FromQuery] bool orderByName = false)
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new ListResult<Group>();

        // Fast path when user is not in the filter.
        var filterPreset = _factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID);
        if (!results.Any()) return new ListResult<Group>();

        var groups = results
            .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key))
            .Where(group =>
            {
                // not top level groups
                if (group == null || group.AnimeGroupParentID.HasValue)
                    return false;

                return includeEmpty || group.AllSeries
                    .Any(s => s.AnimeEpisodes.Any(e => e.VideoLocals.Count > 0));
            });

        if (orderByName)
            groups = groups.OrderBy(group => group.SortName);

        return groups
            .ToListResult(group => new Group(group, User.JMMUserID, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a dictionary with the count for each starting character in each of
    /// the top-level group's name with the live filter applied.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing
    ///     <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpPost("Preview/Group/Letters")]
    public ActionResult<Dictionary<char, int>> GetPreviewGroupNameLettersInFilter([FromBody] Filter.Input.CreateOrUpdateFilterBody filter, [FromQuery] bool includeEmpty = false)
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new Dictionary<char, int>();

        // Fast path when user is not in the filter
        var filterPreset = _factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new Dictionary<char, int>();

        return results
            .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key))
            .Where(group =>
            {
                if (group is not { AnimeGroupParentID: null })
                    return false;

                return includeEmpty || group.AllSeries
                    .Any(s => s.AnimeEpisodes.Any(e => e.VideoLocals.Count > 0));
            })
            .GroupBy(group => group.SortName[0])
            .OrderBy(groupList => groupList.Key)
            .ToDictionary(groupList => groupList.Key, groupList => groupList.Count());
    }

    /// <summary>
    /// Get a paginated list of all the <see cref="Series"/> within the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/>.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpPost("Preview/Series")]
    public ActionResult<ListResult<Series>> GetPreviewSeriesInFilteredGroup([FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromQuery, Range(0, 100)] int pageSize = 50, [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeMissing = false)
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new ListResult<Series>();

        // Return early if every series will be filtered out.
        var filterPreset = _factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new ListResult<Series>();

        // We don't need separate logic for ApplyAtSeriesLevel, as the FilterEvaluator handles that
        return results.SelectMany(a => a.Select(id => RepoFactory.AnimeSeries.GetByID(id)))
            .Where(series => series != null && (includeMissing || series.VideoLocals.Count > 0))
            .OrderBy(series => series.PreferredTitle.ToLowerInvariant())
            .ToListResult(series => new Series(series, User.JMMUserID, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a list of all the sub-<see cref="Group"/>s belonging to the <see cref="Group"/> with the given <paramref name="groupID"/> and which are present within the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpPost("Preview/Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetPreviewFilteredSubGroups([FromBody] Filter.Input.CreateOrUpdateFilterBody filter, [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeEmpty = false)
    {
        var filterPreset = _factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        if (!User.AllowedGroup(group))
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
        var orderedGroups = results.SelectMany(a => RepoFactory.AnimeGroup.GetByID(a.Key).TopLevelAnimeGroup.AllChildren.Select(b => b.AnimeGroupID)).ToArray();
        var groups = orderedGroups.ToHashSet();

        return group.Children
            .Where(subGroup =>
            {
                if (subGroup == null)
                    return false;

                if (!User.AllowedGroup(subGroup))
                    return false;

                if (!includeEmpty && !subGroup.AllSeries
                        .Any(s => s.AnimeEpisodes.Any(e => e.VideoLocals.Count > 0)))
                    return false;

                return groups.Contains(subGroup.AnimeGroupID);
            })
            .OrderBy(a => Array.IndexOf(orderedGroups, a.AnimeGroupID))
            .Select(g => new Group(g, User.JMMUserID, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// /// <returns></returns>
    [HttpPost("Preview/Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetPreviewSeriesInFilteredGroup([FromBody] Filter.Input.CreateOrUpdateFilterBody filter, [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = false,
        [FromQuery] bool randomImages = false, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var filterPreset = _factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        if (!User.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory())
            return new List<Series>();

        if (!filterPreset.ApplyAtSeriesLevel)
            return (recursive ? group.AllSeries : group.Series)
                .Where(a => User.AllowedSeries(a))
                .OrderBy(series => series.AniDB_Anime?.AirDate ?? DateTime.MaxValue)
                .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
                .Where(series => series.Size > 0 || includeMissing)
                .ToList();

        // Just return early because the every series will be filtered out.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new List<Series>();

        var seriesIDs = results.FirstOrDefault(a => a.Key == groupID)?.ToList();
        seriesIDs ??= recursive
            ? group.AllChildren.SelectMany(a => results.FirstOrDefault(b => b.Key == a.AnimeGroupID)?.ToList() ?? []).ToList()
            : results.FirstOrDefault(a => a.Key == groupID)?.ToList();

        var series = seriesIDs?.Select(RepoFactory.AnimeSeries.GetByID).Where(a => includeMissing || ((a?.VideoLocals.Count ?? 0) != 0)) ?? [];
        return series
            .Select(a => new Series(a, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    public FilterController(ISettingsProvider settingsProvider, FilterFactory factory, FilterEvaluator filterEvaluator) : base(settingsProvider)
    {
        _factory = factory;
        _filterEvaluator = filterEvaluator;
    }
}
