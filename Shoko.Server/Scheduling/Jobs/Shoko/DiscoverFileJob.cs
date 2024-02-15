using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class DiscoverFileJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly VideoLocal_PlaceService _vlPlaceService;

    public string FileName { get; set; }
    public bool SkipMyList { get; set; }

    public override string Name => "Discover File";
    public override QueueStateStruct Description => new()
    {
        message = "Checking File for Hashes: {0}",
        extraParams = new[] { FileName },
        queueState = QueueStateEnum.CheckingFile
    };

    public override async Task Process()
    {
        // The flow has changed.
        // Check for previous existence, merge info if needed
        // If it's a new file or info is missing, queue a hash
        // HashFileJob will create the records for a new file, so don't save an empty record.
        _logger.LogInformation("Checking File For Hashes: {Filename}", FileName);

        var (shouldSave, vlocal, vlocalplace) = GetVideoLocal();
        if (vlocal == null || vlocalplace == null)
        {
            _logger.LogWarning("Could not get or create VideoLocal. exiting");
            return;
        }

        var filename = vlocalplace.FileName;

        _logger.LogTrace("No existing hash in VideoLocal (or forced), checking XRefs");
        if (vlocal.HasAnyEmptyHashes())
        {
            // try getting the hash from the CrossRef
            if (TrySetHashFromXrefs(filename, vlocal))
            {
                shouldSave = true;
                _logger.LogTrace("Found Hash in CrossRef_File_Episode: {Hash}", vlocal.Hash);
            }
            else if (TrySetHashFromFileNameHash(filename, vlocal))
            {
                shouldSave = true;
                _logger.LogTrace("Found Hash in FileNameHash: {Hash}", vlocal.Hash);
            }

            if (string.IsNullOrEmpty(vlocal.Hash) || string.IsNullOrEmpty(vlocal.CRC32) || string.IsNullOrEmpty(vlocal.MD5) ||
                string.IsNullOrEmpty(vlocal.SHA1))
                shouldSave |= FillHashesAgainstVideoLocalRepo(vlocal);
        }

        var (needEd2k, needCRC32, needMD5, needSHA1) = ShouldHash(vlocal);
        var shouldHash = needEd2k || needCRC32 || needMD5 || needSHA1;
        var scheduler = await _schedulerFactory.GetScheduler();
        if (!shouldHash && !shouldSave)
        {
            var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(vlocal.Hash);
            if (xrefs.Count != 0)
            {
                _logger.LogTrace("Hashes were not necessary for file, so exiting: {File}, Hash: {Hash}", FileName, vlocal.Hash);
                return;
            }

            _logger.LogTrace("Hashes were found, but xrefs are missing. Queuing a rescan for: {File}, Hash: {Hash}", FileName, vlocal.Hash);
            await scheduler.StartJobNow<ProcessFileJob>(a => a.VideoLocalID = vlocal.VideoLocalID);
            return;
        }

        if (shouldSave)
        {
            _logger.LogTrace("Saving VideoLocal: Filename: {FileName}, Hash: {Hash}", FileName, vlocal.Hash);
            RepoFactory.VideoLocal.Save(vlocal, true);

            _logger.LogTrace("Saving VideoLocal_Place: Path: {Path}", FileName);
            vlocalplace.VideoLocalID = vlocal.VideoLocalID;
            RepoFactory.VideoLocalPlace.Save(vlocalplace);
            
            var duplicateRemoved = await ProcessDuplicates(vlocal, vlocalplace);
            // it was removed. Don't try to hash
            if (duplicateRemoved) return;
        }

        if (shouldHash)
        {
            await scheduler.StartJobNow<HashFileJob>(a =>
            {
                a.FileName = FileName;
                a.SkipMyList = SkipMyList;
            });
        }
    }

    private (bool existing, SVR_VideoLocal, SVR_VideoLocal_Place) GetVideoLocal()
    {
        // hash and read media info for file
        var (folder, filePath) = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
        if (folder == null)
        {
            _logger.LogError("Unable to locate Import Folder for {FileName}", FileName);
            return default;
        }

        var existing = false;

        if (!File.Exists(FileName))
        {
            _logger.LogError("File does not exist: {Filename}", FileName);
            return default;
        }

        var nshareID = folder.ImportFolderID;

        // check if we have already processed this file
        var vlocalplace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(filePath, nshareID);
        SVR_VideoLocal vlocal = null;
        var filename = Path.GetFileName(filePath);

        if (vlocalplace != null)
        {
            vlocal = vlocalplace.VideoLocal;
            if (vlocal != null)
            {
                existing = true;
                _logger.LogTrace("VideoLocal record found in database: {Filename}", FileName);

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
                FilePath = filePath, ImportFolderID = nshareID, ImportFolderType = folder.ImportFolderType
            };
            // Make sure we have an ID
            RepoFactory.VideoLocalPlace.Save(vlocalplace);
        }
        
        return (existing, vlocal, vlocalplace);
    }
    
    private bool TrySetHashFromXrefs(string filename, SVR_VideoLocal vlocal)
    {
        var crossRefs =
            RepoFactory.CrossRef_File_Episode.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (!crossRefs.Any()) return false;

        vlocal.Hash = crossRefs[0].Hash;
        vlocal.HashSource = (int)HashSource.DirectHash;
        _logger.LogTrace("Got hash from xrefs: {Filename} ({Hash})", FileName, crossRefs[0].Hash);
        return true;
    }

    private bool TrySetHashFromFileNameHash(string filename, SVR_VideoLocal vlocal)
    {
        // TODO support reading MD5 and SHA1 from files via the standard way
        var fnhashes = RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (fnhashes is { Count: > 1 })
        {
            // if we have more than one record it probably means there is some sort of corruption
            // lets delete the local records
            foreach (var fnh in fnhashes)
            {
                RepoFactory.FileNameHash.Delete(fnh.FileNameHashID);
            }
        }

        // reinit this to check if we erased them
        fnhashes = RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);

        if (fnhashes is not { Count: 1 }) return false;

        _logger.LogTrace("Got hash from LOCAL cache: {Filename} ({Hash})", FileName, fnhashes[0].Hash);
        vlocal.Hash = fnhashes[0].Hash;
        vlocal.HashSource = (int)HashSource.WebCacheFileName;
        return true;
    }
    
    private static bool FillHashesAgainstVideoLocalRepo(SVR_VideoLocal v)
    {
        var changed = false;
        if (!string.IsNullOrEmpty(v.ED2KHash))
        {
            var n = RepoFactory.VideoLocal.GetByHash(v.ED2KHash);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32) && !n.CRC32.Equals(v.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                    changed = true;
                }

                if (!string.IsNullOrEmpty(n.MD5) && !n.MD5.Equals(v.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                    changed = true;
                }

                if (!string.IsNullOrEmpty(n.SHA1) && !n.SHA1.Equals(v.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                    changed = true;
                }

                return changed;
            }
        }

        if (!string.IsNullOrEmpty(v.SHA1))
        {
            var n = RepoFactory.VideoLocal.GetBySHA1(v.SHA1);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32) && !n.CRC32.Equals(v.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                    changed = true;
                }

                if (!string.IsNullOrEmpty(n.MD5) && !n.MD5.Equals(v.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                    changed = true;
                }

                if (!string.IsNullOrEmpty(n.SHA1) && !n.SHA1.Equals(v.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                    changed = true;
                }

                return changed;
            }
        }

        if (!string.IsNullOrEmpty(v.MD5))
        {
            var n = RepoFactory.VideoLocal.GetByMD5(v.MD5);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32) && !n.CRC32.Equals(v.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                    changed = true;
                }

                if (!string.IsNullOrEmpty(n.MD5) && !n.MD5.Equals(v.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                    changed = true;
                }

                if (!string.IsNullOrEmpty(n.SHA1) && !n.SHA1.Equals(v.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                    changed = true;
                }

                return changed;
            }
        }

        return false;
    }
    
    private (bool needEd2k, bool needCRC32, bool needMD5, bool needSHA1) ShouldHash(SVR_VideoLocal vlocal)
    {
        var hasherSettings = _settingsProvider.GetSettings().Import.Hasher;
        var needEd2k = string.IsNullOrEmpty(vlocal.Hash);
        var needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes;
        var needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes;
        var needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes;
        if (needCRC32 || needMD5 || needSHA1) FillHashesAgainstVideoLocalRepo(vlocal);

        needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes;
        needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes;
        needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes;
        return (needEd2k, needCRC32, needMD5, needSHA1);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vlocal"></param>
    /// <param name="vlocalplace"></param>
    /// <returns>true if the file was removed</returns>
    private async Task<bool> ProcessDuplicates(SVR_VideoLocal vlocal, SVR_VideoLocal_Place vlocalplace)
    {
        if (vlocal == null) return false;
        // If the VideoLocalID == 0, then it's a new file that wasn't merged after hashing, so it can't be a dupe
        if (vlocal.VideoLocalID == 0) return false;

        var preps = vlocal.Places.Where(a =>
        {
            if (vlocalplace.FullServerPath.Equals(a.FullServerPath)) return false;
            if (a.FullServerPath == null) return true;
            return !File.Exists(a.FullServerPath);
        }).ToList();
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            using var transaction = session.BeginTransaction();
            await RepoFactory.VideoLocalPlace.DeleteWithOpenTransactionAsync(session, preps);
            await transaction.CommitAsync();
        }

        var dupPlace = vlocal.Places.FirstOrDefault(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath));
        if (dupPlace == null) return false;

        _logger.LogWarning("Found Duplicate File");
        _logger.LogWarning("---------------------------------------------");
        _logger.LogWarning("New File: {FullServerPath}", vlocalplace.FullServerPath);
        _logger.LogWarning("Existing File: {FullServerPath}", dupPlace.FullServerPath);
        _logger.LogWarning("---------------------------------------------");

        var settings = _settingsProvider.GetSettings();
        if (settings.Import.AutomaticallyDeleteDuplicatesOnImport)
        {
            await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(vlocalplace);
            return true;
        }
        return false;
    }

    public DiscoverFileJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, VideoLocal_PlaceService vlPlaceService)
    {
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _vlPlaceService = vlPlaceService;
    }

    protected DiscoverFileJob() { }
}
