using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class GroupController : BaseController
{
    private readonly AnimeGroupCreator _groupCreator;
    private readonly AnimeSeriesService _seriesService;
    private readonly AnimeGroupService _groupService;

    #region Return messages

    internal const string GroupNotFound = "No Group entry for the given groupID";

    internal const string GroupForbiddenForUser = "Accessing Group is not allowed for the current user";

    #endregion

    #region Metadata

    #region Get Many

    /// <summary>
    /// Get a list of all groups available to the current user
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomize images shown for the main <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="topLevelOnly">Only list the top level groups if set.</param>
    /// <param name="startsWith">Search only for groups that start with the given query.</param>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<ListResult<Group>> GetAllGroups([FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeEmpty = false,
        [FromQuery] bool randomImages = false, [FromQuery] bool topLevelOnly = true, [FromQuery] string startsWith = "")
    {
        startsWith = startsWith.ToLowerInvariant();
        var user = User;
        return RepoFactory.AnimeGroup.GetAll()
            .Where(group =>
            {
                if (topLevelOnly && group.AnimeGroupParentID.HasValue)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(startsWith) && !group.GroupName.StartsWith(startsWith, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }

                if (!user.AllowedGroup(group))
                {
                    return false;
                }

                return includeEmpty || group.AllSeries
                    .Any(s => s.AnimeEpisodes.Any(e => e.VideoLocals.Count > 0));
            })
            .OrderBy(group => group.SortName)
            .ToListResult(group => new Group(group, User.JMMUserID, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a dictionary with the count for each starting character in each of
    /// the group's name.
    /// </summary>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <param name="topLevelOnly">Only count top-level groups (groups with no
    /// parent group).</param>
    /// <returns></returns>
    [HttpGet("Letters")]
    public ActionResult<Dictionary<char, int>> GetGroupNameLetters([FromQuery] bool includeEmpty = false, [FromQuery] bool topLevelOnly = true)
    {
        var user = User;
        return RepoFactory.AnimeGroup.GetAll()
            .Where(group =>
            {
                if (topLevelOnly && group.AnimeGroupParentID.HasValue)
                    return false;

                if (!user.AllowedGroup(group))
                    return false;

                return includeEmpty || group.AllSeries
                    .Any(s => s.AnimeEpisodes.Any(e => e.VideoLocals.Count > 0));
            })
            .GroupBy(group => group.SortName[0])
            .OrderBy(groupList => groupList.Key)
            .ToDictionary(groupList => groupList.Key, groupList => groupList.Count());
    }

    #endregion

    #region Create new or update existing

    /// <summary>
    /// Create a new group using the provided details.
    /// </summary>
    /// <remarks>
    /// Use <see cref="SeriesController.MoveSeries"/> to move a single series to
    /// the group, or use <see cref="GroupController.PutGroup"/> or
    /// <see cref="GroupController.PatchGroup"/> to move multiple series and/or
    /// child groups to the group.
    /// </remarks>
    /// <param name="body">The details for the group to be created.</param>
    /// <returns>The new group.</returns>
    [HttpPost]
    public ActionResult<Group> CreateGroup([FromBody] Group.Input.CreateOrUpdateGroupBody body)
    {
        var animeGroup = new AnimeGroup
        {
            GroupName = string.Empty,
            Description = string.Empty,
            IsManuallyNamed = 0,
            DateTimeCreated = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
            MissingEpisodeCount = 0,
            MissingEpisodeCountGroups = 0,
            OverrideDescription = 0,
        };
        var group = body.MergeWithExisting(animeGroup, User.JMMUserID, ModelState);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Created($"/api/v3/Group/{animeGroup.AnimeGroupID}", group);
    }

    #endregion

    #region Get One

    /// <summary>
    /// Get the group with ID
    /// </summary>
    /// <param name="groupID"></param>
    /// <returns></returns>
    [HttpGet("{groupID}")]
    public ActionResult<Group> GetGroup([FromRoute, Range(1, int.MaxValue)] int groupID)
    {
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupNotFound);
        }

        if (!User.AllowedGroup(group))
        {
            return Forbid(GroupForbiddenForUser);
        }

        return new Group(group, User.JMMUserID);
    }

    /// <summary>
    /// Update an existing group using the provided details.
    /// </summary>
    /// <remarks>
    /// Use this method to update the details or merge more series/groups into
    /// an existing group.
    /// </remarks>
    /// <param name="groupID">The ID of the group to be updated.</param>
    /// <param name="body">The new details for the group.</param>
    /// <returns>The updated group.</returns>
    [HttpPut("{groupID}")]
    public ActionResult<Group> PutGroup([FromRoute, Range(1, int.MaxValue)] int groupID, [FromBody] Group.Input.CreateOrUpdateGroupBody body)
    {
        var animeGroup = RepoFactory.AnimeGroup.GetByID(groupID);
        if (animeGroup == null)
        {
            return NotFound(GroupNotFound);
        }

        if (!User.AllowedGroup(animeGroup))
        {
            return Forbid(GroupForbiddenForUser);
        }

        var group = body.MergeWithExisting(animeGroup, User.JMMUserID, ModelState);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return group;
    }

    /// <summary>
    /// Partially update an existing group using the provided JSON Patch document.
    /// </summary>
    /// <remarks>
    /// Use this method to apply a set of changes to an existing group.
    /// The changes are described in the JSON Patch document included in the request body.
    /// If you need to completely replace the details of a group, use
    /// <see cref="GroupController.PutGroup"/> instead.
    /// </remarks>
    /// <param name="groupID">The ID of the group to be updated.</param>
    /// <param name="patchDocument">The JSON Patch document containing the changes to be applied to the group.</param>
    /// <returns>The updated group.</returns>
    [HttpPatch("{groupID}")]
    public ActionResult<Group> PatchGroup([FromRoute, Range(1, int.MaxValue)] int groupID, [FromBody] JsonPatchDocument<Group.Input.CreateOrUpdateGroupBody> patchDocument)
    {
        var animeGroup = RepoFactory.AnimeGroup.GetByID(groupID);
        if (animeGroup == null)
        {
            return NotFound(GroupNotFound);
        }

        if (!User.AllowedGroup(animeGroup))
        {
            return Forbid(GroupForbiddenForUser);
        }

        // Patch the body with the existing model.
        var body = new Group.Input.CreateOrUpdateGroupBody(animeGroup);
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var group = body.MergeWithExisting(animeGroup, User.JMMUserID, ModelState);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return group;
    }

    #endregion

    #region Get Relations

    /// <summary>
    /// Get all relations to locally available series within the group.
    /// </summary>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="recursive">Show relations for all series within the group, even for series within sub-groups.</param>
    /// <returns></returns>
    [HttpGet("{groupID}/Relations")]
    public ActionResult<List<SeriesRelation>> GetShokoRelationsBySeriesID([FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool recursive = false)
    {
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupForbiddenForUser);
        }

        var seriesDict = (recursive ? group.AllSeries : group.Series)
            .ToDictionary(series => series.AniDB_ID);
        var animeIds = seriesDict.Values
            .Select(series => series.AniDB_ID)
            .ToHashSet();

        // TODO: Replace with a more generic implementation capable of supplying relations from more than just AniDB.
        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(animeIds).OfType<IRelatedMetadata>()
            .Concat(RepoFactory.AniDB_Anime_Relation.GetByRelatedAnimeID(animeIds).OfType<IRelatedMetadata>().Select(a => a.Reversed))
            .Distinct()
            .Select(relation => (relation, relatedSeries: RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedID)))
            .Where(tuple => tuple.relatedSeries != null && animeIds.Contains(tuple.relatedSeries.AniDB_ID))
            .OrderBy(tuple => tuple.relation.BaseID)
            .ThenBy(tuple => tuple.relation.RelatedID)
            .ThenBy(tuple => tuple.relation.RelationType)
            .Select(tuple => new SeriesRelation(tuple.relation, seriesDict[tuple.relation.BaseID], tuple.relatedSeries))
            .ToList();
    }

    #endregion

    #endregion

    #region Delete

    /// <summary>
    /// Delete a group recursively.
    /// </summary>
    /// <param name="groupID">The ID of the group to delete</param>
    /// <param name="deleteSeries">Whether to delete the series in the group. It will error if this is false and the group is not empty.</param>
    /// <param name="deleteFiles">Whether to delete the all of the files in the group from the disk.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{groupID}")]
    public async Task<ActionResult> DeleteGroup([FromRoute, Range(1, int.MaxValue)] int groupID, bool deleteSeries = false, bool deleteFiles = false)
    {
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupNotFound);
        }

        var seriesList = group.AllSeries;
        if (!deleteSeries && seriesList.Count != 0)
        {
            return BadRequest(
                $"{nameof(deleteSeries)} is not true, and the group contains series. Move them, or set {nameof(deleteSeries)} to true");
        }

        foreach (var series in seriesList)
        {
            await _seriesService.DeleteSeries(series, deleteFiles, false);
        }

        _groupService.DeleteGroup(group);

        return NoContent();
    }

    #endregion

    #region Recalculate

    /// <summary>
    /// Recalculate all stats and contracts for a group
    /// </summary>
    /// <param name="groupID"></param>
    /// <returns></returns>
    [HttpPost("{groupID}/Recalculate")]
    public async Task<ActionResult> RecalculateStats([FromRoute, Range(1, int.MaxValue)] int groupID)
    {
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupNotFound);
        }

        await _groupCreator.RecalculateStatsContractsForGroup(group);
        return Ok();
    }

    #endregion

    public GroupController(ISettingsProvider settingsProvider, AnimeGroupCreator groupCreator, AnimeSeriesService seriesService, AnimeGroupService groupService) : base(settingsProvider)
    {
        _groupCreator = groupCreator;
        _seriesService = seriesService;
        _groupService = groupService;
    }
}
