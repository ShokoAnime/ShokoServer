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

[ApiController, Route("/api/v{version:apiVersion}/File"), ApiV3]
[Authorize]
public class ObsoleteFileController : BaseController
{
    public ObsoleteFileController(ISettingsProvider settingsProvider) : base(settingsProvider) { }

    /// <summary>
    /// Get recently added files.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Recent/{limit:int?}")]
    [Obsolete("Use the universal file endpoint instead.")]
    public ActionResult<ListResult<File>> GetRecentFilesObselete([FromRoute] [Range(0, 1000)] int limit = 100)
        => GetRecentFiles(limit);

    /// <summary>
    /// Get recently added files.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Recent")]
    public ActionResult<ListResult<File>> GetRecentFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.VideoLocal.GetMostRecentlyAdded(-1, 0, User.JMMUserID)
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get ignored files.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Ignored")]
    public ActionResult<ListResult<File>> GetIgnoredFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.VideoLocal.GetIgnoredVideos()
            .ToListResult(file => new File(HttpContext, file), page, pageSize);
    }

    /// <summary>
    /// Get files with more than one location.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Duplicates")]
    public ActionResult<ListResult<File>> GetExactDuplicateFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = false)
    {
        return RepoFactory.VideoLocal.GetExactDuplicateVideos()
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get files with no cross-reference.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Linked")]
    public ActionResult<ListResult<File>> GetManuallyLinkedFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.VideoLocal.GetManuallyLinkedVideos()
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get unrecognized files.
    /// Use pageSize and page (index 0) in the query to enable pagination.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Unrecognized")]
    public ActionResult<ListResult<File>> GetUnrecognizedFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.VideoLocal.GetVideosWithoutEpisode()
            .ToListResult(file => new File(HttpContext, file), page, pageSize);
    }
}
