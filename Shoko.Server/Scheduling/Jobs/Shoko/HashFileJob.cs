using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[LimitConcurrency]
public class HashFileJob : BaseJob
{
    public virtual string FileName { get; set; }
    public virtual bool ForceHash { get; set; }
    public virtual bool SkipMyList { get; set; }

    public override QueueStateStruct Description => new()
    {
        message = "Hashing File: {0}",
        extraParams = new[] { FileName },
        queueState = QueueStateEnum.HashingFile
    };

    private readonly ISettingsProvider _settingsProvider;
    private readonly ICommandRequestFactory _commandFactory;

    public HashFileJob(ILoggerFactory loggerFactory, ISettingsProvider settingsProvider, ICommandRequestFactory commandFactory) : base(loggerFactory)
    {
        _settingsProvider = settingsProvider;
        _commandFactory = commandFactory;
    }

    protected HashFileJob() {
    }

    protected override async Task Process(IJobExecutionContext context)
    {
        var (shouldSave, vlocal, vlocalplace, folder) = GetVideoLocal();
        Exception e = null;
        var filename = vlocalplace.FileName;
        var fileSize = GetFileInfo(folder, ref e);
        if (fileSize == 0 && e != null)
        {
            Logger.LogError(e, "Could not access file. Exiting");
            return;
        }

        var (newFile, needEd2K, needCRC32, needMD5, needSHA1) = ShouldHash(vlocal);
        if (!needEd2K && !ForceHash) return;
        shouldSave |= !newFile;
        FillMissingHashes(vlocal, needMD5, needSHA1, needCRC32, ForceHash);

        // We should have a hash by now
        // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)
        // TODO change this back to lookup by hash and filesize, but it'll need database migration and changes to other lookups
        var tlocal = RepoFactory.VideoLocal.GetByHash(vlocal.Hash);

        if (tlocal != null)
        {
            Logger.LogTrace("Found existing VideoLocal with hash, merging info from it");
            // Merge hashes and save, regardless of duplicate file
            shouldSave |= MergeInfoFrom(tlocal, vlocal);
            vlocal = tlocal;
        }

        if (shouldSave)
        {
            Logger.LogTrace("Saving VideoLocal: Filename: {FileName}, Hash: {Hash}", FileName, vlocal.Hash);
            RepoFactory.VideoLocal.Save(vlocal, true);
        }

        vlocalplace.VideoLocalID = vlocal.VideoLocalID;
        RepoFactory.VideoLocalPlace.Save(vlocalplace);

        var duplicate = await ProcessDuplicates(vlocal, vlocalplace);
        if (duplicate)
        {
            var crProcfile3 = _commandFactory.Create<CommandRequest_ProcessFile>(
                c =>
                {
                    c.VideoLocalID = vlocal.VideoLocalID;
                    c.ForceAniDB = false;
                }
            );
            _commandFactory.Save(crProcfile3);
            return;
        }

        SaveFileNameHash(filename, vlocal);

        if ((vlocal.Media?.GeneralStream?.Duration ?? 0) == 0 || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
        {
            if (vlocalplace.RefreshMediaInfo())
            {
                RepoFactory.VideoLocal.Save(vlocalplace.VideoLocal, true);
            }
        }

        ShokoEventHandler.Instance.OnFileHashed(folder, vlocalplace);

        // now add a command to process the file
        _commandFactory.CreateAndSave<CommandRequest_ProcessFile>(c =>
            {
                c.VideoLocalID = vlocal.VideoLocalID;
                c.ForceAniDB = ForceHash;
                c.SkipMyList = SkipMyList;
            });
    }
    
    private (bool existing, SVR_VideoLocal, SVR_VideoLocal_Place, SVR_ImportFolder) GetVideoLocal()
    {
        // hash and read media info for file
        var (folder, filePath) = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
        if (folder == null)
        {
            Logger.LogError("Unable to locate Import Folder for {FileName}", FileName);
            return default;
        }

        var existing = false;

        if (!File.Exists(FileName))
        {
            Logger.LogError("File does not exist: {Filename}", FileName);
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
                Logger.LogTrace("VideoLocal record found in database: {Filename}", FileName);

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

                if (vlocal != null && ForceHash)
                {
                    vlocal.DateTimeUpdated = DateTime.Now;
                }
            }
        }

        if (vlocal == null)
        {
            Logger.LogTrace("No existing VideoLocal, creating temporary record");
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
            Logger.LogTrace("No existing VideoLocal_Place, creating a new record");
            vlocalplace = new SVR_VideoLocal_Place
            {
                FilePath = filePath, ImportFolderID = nshareID, ImportFolderType = folder.ImportFolderType
            };
            // Make sure we have an ID
            RepoFactory.VideoLocalPlace.Save(vlocalplace);
        }
        
        return (existing, vlocal, vlocalplace, folder);
    }

