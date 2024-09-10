using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class AniDBController : BaseController
{
    public AniDBController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }

    /// <summary>
    /// Get the known anidb release groups stored in shoko.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include missing release groups.</param>
    /// <returns></returns>
    [HttpGet("ReleaseGroup")]
    public ActionResult<ListResult<ReleaseGroup>> GetReleaseGroups(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False)
    {
        return includeMissing switch
        {
            IncludeOnlyFilter.False => RepoFactory.AniDB_ReleaseGroup.GetUsedReleaseGroups()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
            IncludeOnlyFilter.Only => RepoFactory.AniDB_ReleaseGroup.GetUnusedReleaseGroups()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
            _ => RepoFactory.AniDB_ReleaseGroup.GetAll()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
        };
    }

    /// <summary>
    /// Get an anidb release group by id.
    /// </summary>
    /// <param name="id">The release group id.</param>
    /// <returns></returns>
    [HttpGet("ReleaseGroup/{id}")]
    public ActionResult<ReleaseGroup> GetReleaseGroup(int id)
    {
        var group = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(id);
        if (group == null)
            return NotFound();
        return new ReleaseGroup(group);
    }

    /// <summary>
    /// Get all anidb creators.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("Creator")]
    public ActionResult<ListResult<Creator>> GetCreators([FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.AniDB_Creator.GetAll()
            .ToListResult(c => new Creator(c), page, pageSize);
    }

    /// <summary>
    /// Get an anidb creator by id.
    /// </summary>
    /// <param name="id">The creator id.</param>
    /// <returns></returns>
    [HttpGet("Creator/{id}")]
    public ActionResult<Creator> GetCreator(int id)
    {
        var creator = RepoFactory.AniDB_Creator.GetByCreatorID(id);
        if (creator == null)
            return NotFound();

        return new Creator(creator);
    }

    /// <summary>
    /// Get an anidb creator by name.
    /// </summary>
    /// <param name="name">The creator name.</param>
    /// <returns></returns>
    [HttpGet("Creator/Name/{name}")]
    public ActionResult<Creator> GetCreator(string name)
    {
        var creator = RepoFactory.AniDB_Creator.GetByName(name);
        if (creator == null)
            return NotFound();

        return new Creator(creator);
    }
}
