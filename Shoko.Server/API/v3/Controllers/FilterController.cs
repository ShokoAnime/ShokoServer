using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class FilterController(
    ISettingsProvider settingsProvider,
    TreeController treeController,
    FilterFactory factory,
    IMetadataFilteringService filteringService,
    AnimeGroupRepository animeGroupRepository,
    AnimeSeriesRepository animeSeriesRepository,
    FilterPresetRepository filterPresetRepository
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

        return filteringService.Engine.BatchPrepareFiltersWithGrouping(filterPresetRepository.GetTopLevel(), user, skipSorting: true, cancellationToken: HttpContext.RequestAborted)
            .Where(kv =>
            {
                var filter = kv.Key;
                if (!showHidden && filter.Hidden)
                    return false;

                if (includeEmpty || (filter.IsDirectory ? filterPresetRepository.GetByParentID(filter.FilterPresetID).Count > 0 : kv.Value.Any()))
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

            filterPresetRepository.Save(filterPreset);
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
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
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
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
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

            filterPresetRepository.Save(filterPreset);
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
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        try
        {
            filterPreset = factory.GetFilterPreset(body, ModelState, filterPreset);
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            filterPresetRepository.Save(filterPreset);
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
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        filterPresetRepository.Delete(filterPreset);

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
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        if (!filterPreset.IsDirectory)
            return new ListResult<Filter>();

        return factory.GetFilters(
                filterPresetRepository.GetByParentID(filterID)
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
    /// <param name="topLevelOnly">Only list the top level groups if set.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <returns></returns>
    [HttpGet("{filterID}/Group")]
    public ActionResult<ListResult<Group>> GetFilteredGroups(
        [FromRoute, Range(0, int.MaxValue)] int filterID,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool topLevelOnly = true,
        [FromQuery] bool includeEmpty = true,
        [FromQuery] bool randomImages = false
    )
    {
        // Return the top level groups with no filter.
        if (filterID is 0)
        {
            var user = User;
            return animeGroupRepository.GetAll()
                .Where(group =>
                    (!topLevelOnly || group is { AnimeGroupParentID: null }) &&
                    user.AllowedGroup(group) &&
                    (includeEmpty || group.AllSeries.Any(s => s.VideoLocals.Count > 0))
                )
                .OrderBy(group => group.SortName)
                .ToListResult(group => new Group(group, User.JMMUserID, randomImages), page, pageSize);
        }

        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new ListResult<Group>();

        return GetFilteredGroups(filterPreset, pageSize, page, topLevelOnly, includeEmpty, randomImages);
    }

    private ListResult<Group> GetFilteredGroups(FilterPreset filterPreset, int pageSize, int page, bool topLevelOnly, bool includeEmpty, bool randomImages)
    {
        var user = User;
        return (
            topLevelOnly
                ? filteringService.GetTopLevelFilteredGroups(filterPreset, user, cancellationToken: HttpContext.RequestAborted)
                : filteringService.GetAllFilteredGroupsWithChains(filterPreset, user, cancellationToken: HttpContext.RequestAborted)
        )
            .Select(r => (r, group: (AnimeGroup)r.Group))
            .Where(t => includeEmpty || t.group.AllSeries.Any(s => s.VideoLocals.Count > 0))
            .ToListResult(t => new Group(t.group, user.JMMUserID, randomImages, t.r.GroupIDChains, t.r.SeriesIDs), page, pageSize);
    }

    /// <summary>
    ///   Get a list of filtered group IDs with hierarchy chain information for
    ///   the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="topLevelOnly">Only list the top level groups if set.</param>
    /// <param name="includeEmpty">Include <see cref="Group"/>s with no <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpGet("{filterID}/FilteredIDs")]
    public ActionResult<List<FilteredGroupIDs>> GetFilteredGroupIDs(
        [FromRoute, Range(0, int.MaxValue)] int filterID,
        [FromQuery] bool topLevelOnly = true,
        [FromQuery] bool includeEmpty = true
    )
    {
        // Return all groups with no filter.
        if (filterID is 0)
        {
            var user = User;
            return animeGroupRepository.GetAll()
                .Where(group =>
                    (!topLevelOnly || group is { AnimeGroupParentID: null }) &&
                    user.AllowedGroup(group) &&
                    (includeEmpty || group.AllSeries.Any(s => s.VideoLocals.Count > 0))
                )
                .Select(group => new FilteredGroupIDs
                {
                    GroupID = group.AnimeGroupID,
                    GroupIDChains = [[group.AnimeGroupID]],
                    SeriesIDs = group.AllSeries.Select(s => s.AnimeSeriesID).ToHashSet(),
                })
                .ToList();
        }

        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<FilteredGroupIDs>();

        return GetFilteredGroupIDs(filterPreset, topLevelOnly, includeEmpty);
    }

    private List<FilteredGroupIDs> GetFilteredGroupIDs(FilterPreset filterPreset, bool topLevelOnly, bool includeEmpty)
    {
        var user = User;
        return (
            topLevelOnly
                ? filteringService.GetTopLevelFilteredGroups(filterPreset, user, cancellationToken: HttpContext.RequestAborted)
                : filteringService.GetAllFilteredGroupsWithChains(filterPreset, user, cancellationToken: HttpContext.RequestAborted)
        )
            .Select(r => (r, group: (AnimeGroup)r.Group))
            .Where(t => includeEmpty || t.group.AllSeries.Any(s => s.VideoLocals.Count > 0))
            .Select(t => new FilteredGroupIDs
            {
                GroupID = t.group.AnimeGroupID,
                GroupIDChains = t.r.GroupIDChains,
                SeriesIDs = t.r.SeriesIDs,
            })
            .ToList();
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
            return animeSeriesRepository.GetAll()
                .Where(series => user.AllowedSeries(series) && (includeMissing || series.VideoLocals.Count > 0))
                .OrderBy(series => series.Title.ToSortName())
                .ThenBy(series => series.AniDB_ID)
                .ToListResult(series => new Series(series, User.JMMUserID, randomImages), page, pageSize);

        // Check if the group filter exists.
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new ListResult<Series>();

        return GetFilteredSeries(filterPreset, pageSize, page, includeMissing, randomImages);
    }

    private ListResult<Series> GetFilteredSeries(FilterPreset filterPreset, int pageSize, int page, bool includeMissing, bool randomImages)
    {
        var user = User;
        return filteringService.GetAllFilteredSeries(filterPreset, user, cancellationToken: HttpContext.RequestAborted)
            .Cast<AnimeSeries>()
            .Where(s => s is not null && (includeMissing || s.VideoLocals.Count > 0))
            .ToListResult(s => new Series(s, user.JMMUserID, randomImages), page, pageSize);
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
            return animeSeriesRepository.GetAll()
                .Select(group => group.AnimeSeriesID)
                .ToList();
        }

        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<int>();

        // Gets Series and Series IDs in a filter, already sorted by the filter
        var results = filteringService.Engine.EvaluateFilterWithTuples(filterPreset, user, cancellationToken: HttpContext.RequestAborted);
        return results
            .Select(tuple => tuple.SeriesID)
            .ToList();
    }

    /// <summary>
    ///   Get a list of all (GroupID, SeriesID) tuples for the <see cref="Filter"/>
    ///   with the given <paramref name="filterID"/> for client-side filtering.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <returns></returns>
    [HttpGet("{filterID}/TupleIDs")]
    public ActionResult<List<GroupSeriesTuple>> GetFilteredTuples([FromRoute, Range(0, int.MaxValue)] int filterID)
    {
        var user = User;
        if (filterID is 0)
        {
            return animeSeriesRepository.GetAll()
                .Select(series => new GroupSeriesTuple
                {
                    GroupID = series.AnimeGroupID,
                    SeriesID = series.AnimeSeriesID,
                })
                .ToList();
        }

        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<GroupSeriesTuple>();

        var results = filteringService.Engine.EvaluateFilterWithTuples(filterPreset, user, cancellationToken: HttpContext.RequestAborted);
        return results
            .Select(tuple => new GroupSeriesTuple
            {
                GroupID = tuple.GroupID,
                SeriesID = tuple.SeriesID,
            })
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
        {
            treeController.ControllerContext = ControllerContext;
            return treeController.GetSubGroups(groupID, randomImages, includeEmpty);
        }

        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        // Check if the group exists.
        if (animeGroupRepository.GetByID(groupID) is not { } group)
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
        var user = User;
        return filteringService.GetFilteredSubGroups(filterPreset, group, user, cancellationToken: HttpContext.RequestAborted)
            .Select(r => (r, group: (AnimeGroup)r.Group))
            .Where(t => includeEmpty || t.group.AllSeries.Any(s => s.VideoLocals.Count > 0))
            .Select(t => new Group(t.group, user.JMMUserID, randomImages, t.r.GroupIDChains, t.r.SeriesIDs))
            .ToList();
    }

    /// <summary>
    ///   Get a list of filtered sub-group IDs with hierarchy chain information
    ///   for the <see cref="Group"/> within the <see cref="Filter"/>.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="includeEmpty">Include <see cref="Group"/>s with no <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpGet("{filterID}/Group/{groupID}/FilteredIDs")]
    public ActionResult<List<FilteredGroupIDs>> GetFilteredSubGroupIDs(
        [FromRoute, Range(0, int.MaxValue)] int filterID,
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool includeEmpty = true
    )
    {
        // Return sub-groups with no filter applied.
        if (filterID is 0)
        {
            if (animeGroupRepository.GetByID(groupID) is not { } rootGroup)
                return NotFound(GroupController.GroupNotFound);

            var currentUser = User;
            if (!currentUser.AllowedGroup(rootGroup))
                return Forbid(GroupController.GroupForbiddenForUser);

            return rootGroup.Children
                .Where(child =>
                    currentUser.AllowedGroup(child) &&
                    (includeEmpty || child.AllSeries.Any(s => s.VideoLocals.Count > 0))
                )
                .Select(child => new FilteredGroupIDs
                {
                    GroupID = child.AnimeGroupID,
                    GroupIDChains = [[rootGroup.AnimeGroupID, child.AnimeGroupID]],
                    SeriesIDs = child.AllSeries.Select(s => s.AnimeSeriesID).ToHashSet(),
                })
                .ToList();
        }

        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        if (animeGroupRepository.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory)
            return new List<FilteredGroupIDs>();

        return GetFilteredSubGroupIDs(group, filterPreset, includeEmpty);
    }

    private List<FilteredGroupIDs> GetFilteredSubGroupIDs(AnimeGroup group, FilterPreset filterPreset, bool includeEmpty)
    {
        var user = User;
        return filteringService.GetFilteredSubGroups(filterPreset, group, user, cancellationToken: HttpContext.RequestAborted)
            .Select(r => (r, group: (AnimeGroup)r.Group))
            .Where(t => includeEmpty || t.group.AllSeries.Any(s => s.VideoLocals.Count > 0))
            .Select(t => new FilteredGroupIDs
            {
                GroupID = t.group.AnimeGroupID,
                GroupIDChains = t.r.GroupIDChains,
                SeriesIDs = t.r.SeriesIDs,
            })
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
        {
            treeController.ControllerContext = ControllerContext;
            return treeController.GetSeriesInGroup(groupID, recursive, includeMissing, randomImages, includeDataFrom);
        }

        // Check if the group filter exists.
        if (filterPresetRepository.GetByID(filterID) is not { } filterPreset)
            return NotFound(FilterNotFound);

        if (!filterPreset.ApplyAtSeriesLevel)
        {
            treeController.ControllerContext = ControllerContext;
            return treeController.GetSeriesInGroup(groupID, recursive, includeMissing, randomImages);
        }

        // Check if the group exists.
        if (animeGroupRepository.GetByID(groupID) is not { } group)
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
        var user = User;
        return filteringService.GetFilteredSeriesInGroup(filterPreset, group, recursive, user, cancellationToken: HttpContext.RequestAborted)
            .Cast<AnimeSeries>()
            .Where(a => includeMissing || (a.VideoLocals.Count > 0))
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
    /// <param name="topLevelOnly">Only list the top level groups if set.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Group"/>.</param>
    /// <returns></returns>
    [HttpPost("Preview/Group")]
    public ActionResult<ListResult<Group>> GetPreviewFilteredGroups(
        [FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool topLevelOnly = true,
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

        return GetFilteredGroups(filterPreset, pageSize, page, topLevelOnly, includeEmpty, randomImages);
    }

    /// <summary>
    ///   Get a list of filtered group IDs with hierarchy chain information for
    ///   the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="topLevelOnly">Only list the top level groups if set.</param>
    /// <param name="includeEmpty">Include <see cref="Group"/>s with no <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpPost("Preview/FilteredIDs")]
    public ActionResult<List<FilteredGroupIDs>> GetPreviewFilteredGroupIDs(
        [FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromQuery] bool topLevelOnly = true,
        [FromQuery] bool includeEmpty = true
    )
    {
        // Directories should only contain sub-filters, not groups and series.
        if (filter.IsDirectory)
            return new List<FilteredGroupIDs>();

        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return GetFilteredGroupIDs(filterPreset, topLevelOnly, includeEmpty);
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

        var results = filteringService.Engine.EvaluateFilterWithTuples(filterPreset, User, cancellationToken: HttpContext.RequestAborted);
        return results
            .Select(tuple => tuple.SeriesID)
            .ToList();
    }

    /// <summary>
    ///   Get a list of all (GroupID, SeriesID) tuples for the live filter
    ///   for client-side filtering.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <returns></returns>
    [HttpPost("Preview/TupleIDs")]
    public ActionResult<List<GroupSeriesTuple>> GetPreviewFilteredTuples([FromBody] Filter.Input.CreateOrUpdateFilterBody filter)
    {
        if (filter.IsDirectory)
            return new List<GroupSeriesTuple>();

        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var results = filteringService.Engine.EvaluateFilterWithTuples(filterPreset, User, cancellationToken: HttpContext.RequestAborted);
        return results
            .Select(tuple => new GroupSeriesTuple
            {
                GroupID = tuple.GroupID,
                SeriesID = tuple.SeriesID,
            })
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
        if (animeGroupRepository.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        if (!User.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        return GetFilteredSubGroups(group, filterPreset, randomImages, includeEmpty);
    }

    /// <summary>
    ///   Get a list of filtered sub-group IDs with hierarchy chain information
    ///   for the <see cref="Group"/> within the live filter.
    /// </summary>
    /// <param name="filter">The filter to preview</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="includeEmpty">Include <see cref="Group"/>s with no <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpPost("Preview/Group/{groupID}/FilteredIDs")]
    public ActionResult<List<FilteredGroupIDs>> GetPreviewFilteredSubGroupIDs(
        [FromBody] Filter.Input.CreateOrUpdateFilterBody filter,
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool includeEmpty = true
    )
    {
        var filterPreset = factory.GetFilterPreset(filter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (animeGroupRepository.GetByID(groupID) is not { } group)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        return GetFilteredSubGroupIDs(group, filterPreset, includeEmpty);
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

        if (animeGroupRepository.GetByID(groupID) is not { } group)
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
        [FromQuery] FilterExpressionParameterType[]? types = null,
        [FromQuery] FilterExpressionGroup[]? groups = null
    )
    {
        types ??= [];
        groups ??= [];
        _expressionTypes ??= ExpressionDiscovery.GetExpressionHelp()
            .Select(a => new Filter.FilterExpressionHelp(a))
            .ToArray();
        if (types.Length == 0 && groups.Length == 0)
            return _expressionTypes;

        return _expressionTypes
            .Where(a => (types.Length == 0 || types.Contains(a.Type)) && (groups.Length == 0 || groups.Contains(a.Group)))
            .ToArray();
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
        => _sortingTypes ??= ExpressionDiscovery.GetSortingExpressionHelp()
            .Select(a => new Filter.SortingCriteriaHelp(a))
            .ToArray();

    #endregion
}
