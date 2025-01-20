using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class TagController : BaseController
{
    /// <summary>
    /// Get a list of all known anidb tags, optionally with a
    /// <paramref name="filter"/> applied.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="filter">Tag filter.</param>
    /// <param name="excludeDescriptions">Exclude tag descriptions from response.</param>
    /// <param name="onlyVerified">Only show verified tags.</param>
    /// <param name="includeCount">Include tag count in response.</param>
    /// <returns></returns>
    [HttpGet("AniDB")]
    public ActionResult<ListResult<Tag>> GetAllAnidbTags(
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] TagFilter.Filter filter = 0,
        [FromQuery] bool excludeDescriptions = false,
        [FromQuery] bool onlyVerified = true,
        [FromQuery] bool includeCount = false
    )
    {
        var user = User;
        var selectedTags = RepoFactory.AniDB_Tag.GetAll()
            .Where(tag => !onlyVerified || tag.Verified)
            .DistinctBy(a => a.TagName)
            .ToList();
        var tagFilter = new TagFilter<AniDB_Tag>(
            name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault(), tag => tag.TagName,
            name => new AniDB_Tag { TagNameSource = name }
        );
        return tagFilter
            .ProcessTags(filter, selectedTags)
            .Where(tag => user.IsAdmin == 1 || user.AllowedTag(tag))
            .OrderBy(tag => tag.TagName)
            .ToListResult(tag => new Tag(tag, excludeDescriptions, includeCount ? RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID).Count : null), page, pageSize);
    }

    /// <summary>
    /// Get an anidb tag by it's id.
    /// </summary>
    /// <param name="tagID">Anidb Tag ID</param>
    /// <param name="excludeDescription">Exclude tag description from response.</param>
    /// <param name="includeCount">Include tag count in response.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{tagID}")]
    public ActionResult<Tag> GetAnidbTag([FromRoute] int tagID, [FromQuery] bool excludeDescription = false, [FromQuery] bool includeCount = false)
    {
        var tag = tagID <= 0 ? null : RepoFactory.AniDB_Tag.GetByTagID(tagID);
        if (tag == null)
            return NotFound("No AniDB Tag entry for the given tagID");

        var user = User;
        if (user.IsAdmin != 1 && !user.AllowedTag(tag))
            return Forbid("Accessing Tag is not allowed for the current user");

        return new Tag(tag, excludeDescription, includeCount ? RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID).Count : null);
    }

    /// <summary>
    /// Get a list of all user tags.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="excludeDescriptions">Exclude tag descriptions from response.</param>
    /// <param name="includeCount">Include tag count in response.</param>
    /// <returns></returns>
    [HttpGet("User")]
    public ActionResult<ListResult<Tag>> GetAllUserTags(
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool excludeDescriptions = false,
        [FromQuery] bool includeCount = false)
    {
        return RepoFactory.CustomTag.GetAll()
            .OrderBy(tag => tag.TagName)
            .ToListResult(tag => new Tag(tag, excludeDescriptions, includeCount ? RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID).Count : null), page, pageSize);
    }

    /// <summary>
    /// Add a new user tag.
    /// </summary>
    /// <param name="body">Details for the new user tag.</param>
    /// <returns>The new user tag, or an error action result.</returns>
    [HttpPost("User")]
    [Authorize("admin")]
    public ActionResult<Tag> AddUserTag([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Tag.Input.CreateOrUpdateCustomTagBody body)
    {
        if (string.IsNullOrEmpty(body.Name?.Trim()))
            return ValidationProblem("Name must be set for new tags.", nameof(body.Name));

        var tag = body.MergeWithExisting(new(), ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return tag;
    }

    /// <summary>
    /// Get an user tag by it's <paramref name="tagID"/>.
    /// </summary>
    /// <param name="tagID">User Tag ID</param>
    /// <param name="excludeDescription">Exclude tag description from response.</param>
    /// <param name="includeCount">Include tag count in response.</param>
    /// <returns></returns>
    [HttpGet("User/{tagID}")]
    public ActionResult<Tag> GetUserTag([FromRoute, Range(1, int.MaxValue)] int tagID, [FromQuery] bool excludeDescription = false, [FromQuery] bool includeCount = false)
    {
        var tag = RepoFactory.CustomTag.GetByID(tagID);
        if (tag == null)
            return NotFound("No User Tag entry for the given tagID");

        return new Tag(tag, excludeDescription, includeCount ? RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID).Count : null);
    }

    /// <summary>
    /// Update an existing user tag by directly replacing it's fields.
    /// </summary>
    /// <param name="tagID">User Tag ID.</param>
    /// <param name="body">Details about what to update for the existing tag.</param>
    /// <returns>The updated user tag, or an error action result.</returns>
    [HttpPut("User/{tagID}")]
    [Authorize("admin")]
    public ActionResult<Tag> EditUserTag([FromRoute, Range(1, int.MaxValue)] int tagID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Tag.Input.CreateOrUpdateCustomTagBody body)
    {
        var tag = RepoFactory.CustomTag.GetByID(tagID);
        if (tag == null)
            return NotFound("No User Tag entry for the given tagID");

        var result = body.MergeWithExisting(tag, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return result!;
    }

    /// <summary>
    /// Update an existing user tag using JSON patch.
    /// </summary>
    /// <param name="tagID">User Tag ID.</param>
    /// <param name="patchDocument">The JSON patch document containing the
    /// details about what to update for the existing tag.</param>
    /// <returns>The updated user tag, or an error action result.</returns>
    [HttpPatch("User/{tagID}")]
    [Authorize("admin")]
    public ActionResult<Tag> PatchUserTag([FromRoute, Range(1, int.MaxValue)] int tagID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<Tag.Input.CreateOrUpdateCustomTagBody> patchDocument)
    {
        var tag = RepoFactory.CustomTag.GetByID(tagID);
        if (tag == null)
            return NotFound("No User Tag entry for the given tagID");
        var body = new Tag.Input.CreateOrUpdateCustomTagBody();
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = body.MergeWithExisting(tag, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return result!;
    }

    /// <summary>
    /// Remove an existing user tag.
    /// </summary>
    /// <param name="tagID">User Tag ID.</param>
    /// <returns>No content or an error action result.</returns>
    [HttpDelete("User/{tagID}")]
    [Authorize("admin")]
    public ActionResult RemoveUserTag([FromRoute, Range(1, int.MaxValue)] int tagID)
    {
        var tag = RepoFactory.CustomTag.GetByID(tagID);
        if (tag == null)
            return NotFound("No User Tag entry for the given tagID");

        var xrefs = RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tagID);
        RepoFactory.CrossRef_CustomTag.Delete(xrefs);
        RepoFactory.CustomTag.Delete(tag);

        return NoContent();
    }

    public TagController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
