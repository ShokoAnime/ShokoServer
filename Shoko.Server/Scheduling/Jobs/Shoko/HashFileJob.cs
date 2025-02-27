using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Quartz;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS8618
#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[LimitConcurrency]
[JobKeyGroup(JobKeyGroup.Import)]
public class HashFileJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly IVideoHashingService _videoHashingService;

    private readonly VideoLocal_PlaceService _vlPlaceService;

    private readonly ImportFolderRepository _importFolders;

    public string FilePath { get; set; }

    public bool ForceHash { get; set; }

    public bool SkipMyList { get; set; }

    public override string TypeName => "Hash File";

    public override string Title => "Hashing File";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object> { { "File Path", Utils.GetDistinctPath(FilePath) } };
            if (ForceHash) result["Force"] = true;
            if (!SkipMyList) result["Add to MyList"] = true;
            return result;
        }
    }

    protected HashFileJob() { }

    public HashFileJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, IVideoHashingService videoHashingService, VideoLocal_PlaceService vlPlaceService, ImportFolderRepository importFolders)
    {
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _videoHashingService = videoHashingService;
        _vlPlaceService = vlPlaceService;
        _importFolders = importFolders;
    }

    public override async Task Process()
    {
        var resolvedFilePath = File.ResolveLinkTarget(FilePath, true)?.FullName ?? FilePath;
        if (resolvedFilePath != FilePath)
            _logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedFilePath);
        var (video, videoLocation, folder) = GetVideoLocal(resolvedFilePath);
        if (video == null || videoLocation == null || folder == null)
            return;
        Exception? e = null;
        var filename = videoLocation.FileName;
        var fileSize = GetFileSize(folder, resolvedFilePath, ref e);
        if (fileSize is 0 && e is not null)
        {
            _logger.LogError(e, "Could not access file. Exiting");
            return;
        }

        var existingHashes = ForceHash ? [] : video.Hashes;
        var hashes = await _videoHashingService.GetHashesForFile(new FileInfo(resolvedFilePath), existingHashes);
        var ed2k = hashes.FirstOrDefault(x => x.Type is "ED2K");
        if (ed2k is not { Type: "ED2K", Value.Length: 32 })
        {
            _logger.LogError("Could not get ED2K hash for {FilePath}", FilePath);
            return;
        }

        if (RepoFactory.VideoLocal.GetByEd2k(ed2k.Value) is { } otherVideo)
        {
            _logger.LogTrace("Found existing VideoLocal with hash");
            video = otherVideo;
        }

        // Store the hashes
        _logger.LogTrace("Saving VideoLocal: Filename: {FileName}, Hash: {Hash}", FilePath, video.Hash);
        video.Hash = ed2k.Value;
        video.FileSize = fileSize;
        video.DateTimeUpdated = DateTime.Now;
        RepoFactory.VideoLocal.Save(video, false);

        // Save the hashes
        var newHashes = hashes
            .Select(x => new VideoLocal_HashDigest()
            {
                VideoLocalID = video.VideoLocalID,
                Type = x.Type,
                Value = x.Value,
                Metadata = x.Metadata,
            })
            .ToList();

        // Re-fetch the hashes in case we changed to an existing video.
        existingHashes = video.Hashes;

        var toRemove = existingHashes.Except(newHashes).ToList();
        var toSave = newHashes.Except(existingHashes).ToList();
        RepoFactory.VideoLocalHashDigest.Save(toSave);
        RepoFactory.VideoLocalHashDigest.Delete(toRemove);

        videoLocation.VideoLocalID = video.VideoLocalID;
        RepoFactory.VideoLocalPlace.Save(videoLocation);

        var scheduler = await _schedulerFactory.GetScheduler();
        var duplicate = await ProcessDuplicates(video, videoLocation);
        if (!duplicate)
        {
            SaveFileNameHash(filename, video);

            if ((video.MediaInfo?.GeneralStream?.Duration ?? 0) == 0 || video.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
            {
                if (_vlPlaceService.RefreshMediaInfo(videoLocation))
                    RepoFactory.VideoLocal.Save(video, false);
            }
        }

        ShokoEventHandler.Instance.OnFileHashed(folder, videoLocation, video);

        // Add the process file job if we're not forcefully re-hashing the file.
        if (!ForceHash && video.ReleaseInfo is not null)
            return;

        await scheduler.StartJobNow<ProcessFileJob>(c =>
            {
                c.VideoLocalID = video.VideoLocalID;
                c.ForceRecheck = ForceHash;
                c.SkipMyList = SkipMyList;
            });
    }

    private (SVR_VideoLocal?, SVR_VideoLocal_Place?, SVR_ImportFolder?) GetVideoLocal(string resolvedFilePath)
    {
        if (!File.Exists(resolvedFilePath))
        {
            _logger.LogError("File does not exist: {Filename}", resolvedFilePath);
            return default;
        }

        // hash and read media info for file
        var (folder, filePath) = _importFolders.GetFromFullPath(FilePath);
        if (folder == null)
        {
            _logger.LogError("Unable to locate Import Folder for {FileName}", FilePath);
            return default;
        }

        // check if we have already processed this file
        var importFolderID = folder.ImportFolderID;
        var videoLocation = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(filePath, importFolderID);
        var filename = Path.GetFileName(filePath);

        SVR_VideoLocal? vlocal = null;
        if (videoLocation != null)
        {
            vlocal = videoLocation.VideoLocal;
            if (vlocal != null)
            {
                _logger.LogTrace("VideoLocal record found in database: {Filename}", FilePath);

                // This will only happen with DB corruption, so just clean up the mess.
                if (videoLocation.FullServerPath == null)
                {
                    if (vlocal.Places.Count == 1)
                    {
                        RepoFactory.VideoLocal.Delete(vlocal);
                        vlocal = null;
                    }

                    RepoFactory.VideoLocalPlace.Delete(videoLocation);
                    videoLocation = null;
                }
            }
        }

        if (vlocal == null)
        {
            _logger.LogTrace("No existing VideoLocal, creating temporary record");
            vlocal = new SVR_VideoLocal
            {
                DateTimeUpdated = DateTime.Now,
                DateTimeCreated = DateTime.Now,
                FileName = filename,
                Hash = string.Empty,
            };
        }

        if (videoLocation == null)
        {
            _logger.LogTrace("No existing VideoLocal_Place, creating a new record");
            videoLocation = new SVR_VideoLocal_Place
            {
                FilePath = filePath,
                ImportFolderID = importFolderID,
                ImportFolderType = folder.ImportFolderType,
            };
            if (vlocal.VideoLocalID != 0) videoLocation.VideoLocalID = vlocal.VideoLocalID;
        }

        return (vlocal, videoLocation, folder);
    }

    private long GetFileSize(SVR_ImportFolder folder, string resolvedFilePath, ref Exception? e)
    {
        var settings = _settingsProvider.GetSettings();
        var access = folder.IsDropSource == 1 ? FileAccess.ReadWrite : FileAccess.Read;

        if (settings.Import.FileLockChecking)
        {
            var waitTime = settings.Import.FileLockWaitTimeMS;

            waitTime = waitTime < 1000 ? 4000 : waitTime;
            settings.Import.FileLockWaitTimeMS = waitTime;
            _settingsProvider.SaveSettings();

            var policy = Policy
                .HandleResult<long>(result => result == 0)
                .Or<IOException>()
                .Or<UnauthorizedAccessException>(ex => HandleReadOnlyException(ex, resolvedFilePath != FilePath))
                .Or<Exception>(ex =>
                {
                    _logger.LogError(ex, "Could not access file: {Filename}", resolvedFilePath);
                    return false;
                })
                .WaitAndRetry(60, _ => TimeSpan.FromMilliseconds(waitTime), (_, _, count, _) =>
                {
                    _logger.LogTrace("Failed to access, (or filesize is 0) Attempt # {NumAttempts}, {FileName}", count, resolvedFilePath);
                });

            var result = policy.ExecuteAndCapture(() => GetFileSize(resolvedFilePath, access));
            if (result.Outcome == OutcomeType.Failure)
            {
                if (result.FinalException is not null)
                {
                    _logger.LogError(result.FinalException, "Could not access file: {Filename}", resolvedFilePath);
                    e = result.FinalException;
                }
                else
                    _logger.LogError("Could not access file: {Filename}", resolvedFilePath);
            }
        }

        if (File.Exists(resolvedFilePath))
        {
            try
            {
                return GetFileSize(resolvedFilePath, access);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not access file: {Filename}", resolvedFilePath);
                e = exception;
                return 0;
            }
        }

        _logger.LogError("Could not access file: {Filename}", resolvedFilePath);
        return 0;
    }

    private static long GetFileSize(string fileName, FileAccess accessType)
    {
        using var fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite);
        var size = fs.Seek(0, SeekOrigin.End);
        return size;
    }

    private bool HandleReadOnlyException(Exception ex, bool isSymbolicLink)
    {
        // If it's a symbolic link or we're running on linux or mac then abort now.
        if (isSymbolicLink || !Utils.IsRunningOnLinuxOrMac())
        {
#pragma warning disable CA2254 // Template should be a static expression
            _logger.LogError(ex, $"Failed to read read-only {(isSymbolicLink ? "symbolic link" : "file")} on {Environment.OSVersion.VersionString}: {{Filename}}", FilePath);
#pragma warning restore CA2254 // Template should be a static expression
            return false;
        }

        _logger.LogTrace("File {FileName} is Read-Only, attempting to unmark", FilePath);
        try
        {
            var info = new FileInfo(FilePath);
            if (info.IsReadOnly) info.IsReadOnly = false;
            if (!info.IsReadOnly)
                return true;
        }
        catch
        {
            // ignore, we tried
        }

        return false;
    }

    private async Task<bool> ProcessDuplicates(SVR_VideoLocal vlocal, SVR_VideoLocal_Place vlocalplace)
    {
        if (vlocal == null) return false;
        // If the VideoLocalID == 0, then it's a new file that wasn't merged after hashing, so it can't be a dupe
        if (vlocal.VideoLocalID == 0) return false;

        // remove missing files
        var preps = vlocal.Places.Where(a =>
        {
            if (string.Equals(a.FullServerPath, vlocalplace.FullServerPath)) return false;
            if (a.FullServerPath == null) return true;
            return !File.Exists(a.FullServerPath);
        }).ToList();
        RepoFactory.VideoLocalPlace.Delete(preps);

        var dupPlace = vlocal.Places.FirstOrDefault(a => !string.Equals(a.FullServerPath, vlocalplace.FullServerPath));
        if (dupPlace == null) return false;

        _logger.LogWarning("Found Duplicate File");
        _logger.LogWarning("---------------------------------------------");
        _logger.LogWarning("New File: {FullServerPath}", vlocalplace.FullServerPath);
        _logger.LogWarning("Existing File: {FullServerPath}", dupPlace.FullServerPath);
        _logger.LogWarning("---------------------------------------------");

        var settings = _settingsProvider.GetSettings();
        if (settings.Import.AutomaticallyDeleteDuplicatesOnImport) await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(vlocalplace);
        return true;
    }

    private static void SaveFileNameHash(string filename, SVR_VideoLocal vlocal)
    {
        // also save the filename to hash record
        // replace the existing records just in case it was corrupt
        var fnhashes2 = RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (fnhashes2 is { Count: > 1 })
        {
            // if we have more than one record it probably means there is some sort of corruption
            // lets delete the local records
            RepoFactory.FileNameHash.Delete(fnhashes2);
        }

        var fnhash = fnhashes2 is { Count: 1 } ? fnhashes2[0] : new FileNameHash();

        fnhash.FileName = filename;
        fnhash.FileSize = vlocal.FileSize;
        fnhash.Hash = vlocal.Hash;
        fnhash.DateTimeUpdated = DateTime.Now;
        RepoFactory.FileNameHash.Save(fnhash);
    }
}
