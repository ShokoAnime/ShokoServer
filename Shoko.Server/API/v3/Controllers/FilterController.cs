using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class FilterController(
    ISettingsProvider settingsProvider,
    TreeController treeController,
    FilterFactory factory,
    IFilterEvaluator filterEvaluator
) : BaseController(settingsProvider)
{
    internal const string FilterNotFound = "No Filter entry for the given filterID";

    private static Filter.FilterExpressionHelp[]? _expressionTypes;

    private static Filter.SortingCriteriaHelp[]? _sortingTypes;

    #region Existing Filters

    /// <summary>
    /// Get all <see cref="Filter"/>s except the live filter.
    /// </summary>
    /// <param name="includeEmpty">Include empty filters.</param>
    /// <param name="includeEmptyGroups">Include empty groups for size calculations.</param>
    /// <param name="showHidden">Show hidden filters.</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="withConditions">Include conditions and sort criteria in the response.</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<ListResult<Filter>> GetAllFilters([FromQuery] bool includeEmpty = false, [FromQuery] bool includeEmptyGroups = true,
        [FromQuery] bool showHidden = false, [FromQuery, Range(0, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery] bool withConditions = false)
    {
        var user = User;

        return filterEvaluator.BatchPrepareFiltersWithGrouping(RepoFactory.FilterPreset.GetTopLevel(), user, skipSorting: true)
            .Where(kv =>
            {
                var filter = kv.Key;
                if (!showHidden && filter.Hidden)
                    return false;

                if (includeEmpty || (filter.IsDirectory ? RepoFactory.FilterPreset.GetByParentID(filter.FilterPresetID).Count > 0 : kv.Value.Any()))
                    return true;

                return false;
            })
            .Select(a => a.Key)
            .OrderBy(filter => filter.Name)
            .ToListResult(filter => factory.GetFilter(filter, withConditions, includeEmptyGroups), page, pageSize);
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
            var filterPreset = factory.GetFilterPreset(body, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            RepoFactory.FilterPreset.Save(filterPreset);
            return factory.GetFilter(filterPreset, true);
        }
        catch (ArgumentException e)
        {
            return ValidationProblem(e.Message, "Expression");
        }
    }

    /// <summary>
    /// Get the <see cref="Filter"/> for the given <paramref name="filterID"/>.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="withConditions">Include conditions and sort criteria in the response.</param>
    /// <param name="includeEmptyGroups">Include empty groups for size calculations.</param>
    /// <returns>The filter</returns>
    [HttpGet("{filterID}")]
    public ActionResult<Filter> GetFilter([FromRoute, Range(1, int.MaxValue)] int filterID, [FromQuery] bool withConditions = false, [FromQuery] bool includeEmptyGroups = true)
    {
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        return factory.GetFilter(filterPreset, withConditions, includeEmptyGroups);
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
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        try
        {
            var body = factory.GetPostModel(filterPreset);
            document.ApplyTo(body, ModelState);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            filterPreset = factory.GetFilterPreset(body, ModelState, filterPreset);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            RepoFactory.FilterPreset.Save(filterPreset);
            return factory.GetFilter(filterPreset, true);
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
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        try
        {
            filterPreset = factory.GetFilterPreset(body, ModelState, filterPreset);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            RepoFactory.FilterPreset.Save(filterPreset);
            return factory.GetFilter(filterPreset, true);
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
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        RepoFactory.FilterPreset.Delete(filterPreset);

        return NoContent();
    }

    /// <summary>
    /// Get a list of all the sub-<see cref="Filter"/> for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to true to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="showHidden">Show hidden filters</param>
    /// <returns></returns>
    [HttpGet("{filterID}/Filter")]
    public ActionResult<ListResult<Filter>> GetSubFilters
        ([FromRoute, Range(1, int.MaxValue)] int filterID,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showHidden = false
    )
    {
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        if (!filterPreset.IsDirectory)
            return new ListResult<Filter>();

        return factory.GetFilters(
                RepoFactory.FilterPreset.GetByParentID(filterID)
                    .Where(filter => showHidden || !filter.Hidden)
                    .OrderBy(a => a.Name)
                    .ToList()
            )
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of all the top-level <see cref="Group"/>s for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <returns></returns>
    [HttpGet("{filterID}/Group")]
    public ActionResult<ListResult<Group>> GetFilteredGroups(
        [FromRoute, Range(0, int.MaxValue)] int filterID,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeEmpty = true,
        [FromQuery] bool randomImages = false
    )
    {
        // Return the top level groups with no filter.
        if (filterID is 0)
        {
            var user = User;
            return RepoFactory.AnimeGroup.GetAll()
                .Where(group =>
                    group is { AnimeGroupParentID: null } &&
                    user.AllowedGroup(group) &&
                    (includeEmpty || group.AllSeries.Any(s => s.VideoLocals.Count > 0))
                )
                .OrderBy(group => group.SortName)
                .ToListResult(group => new Group(group, User.JMMUserID, randomImages), page, pageSize);
        }

        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new ListResult<Group>();

        return GetFilteredGroups(filterPreset, pageSize, page, includeEmpty, randomImages);
    }

    private ListResult<Group> GetFilteredGroups(FilterPreset filterPreset, int pageSize, int page, bool includeEmpty, bool randomImages)
    {
        // Just return early because the every group will be filtered out.
        var results = filterEvaluator.EvaluateFilterWithGrouping(filterPreset, User);
        if (results.Count is 0)
            return new ListResult<Group>();

        // Sort the results because they're unordered.
        if (filterPreset.SortingExpression is null)
            return results
                .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key)?.TopLevelAnimeGroup)
                .WhereNotNull()
                .DistinctBy(group => group.AnimeGroupID)
                .Where(group => includeEmpty || group.AllSeries.Any(s => s.VideoLocals.Count > 0))
                .OrderBy(group => group.SortName)
                .ToListResult(group => new Group(group, User.JMMUserID, randomImages), page, pageSize);

        // The results are pre-sorted, so just return them as-is.
        return results
            .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key)?.TopLevelAnimeGroup)
            .WhereNotNull()
            .DistinctBy(group => group.AnimeGroupID)
            .Where(group => includeEmpty || group.AllSeries.Any(s => s.VideoLocals.Count > 0))
            .ToListResult(group => new Group(group, User.JMMUserID, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of all the <see cref="Series"/> within a <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter.
    /// <br/>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpGet("{filterID}/Series")]
    public ActionResult<ListResult<Series>> GetSeriesInFilteredGroup(
        [FromRoute, Range(0, int.MaxValue)] int filterID,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeMissing = true,
        [FromQuery] bool randomImages = false
    )
    {
        // Return the series with no group filter applied.
        var user = User;
        if (filterID is 0)
            return RepoFactory.AnimeSeries.GetAll()
                .Where(series => user.AllowedSeries(series) && (includeMissing || series.VideoLocals.Count > 0))
                .OrderBy(series => series.Title.ToSortName())
                .ThenBy(series => series.AniDB_ID)
                .ToListResult(series => new Series(series, User.JMMUserID, randomImages), page, pageSize);

        // Check if the group filter exists.
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new ListResult<Series>();

        return GetFilteredSeries(filterPreset, pageSize, page, includeMissing, randomImages);
    }

    private ListResult<Series> GetFilteredSeries(FilterPreset filterPreset, int pageSize, int page, bool includeMissing, bool randomImages)
    {
        // Just return early because the every group will be filtered out.
        var results = filterEvaluator.EvaluateFilterWithTuples(filterPreset, User);
        if (results.Count is 0)
            return new ListResult<Series>();

        // Sort the results because they're unordered.
        var user = User;
        if (filterPreset.SortingExpression is null)
            return results
                .Select(tuple => RepoFactory.AnimeSeries.GetByID(tuple.SeriesID))
                .Where(series => user.AllowedSeries(series) && (includeMissing || series.VideoLocals.Count > 0))
                .OrderBy(series => series.Title.ToSortName())
                .ThenBy(series => series.AniDB_ID)
                .ToListResult(series => new Series(series, User.JMMUserID, randomImages), page, pageSize);

        // The results are pre-sorted, so just return them as-is.
        return results
            .Select(tuple => RepoFactory.AnimeSeries.GetByID(tuple.SeriesID))
            .Where(series => user.AllowedSeries(series) && (includeMissing || series.VideoLocals.Count > 0))
            .ToListResult(series => new Series(series, User.JMMUserID, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a raw list of <see cref="Series"/> IDs for the <see cref="Filter"/>
    /// with the given <paramref name="filterID"/> for client-side filtering.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{filterID}/Series/OnlyIDs")]
    public ActionResult<List<int>> GetFilteredSeriesIDs([FromRoute, Range(0, int.MaxValue)] int filterID)
    {
        var user = User;
        if (filterID is 0)
        {
            return RepoFactory.AnimeSeries.GetAll()
                .Select(group => group.AnimeSeriesID)
                .ToList();
        }

        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<int>();

        // Gets Series and Series IDs in a filter, already sorted by the filter
        var results = filterEvaluator.EvaluateFilterWithTuples(filterPreset, user);
        return results
            .Select(tuple => tuple.SeriesID)
            .ToList();
    }

    /// <summary>
    /// Get a list of all the sub-<see cref="Group"/>s belonging to the <see cref="Group"/> with the given <paramref name="groupID"/> and which are present within the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("{filterID}/Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetFilteredSubGroups([FromRoute, Range(0, int.MaxValue)] int filterID, [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeEmpty = true)
    {
        // Return sub-groups with no group filter applied.
        if (filterID is 0)
            return treeController.GetSubGroups(groupID, randomImages, includeEmpty);

        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Check if the group exists.
        if (RepoFactory.AnimeGroup.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<Group>();

        return GetFilteredSubGroups(group, filterPreset, randomImages, includeEmpty);
    }

    private List<Group> GetFilteredSubGroups(AnimeGroup group, FilterPreset filterPreset, bool randomImages, bool includeEmpty)
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return [];

        // Just return early because the every group will be filtered out.
        var user = User;
        var results = filterEvaluator.EvaluateFilterWithGrouping(filterPreset, user);
        if (results.Count is 0)
            return [];

        var groupIDs = group.Children.Select(a => a.AnimeGroupID).ToHashSet();
        var orderedGroupIDs = results
            .Select(a =>
                RepoFactory.AnimeGroup.GetByID(a.Key)!.AllGroupsAbove
                    .Select(b => b.AnimeGroupID)
                    .Append(a.Key)
                    .ToArray()
            )
            .Where(groupIDs.Overlaps)
            .SelectMany(a => a)
            .ToArray();
        groupIDs.IntersectWith(orderedGroupIDs);
        if (groupIDs.Count is 0)
            return [];

        // Sort the results because they're unordered.
        if (filterPreset.SortingExpression is null)
            return groupIDs
                .Select(RepoFactory.AnimeGroup.GetByID)
                .Where(a => user.AllowedGroup(a) && (includeEmpty || !a.AllSeries.Any(s => s.VideoLocals.Count > 0)))
                .OrderBy(g => g.SortName)
                .Select(g => new Group(g, User.JMMUserID, randomImages))
                .ToList();

        // The results are pre-sorted, so just return them as-is.
        return groupIDs
            .OrderBy(a => Array.IndexOf(orderedGroupIDs, a))
            .Select(RepoFactory.AnimeGroup.GetByID)
            .Where(a => user.AllowedGroup(a) && (includeEmpty || !a.AllSeries.Any(s => s.VideoLocals.Count > 0)))
            .Select(g => new Group(g, user.JMMUserID, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter.
    /// <br/>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// /// <returns></returns>
    [HttpGet("{filterID}/Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetSeriesInFilteredGroup([FromRoute, Range(0, int.MaxValue)] int filterID, [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = true,
        [FromQuery] bool randomImages = false, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null)
    {
        // Return the groups with no group filter applied.
        if (filterID is 0)
            return treeController.GetSeriesInGroup(groupID, recursive, includeMissing, randomImages, includeDataFrom);

        // Check if the group filter exists.
        if (RepoFactory.FilterPreset.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterController.FilterNotFound);

        if (!filterPreset.ApplyAtSeriesLevel)
            return treeController.GetSeriesInGroup(groupID, recursive, includeMissing, randomImages);

        // Check if the group exists.
        if (RepoFactory.AnimeGroup.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<Series>();

        return GetSeriesInFilteredGroup(group, filterPreset, recursive, includeMissing, randomImages, includeDataFrom);
    }

    private List<Series> GetSeriesInFilteredGroup(
        AnimeGroup group, FilterPreset filterPreset, bool recursive, bool includeMissing, bool randomImages, HashSet<DataSourceType>? includeDataFrom
    )
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return [];

        var user = User;
        if (!filterPreset.ApplyAtSeriesLevel)
            return (recursive ? group.AllSeries : group.Series)
                .Where(a => user.AllowedSeries(a) && (includeMissing || a.VideoLocals.Count > 0))
                .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .Select(series => new Series(series, user.JMMUserID, randomImages, includeDataFrom))
                .ToList();

        // Just return early because the every series will be filtered out.
        var results = filterEvaluator.EvaluateFilterWithTuples(filterPreset, user);
        if (results.Count is 0)
            return [];

        var validGroupIDs = recursive
            ? group.AllChildren.Prepend(group).Select(a => a.AnimeGroupID).ToHashSet()
            : [group.AnimeGroupID];
        var seriesIDs = results.Where(a => validGroupIDs.Contains(a.GroupID)).Select(a => a.SeriesID).ToHashSet();
        if (seriesIDs.Count is 0)
            return [];

        // Sort the results because they're unordered.
        if (filterPreset.SortingExpression is null)
            return seriesIDs
                .Select(RepoFactory.AnimeSeries.GetByID)
                .Where(a => user.AllowedSeries(a) && (includeMissing || ((a?.VideoLocals.Count ?? 0) != 0)))
                .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .Select(a => new Series(a, user.JMMUserID, randomImages, includeDataFrom))
                .ToList();

        // The results are pre-sorted, so just return them as-is.
        return seriesIDs
            .Select(RepoFactory.AnimeSeries.GetByID)
            .Where(a => user.AllowedSeries(a) && (includeMissing || ((a?.VideoLocals.Count ?? 0) != 0)))
            .Select(a => new Series(a, user.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    #region Preview/Live Filter

    /// <summary>
    /// Get a paginated list of all the top-level <see cref="Group"/>s for the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <returns></returns>
    [HttpPost("Preview/Group")]
    public ActionResult<ListResult<Group>> GetPreviewFilteredGroups(
        [FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeEmpty = true,
        [FromQuery] bool randomImages = false
    )
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new ListResult<Group>();

        // Fast path when user is not in the filter.
        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return GetFilteredGroups(filterPreset, pageSize, page, includeEmpty, randomImages);
    }

    /// <summary>
    /// Get a paginated list of all the <see cref="Series"/> within the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpPost("Preview/Series")]
    public ActionResult<ListResult<Series>> GetPreviewSeriesInFilteredGroup(
        [FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeMissing = true,
        [FromQuery] bool randomImages = false
    )
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new ListResult<Series>();

        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return GetFilteredSeries(filterPreset, pageSize, page, includeMissing, randomImages);
    }

    /// <summary>
    /// Get a raw list of all <see cref="Series"/> IDs for the live filter for
    /// client-side filtering.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <returns></returns>
    [HttpPost("Preview/Series/OnlyIDs")]
    public ActionResult<List<int>> GetPreviewFilteredSeriesIDs([FromBody] Filter.Input.CreateOrUpdateFilterBody filter)
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new List<int>();

        // Fast path when user is not in the filter.
        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var results = filterEvaluator.EvaluateFilterWithTuples(filterPreset, User);
        return results
            .Select(tuple => tuple.SeriesID)
            .ToList();
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
        [FromQuery] bool randomImages = false, [FromQuery] bool includeEmpty = true)
    {
        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Check if the group exists.
        if (RepoFactory.AnimeGroup.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        if (!User.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        return GetFilteredSubGroups(group, filterPreset, randomImages, includeEmpty);
    }

    /// <summary>
    /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomize images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// /// <returns></returns>
    [HttpPost("Preview/Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetPreviewSeriesInFilteredGroup(
        [FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool recursive = false,
        [FromQuery] bool includeMissing = true,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null
    )
    {
        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (RepoFactory.AnimeGroup.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        return GetSeriesInFilteredGroup(group, filterPreset, recursive, includeMissing, randomImages, includeDataFrom);
    }

    #endregion

    #region Expressions

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
    public ActionResult<Filter.FilterExpressionHelp[]> GetExpressions(
        [FromQuery] Filter.FilterExpressionHelp.FilterExpressionParameterType[]? types = null,
        [FromQuery] Filter.FilterExpressionHelp.FilterExpressionGroup[]? groups = null
    )
    {
        types ??= [];
        groups ??= [];

        // get all classes that derive from FilterExpression, but not SortingExpression
        _expressionTypes ??= AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a =>
                a != typeof(FilterExpression) && !a.IsGenericType &&
                typeof(FilterExpression).IsAssignableFrom(a) &&
                !typeof(SortingExpression).IsAssignableFrom(a)
            )
            .OrderBy(a => a.FullName)
            .Select(a =>
            {
                var expression = (FilterExpression?)Activator.CreateInstance(a);
                if (expression == null)
                    return null;
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
                    IWithBoolParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Bool,
                    IWithDateParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Date,
                    IWithNumberParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.Number,
                    IWithStringParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.String,
                    IWithStringSetParameter => Filter.FilterExpressionHelp.FilterExpressionParameterType.StringSet,
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
                    Group = (Filter.FilterExpressionHelp.FilterExpressionGroup)expression.Group,
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
            })
            .WhereNotNull()
            .Where(a => (types.Length == 0 || types.Contains(a.Type)) && (groups.Length == 0 || groups.Contains(a.Group)))
            .ToArray();
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
                var criteria = (SortingExpression?)Activator.CreateInstance(a);
                if (criteria == null) return null;
                return new Filter.SortingCriteriaHelp
                {
                    Type = a.Name.TrimEnd("SortingSelector").Trim(),
                    Name = criteria.Name,
                    Description = criteria.HelpDescription
                };
            }).WhereNotNull().ToArray();
        return _sortingTypes;
    }

    #endregion
}
