using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Quartz;
using Shoko.Models.Server;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
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
            if (SkipMyList) result["Add to MyList"] = !SkipMyList;
            return result;
        }
    }

    public override async Task Process()
    {
        var resolvedFilePath = File.ResolveLinkTarget(FilePath, true)?.FullName ?? FilePath;
        if (resolvedFilePath != FilePath)
            _logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedFilePath);

        var (vlocal, vlocalplace, folder) = GetVideoLocal(resolvedFilePath);
        if (vlocal == null || vlocalplace == null) return;
        if (vlocal.HasAnyEmptyHashes())
            FillHashesAgainstVideoLocalRepo(vlocal);
        Exception? e = null;
        var filename = vlocalplace.FileName;
        var fileSize = GetFileInfo(folder, resolvedFilePath, ref e);
        if (fileSize == 0 && e != null)
        {
            _logger.LogError(e, "Could not access file. Exiting");
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        if (vlocal.FileSize == 0) vlocal.FileSize = fileSize;
        var (needED2K, needCRC32, needMD5, needSHA1) = ShouldHash(vlocal, ForceHash);
        if (!needED2K && !ForceHash) return;
        FillMissingHashes(vlocal, needED2K, needMD5, needSHA1, needCRC32);

        // We should have a hash by now
        // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)
        // TODO change this back to lookup by hash and filesize, but it'll need database migration and changes to other lookups
        var tlocal = RepoFactory.VideoLocal.GetByEd2k(vlocal.Hash);

        if (tlocal != null)
        {
            _logger.LogTrace("Found existing VideoLocal with hash, merging info from it");
            // Merge hashes and save, regardless of duplicate file
            MergeInfoFrom(tlocal, vlocal);
            vlocal = tlocal;
        }

        vlocal.FileSize = fileSize;
        vlocal.DateTimeUpdated = DateTime.Now;
        _logger.LogTrace("Saving VideoLocal: Filename: {FileName}, Hash: {Hash}", FilePath, vlocal.Hash);
        RepoFactory.VideoLocal.Save(vlocal, true);

        vlocalplace.VideoLocalID = vlocal.VideoLocalID;
        RepoFactory.VideoLocalPlace.Save(vlocalplace);

        var duplicate = await ProcessDuplicates(vlocal, vlocalplace);
        if (duplicate)
        {
            await scheduler.StartJobNow<ProcessFileJob>(c => c.VideoLocalID = vlocal.VideoLocalID);
            return;
        }

        SaveFileNameHash(filename, vlocal);

        if ((vlocal.MediaInfo?.GeneralStream?.Duration ?? 0) == 0 || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
        {
            if (_vlPlaceService.RefreshMediaInfo(vlocalplace))
            {
                RepoFactory.VideoLocal.Save(vlocal, true);
            }
        }

        ShokoEventHandler.Instance.OnFileHashed(folder, vlocalplace, vlocal);

        // now add a command to process the file
        await scheduler.StartJobNow<ProcessFileJob>(c =>
            {
                c.VideoLocalID = vlocal.VideoLocalID;
                c.ForceAniDB = ForceHash;
                c.SkipMyList = SkipMyList;
            });
    }

    private (SVR_VideoLocal, SVR_VideoLocal_Place, SVR_ImportFolder) GetVideoLocal(string resolvedFilePath)
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

        var importFolderID = folder.ImportFolderID;

        // check if we have already processed this file
        var vlocalplace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(filePath, importFolderID);
        SVR_VideoLocal? vlocal = null;
        var filename = Path.GetFileName(filePath);

        if (vlocalplace != null)
        {
            vlocal = vlocalplace.VideoLocal;
            if (vlocal != null)
            {
                _logger.LogTrace("VideoLocal record found in database: {Filename}", FilePath);

                // This will only happen with DB corruption, so just clean up the mess.
                if (vlocalplace.FullServerPath == null)
                {
                    if (vlocal.Places.Count == 1)
                    {
                        RepoFactory.VideoLocal.Delete(vlocal);
                        vlocal = null;
                    }

                    RepoFactory.VideoLocalPlace.Delete(vlocalplace);
                    vlocalplace = null;
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
                CRC32 = string.Empty,
                MD5 = string.Empty,
                SHA1 = string.Empty,
                IsIgnored = false,
                IsVariation = false
            };
        }

        if (vlocalplace == null)
        {
            _logger.LogTrace("No existing VideoLocal_Place, creating a new record");
            vlocalplace = new SVR_VideoLocal_Place
            {
                FilePath = filePath,
                ImportFolderID = importFolderID,
                ImportFolderType = folder.ImportFolderType,
            };
            if (vlocal.VideoLocalID != 0) vlocalplace.VideoLocalID = vlocal.VideoLocalID;
        }

        return (vlocal, vlocalplace, folder);
    }

    private long GetFileInfo(SVR_ImportFolder folder, string resolvedFilePath, ref Exception? e)
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

    private (bool needEd2k, bool needCRC32, bool needMD5, bool needSHA1) ShouldHash(SVR_VideoLocal vlocal, bool force)
    {
        var hasherSettings = _settingsProvider.GetSettings().Import.Hasher;
        var needED2K = string.IsNullOrEmpty(vlocal.Hash) || force;
        var needCRC32 = (string.IsNullOrEmpty(vlocal.CRC32) || force) && (hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes);
        var needMD5 = (string.IsNullOrEmpty(vlocal.MD5) || force) && (hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes);
        var needSHA1 = (string.IsNullOrEmpty(vlocal.SHA1) || force) && (hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes);
        return (needED2K, needCRC32, needMD5, needSHA1);
    }

    private void FillMissingHashes(SVR_VideoLocal vlocal, bool needED2K, bool needCRC32, bool needMD5, bool needSHA1)
    {
        var start = DateTime.Now;
        var tp = new List<string>() { "ED2K" };
        if (needSHA1) tp.Add("SHA1");
        if (needMD5) tp.Add("MD5");
        if (needCRC32) tp.Add("CRC32");

        _logger.LogTrace("Calculating missing {Types} hashes for: {Filename}", string.Join(",", tp), FilePath);
        var hashes = Hasher.CalculateHashes(
            FilePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar),
            ShokoServer.OnHashProgress,
            needCRC32,
            needMD5,
            needSHA1
        );
        var ts = DateTime.Now - start;
        _logger.LogTrace("Hashed file in {TotalSeconds:#0.00} seconds --- {Filename} ({Size})", ts.TotalSeconds,
            FilePath, Utils.FormatByteSize(vlocal.FileSize));

        if (needED2K && !string.IsNullOrEmpty(hashes.ED2K))
            vlocal.Hash = hashes.ED2K.ToUpperInvariant();
        if (needSHA1 && !string.IsNullOrEmpty(hashes.SHA1))
            vlocal.SHA1 = hashes.SHA1.ToUpperInvariant();
        if (needMD5 && !string.IsNullOrEmpty(hashes.MD5))
            vlocal.MD5 = hashes.MD5.ToUpperInvariant();
        if (needCRC32 && !string.IsNullOrEmpty(hashes.CRC32))
            vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();

        _logger.LogTrace("Hashed file {Filename} ({Size}): Hash: {Hash}, CRC: {CRC}, SHA1: {SHA1}, MD5: {MD5}", FilePath, Utils.FormatByteSize(vlocal.FileSize),
            vlocal.Hash, vlocal.CRC32, vlocal.SHA1, vlocal.MD5);
    }

    private static void FillHashesAgainstVideoLocalRepo(SVR_VideoLocal v)
    {
        if (!string.IsNullOrEmpty(v.Hash))
        {
            var n = RepoFactory.VideoLocal.GetByEd2k(v.Hash);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32) && !n.CRC32.Equals(v.CRC32)) v.CRC32 = n.CRC32.ToUpperInvariant();
                if (!string.IsNullOrEmpty(n.MD5) && !n.MD5.Equals(v.MD5)) v.MD5 = n.MD5.ToUpperInvariant();
                if (!string.IsNullOrEmpty(n.SHA1) && !n.SHA1.Equals(v.SHA1)) v.SHA1 = n.SHA1.ToUpperInvariant();

                return;
            }
        }

        if (!string.IsNullOrEmpty(v.SHA1))
        {
            var n = RepoFactory.VideoLocal.GetBySha1(v.SHA1);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32) && !n.CRC32.Equals(v.CRC32)) v.CRC32 = n.CRC32.ToUpperInvariant();
                if (!string.IsNullOrEmpty(n.MD5) && !n.MD5.Equals(v.MD5)) v.MD5 = n.MD5.ToUpperInvariant();
                if (!string.IsNullOrEmpty(n.SHA1) && !n.SHA1.Equals(v.SHA1)) v.SHA1 = n.SHA1.ToUpperInvariant();

                return;
            }
        }

        if (!string.IsNullOrEmpty(v.MD5))
        {
            var n = RepoFactory.VideoLocal.GetByMd5(v.MD5);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32) && !n.CRC32.Equals(v.CRC32)) v.CRC32 = n.CRC32.ToUpperInvariant();
                if (!string.IsNullOrEmpty(n.MD5) && !n.MD5.Equals(v.MD5)) v.MD5 = n.MD5.ToUpperInvariant();
                if (!string.IsNullOrEmpty(n.SHA1) && !n.SHA1.Equals(v.SHA1)) v.SHA1 = n.SHA1.ToUpperInvariant();
            }
        }
    }

    private static void MergeInfoFrom(SVR_VideoLocal target, SVR_VideoLocal source)
    {
        if (string.IsNullOrEmpty(target.Hash) && !string.IsNullOrEmpty(source.Hash)) target.Hash = source.Hash;
        if (string.IsNullOrEmpty(target.CRC32) && !string.IsNullOrEmpty(source.CRC32)) target.CRC32 = source.CRC32;
        if (string.IsNullOrEmpty(target.MD5) && !string.IsNullOrEmpty(source.MD5)) target.MD5 = source.MD5;
        if (string.IsNullOrEmpty(target.SHA1) && !string.IsNullOrEmpty(source.SHA1)) target.SHA1 = source.SHA1;
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

    public HashFileJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, VideoLocal_PlaceService vlPlaceService, ImportFolderRepository importFolders)
    {
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _vlPlaceService = vlPlaceService;
        _importFolders = importFolders;
    }

    protected HashFileJob() { }
}
