using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using ReleaseGroup = Shoko.Server.API.v3.Models.Release.ReleaseGroup;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class AniDBController(
    ISettingsProvider settingsProvider,
    IAnidbService anidbService,
    StoredReleaseInfoRepository storedReleaseInfos,
    AniDB_CreatorRepository anidbCreators,
    AniDB_CharacterRepository anidbCharacters
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
            { "UDP", new AnidbBannedStatus(anidbService.LastUdpBanEventArgs) },
            { "HTTP", new AnidbBannedStatus(anidbService.LastHttpBanEventArgs) },
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
            IncludeOnlyFilter.False => storedReleaseInfos.GetUsedReleaseGroups()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
            IncludeOnlyFilter.Only => storedReleaseInfos.GetUnusedReleaseGroups()
                .ToListResult(g => new ReleaseGroup(g), page, pageSize),
            _ => storedReleaseInfos.GetReleaseGroups()
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
        if (storedReleaseInfos.GetByGroupAndProviderIDs(id.ToString(), "AniDB") is not IReleaseInfo { Group.Source: "AniDB" } releaseInfo)
            return NotFound();

        return new ReleaseGroup(releaseInfo.Group);
    }

    /// <summary>
    /// Get all anidb creators.
    /// </summary>
    /// <param name="query">An optional query to filter creators by name.</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("Creator")]
    public ActionResult<ListResult<AnidbCreator>> GetCreators(
        [FromQuery] string? query = null,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        if (!string.IsNullOrEmpty(query))
            return anidbCreators.GetAll()
                .Search(query, c => [c.Name, c.OriginalName])
                .ToListResult(c => new AnidbCreator(c.Result), page, pageSize);

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

    /// <summary>
    ///   Get all anidb characters.
    /// </summary>
    /// <param name="query">An optional query to filter characters by name.</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("Character")]
    public ActionResult<ListResult<AnidbCharacter>> GetCharacters(
        [FromQuery] string? query = null,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (!string.IsNullOrEmpty(query))
            return anidbCharacters.GetAll()
                .Search(query, c => [c.Name, c.OriginalName])
                .ToListResult(c => new AnidbCharacter(c.Result), page, pageSize);

        return anidbCharacters.GetAll()
            .ToListResult(c => new AnidbCharacter(c), page, pageSize);
    }

    /// <summary>
    /// Get an anidb character by id.
    /// </summary>
    /// <param name="id">The character id.</param>
    /// <returns></returns>
    [HttpGet("Character/{id}")]
    public ActionResult<AnidbCharacter> GetCharacter(int id)
    {
        var character = anidbCharacters.GetByCharacterID(id);
        if (character == null)
            return NotFound();

        return new AnidbCharacter(character);
    }

    /// <summary>
    /// Get an anidb character by name.
    /// </summary>
    /// <param name="name">The character name.</param>
    /// <returns></returns>
    [HttpGet("Character/Name/{name}")]
    public ActionResult<AnidbCharacter> GetCharacter(string name)
    {
        var character = anidbCharacters.GetByName(name);
        if (character == null)
            return NotFound();

        return new AnidbCharacter(character);
    }
}
