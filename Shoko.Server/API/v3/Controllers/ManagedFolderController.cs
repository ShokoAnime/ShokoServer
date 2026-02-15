using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quartz;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;

using AbstractDropFolderType = Shoko.Abstractions.Enums.DropFolderType;
using Directory = System.IO.Directory;
using Path = System.IO.Path;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Responsible for handling managed folders.
/// </summary>
/// <param name="settingsProvider"></param>
/// <param name="schedulerFactory"></param>
/// <param name="videoService"></param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ManagedFolderController(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, IVideoService videoService) : BaseController(settingsProvider)
{
    /// <summary>
    /// List all managed folders
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ManagedFolder>> GetAllManagedFolders()
        => RepoFactory.ShokoManagedFolder.GetAll()
            .Select(a => new ManagedFolder(a))
            .ToList();

    /// <summary>
    /// Add a new managed folder. Does not run import on the folder, so you must scan it yourself.
    /// </summary>
    /// <returns><see cref="ManagedFolder"/> with generated values like ID</returns>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<ManagedFolder> AddManagedFolder(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ManagedFolder.Input.CreateManagedFolderBody body
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrEmpty(body.Path))
            return ValidationProblem("Path not provided. Managed Folders must be a location that exists on the server.", nameof(body.Path));

        body.Path = body.Path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (body.Path[^1] != Path.DirectorySeparatorChar)
            body.Path += Path.DirectorySeparatorChar;
        if (!Directory.Exists(body.Path))
            return ValidationProblem("Path does not exist. Managed Folders must be a location that exists on the server.", nameof(body.Path));

        if (RepoFactory.ShokoManagedFolder.GetAll().Any(iF => body.Path.StartsWith(iF.Path, StringComparison.OrdinalIgnoreCase) || iF.Path.StartsWith(body.Path, StringComparison.OrdinalIgnoreCase)))
            return ValidationProblem("Unable to nest an managed folder within another managed folder.", nameof(body.Path));

        try
        {
            var dropFolderType = AbstractDropFolderType.Excluded;
            if (body.DropFolderType.HasFlag(DropFolderType.Source))
                dropFolderType |= AbstractDropFolderType.Source;
            if (body.DropFolderType.HasFlag(DropFolderType.Destination))
                dropFolderType |= AbstractDropFolderType.Destination;
            var newFolder = (ShokoManagedFolder)videoService.AddManagedFolder(body.Name, body.Path, dropFolderType, body.WatchForNewFiles);
            return new ManagedFolder(newFolder);
        }
        catch (Exception e)
        {
            return InternalError(e.Message);
        }
    }

    /// <summary>
    ///   Notify the server that it may have been changes at the given absolute path.
    /// </summary>
    /// <param name="body">The body containing the absolute path.</param>
    /// <returns>
    ///   No content.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("NotifyChangeDetected")]
    public ActionResult NotifyVideoFileChangeDetectedAbsolutePath(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ManagedFolder.Input.NotifyChangeDetectedAbsoluteBody body
    )
    {
        Task.Run(() => videoService.NotifyVideoFileChangeDetected(body.Path, body.UpdateMyList));
        return NoContent();
    }

    /// <summary>
    /// Get the <see cref="ManagedFolder"/> by the given <paramref name="folderID"/>.
    /// </summary>
    /// <param name="folderID">Managed Folder ID</param>
    /// <returns></returns>
    [HttpGet("{folderID}")]
    public ActionResult<ManagedFolder> GetManagedFolderByFolderID([FromRoute, Range(1, int.MaxValue)] int folderID)
    {
        if (RepoFactory.ShokoManagedFolder.GetByID(folderID) is not { } folder)
            return NotFound("Folder not found.");

        return new ManagedFolder(folder);
    }

    /// <summary>
    /// Patch the <see cref="ManagedFolder"/> by the given <paramref name="folderID"/> using JSON Patch.
    /// </summary>
    /// <param name="folderID">Managed Folder ID</param>
    /// <param name="patch">JSON Patch document</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPatch("{folderID}")]
    public ActionResult PatchManagedFolderByFolderID([FromRoute, Range(1, int.MaxValue)] int folderID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] JsonPatchDocument<ManagedFolder> patch)
    {
        if (RepoFactory.ShokoManagedFolder.GetByID(folderID) is not { } folder)
            return NotFound("Folder not found.");

        var patchModel = new ManagedFolder(folder);
        patch.ApplyTo(patchModel, ModelState);
        if (!TryValidateModel(patchModel))
            return ValidationProblem(ModelState);

        var serverModel = patchModel.GetServerModel();
        RepoFactory.ShokoManagedFolder.SaveFolder(serverModel);

        return Ok();
    }

    /// <summary>
    /// Edit Managed Folder. This replaces all values. 
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut]
    public ActionResult EditManagedFolder([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ManagedFolder body)
    {
        if (body.ID is 0)
            ModelState.AddModelError(nameof(body.ID), "ID missing. If this is a new Folder, then use POST.");
        else if (RepoFactory.ShokoManagedFolder.GetByID(body.ID) == null)
            ModelState.AddModelError(nameof(body.ID), "ID invalid. If this is a new Folder, then use POST.");

        if (string.IsNullOrEmpty(body.Path))
            ModelState.AddModelError(nameof(body.Path), "Path not provided. Managed Folders must be a location that exists on the server.");
        body.Path = body.Path?.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar) ?? string.Empty;
        if (body.Path[^1] != Path.DirectorySeparatorChar)
            body.Path += Path.DirectorySeparatorChar;
        if (!string.IsNullOrEmpty(body.Path) && !Directory.Exists(body.Path))
            ModelState.AddModelError(nameof(body.Path), "Path does not exist. Managed Folders must be a location that exists on the server.");
        if (RepoFactory.ShokoManagedFolder.GetAll().ExceptBy([body.ID], iF => iF.ID)
            .Any(iF => body.Path.StartsWith(iF.Path, StringComparison.OrdinalIgnoreCase) || iF.Path.StartsWith(body.Path, StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError(nameof(body.Path), "Unable to nest an managed folder within another managed folder.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        RepoFactory.ShokoManagedFolder.SaveFolder(body.GetServerModel());

        return Ok();
    }

    /// <summary>
    /// Delete an Managed Folder
    /// </summary>
    /// <param name="folderID">Managed Folder ID</param>
    /// <param name="removeRecords">If this is false, then VideoLocals, DuplicateFiles, and several other things will be left intact. This is for migration of files to new locations.</param>
    /// <param name="updateMyList">Pretty self explanatory. If this is true, and <paramref name="removeRecords"/> is true, then it will update the list status</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{folderID}")]
    public async Task<ActionResult> DeleteManagedFolderByFolderID([FromRoute, Range(1, int.MaxValue)] int folderID, [FromQuery] bool removeRecords = true,
        [FromQuery] bool updateMyList = true)
    {
        if (RepoFactory.ShokoManagedFolder.GetByID(folderID) is not { } folder)
            return NotFound("Folder not found.");

        await videoService.RemoveManagedFolder(folder, !removeRecords, updateMyList);

        return Ok();
    }

    /// <summary>
    ///   Scan a specific managed folder. This will checks ALL files, not just
    ///   new ones. Good for cleaning up files in strange states and making drop
    ///   folders retry moves.
    /// </summary>
    /// <param name="folderID">Managed Folder ID</param>
    /// <param name="relativePath">Relative path to scan.</param>
    /// <param name="onlyNewFiles">Only scan new files</param>
    /// <param name="skipMylist">Skip updating the MyList for this folder</param>
    /// <param name="priority">Prioritize this job in the queue.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{folderID}/Scan")]
    public async Task<ActionResult> ScanManagedFolderByFolderID(
        [FromRoute, Range(1, int.MaxValue)] int folderID,
        [FromQuery] string relativePath = "",
        [FromQuery] bool onlyNewFiles = false,
        [FromQuery] bool skipMylist = false,
        [FromQuery] bool priority = false
    )
    {
        if (RepoFactory.ShokoManagedFolder.GetByID(folderID) is not { } folder)
            return NotFound("Folder not found");

        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<ScanFolderJob>(j => (j.ManagedFolderID, j.RelativePath, j.OnlyNewFiles, j.SkipMyList) = (folderID, relativePath, onlyNewFiles, skipMylist), prioritize: priority);
        return Ok();
    }

    /// <summary>
    ///   Notify the server that it may have been changes at the given relative path.
    /// </summary>
    /// <param name="folderID">Managed Folder ID</param>
    /// <param name="body">The body containing the absolute path.</param>
    /// <returns>
    ///   No content.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("{folderID}/NotifyChangeDetected")]
    public ActionResult NotifyVideoFileChangeDetectedRelativePathByFolderID(
        [FromRoute, Range(1, int.MaxValue)] int folderID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ManagedFolder.Input.NotifyChangeDetectedRelativeBody body
    )
    {
        if (RepoFactory.ShokoManagedFolder.GetByID(folderID) is not { } folder)
            return NotFound("Folder not found.");
        Task.Run(() => videoService.NotifyVideoFileChangeDetected(folder, body.RelativePath, body.UpdateMyList));
        return NoContent();
    }

    /// <summary>
    /// Get all <see cref="File"/>s in the <see cref="ManagedFolder"/> with the given <paramref name="folderID"/>.
    /// </summary>
    /// <param name="folderID">Managed folder ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="folderPath">Filter the list to only contain files starting with the given parent folder path.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns></returns>
    [HttpGet("{folderID}/File")]
    public ActionResult<ListResult<File>> GetFilesInManagedFolder([FromRoute, Range(1, int.MaxValue)] int folderID,
        [FromQuery, Range(0, 10000)] int pageSize = 200,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string folderPath = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        include ??= [];

        if (RepoFactory.ShokoManagedFolder.GetByID(folderID) is not { } folder)
            return NotFound("Folder not found: " + folderID);

        IEnumerable<VideoLocal_Place> locations = RepoFactory.VideoLocalPlace.GetByManagedFolderID(folder.ID);

        // Filter the list to only files matching a certain sub-path.
        if (!string.IsNullOrEmpty(folderPath))
        {
            folderPath = folderPath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            // Remove leading separator.
            if (folderPath.Length > 0 && folderPath[0] == Path.DirectorySeparatorChar)
                folderPath = folderPath[1..];

            // Append tailing separator if the string is not empty, since we're searching for the folder path.
            if (folderPath.Length > 0 && folderPath[^1] != Path.DirectorySeparatorChar)
                folderPath += Path.DirectorySeparatorChar;

            // Only filter if we still have a path to filter.
            if (!string.IsNullOrEmpty(folderPath))
                locations = locations
                    .Where(place => place.RelativePath.StartsWith(folderPath));
        }

        return locations
            .GroupBy(place => place.VideoID)
            .Select(places => RepoFactory.VideoLocal.GetByID(places.Key))
            .WhereNotNull()
            .OrderBy(file => file.DateTimeCreated)
            .ToListResult(file => new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths)), page, pageSize);
    }
}
