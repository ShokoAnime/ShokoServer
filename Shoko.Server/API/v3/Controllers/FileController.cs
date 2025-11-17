using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.StaticFiles;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Relocation;
using Shoko.Server.API.v3.Models.Relocation.Input;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AVDump = Shoko.Server.API.v3.Models.Shoko.AVDump;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using File = Shoko.Server.API.v3.Models.Shoko.File;
using MediaInfoDto = Shoko.Server.API.v3.Models.Shoko.MediaInfo;
using Path = System.IO.Path;
using ReleaseInfo = Shoko.Server.API.v3.Models.Release.ReleaseInfo;

namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[Authorize]
public class FileController(
    TraktTVHelper _traktHelper,
    ISchedulerFactory _schedulerFactory,
    VideoLocalService _vlService,
    VideoLocal_PlaceService _vlPlaceService,
    VideoLocal_UserRepository _vlUsers,
    IVideoReleaseService _videoReleaseService,
    IUserDataService _userDataService,
    IRelocationService _relocationService,
    ISettingsProvider settingsProvider
) : BaseController(settingsProvider)
{
    private const string FileUserStatsNotFoundWithFileID = "No FileUserStats entry for the given fileID for the current user";

    private const string FileNoPath = "Unable to resolve file location.";

    private const string AnidbReleaseNotFoundForFileID = "No AniDB ReleaseInfo entry for the given fileID";

    private const string AnidbReleaseNotFoundForAnidbFileID = "No AniDB ReleaseInfo entry for the given anidbFileID";

    private const string ReleaseNotFoundForFileID = "No ReleaseInfo entry for the given fileID";

    internal const string FileNotFoundWithFileID = "No File entry for the given fileID";

    internal const string FileNotFoundWithHash = "No File entry for the given hash and file size.";

    internal const string FileLocationNotFoundWithLocationID = "No File.Location entry for the given locationID.";

    internal const string FileForbiddenForUser = "Accessing File is not allowed for the current user";

    /// <summary>
    /// Get or search through the files accessible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns>A sliced part of the results for the current query.</returns>
    [HttpGet]
    public ActionResult<ListResult<File>> GetFiles(
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[] exclude = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[] include_only = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string> sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        return ModelHelper.FilterFiles(RepoFactory.VideoLocal.GetAll(), User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
    }

    /// <summary>
    /// Get or search through the files accessible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="query">An optional search query to filter files based on their absolute paths.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns>A sliced part of the results for the current query.</returns>
    [HttpGet("Search/{*query}")]
    public ActionResult<ListResult<File>> Search([FromRoute] string query,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[] exclude = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[] include_only = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string> sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool fuzzy = true)
    {
        // Search.
        var searched = RepoFactory.VideoLocal.GetAll()
            .Search(query, tuple => tuple.Places.Select(place => place?.RelativePath).Where(path => path != null), fuzzy)
            .Select(result => result.Result)
            .ToList();
        return ModelHelper.FilterFiles(searched, User, pageSize, page, include, exclude, include_only, sortOrder,
            includeDataFrom, skipSort: true);
    }

    /// <summary>
    /// Batch delete files using file ids.
    /// </summary>
    /// <param name="body">The body containing the file ids to delete.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete]
    public async Task<ActionResult> DeleteFiles([FromBody] File.Input.BatchDeleteFilesBody body = null)
    {
        if (body == null)
            return ValidationProblem("Missing Body.");

        if (body.fileIDs.Length == 0)
            ModelState.AddModelError("fileIds", "Missing file ids.");

        var files = body.fileIDs
            .Select(fileId =>
            {
                var file = RepoFactory.VideoLocal.GetByID(fileId);
                if (file == null)
                    ModelState.AddModelError("fileIds", $"Unable to find a file with id {fileId}");
                return file;
            })
            .Where(a => a != null)
            .ToList();

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        foreach (var file in files)
        {
            foreach (var place in file.Places)
            {
                if (body.removeFiles)
                    await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(place, body.removeFolders);
                else
                    await _vlPlaceService.RemoveRecord(place);
            }
        }

        return Ok();
    }

    #region Hash Lookup

    /// <summary>
    /// Get a file by it's ED2K hash and file size.
    /// </summary>
    /// <param name="hash">ED2K hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns>The file that matches the given ED2K hash and file size, if found.</returns>
    [HttpGet("Hash/ED2K")]
    public ActionResult<File> GetFileByEd2k(
        [FromQuery, Required, Length(32, 32)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetByEd2kAndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Get a file by it's CRC32 hash and file size.
    /// </summary>
    /// <param name="hash">CRC32 hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns>The file that matches the given CRC32 hash and file size, if found.</returns>
    [HttpGet("Hash/CRC32")]
    public ActionResult<File> GetFileByCrc32(
        [FromQuery, Required, Length(8, 8)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetByCrc32AndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Get a file by it's MD5 hash and file size.
    /// </summary>
    /// <param name="hash">MD5 hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns>The file that matches the given MD5 hash and file size, if found.</returns>
    [HttpGet("Hash/MD5")]
    public ActionResult<File> GetFileByMd5(
        [FromQuery, Required, Length(32, 32)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetByMd5AndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Get a file by it's SHA1 hash and file size.
    /// </summary>
    /// <param name="hash">SHA1 hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns>The file that matches the given SHA1 hash and file size, if found.</returns>
    [HttpGet("Hash/SHA1")]
    public ActionResult<File> GetFileBySha1(
        [FromQuery, Required, Length(40, 40)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetBySha1AndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    #endregion

    /// <summary>
    /// Get File Details
    /// </summary>
    /// <param name="fileID">Shoko VideoLocalID</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns></returns>
    [HttpGet("{fileID}")]
    public ActionResult<File> GetFile(
        [FromRoute, Range(1, int.MaxValue)] int fileID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Delete a file.
    /// </summary>
    /// <param name="fileID">The VideoLocal_Place ID. This cares about which location we are deleting from.</param>
    /// <param name="removeFiles">Remove all physical file locations.</param>
    /// <param name="removeFolder">This causes the empty folder removal to skipped if set to false.
    /// This significantly speeds up batch deleting if you are deleting many files in the same folder.
    /// It may be specified in the query.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{fileID}")]
    public async Task<ActionResult> DeleteFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool removeFiles = true, [FromQuery] bool removeFolder = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        foreach (var place in file.Places)
            if (removeFiles)
                await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(place, removeFolder);
            else
                await _vlPlaceService.RemoveRecord(place);
        return Ok();
    }

    /// <summary>
    /// Retrieves all file locations associated with a given file ID.
    /// </summary>
    /// <param name="fileID">File ID</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <returns>A list of file locations associated with the specified file ID.</returns>
    [HttpGet("{fileID}/Location")]
    public ActionResult<List<File.Location>> GetFileLocations([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool includeAbsolutePaths = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileLocationNotFoundWithLocationID);

        return file.Places
            .Select(location => new File.Location(location, includeAbsolutePaths))
            .ToList();
    }

    /// <summary>
    /// Deletes file locations in batches, optionally also deleting the physical
    /// file.
    /// </summary>
    /// <param name="body">
    /// The body with the IDs to delete, optionally with other options.
    /// </param>
    /// <returns>Returns the file location information.</returns>
    [Authorize("admin")]
    [HttpDelete("Location")]
    public async Task<ActionResult> BatchDeleteFileLocations([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] File.Input.BatchDeleteFileLocationsBody body)
    {
        var locations = body.locationIDs
            .Select(locationID => locationID <= 0 ? null : RepoFactory.VideoLocalPlace.GetByID(locationID))
            .WhereNotNull()
            .ToList();
        var errors = new List<Exception>();
        foreach (var location in locations)
        {
            try
            {
                if (body.removeFiles)
                    await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(location, body.removeFolders);
                else
                    await _vlPlaceService.RemoveRecord(location);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException(errors);

        return Ok();
    }

    /// <summary>
    /// Retrieves information about a specific file location.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be retrieved.
    /// </param>
    /// <returns>Returns the file location information.</returns>
    [HttpGet("Location/{locationID}")]
    public ActionResult<File.Location> GetFileLocation([FromRoute, Range(1, int.MaxValue)] int locationID)
    {
        if (RepoFactory.VideoLocalPlace.GetByID(locationID) is not { } fileLocation)
            return NotFound(FileLocationNotFoundWithLocationID);

        return new File.Location(fileLocation, true);
    }

    /// <summary>
    /// Directly relocates a file to a new location specified by the user.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be relocated.</param>
    /// <param name="body">New location information.</param>
    /// <returns>A result object containing information about the relocation process.</returns>
    [Authorize("admin")]
    [HttpPost("Location/{locationID}")]
    public async Task<ActionResult<RelocationResult>> DirectlyRelocateFileLocation([FromRoute, Range(1, int.MaxValue)] int locationID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] RelocateBody body)
    {
        if (RepoFactory.VideoLocalPlace.GetByID(locationID) is not { } fileLocation)
            return NotFound(FileLocationNotFoundWithLocationID);

        if (RepoFactory.ShokoManagedFolder.GetByID(body.ManagedFolderID) is not { } folder)
            return BadRequest($"Unknown managed folder with the given id `{body.ManagedFolderID}`.");

        // Store the old managed folder id and relative path for comparison.
        var oldFolderId = fileLocation.ManagedFolderID;
        var oldRelativePath = fileLocation.RelativePath;

        // Rename and move the file.
        var result = await _relocationService.DirectlyRelocateFile(
            fileLocation,
            new()
            {
                CancellationToken = HttpContext.RequestAborted,
                ManagedFolder = folder,
                RelativePath = body.RelativePath,
                DeleteEmptyDirectories = body.DeleteEmptyDirectories,
                AllowRelocationInsideDestination = true,
            }
        );
        if (!result.Success)
            return new RelocationResult
            {
                FileID = fileLocation.VideoID,
                FileLocationID = fileLocation.ID,
                IsSuccess = false,
                ErrorMessage = result.Error.Message,
            };

        // Check if it was actually relocated, or if we landed on the same location as earlier.
        var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldFolderId != result.ManagedFolder.ID;
        return new RelocationResult
        {
            FileID = fileLocation.VideoID,
            FileLocationID = fileLocation.ID,
            ManagedFolderID = result.ManagedFolder.ID,
            IsSuccess = true,
            IsRelocated = relocated,
            RelativePath = result.RelativePath,
            AbsolutePath = result.AbsolutePath,
        };
    }

    /// <summary>
    /// Deletes a file location, optionally also deleting the physical file.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be deleted.
    /// </param>
    /// <param name="deleteFile">Whether to delete the physical file.</param>
    /// <param name="deleteFolder">Whether to delete any empty folders after removing the file.</param>
    /// <returns>Returns a result indicating if the deletion was successful.
    /// </returns>
    [Authorize("admin")]
    [HttpDelete("Location/{locationID}")]
    public async Task<ActionResult> DeleteFileLocation([FromRoute, Range(1, int.MaxValue)] int locationID, [FromQuery] bool deleteFile = true, [FromQuery] bool deleteFolder = true)
    {
        var fileLocation = RepoFactory.VideoLocalPlace.GetByID(locationID);
        if (fileLocation == null)
            return NotFound(FileLocationNotFoundWithLocationID);

        if (deleteFile)
            await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(fileLocation, deleteFolder);
        else
            await _vlPlaceService.RemoveRecord(fileLocation);

        return Ok();
    }

    /// <summary>
    /// Get the <see cref="ReleaseInfo"/> using the <paramref name="fileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="fileID">Shoko File ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/Release")]
    public ActionResult<ReleaseInfo> GetFileReleaseByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        if (file.ReleaseInfo is not { ReleaseURI: not null } releaseInfo)
            return NotFound(ReleaseNotFoundForFileID);

        return new ReleaseInfo(releaseInfo);
    }

    /// <summary>
    /// Get the <see cref="ReleaseInfo"/> using the <paramref name="fileID"/> if the provider is AniDB.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="fileID">Shoko File ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/AniDB")]
    public ActionResult<ReleaseInfo> GetFileAnidbByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        if (file.ReleaseInfo is not { ReleaseURI: not null } releaseInfo || !releaseInfo.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix))
            return NotFound(AnidbReleaseNotFoundForFileID);

        return new ReleaseInfo(releaseInfo);
    }

    /// <summary>
    /// Get the <see cref="ReleaseInfo"/> using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}")]
    public ActionResult<ReleaseInfo> GetFileAnidbByAnidbFileID([FromRoute, Range(1, int.MaxValue)] int anidbFileID)
    {
        if (
            RepoFactory.StoredReleaseInfo.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{anidbFileID}") is not { ReleaseURI: not null } anidb ||
            !anidb.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix)
        )
            return NotFound(AnidbReleaseNotFoundForAnidbFileID);

        return new ReleaseInfo(anidb);
    }

    /// <summary>
    /// Get the <see cref="File"/> for the AniDB file using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}/File")]
    public ActionResult<File> GetFileByAnidbFileID(
        [FromRoute, Range(1, int.MaxValue)] int anidbFileID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default)
    {
        if (
            RepoFactory.StoredReleaseInfo.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{anidbFileID}") is not { ReleaseURI: not null } anidb ||
            !anidb.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix)
        )
            return NotFound(AnidbReleaseNotFoundForAnidbFileID);

        var file = RepoFactory.VideoLocal.GetByEd2kAndSize(anidb.ED2K, anidb.FileSize);
        if (file == null)
            return NotFound(AnidbReleaseNotFoundForAnidbFileID);

        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Rescan a file on AniDB using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <param name="priority">Increase the priority to the max for the queued command.</param>
    /// <returns></returns>
    [HttpPost("AniDB/{anidbFileID}/Rescan")]
    public async Task<ActionResult> RescanFileByAniDBFileID([FromRoute, Range(1, int.MaxValue)] int anidbFileID, [FromQuery] bool priority = false)
    {
        if (
            RepoFactory.StoredReleaseInfo.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{anidbFileID}") is not { ReleaseURI: not null } anidb ||
            !anidb.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix)
        )
            return NotFound(AnidbReleaseNotFoundForFileID);

        var file = RepoFactory.VideoLocal.GetByEd2kAndSize(anidb.ED2K, anidb.FileSize);
        if (file == null)
            return NotFound(AnidbReleaseNotFoundForFileID);

        var filePath = file.FirstResolvedPlace?.Path;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        if (!_videoReleaseService.AutoMatchEnabled)
            return ValidationProblem("Release auto-matching is currently disabled.", "IVideoReleaseService");

        await _videoReleaseService.ScheduleFindReleaseForVideo(file, force: true, prioritize: priority);
        return Ok();
    }

    /// <summary>
    /// Returns a file stream for the specified file ID.
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <param name="streamPositionScrobbling">If this is enabled, then the file is marked as watched when the stream reaches the end.
    /// This is not a good way to scrobble, but it allows for players without plugin support to have an option to scrobble.
    /// The read-ahead buffer on the player would determine the required percentage to scrobble.</param>
    /// <returns>A file stream for the specified file.</returns>
    [AllowAnonymous]
    [HttpGet("{fileID}/Stream")]
    [HttpHead("{fileID}/Stream")]
    public ActionResult GetFileStream([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool streamPositionScrobbling = false)
        => GetFileStreamInternal(fileID, null, streamPositionScrobbling);

    /// <summary>
    /// Returns a file stream for the specified file ID.
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <param name="filename">Can use this to select a specific place (if the name is different). This is mostly used as a hint for players</param>
    /// <param name="streamPositionScrobbling">If this is enabled, then the file is marked as watched when the stream reaches the end.
    /// This is not a good way to scrobble, but it allows for players without plugin support to have an option to scrobble.
    /// The read-ahead buffer on the player would determine the required percentage to scrobble.</param>
    /// <returns>A file stream for the specified file.</returns>
    [AllowAnonymous]
    [HttpGet("{fileID}/StreamDirectory/{filename}")]
    [HttpHead("{fileID}/StreamDirectory/{filename}")]
    public ActionResult GetFileStreamWithDirectory([FromRoute, Range(1, int.MaxValue)] int fileID, [FromRoute] string filename = null, [FromQuery] bool streamPositionScrobbling = false)
        => GetFileStreamInternal(fileID, filename, streamPositionScrobbling);

    [NonAction]
    public ActionResult GetFileStreamInternal(int fileID, string filename = null, bool streamPositionScrobbling = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var bestLocation = file.Places.FirstOrDefault(a => a.FileName.Equals(filename));
        bestLocation ??= file.FirstValidPlace;

        var fileInfo = bestLocation.FileInfo;
        if (fileInfo == null)
            return InternalError("Unable to find physical file for reading the stream data.");

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileInfo.FullName, out var contentType))
            contentType = "application/octet-stream";

        if (streamPositionScrobbling)
        {
            var scrobbleFile = new ScrobblingFileResult(file, User, fileInfo.FullName, contentType)
            {
                FileDownloadName = filename ?? fileInfo.Name
            };
            return scrobbleFile;
        }

        var physicalFile = PhysicalFile(fileInfo.FullName, contentType, enableRangeProcessing: true);
        physicalFile.FileDownloadName = filename ?? fileInfo.Name;
        return physicalFile;
    }

    /// <summary>
    /// Returns the external subtitles for a file
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <returns>A file stream for the specified file.</returns>
    [AllowAnonymous]
    [HttpGet("{fileID}/StreamDirectory/")]
    public ActionResult GetFileStreamDirectory([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var routeTemplate = Request.Scheme + "://" + Request.Host + "/api/v3/File/" + fileID + "/StreamDirectory/ExternalSub/";
        return new ObjectResult("<table>" + string.Join(string.Empty,
            file.MediaInfo.TextStreams.Where(a => a.External).Select(a => $"<tr><td><a href=\"{routeTemplate + a.Filename}\"/></td></tr>")) + "</table>");
    }

    /// <summary>
    /// Gets an external subtitle file
    /// </summary>
    /// <param name="fileID"></param>
    /// <param name="filename"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("{fileID}/StreamDirectory/ExternalSub/{filename}")]
    public ActionResult GetExternalSubtitle([FromRoute, Range(1, int.MaxValue)] int fileID, [FromRoute] string filename)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        foreach (var place in file.Places)
        {
            var path = place.FileInfo?.Directory?.FullName;
            if (path == null) continue;
            path = Path.Combine(path, filename);
            var subFile = new FileInfo(path);
            if (!subFile.Exists) continue;

            return PhysicalFile(subFile.FullName, "application/octet-stream");
        }

        return NotFound();
    }

    /// <summary>
    /// Get the MediaInfo model for file with VideoLocal ID
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/MediaInfo")]
    public ActionResult<MediaInfoDto> GetFileMediaInfo([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var mediaContainer = file.MediaInfo;
        if (mediaContainer == null)
            return InternalError("Unable to find media container for File");

        return new MediaInfoDto(file, mediaContainer);
    }

    /// <summary>
    /// Return the user stats for the file with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <returns>The user stats if found.</returns>
    [HttpGet("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> GetFileUserStats([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var user = HttpContext.GetUser();
        var userStats = _vlUsers.GetByUserIDAndVideoLocalID(user.JMMUserID, file.VideoLocalID);

        if (userStats == null)
            return NotFound(FileUserStatsNotFoundWithFileID);

        return new File.FileUserStats(userStats);
    }

    /// <summary>
    /// Put a <see cref="File.FileUserStats"/> object down for the <see cref="File"/> with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <param name="fileUserStats">The new and/or update file stats to put for the file.</param>
    /// <returns>The new and/or updated user stats.</returns>
    [HttpPut("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> PutFileUserStats([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody] File.FileUserStats fileUserStats)
    {
        // Make sure the file exists.
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Get the user data.
        var user = HttpContext.GetUser();
        var userStats = _vlService.GetOrCreateUserRecord(file, user.JMMUserID);

        // Merge with the existing entry and return an updated version of the stats.
        return fileUserStats.MergeWithExisting(userStats, file);
    }

    /// <summary>
    /// Patch a <see cref="File.FileUserStats"/> object down for the <see cref="File"/> with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <param name="patchDocument">The JSON patch document to apply to the existing <see cref="File.FileUserStats"/>.</param>
    /// <returns>The new and/or updated user stats.</returns>
    [HttpPatch("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> PatchFileUserStats([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody] JsonPatchDocument<File.FileUserStats> patchDocument)
    {
        // Make sure the file exists.
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Get the user data.
        var user = HttpContext.GetUser();
        var userStats = _vlService.GetOrCreateUserRecord(file, user.JMMUserID);

        // Patch the body with the existing model.
        var body = new File.FileUserStats(userStats);
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Merge with the existing entry and return an updated version of the stats.
        return body.MergeWithExisting(userStats, file);
    }

    /// <summary>
    /// Mark a file as watched or unwatched.
    /// </summary>
    /// <param name="fileID">VideoLocal ID. Watched Status is kept per file, no matter how many copies or where they are.</param>
    /// <param name="watched">Is it watched?</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Watched/{watched?}")]
    public async Task<ActionResult> SetWatchedStatusOnFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromRoute] bool watched = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        await _userDataService.SetVideoWatchedStatus(User, file, watched);

        return Ok();
    }

    /// <summary>
    /// Update either watch status, resume position, or both.
    /// </summary>
    /// <param name="fileID">VideoLocal ID. Watch status and resume position is kept per file, regardless of how many duplicates the file has.</param>
    /// <param name="eventName">The name of the event that triggered the scrobble.</param>
    /// <param name="episodeID">The episode id to scrobble to trakt.</param>
    /// <param name="watched">True if file should be marked as watched, false if file should be unmarked, or null if it shall not be updated.</param>
    /// <param name="resumePosition">Number of ticks into the video to resume from, or null if it shall not be updated.</param>
    /// <returns></returns>
    [HttpGet("{fileID}/Scrobble")]
    [HttpPatch("{fileID}/Scrobble")]
    public async Task<ActionResult> ScrobbleFileAndEpisode([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery(Name = "event")] string eventName = null, [FromQuery] int? episodeID = null, [FromQuery] bool? watched = null, [FromQuery] long? resumePosition = null)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var episode = episodeID.HasValue ? RepoFactory.AnimeEpisode.GetByID(episodeID.Value) : file.AnimeEpisodes?.FirstOrDefault();
        if (episode == null)
            return ValidationProblem($"Could not get Episode with ID: {episodeID}", nameof(episodeID));

        var playbackPositionTicks = TimeSpan.FromTicks(resumePosition ?? 0);
        var totalDurationTicks = file.DurationTimeSpan;
        if (playbackPositionTicks >= totalDurationTicks)
        {
            watched = true;
            playbackPositionTicks = TimeSpan.Zero;
        }

        switch (eventName)
        {
            // The playback was started.
            case "play":
            // The playback was resumed after a pause.
            case "resume":
                ScrobbleToTrakt(episode, (float)(playbackPositionTicks / totalDurationTicks), ScrobblePlayingStatus.Start);
                break;
            // The playback was paused.
            case "pause":
                ScrobbleToTrakt(episode, (float)(playbackPositionTicks / totalDurationTicks), ScrobblePlayingStatus.Pause);
                break;
            // The playback was ended.
            case "stop":
                ScrobbleToTrakt(episode, (float)(playbackPositionTicks / totalDurationTicks), ScrobblePlayingStatus.Stop);
                break;
            // The playback is still active, but the playback position changed.
            case "scrobble":
                break;
            // A user interaction caused the watch state to change.
            case "user-interaction":
                break;
        }

        var reason = eventName switch
        {
            "play" => UserDataSaveReason.PlaybackStart,
            "resume" => UserDataSaveReason.PlaybackResume,
            "pause" => UserDataSaveReason.PlaybackPause,
            "stop" => UserDataSaveReason.PlaybackEnd,
            "scrobble" => UserDataSaveReason.PlaybackProgress,
            "user-interaction" => UserDataSaveReason.UserInteraction,
            _ => UserDataSaveReason.None,
        };
        var now = DateTime.Now;
        var userData = _userDataService.GetVideoUserData(User.JMMUserID, file.VideoLocalID);
        await _userDataService.SaveVideoUserData(User, file, new()
        {
            ResumePosition = resumePosition.HasValue
                ? playbackPositionTicks
                : (watched.HasValue ? TimeSpan.Zero : null),
            LastPlayedAt = watched.HasValue
                ? (watched.Value ? now : null)
                : userData?.LastPlayedAt,
            LastUpdatedAt = now,
        }, reason);

        return NoContent();
    }

    [NonAction]
    private void ScrobbleToTrakt(SVR_AnimeEpisode episode, float percentage, ScrobblePlayingStatus status)
    {
        if (User.IsTraktUser == 0)
            return;

        var scrobbleType = episode.AnimeSeries?.AniDB_Anime?.AnimeType == (int)AnimeType.Movie
            ? ScrobblePlayingType.movie
            : ScrobblePlayingType.episode;

        _traktHelper.Scrobble(scrobbleType, episode.AnimeEpisodeID.ToString(), status, percentage);
    }

    /// <summary>
    /// Mark or unmark a file as ignored.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="value">Thew new ignore value.</param>
    /// <returns></returns>
    [HttpPut("{fileID}/Ignore")]
    public ActionResult MarkFileAsIgnored([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool value = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        file.IsIgnored = value;
        RepoFactory.VideoLocal.Save(file, false);

        return Ok();
    }

    /// <summary>
    /// Mark or unmark a file as a variation.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="value">Thew new variation value.</param>
    /// <returns></returns>
    [HttpPut("{fileID}/Variation")]
    public ActionResult MarkFileAsVariation([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool value = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        file.IsVariation = value;
        RepoFactory.VideoLocal.Save(file, false);

        return Ok();
    }

    /// <summary>
    /// Run a file through AVDump and return the result.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="priority">Increase the priority to the max for the queued command.</param>
    /// <param name="immediate">Immediately run the AVDump, without adding the command to the queue.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/AVDump")]
    public async Task<ActionResult<AVDump.Result>> AvDumpFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool priority = false,
        [FromQuery] bool immediate = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var settings = SettingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
            ModelState.AddModelError("Settings", "Missing AVDump API key");

        var filePath = file.FirstResolvedPlace?.Path;
        if (string.IsNullOrEmpty(filePath))
            ModelState.AddModelError("File", FileNoPath);

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var files = new Dictionary<int, string> { { file.VideoLocalID, filePath } };
        if (immediate)
            AVDumpHelper.DumpFiles(files, true);
        else
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = files, prioritize: priority).ConfigureAwait(false);
        }

        return Ok();
    }

    /// <summary>
    /// Rescan a file on AniDB.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="priority">Increase the priority to the max for the queued command.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Rescan")]
    public async Task<ActionResult> RescanFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool priority = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var filePath = file.FirstResolvedPlace?.Path;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        if (!_videoReleaseService.AutoMatchEnabled)
            return ValidationProblem("Release auto-matching is currently disabled.", "IVideoReleaseService");

        await _videoReleaseService.ScheduleFindReleaseForVideo(file, force: true, prioritize: priority);
        return Ok();
    }

    /// <summary>
    /// Rehash a file.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="priority">Whether to start the job immediately. Default true</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Rehash")]
    public async Task<ActionResult> RehashFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool priority = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var filePath = file.FirstResolvedPlace?.Path;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<HashFileJob>(
            c => (c.FilePath, c.ForceHash) = (filePath, true),
            prioritize: priority
        ).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    /// Force add a file to AniDB MyList
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/AddToMyList")]
    public async Task<ActionResult> AddFileToMyList([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<AddFileToMyListJob>(c => c.Hash = file.Hash, prioritize: true).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    /// Schedule a file and all it's locations to be automatically relocated in the queue.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="priority">Whether to start the job immediately. Default false</param>
    /// <returns>A result object containing information about the relocation process.</returns>
    [Authorize("admin")]
    [HttpPost("{fileID}/Action/AutoRelocate")]
    public async Task<ActionResult> ScheduleAutoRelocationForFileLocation([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery] bool priority = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        await _relocationService.ScheduleAutoRelocationForVideo(file, prioritize: priority);

        return Ok();
    }

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith")]
    public ActionResult<List<File>> PathEndsWithQuery([FromQuery] string path,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(path, include, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for. URL encoded.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith/{*path}")]
    public ActionResult<List<File>> PathEndsWithPath([FromRoute] string path,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(Uri.UnescapeDataString(path), include, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [NonAction]
    private ActionResult<List<File>> PathEndsWithInternal(string path, FileNonDefaultIncludeType[] include, int limit = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<File>();

        var query = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var results = RepoFactory.VideoLocalPlace.GetAll()
            .AsParallel()
            .Where(location => location.Path?.EndsWith(query, StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(location => location.VideoLocal)
            .Where(file =>
            {
                if (file == null)
                    return false;

                var xrefs = file.EpisodeCrossReferences;
                var series = xrefs.FirstOrDefault(xref => xref.AnimeID is not 0)?.AnimeSeries;
                return series == null || User.AllowedSeries(series);
            })
            .DistinctBy(file => file.VideoLocalID);

        if (limit <= 0)
            return results
                .Select(a => new File(HttpContext, a, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths)))
                .ToList();

        return results
            .Take(limit)
            .Select(a => new File(HttpContext, a, include.Contains(FileNonDefaultIncludeType.XRefs), include.Contains(FileNonDefaultIncludeType.ReleaseInfo),
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths)))
            .ToList();
    }

    /// <summary>
    /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
    /// </summary>
    /// <param name="path">a path to search for. URL Encoded</param>
    /// <returns></returns>
    [HttpGet("PathRegex/{*path}")]
    public ActionResult<List<File>> RegexSearchByPath([FromRoute] string path)
    {
        var query = path;
        if (query.Contains('%') || query.Contains('+')) query = Uri.UnescapeDataString(query);
        if (query.Contains('%')) query = Uri.UnescapeDataString(query);
        if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
        Regex regex;

        try
        {
            regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (RegexParseException e)
        {
            return ValidationProblem(e.Message, "path");
        }

        var results = RepoFactory.VideoLocalPlace.GetAll().AsParallel()
            .Where(a => regex.IsMatch(a.Path)).Select(a => a.VideoLocal)
            .Distinct()
            .Where(a =>
            {
                var ser = a?.AnimeEpisodes.FirstOrDefault()?.AnimeSeries;
                return ser == null || User.AllowedSeries(ser);
            }).Select(a => new File(HttpContext, a, true)).ToList();
        return results;
    }

    /// <summary>
    /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
    /// </summary>
    /// <param name="path">a path to search for. URL Encoded</param>
    /// <returns></returns>
    [HttpGet("FilenameRegex/{*path}")]
    public ActionResult<List<File>> RegexSearchByFileName([FromRoute] string path)
    {
        var query = path;
        if (query.Contains('%') || query.Contains('+')) query = Uri.UnescapeDataString(query);
        if (query.Contains('%')) query = Uri.UnescapeDataString(query);
        if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
        Regex regex;

        try
        {
            regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (RegexParseException e)
        {
            return ValidationProblem(e.Message, "path");
        }

        var results = RepoFactory.VideoLocalPlace.GetAll().AsParallel()
            .Where(a => regex.IsMatch(a.FileName)).Select(a => a.VideoLocal)
            .Distinct()
            .Where(a =>
            {
                var ser = a?.AnimeEpisodes.FirstOrDefault()?.AnimeSeries;
                return ser == null || User.AllowedSeries(ser);
            }).Select(a => new File(HttpContext, a, true)).ToList();
        return results;
    }
}