    private long GetFileInfo(SVR_ImportFolder folder, ref Exception e)
    {
        // TODO it is bad to spin wait in Quartz, as it blocks the queue. We can schedule a hash for a minute in the future
        // it might just need testing, because we don't know if it'll block for 1.5s or a minute
        long filesize;
        var settings = _settingsProvider.GetSettings();
        if (settings.Import.FileLockChecking)
        {
            var numAttempts = 0;
            var writeAccess = folder.IsDropSource == 1;

            // At least 1s between to ensure that size has the chance to change
            var waitTime = settings.Import.FileLockWaitTimeMS;
            if (waitTime < 1000)
            {
                waitTime = settings.Import.FileLockWaitTimeMS = 4000;
                _settingsProvider.SaveSettings();
            }

            // We do checks in the file watcher, but we want to make sure we can still access the file
            // Wait 1 minute before giving up on trying to access the file
            while ((filesize = CanAccessFile(FileName, writeAccess, ref e)) == 0 && numAttempts < 60)
            {
                numAttempts++;
                Thread.Sleep(waitTime);
                Logger.LogTrace("Failed to access, (or filesize is 0) Attempt # {NumAttempts}, {FileName}",
                    numAttempts, FileName);
            }

            // if we failed to access the file, return
            if (numAttempts >= 60 || filesize == 0)
            {
                Logger.LogError("Could not access file: {Filename}", FileName);
                return filesize;
            }
        }

        if (!File.Exists(FileName))
        {
            Logger.LogError("Could not access file: {Filename}", FileName);
            return 0;
        }

        return 0;
    }
    
    private long CanAccessFile(string fileName, bool writeAccess, ref Exception e)
    {
        var accessType = writeAccess ? FileAccess.ReadWrite : FileAccess.Read;
        try
        {
            return GetFileSize(fileName, accessType);
        }
        catch (IOException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            // This shouldn't cause a recursion, as it'll throw if failing
            Logger.LogTrace("File {FileName} is Read-Only, attempting to unmark", fileName);
            try
            {
                var info = new FileInfo(fileName);
                if (info.IsReadOnly) info.IsReadOnly = false;

                // check to see if it stuck. On linux, we can't just winapi hack our way out, so don't recurse in that case, anyway
                if (!new FileInfo(fileName).IsReadOnly && !Utils.IsRunningOnLinuxOrMac())
                {
                    return GetFileSize(fileName, accessType);
                }
            }
            catch
            {
                // ignore, we tried
            }

            e = ex;
            return 0;
        }
    }

    private static long GetFileSize(string fileName, FileAccess accessType)
    {
        using var fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite);
        var size = fs.Seek(0, SeekOrigin.End);
        return size;
    }

    private (bool changed, bool needEd2k, bool needCRC32, bool needMD5, bool needSHA1) ShouldHash(SVR_VideoLocal vlocal)
    {
        var changed = false;
        var hasherSettings = _settingsProvider.GetSettings().Import.Hasher;
        var needEd2k = string.IsNullOrEmpty(vlocal.Hash);
        var needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes;
        var needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes;
        var needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes;
        if (needCRC32 || needMD5 || needSHA1) changed = FillHashesAgainstVideoLocalRepo(vlocal);

        needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes;
        needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes;
        needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes;
        return (changed, needEd2k, needCRC32, needMD5, needSHA1);
    }

    private void FillMissingHashes(SVR_VideoLocal vlocal, bool needMD5, bool needSHA1, bool needCRC32, bool force)
    {
        var start = DateTime.Now;
        var tp = new List<string>();
        if (needSHA1) tp.Add("SHA1");
        if (needMD5) tp.Add("MD5");
        if (needCRC32) tp.Add("CRC32");

        Logger.LogTrace("Calculating missing {Filename} hashes for: {Types}", FileName, string.Join(",", tp));
        // update the VideoLocal record with the Hash, since cloud support we calculate everything
        var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{Path.DirectorySeparatorChar}"), true,
            ShokoServer.OnHashProgress,
            needCRC32, needMD5, needSHA1);
        var ts = DateTime.Now - start;
        Logger.LogTrace("Hashed file in {TotalSeconds:#0.0} seconds --- {Filename} ({Size})", ts.TotalSeconds,
            FileName, Utils.FormatByteSize(vlocal.FileSize));

        if (string.IsNullOrEmpty(vlocal.Hash) || force) vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
        if (needSHA1) vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
        if (needMD5) vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
        if (needCRC32) vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
        Logger.LogTrace("Hashed file {Filename} ({Size}): Hash: {Hash}, CRC: {CRC}, SHA1: {SHA1}, MD5: {MD5}", FileName, Utils.FormatByteSize(vlocal.FileSize),
            vlocal.Hash, vlocal.CRC32, vlocal.SHA1, vlocal.MD5);
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

    private static bool MergeInfoFrom(SVR_VideoLocal target, SVR_VideoLocal source)
    {
        var changed = false;
        if (string.IsNullOrEmpty(target.Hash) && !string.IsNullOrEmpty(source.Hash))
        {
            target.Hash = source.Hash;
            changed = true;
        }
        if (string.IsNullOrEmpty(target.CRC32) && !string.IsNullOrEmpty(source.CRC32))
        {
            target.CRC32 = source.CRC32;
            changed = true;
        }
        if (string.IsNullOrEmpty(target.MD5) && !string.IsNullOrEmpty(source.MD5))
        {
            target.MD5 = source.MD5;
            changed = true;
        }
        if (string.IsNullOrEmpty(target.SHA1) && !string.IsNullOrEmpty(source.SHA1))
        {
            target.SHA1 = source.SHA1;
            changed = true;
        }
        return changed;
    }

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

        Logger.LogWarning("Found Duplicate File");
        Logger.LogWarning("---------------------------------------------");
        Logger.LogWarning("New File: {FullServerPath}", vlocalplace.FullServerPath);
        Logger.LogWarning("Existing File: {FullServerPath}", dupPlace.FullServerPath);
        Logger.LogWarning("---------------------------------------------");

        var settings = _settingsProvider.GetSettings();
        if (settings.Import.AutomaticallyDeleteDuplicatesOnImport) vlocalplace.RemoveRecordAndDeletePhysicalFile();
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
