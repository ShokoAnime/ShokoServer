using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Caching.Memory;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
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
    private static IMemoryCache PreviewCache = new MemoryCache(new MemoryCacheOptions() {
        ExpirationScanFrequency = TimeSpan.FromMinutes(50),
    });

    internal const string FilterNotFound = "No Filter entry for the given filterID";

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
        return RepoFactory.GroupFilter.GetTopLevel()
            .Where(filter =>
            {
                if (!showHidden && filter.IsHidden)
                    return false;

                if (includeEmpty || (filter.IsDirectory ? (
                        // Check if the directory filter have any sub-directories
                        RepoFactory.GroupFilter.GetByParentID(filter.GroupFilterID).Count > 0
                    ) : (
                        // Check if the filter have any groups for the current user.
                        filter.GroupsIds.ContainsKey(user.JMMUserID) && filter.GroupsIds[user.JMMUserID].Count > 0
                    )))
                    return true;

                return false;
            })
            .OrderBy(filter => filter.GroupFilterName)
            .ToListResult(filter => new Filter(HttpContext, filter, withConditions), page, pageSize);
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
        var groupFilter = new SVR_GroupFilter { FilterType = (int)GroupFilterType.UserDefined };
        var filter = body.MergeWithExisting(HttpContext, groupFilter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return filter;
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
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
            return NotFound(FilterNotFound);

        return new Filter(HttpContext, groupFilter, withConditions);
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
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
            return NotFound(FilterNotFound);

        var body = new Filter.Input.CreateOrUpdateFilterBody(groupFilter);
        document.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var filter = body.MergeWithExisting(HttpContext, groupFilter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return filter;
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
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
            return NotFound(FilterNotFound);

        var filter = body.MergeWithExisting(HttpContext, groupFilter, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return filter;
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
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
            return NotFound(FilterNotFound);

        RepoFactory.GroupFilter.Delete(groupFilter);

        return NoContent();
    }

    #endregion


    #region Preview/On-the-fly Filter

    [NonAction]
    internal static SVR_GroupFilter GetDefaultFilterForUser(SVR_JMMUser user)
    {

        var groupFilter = new SVR_GroupFilter
        {
            FilterType = (int)GroupFilterType.UserDefined,
            GroupFilterName = "Live Filtering",
            InvisibleInClients = 0,
            ApplyToSeries = 0,
            BaseCondition = (int)GroupFilterBaseCondition.Include,
            Conditions = new(),
            SortCriteriaList = new(),
        };

        // TODO: Update default filter for user here.

        return groupFilter;
    }

    [NonAction]
    internal static SVR_GroupFilter GetPreviewFilterForUser(SVR_JMMUser user)
    {
        var userId = user.JMMUserID;
        var key = $"User={userId}";
        if (!PreviewCache.TryGetValue(key, out SVR_GroupFilter groupFilter))
            groupFilter = PreviewCache.Set(key, GetDefaultFilterForUser(user), new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromHours(1) });

        return groupFilter;
    }

    [NonAction]
    internal static bool ResetPreviewFilterForUser(SVR_JMMUser user)
    {
        var userId = user.JMMUserID;
        var key = $"User={userId}";
        if (!PreviewCache.TryGetValue(key, out SVR_GroupFilter groupFilter))
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
        var groupFilter = GetPreviewFilterForUser(User);
        return new Filter.Input.CreateOrUpdateFilterBody(groupFilter);
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
        var groupFilter = GetPreviewFilterForUser(User);

        var body = new Filter.Input.CreateOrUpdateFilterBody(groupFilter);
        document.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var filter = body.MergeWithExisting(HttpContext, groupFilter, ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return new Filter.Input.CreateOrUpdateFilterBody(groupFilter);
    }

    /// <summary>
    /// Edit the live filter for the current user using a raw object.
    /// </summary>
    /// <param name="body">The full document for the changes to be made to the filter.</param>
    /// <returns>The updated live filter.</returns>
    [HttpPut("Preview")]
    public ActionResult<Filter.Input.CreateOrUpdateFilterBody> PutPreviewFilter([FromBody] Filter.Input.CreateOrUpdateFilterBody body)
    {
        var groupFilter = GetPreviewFilterForUser(User);
        var filter = body.MergeWithExisting(HttpContext, groupFilter, ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return new Filter.Input.CreateOrUpdateFilterBody(groupFilter);
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
        var groupFilter = GetPreviewFilterForUser(User);
        if (groupFilter.IsDirectory)
            return new ListResult<Group>();

        // Fast path when user is not in the filter.
        if (!groupFilter.GroupsIds.TryGetValue(User.JMMUserID, out var groupIds))
            return new ListResult<Group>();

        var groups = groupIds
            .Select(group => RepoFactory.AnimeGroup.GetByID(group))
            .Where(group =>
            {
                if (group == null || group.AnimeGroupParentID.HasValue)
                {
                    return false;
                }

                return includeEmpty || group.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            });

        groups = orderByName ? groups.OrderBy(group => group.GetSortName()) :
            groups.OrderByGroupFilter(groupFilter);

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
        var groupFilter = GetPreviewFilterForUser(user);
        if (groupFilter.IsDirectory)
            return new Dictionary<char, int>();

        // Fast path when user is not in the filter
        if (!groupFilter.GroupsIds.TryGetValue(user.JMMUserID, out var groupIds))
            return new Dictionary<char, int>();

        return groupIds
            .Select(group => RepoFactory.AnimeGroup.GetByID(group))
            .Where(group =>
            {
                if (group == null || group.AnimeGroupParentID.HasValue)
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
        var groupFilter = GetPreviewFilterForUser(user);
        if (groupFilter.IsDirectory)
            return new ListResult<Series>();

        // Return all series if group filter is not applied to series.
        if (groupFilter.ApplyToSeries != 1)
            return RepoFactory.AnimeSeries.GetAll()
                .Where(series => user.AllowedSeries(series) && (includeMissing || series.GetVideoLocals().Count > 0))
                .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
                .ToListResult(series => new Series(HttpContext, series, randomImages), page, pageSize);

        // Return early if every series will be filtered out.
        if (!groupFilter.SeriesIds.TryGetValue(user.JMMUserID, out var seriesIDs))
            return new ListResult<Series>();

        return seriesIDs.Select(id => RepoFactory.AnimeSeries.GetByID(id))
            .Where(series => series != null && user.AllowedSeries(series) && (includeMissing || series.GetVideoLocals().Count > 0))
            .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
            .ToListResult(series => new Series(HttpContext, series, randomImages), page, pageSize);
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
        var groupFilter = GetPreviewFilterForUser(user);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (groupFilter.IsDirectory)
            return new List<Group>();

        // Just return early because the every group will be filtered out.
        if (!groupFilter.SeriesIds.TryGetValue(user.JMMUserID, out var seriesIDs))
            return new List<Group>();

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

                if (groupFilter.ApplyToSeries != 1)
                    return true;

                return subGroup.GetAllSeries().Any(series => seriesIDs.Contains(series.AnimeSeriesID));
            })
            .OrderByGroupFilter(groupFilter)
            .Select(group => new Group(HttpContext, group, randomImages))
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
        var groupFilter = GetPreviewFilterForUser(user);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (groupFilter.IsDirectory)
            return new List<Series>();

        if (groupFilter.ApplyToSeries != 1)
            return (recursive ? group.GetAllSeries() : group.GetSeries())
                .Where(a => user.AllowedSeries(a))
                .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
                .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
                .Where(series => series.Size > 0 || includeMissing)
                .ToList();

        // Just return early because the every series will be filtered out.
        if (!groupFilter.SeriesIds.TryGetValue(user.JMMUserID, out var seriesIDs))
            return new List<Series>();

        return (recursive ? group.GetAllSeries() : group.GetSeries())
            .Where(series => seriesIDs.Contains(series.AnimeSeriesID))
            .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
            .Where(series => series.Size > 0 || includeMissing)
            .ToList();
    }

    #endregion

    public FilterController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
