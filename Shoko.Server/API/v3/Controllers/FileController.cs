using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.StaticFiles;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.v3.Models.Shoko.Relocation;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AVDump = Shoko.Server.API.v3.Models.Shoko.AVDump;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using File = Shoko.Server.API.v3.Models.Shoko.File;
using MediaInfo = Shoko.Server.API.v3.Models.Shoko.MediaInfo;
using Path = System.IO.Path;

namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[Authorize]
public class FileController : BaseController
{
    private const string FileUserStatsNotFoundWithFileID = "No FileUserStats entry for the given fileID for the current user";

    private const string FileNoPath = "Unable to resolve file location.";

    private const string AnidbNotFoundForFileID = "No File.Anidb entry for the given fileID";

    internal const string FileNotFoundWithFileID = "No File entry for the given fileID";

    internal const string FileNotFoundWithHash = "No File entry for the given hash and file size.";

    internal const string FileLocationNotFoundWithLocationID = "No File.Location entry for the given locationID.";

    private readonly TraktTVHelper _traktHelper;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly VideoLocalService _vlService;
    private readonly VideoLocal_PlaceService _vlPlaceService;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly WatchedStatusService _watchedService;

    public FileController(TraktTVHelper traktHelper, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider, VideoLocal_PlaceService vlPlaceService, VideoLocal_UserRepository vlUsers, WatchedStatusService watchedService, VideoLocalService vlService) : base(settingsProvider)
    {
        _traktHelper = traktHelper;
        _vlPlaceService = vlPlaceService;
        _vlUsers = vlUsers;
        _watchedService = watchedService;
        _vlService = vlService;
        _schedulerFactory = schedulerFactory;
    }

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
            .Search(query, tuple => tuple.Places.Select(place => place?.FilePath).Where(path => path != null), fuzzy)
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
    [HttpDelete]
    public async Task<ActionResult> DeleteFiles([FromBody] File.Input.BatchDeleteBody body = null)
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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns>The file that matches the given ED2K hash and file size, if found.</returns>
    [HttpGet("Hash/ED2K")]
    public ActionResult<File> GetFileByEd2k(
        [FromQuery, Required, Length(32, 32)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetByHashAndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), includeDataFrom,
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Get a file by it's CRC32 hash and file size.
    /// </summary>
    /// <param name="hash">CRC32 hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns>The file that matches the given CRC32 hash and file size, if found.</returns>
    [HttpGet("Hash/CRC32")]
    public ActionResult<File> GetFileByCrc32(
        [FromQuery, Required, Length(8, 8)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetByCRC32AndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), includeDataFrom,
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Get a file by it's MD5 hash and file size.
    /// </summary>
    /// <param name="hash">MD5 hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns>The file that matches the given MD5 hash and file size, if found.</returns>
    [HttpGet("Hash/MD5")]
    public ActionResult<File> GetFileByMd5(
        [FromQuery, Required, Length(32, 32)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetByMD5AndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), includeDataFrom,
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    /// <summary>
    /// Get a file by it's SHA1 hash and file size.
    /// </summary>
    /// <param name="hash">SHA1 hex-encoded hash.</param>
    /// <param name="size">File size.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns>The file that matches the given SHA1 hash and file size, if found.</returns>
    [HttpGet("Hash/SHA1")]
    public ActionResult<File> GetFileBySha1(
        [FromQuery, Required, Length(40, 40)] string hash,
        [FromQuery, Required, Range(0L, long.MaxValue)] long size,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        if (string.IsNullOrEmpty(hash) || size <= 0)
            return NotFound(FileNotFoundWithHash);

        var file = RepoFactory.VideoLocal.GetBySHA1AndSize(hash, size);
        if (file == null)
            return NotFound(FileNotFoundWithHash);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), includeDataFrom,
            include.Contains(FileNonDefaultIncludeType.MediaInfo), include.Contains(FileNonDefaultIncludeType.AbsolutePaths));
    }

    #endregion

    /// <summary>
    /// Get File Details
    /// </summary>
    /// <param name="fileID">Shoko VideoLocalID</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{fileID}")]
    public ActionResult<File> GetFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        include ??= [];
        return new File(HttpContext, file, include.Contains(FileNonDefaultIncludeType.XRefs), includeDataFrom,
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
    /// Retrieves information about a specific file location.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be retrieved.
    /// </param>
    /// <returns>Returns the file location information.</returns>
    [HttpGet("Location/{locationID}")]
    public ActionResult<File.Location> GetFileLocation([FromRoute, Range(1, int.MaxValue)] int locationID)
    {
        var fileLocation = RepoFactory.VideoLocalPlace.GetByID(locationID);
        if (fileLocation == null)
            return NotFound(FileLocationNotFoundWithLocationID);

        return new File.Location(fileLocation, true);
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
    /// Get the <see cref="File.AniDB"/> using the <paramref name="fileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="fileID">Shoko File ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/AniDB")]
    public ActionResult<File.AniDB> GetFileAnidbByFileID([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var anidb = file.AniDBFile;
        if (anidb == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/> using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}")]
    public ActionResult<File.AniDB> GetFileAnidbByAnidbFileID([FromRoute, Range(1, int.MaxValue)] int anidbFileID)
    {
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/>for file using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}/File")]
    public ActionResult<File> GetFileByAnidbFileID([FromRoute, Range(1, int.MaxValue)] int anidbFileID, [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false, [FromQuery] bool includeAbsolutePaths = false)
    {
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(FileNotFoundWithFileID);

        var file = RepoFactory.VideoLocal.GetByHash(anidb.Hash);
        if (file == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo, includeAbsolutePaths);
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
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(FileNotFoundWithFileID);

        var file = RepoFactory.VideoLocal.GetByHash(anidb.Hash);
        if (file == null)
            return NotFound(AnidbNotFoundForFileID);

        var filePath = file.FirstResolvedPlace?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        var scheduler = await _schedulerFactory.GetScheduler();
        if (priority)
            await scheduler.StartJobNow<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        else
            await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        return Ok();
    }

    /// <summary>
    /// Returns a file stream for the specified file ID.
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <param name="filename">Can use this to select a specific place (if the name is different). This is mostly used as a hint for players</param>
    /// <param name="streamPositionScrobbling">If this is enabled, then the file is marked as watched when the stream reaches the end.
    /// This is not a good way to scrobble, but it allows for players without plugin support to have an option to scrobble.
    /// The readahead buffer on the player would determine the required percentage to scrobble.</param>
    /// <returns>A file stream for the specified file.</returns>
    [AllowAnonymous]
    [HttpGet("{fileID}/Stream")]
    [HttpHead("{fileID}/Stream")]
    [HttpGet("{fileID}/StreamDirectory/{filename}")]
    [HttpHead("{fileID}/StreamDirectory/{filename}")]
    public ActionResult GetFileStream([FromRoute, Range(1, int.MaxValue)] int fileID, [FromRoute] string filename = null, [FromQuery] bool streamPositionScrobbling = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var bestLocation = file.Places.FirstOrDefault(a => a.FileName.Equals(filename));
        bestLocation ??= file.FirstValidPlace;

        var fileInfo = bestLocation.GetFile();
        if (fileInfo == null)
            return InternalError("Unable to find physical file for reading the stream data.");

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileInfo.FullName, out var contentType))
            contentType = "application/octet-stream";

        if (streamPositionScrobbling)
        {
            var scrobbleFile = new ScrobblingFileResult(file, User.JMMUserID, fileInfo.FullName, contentType)
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
            var path = place.GetFile()?.Directory?.FullName;
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
    public ActionResult<MediaInfo> GetFileMediaInfo([FromRoute, Range(1, int.MaxValue)] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var mediaContainer = file.MediaInfo;
        if (mediaContainer == null)
            return InternalError("Unable to find media container for File");

        return new MediaInfo(file, mediaContainer);
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

        await _watchedService.SetWatchedStatus(file, watched, User.JMMUserID);

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
    [HttpPatch("{fileID}/Scrobble")]
    public async Task<ActionResult> ScrobbleFileAndEpisode([FromRoute, Range(1, int.MaxValue)] int fileID, [FromQuery(Name = "event")] string eventName = null, [FromQuery] int? episodeID = null, [FromQuery] bool? watched = null, [FromQuery] long? resumePosition = null)
    {

        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Handle legacy scrobble events.
        if (string.IsNullOrEmpty(eventName))
        {
            return await ScrobbleStatusOnFile(file, watched, resumePosition);
        }

        var episode = episodeID.HasValue ? RepoFactory.AnimeEpisode.GetByID(episodeID.Value) : file.AnimeEpisodes?.FirstOrDefault();
        if (episode == null)
            return ValidationProblem($"Could not get Episode with ID: {episodeID}", nameof(episodeID));

        var playbackPositionTicks = resumePosition ?? 0;
        if (playbackPositionTicks >= file.Duration)
        {
            watched = true;
            playbackPositionTicks = 0;
        }

        switch (eventName)
        {
            // The playback was started.
            case "play":
            // The playback was resumed after a pause.
            case "resume":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Start);
                break;
            // The playback was paused.
            case "pause":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Pause);
                break;
            // The playback was ended.
            case "stop":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Stop);
                break;
            // The playback is still active, but the playback position changed.
            case "scrobble":
                break;
            // A user interaction caused the watch state to change.
            case "user-interaction":
                break;
        }

        if (watched.HasValue)
            await _watchedService.SetWatchedStatus(file, watched.Value, User.JMMUserID);
        _watchedService.SetResumePosition(file, playbackPositionTicks, User.JMMUserID);

        return NoContent();
    }

    [NonAction]
    private void ScrobbleToTrakt(SVR_VideoLocal file, SVR_AnimeEpisode episode, long position, ScrobblePlayingStatus status)
    {
        if (User.IsTraktUser == 0)
            return;

        var percentage = 100 * ((float)position / file.Duration);
        var scrobbleType = episode.AnimeSeries?.AniDB_Anime?.AnimeType == (int)AnimeType.Movie
            ? ScrobblePlayingType.movie
            : ScrobblePlayingType.episode;

        _traktHelper.Scrobble(scrobbleType, episode.AnimeEpisodeID.ToString(), status, percentage);
    }

    [NonAction]
    private async Task<OkResult> ScrobbleStatusOnFile(SVR_VideoLocal file, bool? watched, long? resumePosition)
    {
        if (!(watched ?? false) && resumePosition != null)
        {
            var safeRP = resumePosition.Value;
            if (safeRP < 0) safeRP = 0;

            if (safeRP >= file.Duration)
                watched = true;
            else
                _watchedService.SetResumePosition(file, safeRP, User.JMMUserID);
        }

        if (watched != null)
        {
            var safeWatched = watched.Value;
            await _watchedService.SetWatchedStatus(file, safeWatched, User.JMMUserID);
            if (safeWatched)
                _watchedService.SetResumePosition(file, 0, User.JMMUserID);

        }

        return Ok();
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

        var filePath = file.FirstResolvedPlace?.FullServerPath;
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
            if (priority)
                await scheduler.StartJobNow<AVDumpFilesJob>(a => a.Videos = files);
            else
                await scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = files);
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

        var filePath = file.FirstResolvedPlace?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        var scheduler = await _schedulerFactory.GetScheduler();
        if (priority)
            await scheduler.StartJobNow<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        else
            await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
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

        var filePath = file.FirstResolvedPlace?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        var scheduler = await _schedulerFactory.GetScheduler();
        if (priority)
            await scheduler.StartJobNow<HashFileJob>(c =>
                {
                    c.FilePath = filePath;
                    c.ForceHash = true;
                }
            );
        else
            await scheduler.StartJob<HashFileJob>(c =>
                {
                    c.FilePath = filePath;
                    c.ForceHash = true;
                }
            );

        return Ok();
    }

    /// <summary>
    /// Link one or more episodes to the same file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Link")]
    public async Task<ActionResult> LinkSingleEpisodeToFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody] File.Input.LinkEpisodesBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Validate that we can manually link this file.
        CheckXRefsForFile(file, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the episodes.
        var episodeList = body.EpisodeIDs
            .Select(episodeID =>
            {
                var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (episode == null)
                    ModelState.AddModelError(nameof(body.EpisodeIDs), $"Unable to find shoko episode with id {episodeID}");
                return episode;
            })
            .Where(episode => episode != null)
            .ToList();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove any old links and schedule the linking commands.
        RemoveXRefsForFile(file);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var episode in episodeList)
        {
            await scheduler.StartJobNow<ManualLinkJob>(c =>
                {
                    c.VideoLocalID = fileID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                }
            );
        }

        return Ok();
    }

    /// <summary>
    /// Link one or more episodes from a series to the same file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/LinkFromSeries")]
    public async Task<ActionResult> LinkMultipleEpisodesToFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody] File.Input.LinkSeriesBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Validate that we can manually link this file.
        CheckXRefsForFile(file, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate that the ranges are in a valid syntax and that the series exists.
        var series = RepoFactory.AnimeSeries.GetByID(body.SeriesID);
        if (series == null)
            ModelState.AddModelError(nameof(body.SeriesID), $"Unable to find series with id {body.SeriesID}.");

        var (rangeStart, startType, startErrorMessage) = ModelHelper.GetEpisodeNumberAndTypeFromInput(body.RangeStart);
        if (!string.IsNullOrEmpty(startErrorMessage))
            ModelState.AddModelError(nameof(body.RangeStart), string.Format(startErrorMessage, nameof(body.RangeStart)));

        var (rangeEnd, endType, endErrorMessage) = ModelHelper.GetEpisodeNumberAndTypeFromInput(body.RangeEnd);
        if (!string.IsNullOrEmpty(endErrorMessage))
            ModelState.AddModelError(nameof(body.RangeEnd), string.Format(endErrorMessage, nameof(body.RangeEnd)));

        if (startType != endType)
            ModelState.AddModelError(nameof(body.RangeEnd), "Unable to use different episode types in the `RangeStart` and `RangeEnd`.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate that the ranges are valid for the series.
        var episodeType = startType ?? EpisodeType.Episode;
        var totalEpisodes = ModelHelper.GetTotalEpisodesForType(series!.AllAnimeEpisodes, episodeType);
        if (rangeStart < 1)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be lower then 1.");

        if (rangeStart > totalEpisodes)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be higher then the total number of episodes for the selected type.");

        if (rangeEnd < rangeStart)
            ModelState.AddModelError(nameof(body.RangeEnd), "`RangeEnd`cannot be lower then `RangeStart`.");

        if (rangeEnd > totalEpisodes)
            ModelState.AddModelError(nameof(body.RangeEnd), "`RangeEnd` cannot be higher than the total number of episodes for the selected type.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the episodes.
        var episodeList = new List<SVR_AnimeEpisode>();
        for (var episodeNumber = rangeStart; episodeNumber <= rangeEnd; episodeNumber++)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, episodeType, episodeNumber)[0];
            if (anidbEpisode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the AniDB entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidbEpisode.EpisodeID);
            if (episode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the Shoko entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            episodeList.Add(episode);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove any old links and schedule the linking commands.
        RemoveXRefsForFile(file);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var episode in episodeList)
        {
            await scheduler.StartJobNow<ManualLinkJob>(c =>
                {
                    c.VideoLocalID = fileID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                }
            );
        }

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
        await scheduler.StartJobNow<AddFileToMyListJob>(c => c.Hash = file.Hash);

        return Ok();
    }

    /// <summary>
    /// Unlink all the episodes if no body is given, or only the spesified episodes from the file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">Optional. The body.</param>
    /// <returns></returns>
    [HttpDelete("{fileID}/Link")]
    public async Task<ActionResult> UnlinkMultipleEpisodesFromFile([FromRoute, Range(1, int.MaxValue)] int fileID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] File.Input.UnlinkEpisodesBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Validate that the cross-references are allowed to be removed.
        var all = body == null;
        var episodeIdSet = body?.EpisodeIDs?.ToHashSet() ?? [];
        var seriesIDs = new HashSet<int>();
        var episodeList = file.AnimeEpisodes
            .Where(episode => all || episodeIdSet.Contains(episode.AniDB_EpisodeID))
            .Select(episode => (Episode: episode, XRef: RepoFactory.CrossRef_File_Episode.GetByHashAndEpisodeID(file.Hash, episode.AniDB_EpisodeID)))
            .Where(obj => obj.XRef != null)
            .ToList();
        foreach (var (_, xref) in episodeList)
            if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                ModelState.AddModelError("CrossReferences", $"Unable to remove AniDB cross-reference to anidb episode with id {xref.EpisodeID} for file with id {file.VideoLocalID}.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove the cross-references, and take note of the series ids that
        // needs to be updated later.
        foreach (var (episode, xref) in episodeList)
        {
            seriesIDs.Add(episode.AnimeSeriesID);
            RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
        }

        // Reset the import date.
        if (file.DateTimeImported.HasValue)
        {
            file.DateTimeImported = null;
            RepoFactory.VideoLocal.Save(file);
        }

        // Update any series affected by this unlinking.
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var seriesID in seriesIDs)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            await scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = series.AniDB_ID);
        }

        return Ok();
    }

    /// <summary>
    /// Link multiple files to one or more episodes in a series.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("LinkFromSeries")]
    public async Task<ActionResult> LinkMultipleFiles([FromBody] File.Input.LinkSeriesMultipleBody body)
    {
        // Validate the file ids, series ids, and the range syntax.
        var files = body.FileIDs
            .Select(fileID =>
            {
                var file = RepoFactory.VideoLocal.GetByID(fileID);
                if (file == null)
                    ModelState.AddModelError(nameof(body.FileIDs), $"Unable to find a file with id {fileID}.");
                else
                    CheckXRefsForFile(file, ModelState);

                return file;
            })
            .Where(file => file != null)
            .ToList();
        if (body.FileIDs.Length == 0)
            ModelState.AddModelError(nameof(body.FileIDs), "`FileIDs` must contain at least one element.");

        var series = RepoFactory.AnimeSeries.GetByID(body.SeriesID);
        if (series == null)
            ModelState.AddModelError(nameof(body.SeriesID), $"Unable to find series with id {body.SeriesID}.");

        var (rangeStart, startType, startErrorMessage) = ModelHelper.GetEpisodeNumberAndTypeFromInput(body.RangeStart);
        if (!string.IsNullOrEmpty(startErrorMessage))
            ModelState.AddModelError(nameof(body.RangeStart), string.Format(startErrorMessage, nameof(body.RangeStart)));

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the range.
        var episodeType = startType ?? EpisodeType.Episode;
        var rangeEnd = rangeStart + files.Count - 1;
        var totalEpisodes = ModelHelper.GetTotalEpisodesForType(series!.AllAnimeEpisodes, episodeType);
        if (rangeStart < 1)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be lower then 1.");

        if (rangeStart > totalEpisodes)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be higher then the total number of episodes for the selected type.");

        if (rangeEnd < rangeStart)
            ModelState.AddModelError("RangeEnd", "`RangeEnd`cannot be lower then `RangeStart`.");

        if (rangeEnd > totalEpisodes)
            ModelState.AddModelError("RangeEnd", "`RangeEnd` cannot be higher than the total number of episodes for the selected type.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the episodes.
        var singleEpisode = body.SingleEpisode;
        var episodeNumber = rangeStart;
        var episodeList = new List<(SVR_VideoLocal, SVR_AnimeEpisode)>();
        foreach (var file in files)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, episodeType, episodeNumber)[0];
            if (anidbEpisode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the AniDB entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidbEpisode.EpisodeID);
            if (episode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the Shoko entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            episodeList.Add((file, episode));
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove any old links and schedule the linking commands.
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var (file, episode) in episodeList)
        {
            RemoveXRefsForFile(file);

            await scheduler.StartJobNow<ManualLinkJob>(c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                    c.Percentage = singleEpisode ? (int)Math.Round(1D / files.Count * 100) : 0;
                }
            );
        }

        return Ok();
    }

    /// <summary>
    /// Link multiple files to a single episode.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("Link")]
    public async Task<ActionResult> LinkMultipleFiles([FromBody] File.Input.LinkMultipleFilesBody body)
    {
        // Validate the file ids and episode id.
        var files = body.FileIDs
            .Select(fileID =>
            {
                var file = RepoFactory.VideoLocal.GetByID(fileID);
                if (file == null)
                    ModelState.AddModelError(nameof(body.FileIDs), $"Unable to find a file with id {fileID}.");
                else
                    CheckXRefsForFile(file, ModelState);

                return file;
            })
            .Where(file => file != null)
            .ToList();
        if (body.FileIDs.Length == 0)
            ModelState.AddModelError(nameof(body.FileIDs), "`FileIDs` must contain at least one element.");

        var episode = RepoFactory.AnimeEpisode.GetByID(body.EpisodeID);
        if (episode == null)
            ModelState.AddModelError(nameof(body.EpisodeID), $"Unable to find episode with id {body.EpisodeID}.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var anidbEpisode = episode!.AniDB_Episode;
        if (anidbEpisode == null)
            return InternalError("Could not find the AniDB entry for episode");

        // Remove any old links and schedule the linking commands.
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var file in files)
        {
            RemoveXRefsForFile(file);

            await scheduler.StartJobNow<ManualLinkJob>(c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                    c.Percentage = (int)Math.Round(1D / files.Count * 100);
                }
            );
        }

        return Ok();
    }

    [NonAction]
    private static void RemoveXRefsForFile(SVR_VideoLocal file)
    {
        foreach (var xref in RepoFactory.CrossRef_File_Episode.GetByHash(file.Hash))
        {
            if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                return;

            RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
        }

        // Reset the import date.
        if (file.DateTimeImported.HasValue)
        {
            file.DateTimeImported = null;
            RepoFactory.VideoLocal.Save(file);
        }
    }

    [NonAction]
    private static void CheckXRefsForFile(SVR_VideoLocal file, ModelStateDictionary modelState)
    {
        foreach (var xref in RepoFactory.CrossRef_File_Episode.GetByHash(file.Hash))
            if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                modelState.AddModelError("CrossReferences", $"Unable to remove AniDB cross-reference to anidb episode with id {xref.EpisodeID} for file with id {file.VideoLocalID}.");
    }

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith")]
    public ActionResult<List<File>> PathEndsWithQuery([FromQuery] string path, [FromQuery] bool includeXRefs = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(path, includeXRefs, includeDataFrom, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for. URL encoded.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith/{*path}")]
    public ActionResult<List<File>> PathEndsWithPath([FromRoute] string path, [FromQuery] bool includeXRefs = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(Uri.UnescapeDataString(path), includeXRefs, includeDataFrom, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [NonAction]
    private ActionResult<List<File>> PathEndsWithInternal(string path, bool includeXRefs,
        HashSet<DataSource> includeDataFrom, int limit = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<File>();

        var query = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var results = RepoFactory.VideoLocalPlace.GetAll()
            .AsParallel()
            .Where(location => location.FullServerPath?.EndsWith(query, StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(location => location.VideoLocal)
            .Where(file =>
            {
                if (file == null)
                    return false;

                var xrefs = file.EpisodeCrossRefs;
                var series = xrefs.Count > 0 ? RepoFactory.AnimeSeries.GetByAnimeID(xrefs[0].AnimeID) : null;
                return series == null || User.AllowedSeries(series);
            })
            .DistinctBy(file => file.VideoLocalID);

        if (limit <= 0)
            return results
                .Select(a => new File(HttpContext, a, includeXRefs, includeDataFrom))
                .ToList();

        return results
            .Take(limit)
            .Select(a => new File(HttpContext, a, includeXRefs, includeDataFrom))
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
            .Where(a => regex.IsMatch(a.FullServerPath)).Select(a => a.VideoLocal)
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

    /// <summary>
    /// Get all files with missing cross-references data.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [HttpGet("MissingCrossReferenceData")]
    public ActionResult<ListResult<File>> GetFilesWithMissingCrossReferenceData(
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.VideoLocal.GetVideosWithMissingCrossReferenceData()
            .ToListResult(
                file => new File(HttpContext, file, includeXRefs),
                page,
                pageSize
            );
    }
}
