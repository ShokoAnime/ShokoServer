using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Video;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// A controller to configure the AVDump component.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]"), Tags("Anidb")]
[ApiV3]
[Authorize]
public class AVDumpController(
    ISettingsProvider settingsProvider,
    IAnidbAvdumpService _avdumpService,
    VideoLocalRepository _videoLocals
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get status about the AVDump component.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Status")]
    public Dictionary<string, object?> GetAVDumpVersionInfo()
    {
        return new()
        {
            { "Installed", _avdumpService.IsAvdumpInstalled },
            { "InstalledVersion", _avdumpService.InstalledAvdumpVersion },
            { "ExpectedVersion", _avdumpService.AvailableAvdumpVersion },
        };
    }

    /// <summary>
    /// Update the installed AVDump component on a system.
    /// </summary>
    /// <param name="force">Forcefully update the AVDump component regardless
    /// of the version previously installed, if any.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Update")]
    public ActionResult<bool> UpdateAVDump([FromQuery] bool force = false)
        => _avdumpService.UpdateAvdump(force);

    /// <summary>
    /// Enqueue a request to run one or multiple files through AVDump.
    /// </summary>
    /// <param name="body">Body containing the file ids to dump.</param>
    /// <returns></returns>
    [HttpPost("DumpFiles")]
    public async Task<ActionResult> MultiDump([FromBody] AVDump.Input.DumpFilesBody body)
    {
        if (!_avdumpService.IsAvdumpConfigured)
            ModelState.AddModelError("Settings", "Missing AniDB username or AVDump API key");

        if (body.FileIDs.Count == 0)
            ModelState.AddModelError(nameof(body.FileIDs), "Provide at least one file id.");

        var places = new List<IVideoFile>();
        foreach (var fileID in body.FileIDs)
        {
            var file = _videoLocals.GetByID(fileID);
            if (file == null)
            {
                ModelState.AddModelError(nameof(body.FileIDs), $"No file with id {fileID}");
                continue;
            }

            if (file.FirstResolvedPlace is not { Path.Length: > 0 } place)
            {
                ModelState.AddModelError(nameof(body.FileIDs), $"Unable to find a valid path for file with id {fileID}");
                continue;
            }

            places.Add(place);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        await _avdumpService.ScheduleAvdumpVideoFiles(true, [.. places]).ConfigureAwait(false);

        return Ok();
    }
}
