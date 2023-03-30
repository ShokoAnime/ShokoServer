using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

using EpisodeType = Shoko.Server.API.v3.Models.Shoko.EpisodeType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// This Controller is intended to provide the tree. An example would be "api/v3/filter/4/group/12/series".
/// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}")]
[ApiV3]
[Authorize]
public class TreeController : BaseController
{
    #region Import Folder

    /// <summary>
    /// Get all <see cref="File"/>s in the <see cref="ImportFolder"/> with the given <paramref name="folderID"/>.
    /// </summary>
    /// <param name="folderID">Import folder ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <returns></returns>
    [HttpGet("ImportFolder/{folderID}/File")]
    public ActionResult<ListResult<File>> GetFilesInImportFolder([FromRoute] int folderID,
        [FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeXRefs = false)
    {
        var importFolder = RepoFactory.ImportFolder.GetByID(folderID);
        if (importFolder == null)
        {
            return NotFound("Import folder not found.");
        }

        return RepoFactory.VideoLocalPlace.GetByImportFolder(importFolder.ImportFolderID)
            .GroupBy(place => place.VideoLocalID)
            .Select(places => RepoFactory.VideoLocal.GetByID(places.Key))
            .OrderBy(file => file.DateTimeCreated)
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    #endregion
    #region Filter

    /// <summary>
    /// Get a list of all the sub-<see cref="Filter"/> for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.Directory"/> set to true to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="showHidden">Show hidden filters</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Filter")]
    public ActionResult<ListResult<Filter>> GetSubFilters([FromRoute] int filterID,
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showHidden = false)
    {
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
        {
            return NotFound(FilterController.FilterNotFound);
        }

        if (!((GroupFilterType)groupFilter.FilterType).HasFlag(GroupFilterType.Directory))
        {
            return BadRequest("Filter contains no sub-filters.");
        }

        return RepoFactory.GroupFilter.GetByParentID(filterID)
            .Where(filter => showHidden || filter.InvisibleInClients != 1)
            .OrderBy(filter => filter.GroupFilterName)
            .ToListResult(filter => new Filter(HttpContext, filter), page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of all the top-level <see cref="Group"/>s for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="orderByName">Ignore the group filter sort critaria and always order the returned list by name.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group")]
    public ActionResult<ListResult<Group>> GetFilteredGroups([FromRoute] int filterID,
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeEmpty = false, [FromQuery] bool randomImages = false, [FromQuery] bool orderByName = false)
    {
        // Return the top level groups with no filter.
        IEnumerable<SVR_AnimeGroup> groups;
        if (filterID == 0)
        {
            var user = User;
            groups = RepoFactory.AnimeGroup.GetAll()
                .Where(group => !group.AnimeGroupParentID.HasValue && user.AllowedGroup(group))
                .OrderBy(group => group.GetSortName());
        }
        else
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
            if (groupFilter == null)
            {
                return NotFound(FilterController.FilterNotFound);
            }

            // Fast path when user is not in the filter
            if (!groupFilter.GroupsIds.TryGetValue(User.JMMUserID, out var groupIds))
            {
                return new ListResult<Group>();
            }

            groups = groupIds
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
        }

        return groups
            .ToListResult(group => new Group(HttpContext, group, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a dictionary with the count for each starting character in each of
    /// the top-level group's name with the filter for the given
    /// <paramref name="filterID"/> applied.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group/Letters")]
    public ActionResult<Dictionary<char, int>> GetGroupNameLettersInFilter([FromRoute] int? filterID = null, [FromQuery] bool includeEmpty = false)
    {
        var user = User;
        if (filterID.HasValue && filterID > 0)
        {
            var groupFilter = RepoFactory.GroupFilter.GetByID(filterID.Value);
            if (groupFilter == null)
                return NotFound(FilterController.FilterNotFound);

            // Fast path when user is not in the filter
            if (!groupFilter.GroupsIds.TryGetValue(user.JMMUserID, out var groupIds))
                return new Dictionary<char, int>();

            var groups = groupIds
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

        return RepoFactory.AnimeGroup.GetAll()
            .Where(group =>
            {
                if (group.AnimeGroupParentID.HasValue)
                    return false;

                if (!user.AllowedGroup(group))
                    return false;

                return includeEmpty || group.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            })
            .GroupBy(group => group.GetSortName()[0])
            .OrderBy(groupList => groupList.Key)
            .ToDictionary(groupList => groupList.Key, groupList => groupList.Count());
    }

    /// <summary>
    /// Get a paginated list of all the <see cref="Series"/> within a <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/>.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Series")]
    public ActionResult<ListResult<Series>> GetSeriesInFilteredGroup([FromRoute] int filterID,
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool randomImages = false)
    {
        // Return the series with no group filter applied.
        if (filterID == 0)
        {
            return RepoFactory.AnimeSeries.GetAll()
                .Where(series => User.AllowedSeries(series))
                .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
                .ToListResult(series => new Series(HttpContext, series, randomImages), page, pageSize);
        }

        // Check if the group filter exists.
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
        {
            return NotFound(FilterController.FilterNotFound);
        }

        // Return all series if group filter is not applied to series.
        if (groupFilter.ApplyToSeries != 1)
        {
            return RepoFactory.AnimeSeries.GetAll()
                .Where(series => User.AllowedSeries(series))
                .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
                .ToListResult(series => new Series(HttpContext, series, randomImages), page, pageSize);
        }

        // Return early if every series will be filtered out.
        if (!groupFilter.SeriesIds.TryGetValue(User.JMMUserID, out var seriesIDs))
        {
            return new ListResult<Series>();
        }

        return seriesIDs.Select(id => RepoFactory.AnimeSeries.GetByID(id))
            .Where(series => series != null && User.AllowedSeries(series))
            .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
            .ToListResult(series => new Series(HttpContext, series, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a list of all the sub-<see cref="Group"/>s belonging to the <see cref="Group"/> with the given <paramref name="groupID"/> and which are present within the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetFilteredSubGroups([FromRoute] int filterID, [FromRoute] int groupID,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeEmpty = false)
    {
        // Return sub-groups with no group filter applied.
        if (filterID == 0)
        {
            return GetSubGroups(groupID, randomImages, includeEmpty);
        }

        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
        {
            return NotFound(FilterController.FilterNotFound);
        }

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        // Just return early because the every group will be filtered out.
        if (!groupFilter.SeriesIds.TryGetValue(user.JMMUserID, out var seriesIDs))
        {
            return new List<Group>();
        }

        return group.GetChildGroups()
            .Where(subGroup =>
            {
                if (subGroup == null)
                {
                    return false;
                }

                if (!user.AllowedGroup(subGroup))
                {
                    return false;
                }

                if (!includeEmpty && !subGroup.GetAllSeries()
                        .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0)))
                {
                    return false;
                }

                if (groupFilter.ApplyToSeries != 1)
                {
                    return true;
                }

                return subGroup.GetAllSeries().Any(series => seriesIDs.Contains(series.AnimeSeriesID));
            })
            .OrderByGroupFilter(groupFilter)
            .Select(group => new Group(HttpContext, group, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter or .
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetSeriesInFilteredGroup([FromRoute] int filterID, [FromRoute] int groupID,
        [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = false,
        [FromQuery] bool randomImages = false, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        // Return the groups with no group filter applied.
        if (filterID == 0)
        {
            return GetSeriesInGroup(groupID, recursive, includeMissing, randomImages, includeDataFrom);
        }

        // Check if the group filter exists.
        var groupFilter = RepoFactory.GroupFilter.GetByID(filterID);
        if (groupFilter == null)
        {
            return NotFound(FilterController.FilterNotFound);
        }

        if (groupFilter.ApplyToSeries != 1)
        {
            return GetSeriesInGroup(groupID, recursive, includeMissing, randomImages);
        }

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        // Just return early because the every series will be filtered out.
        if (!groupFilter.SeriesIds.TryGetValue(user.JMMUserID, out var seriesIDs))
        {
            return new List<Series>();
        }

        return (recursive ? group.GetAllSeries() : group.GetSeries())
            .Where(series => seriesIDs.Contains(series.AnimeSeriesID))
            .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
            .Where(series => series.Size > 0 || includeMissing)
            .ToList();
    }

    #endregion

    #region Group

    /// <summary>
    /// Get a list of sub-<see cref="Group"/>s a the <see cref="Group"/>.
    /// </summary>
    /// <param name="groupID"></param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetSubGroups([FromRoute] int groupID, [FromQuery] bool randomImages = false,
        [FromQuery] bool includeEmpty = false)
    {
        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        return group.GetChildGroups()
            .Where(subGroup =>
            {
                if (subGroup == null)
                {
                    return false;
                }

                if (!user.AllowedGroup(subGroup))
                {
                    return false;
                }

                return includeEmpty || group.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            })
            .OrderBy(group => group.GroupName)
            .Select(group => new Group(HttpContext, group, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of <see cref="Series"/> within a <see cref="Group"/>.
    /// </summary>
    /// <remarks>
    /// It will return all the <see cref="Series"/> within the group and all sub-groups if
    /// <paramref name="recursive"/> is set to <code>true</code>.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/></param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetSeriesInGroup([FromRoute] int groupID, [FromQuery] bool recursive = false,
        [FromQuery] bool includeMissing = false, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        return (recursive ? group.GetAllSeries() : group.GetSeries())
            .Where(a => user.AllowedSeries(a))
            .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
            .Where(series => series.Size > 0 || includeMissing)
            .ToList();
    }

    /// <summary>
    /// Get the main <see cref="Series"/> in a <see cref="Group"/>.
    /// </summary>
    /// <remarks>
    /// It will return 1) the default series or 2) the earliest running
    /// series if the group contains a series, or nothing if the group is
    /// empty.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/MainSeries")]
    public ActionResult<Series> GetMainSeriesInGroup([FromRoute] int groupID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        var mainSeries = group.GetMainSeries();
        if (mainSeries == null)
        {
            return InternalError("Unable to find main series for group.");
        }

        return new Series(HttpContext, mainSeries, randomImages, includeDataFrom);
    }

    #endregion

    #region Series

    /// <summary>
    /// Get the <see cref="Episode"/>s for the <see cref="Series"/> with <paramref name="seriesID"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="seriesID">Series ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>.</param>
    /// <returns>A list of episodes based on the specified filters.</returns>
    [HttpGet("Series/{seriesID}/Episode")]
    public ActionResult<ListResult<Episode>> GetEpisodes([FromRoute] int seriesID,
        [FromQuery] [Range(0, 1000)] int pageSize = 20, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False, [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True, [FromQuery] EpisodeType? type = null)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return series.GetAnimeEpisodes(orderList: true, includeHidden: includeHidden != IncludeOnlyFilter.False)
            .Where(a =>
            {
                // Filter by hidden state, if spesified
                if (includeHidden == IncludeOnlyFilter.Only && !a.IsHidden)
                    return false;

                // Filter by episode type, if specified
                if (type.HasValue && type.Value != Episode.MapAniDBEpisodeType((AniDBEpisodeType)a.AniDB_Episode.EpisodeType))
                    return false;

                // Filter by availability, if specified
                if (includeMissing != IncludeOnlyFilter.True)
                {
                    // If we should hide missing episodes and the episode has no files, then hide it.
                    // Or if we should only show missing episodes and the episode has files, the hide it.
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var noFiles = a.GetVideoLocals().Count == 0;
                    if (shouldHideMissing == noFiles)
                        return false;
                }

                // Filter by user watched status, if specified
                if (includeWatched != IncludeOnlyFilter.True)
                {
                    // If we should hide watched episodes and the episode is watched, then hide it.
                    // Or if we should only show watched episodes and the the episode is not watched, then hide it.
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var isWatched = a.GetUserRecord(User.JMMUserID)?.WatchedDate != null;
                    if (shouldHideWatched == isWatched)
                        return false;
                }

                return true;
            })
            .ToListResult(a => new Episode(HttpContext, a, includeDataFrom), page, pageSize);
    }

    /// <summary>
    /// Get the next <see cref="Episode"/> for the <see cref="Series"/> with <paramref name="seriesID"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="seriesID">Series ID</param>
    /// <param name="onlyUnwatched">Only show the next unwatched episode.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/NextUpEpisode")]
    public ActionResult<Episode> GetNextUnwatchedEpisode([FromRoute] int seriesID,
        [FromQuery] bool onlyUnwatched = true, [FromQuery] bool includeSpecials = true,
        [FromQuery] bool includeMissing = true, [FromQuery] bool includeHidden = false,
        [FromQuery] bool includeRewatching = false, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var user = User;
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!user.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        var episode = series.GetNextEpisode(user.JMMUserID, new()
        {
            IncludeCurrentlyWatching = !onlyUnwatched,
            IncludeHidden = includeHidden,
            IncludeMissing = includeMissing,
            IncludeRewatching = includeRewatching,
            IncludeSpecials = includeSpecials,
        });
        if (episode == null)
        {
            return null;
        }

        return new Episode(HttpContext, episode, includeDataFrom);
    }

    /// <summary>
    /// Get the <see cref="File"/>s for the <see cref="Series"/> with the given <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="isManuallyLinked">Omit to select all files. Set to true to only select manually
    /// linked files, or set to false to only select automatically linked files.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/File")]
    public ActionResult<List<File>> GetFilesForSeries([FromRoute] int seriesID, [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool? isManuallyLinked = null, [FromQuery] bool includeMediaInfo = false)
    {
        var user = User;
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!user.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return series.GetVideoLocals(isManuallyLinked.HasValue ? isManuallyLinked.Value ? CrossRefSource.User : CrossRefSource.AniDB : null)        
            .Select(file => new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo))
            .ToList();
    }

    #endregion

    #region Episode

    /// <summary>
    /// Get the <see cref="File"/>s for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="isManuallyLinked">Omit to select all files. Set to true to only select manually
    /// linked files, or set to false to only select automatically linked files.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/File")]
    public ActionResult<List<File>> GetFilesForEpisode([FromRoute] int episodeID, [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool? isManuallyLinked = null, [FromQuery] bool includeMediaInfo = false)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeController.EpisodeNotFoundWithEpisodeID);
        }

        var series = episode.GetAnimeSeries();
        if (series == null)
        {
            return InternalError("No Series entry for given Episode");
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(EpisodeController.EpisodeForbiddenForUser);
        }

        return episode.GetVideoLocals(isManuallyLinked.HasValue ? isManuallyLinked.Value ? CrossRefSource.User : CrossRefSource.AniDB : null)
            .Select(file => new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo))
            .ToList();
    }

    #endregion

    public TreeController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
