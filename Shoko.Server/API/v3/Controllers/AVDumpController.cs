using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// A controller to configure the AVDump component.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class AVDumpController : BaseController
{
    private readonly ISchedulerFactory _schedulerFactory;
    public AVDumpController(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory) : base(settingsProvider)
    {
        _schedulerFactory = schedulerFactory;
    }

    /// <summary>
    /// Get status about the AVDump component.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Status")]
    public Dictionary<string, object?> GetAVDumpVersionInfo()
    {
        return new()
        {
            { "Installed", AVDumpHelper.IsAVDumpInstalled },
            { "InstalledVersion", AVDumpHelper.InstalledAVDumpVersion },
            { "ExpectedVersion", AVDumpHelper.AVDumpVersion },
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
    {
        if (!force)
        {
            var expectedVersion = AVDumpHelper.AVDumpVersion;
            var installedVersion = AVDumpHelper.InstalledAVDumpVersion;
            if (string.Equals(expectedVersion, installedVersion))
                return false;
        }

        return AVDumpHelper.UpdateAVDump();
    }

    /// <summary>
    /// Enqueue a request to run one or multiple files through AVDump.
    /// </summary>
    /// <param name="body">Body containing the file ids to dump.</param>
    /// <returns></returns>
    [HttpPost("DumpFiles")]
    public async Task<ActionResult> MultiDump([FromBody] AVDump.Input.DumpFilesBody body)
    {
        var settings = SettingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
            ModelState.AddModelError("Settings", "Missing AVDump API key");

        if (body.FileIDs.Count == 0)
            ModelState.AddModelError(nameof(body.FileIDs), "Provide at least one file id.");

        var fileDictionary = new Dictionary<int, string>();
        foreach (var fileID in body.FileIDs)
        {
            var file = RepoFactory.VideoLocal.GetByID(fileID);
            if (file == null)
            {
                ModelState.AddModelError(nameof(body.FileIDs), $"No file with id {fileID}");
                continue;
            }

            var filePath = file.FirstResolvedPlace?.Path;
            if (string.IsNullOrEmpty(filePath))
            {
                ModelState.AddModelError(nameof(body.FileIDs), $"Unable to find a valid path for file with id {fileID}");
                continue;
            }

            fileDictionary.Add(fileID, filePath);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<AVDumpFilesJob>(a => a.Videos = fileDictionary);

        return Ok();
    }
}
