using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

#pragma warning disable CS0618 // Type or member is obsolete
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class GroupController(ISettingsProvider settingsProvider, IImageManager _imageManager, AnimeGroupCreator _groupCreator,
    AniDB_Anime_RelationRepository _anidbAnimeRelations,
    AnimeGroupRepository _animeGroups,
    AnimeSeriesRepository _animeSeries,
    IShokoGroupManager _groupManagementService,
    IUserDataService _userDataService
) : BaseController(settingsProvider)
{
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
        return _animeGroups.GetAll()
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
        return _animeGroups.GetAll()
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
        var group = body.MergeWithExisting(null, User.JMMUserID, ModelState);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Created($"/api/v3/Group/{group.IDs.ID}", group);
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
        if (_animeGroups.GetByID(groupID) is not { } animeGroup)
            return NotFound(GroupNotFound);

        if (!User.AllowedGroup(animeGroup))
            return Forbid(GroupForbiddenForUser);

        return new Group(animeGroup, User.JMMUserID);
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
        if (_animeGroups.GetByID(groupID) is not { } animeGroup)
            return NotFound(GroupNotFound);

        if (!User.AllowedGroup(animeGroup))
            return Forbid(GroupForbiddenForUser);

        var group = body.MergeWithExisting(animeGroup, User.JMMUserID, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

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
        if (_animeGroups.GetByID(groupID) is not { } animeGroup)
            return NotFound(GroupNotFound);

        if (!User.AllowedGroup(animeGroup))
            return Forbid(GroupForbiddenForUser);

        // Patch the body with the existing model.
        var body = new Group.Input.CreateOrUpdateGroupBody();
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var group = body.MergeWithExisting(animeGroup, User.JMMUserID, ModelState)!;
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

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
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        var seriesDict = (recursive ? group.AllSeries : group.Series)
            .ToDictionary(series => series.AniDB_ID);
        var animeIds = seriesDict.Values
            .Select(series => series.AniDB_ID)
            .ToHashSet();

        // TODO: Replace with a more generic implementation capable of supplying relations from more than just AniDB.
        return _anidbAnimeRelations.GetByAnimeID(animeIds).OfType<IRelatedMetadata>()
            .Concat(_anidbAnimeRelations.GetByRelatedAnimeID(animeIds).OfType<IRelatedMetadata>().Select(a => a.Reversed))
            .Distinct()
            .Select(relation => (relation, relatedSeries: _animeSeries.GetByAnimeID(relation.RelatedID)))
            .Where(tuple => tuple.relatedSeries != null && animeIds.Contains(tuple.relatedSeries.AniDB_ID))
            .OrderBy(tuple => tuple.relation.BaseID)
            .ThenBy(tuple => tuple.relation.RelatedID)
            .ThenBy(tuple => tuple.relation.RelationType)
            .Select(tuple => new SeriesRelation(tuple.relation, seriesDict[tuple.relation.BaseID], tuple.relatedSeries))
            .ToList();
    }

    #endregion

    #endregion

    #region Images

    private const string InvalidIDForSource = "Invalid image id for selected source.";

    private const string NoDefaultImageForType = "No default image for type.";

    #region All images

    /// <summary>
    /// Get all images for group with ID, optionally with Disabled images, as well.
    /// </summary>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="showLinkedIDs"></param>
    /// <param name="includeDisabled"></param>
    /// <param name="includeUndesired"></param>
    /// <returns></returns>
    [HttpGet("{groupID}/Images")]
    public ActionResult<Images> GetSeriesImages(
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromQuery] bool showLinkedIDs = false,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false
    )
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        return ((IWithImages)group).GetImages(new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true })
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Source)
            .ThenByDescending(a => a.LanguageCode is null)
            .ThenBy(a => a.LanguageCode)
            .ThenByDescending(a => a.CountryCode is null)
            .ThenBy(a => a.CountryCode)
            .ToDto(showLinkedIDs: showLinkedIDs);
    }

    #endregion

    #region Default image

    /// <summary>
    /// Get the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [HttpGet("{seriesID}/Images/{imageType}")]
    public ActionResult<Image> GetSeriesDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromRoute] Image.LegacyImageType imageType)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        var imageEntityType = imageType.ToServer();
        var preferredImage = ((IWithImages)group).GetPreferredImageForType(imageEntityType);
        if (preferredImage is not null)
            return new Image(preferredImage);

        var images = ((IWithImages)group).GetImages(new() { ImageType = imageEntityType }).ToDto();
        var image = imageEntityType switch
        {
            ImageEntityType.Primary => images.Posters.FirstOrDefault(),
            ImageEntityType.Banner => images.Banners.FirstOrDefault(),
            ImageEntityType.Backdrop => images.Backdrops.FirstOrDefault(),
            ImageEntityType.Logo => images.Logos.FirstOrDefault(),
            ImageEntityType.Disc => images.Discs.FirstOrDefault(),
            _ => null,
        };

        if (image is null)
            return NotFound(NoDefaultImageForType);

        return image;
    }

    /// <summary>
    /// Set the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated:</b> Use the image management controller's set preferred endpoint instead.
    /// </remarks>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <param name="body">The body containing the source and id used to set.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("{seriesID}/Images/{imageType}")]
    [Obsolete("Use the image management controller's set preferred endpoint instead.")]
    public ActionResult<Image> SetSeriesDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromRoute] Image.LegacyImageType imageType, [FromBody] Image.Input.DefaultImageBody body)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        // Check if the id is valid for the given type and source.
        var dataSource = body.Source;
        var imageEntityType = imageType.ToServer();
        var image = Guid.TryParse(body.ID, out var imageID)
            ? _imageManager.GetImageByID(imageID)
            : int.TryParse(body.ID, out var legacyImageID)
                ? _imageManager.GetImageByID(legacyImageID)
                : null;
        if (image is null || (dataSource is not DataSource.None && dataSource != image.Source))
            return ValidationProblem(InvalidIDForSource);

        var xref = _imageManager.SetPreferredImageForEntity(group, imageEntityType, image);
        return new Image(ImageStub.Wrap(image, xref));
    }

    /// <summary>
    /// Unset the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Series"/>.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated:</b> Use the image management controller's unset preferred endpoint instead.
    /// </remarks>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{seriesID}/Images/{imageType}")]
    [Obsolete("Use the image management controller's unset preferred endpoint instead.")]
    public ActionResult DeleteSeriesDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int groupID, [FromRoute] Image.LegacyImageType imageType)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        // Check if a default image is set.
        var imageEntityType = imageType.ToServer();
        var xref = _imageManager
            .GetImageCrossReferencesForEntity(group, new() { ImageType = imageEntityType, IsPreferred = true }).FirstOrDefault();
        if (xref is null)
            return ValidationProblem("No default image for the selected type.");

        switch (xref)
        {
            // Unset the preferred if it's not a user xref, or if it's a user xref and a user uploaded image.
            case { Source: not DataSource.User }:
            case { Source: DataSource.User, ImageSource: DataSource.User }:
                _imageManager.UnsetPreferredImageForEntity(xref);
                break;
            // Otherwise remove the user created xref.
            default:
                _imageManager.RemoveImageCrossReference(xref);
                break;
        }

        // Don't return any content.
        return NoContent();
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
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var seriesList = group.AllSeries;
        if (!deleteSeries && seriesList.Count != 0)
        {
            return BadRequest(
                $"{nameof(deleteSeries)} is not true, and the group contains series. Move them, or set {nameof(deleteSeries)} to true");
        }

        await _groupManagementService.DeleteGroup(group, deleteSeries, deleteFiles);

        return NoContent();
    }

    #endregion

    #region User Data

    /// <summary>
    /// Get the <see cref="Group.GroupUserData"/> for the <see cref="Group"/>
    /// with the given <paramref name="groupID"/>.
    /// </summary>
    /// <param name="groupID">Shoko Group ID</param>
    /// <returns></returns>
    [HttpGet("{groupID}/UserData")]
    public ActionResult<Group.GroupUserData> GetGroupUserData([FromRoute, Range(1, int.MaxValue)] int groupID)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        if (!User.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        var userData = _userDataService.GetGroupUserData(group, User);
        return new Group.GroupUserData(userData);
    }

    /// <summary>
    /// Put a <see cref="Group.GroupUserData"/> object down for the
    /// <see cref="Group"/> with the given <paramref name="groupID"/>.
    /// </summary>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="groupUserStats">The user data to save.</param>
    /// <returns></returns>
    [HttpPut("{groupID}/UserData")]
    public ActionResult<Group.GroupUserData> PutGroupUserData([FromRoute, Range(1, int.MaxValue)] int groupID, [FromBody] Group.GroupUserData groupUserStats)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return groupUserStats.MergeWithExisting(user, group);
    }

    /// <summary>
    /// Patch a <see cref="Group.GroupUserData"/> object down for the
    /// <see cref="Group"/> with the given <paramref name="groupID"/>.
    /// </summary>
    /// <param name="groupID">Shoko Group ID</param>
    /// <param name="patchDocument">The JSON patch document to apply to the existing <see cref="Group.GroupUserData"/>.</param>
    /// <returns></returns>
    [HttpPatch("{groupID}/UserData")]
    public ActionResult<Group.GroupUserData> PatchGroupUserData([FromRoute, Range(1, int.MaxValue)] int groupID, [FromBody] JsonPatchDocument<Group.GroupUserData> patchDocument)
    {
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupForbiddenForUser);

        var userData = _userDataService.GetGroupUserData(group, user);
        var body = new Group.GroupUserData(userData);
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return body.MergeWithExisting(user, group);
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
        if (_animeGroups.GetByID(groupID) is not { } group)
            return NotFound(GroupNotFound);

        await _groupCreator.RecalculateStatsContractsForGroup(group);
        return Ok();
    }

    #endregion
}
