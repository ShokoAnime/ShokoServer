using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class AniDBController : BaseController
{
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

    public AniDBController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
