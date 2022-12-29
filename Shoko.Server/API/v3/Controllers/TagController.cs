using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    /// <returns></returns>
    [HttpGet("AniDB")]
    public ActionResult<ListResult<Tag>> GetAllAnidbTags([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] TagFilter.Filter filter = 0,
        [FromQuery] bool excludeDescriptions = false, [FromQuery] bool onlyVerified = true)
    {
        var user = User;
        var selectedTags = RepoFactory.AniDB_Tag.GetAll()
            .Where(tag => !onlyVerified || tag.Verified)
            .DistinctBy(a => a.TagName)
            .ToList();
        var tagFilter = new TagFilter<AniDB_Tag>(name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault(), tag => tag.TagName);
        return tagFilter
            .ProcessTags(filter, selectedTags)
            .Where(tag => user.IsAdmin == 1 || user.AllowedTag(tag))
            .OrderBy(tag => tag.TagName)
            .ToListResult(tag => new Tag(tag, excludeDescriptions), page, pageSize);
    }

    /// <summary>
    /// Get an anidb tag by it's id.
    /// </summary>
    /// <param name="tagID">Anidb Tag ID</param>
    /// <param name="excludeDescription">Exclude tag description from response.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{tagID}")]
    public ActionResult<Tag> GetAnidbTag([FromRoute] int tagID, [FromQuery] bool excludeDescription = false)
    {
        var tag = RepoFactory.AniDB_Tag.GetByTagID(tagID);
        if (tag == null)
            return NotFound("No AniDB Tag entry for the given tagID");

        var user = User;
        if (user.IsAdmin != 1 && !user.AllowedTag(tag))
            return Forbid("Accessing Tag is not allowed for the current user");

        return new Tag(tag, excludeDescription);
    }

    /// <summary>
    /// Get a list of all user tags.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <param name="excludeDescriptions">Exclude tag descriptions from response.</param>
    /// <returns></returns>
    [HttpGet("User")]
    public ActionResult<ListResult<Tag>> GetAllUserTags([FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool excludeDescriptions = false)
    {
        return RepoFactory.CustomTag.GetAll()
            .OrderBy(tag => tag.TagName)
            .ToListResult(tag => new Tag(tag, excludeDescriptions), page, pageSize);
    }

    /// <summary>
    /// Get an user tag by it's <paramref name="tagID"/>.
    /// </summary>
    /// <param name="tagID">User Tag ID</param>
    /// <param name="excludeDescription">Exclude tag description from response.</param>
    /// <returns></returns>
    [HttpGet("User/{tagID}")]
    public ActionResult<Tag> GetUserTag([FromRoute] int tagID, [FromQuery] bool excludeDescription = false)
    {
        var tag = RepoFactory.CustomTag.GetByID(tagID);
        if (tag == null)
            return NotFound("No User Tag entry for the given tagID");

        return new Tag(tag, excludeDescription);
    }

    public TagController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
