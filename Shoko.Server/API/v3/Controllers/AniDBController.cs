using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Settings;

using ReleaseGroup = Shoko.Server.API.v3.Models.Common.ReleaseGroup;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class AniDBController(
    ISettingsProvider settingsProvider,
    IUDPConnectionHandler udpHandler,
    IHttpConnectionHandler httpHandler,
    DatabaseReleaseInfoRepository databaseReleaseInfos,
    AniDB_CreatorRepository anidbCreators
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get the AniDB ban status for the UDP and HTTP connections.
    /// </summary>
    /// <returns>
    /// A dictionary with two entries, one for the UDP connection and one for the HTTP connection,
    /// where the key is the name of the connection and the value is the current ban status.
    /// </returns>
    [HttpGet("BanStatus")]
    public Dictionary<string, AnidbBannedStatus> GetBanStatus()
    {
        return new Dictionary<string, AnidbBannedStatus>
        {
            { "UDP", new AnidbBannedStatus(udpHandler.State) },
            { "HTTP", new AnidbBannedStatus(httpHandler.State) },
        };
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
            IncludeOnlyFilter.False => databaseReleaseInfos.GetUsedReleaseGroups()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
            IncludeOnlyFilter.Only => databaseReleaseInfos.GetUnusedReleaseGroups()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
            _ => databaseReleaseInfos.GetReleaseGroups()
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
        if (databaseReleaseInfos.GetByGroupAndProviderIDs(id.ToString(), "AniDB") is not IReleaseInfo { Group.ProviderID: "AniDB" } releaseInfo)
            return NotFound();

        return new ReleaseGroup(releaseInfo.Group);
    }

    /// <summary>
    /// Get all anidb creators.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("Creator")]
    public ActionResult<ListResult<AnidbCreator>> GetCreators([FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        return anidbCreators.GetAll()
            .ToListResult(c => new AnidbCreator(c), page, pageSize);
    }

    /// <summary>
    /// Get an anidb creator by id.
    /// </summary>
    /// <param name="id">The creator id.</param>
    /// <returns></returns>
    [HttpGet("Creator/{id}")]
    public ActionResult<AnidbCreator> GetCreator(int id)
    {
        var creator = anidbCreators.GetByCreatorID(id);
        if (creator == null)
            return NotFound();

        return new AnidbCreator(creator);
    }

    /// <summary>
    /// Get an anidb creator by name.
    /// </summary>
    /// <param name="name">The creator name.</param>
    /// <returns></returns>
    [HttpGet("Creator/Name/{name}")]
    public ActionResult<AnidbCreator> GetCreator(string name)
    {
        var creator = anidbCreators.GetByName(name);
        if (creator == null)
            return NotFound();

        return new AnidbCreator(creator);
    }
}
