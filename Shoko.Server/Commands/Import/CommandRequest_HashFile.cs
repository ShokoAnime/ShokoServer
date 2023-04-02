using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.HashFile)]
public class CommandRequest_HashFile : CommandRequestImplementation
{
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;
    public virtual string FileName { get; set; }
    public virtual bool ForceHash { get; set; }

    public virtual bool SkipMyList { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Checking File for Hashes: {0}",
        queueState = QueueStateEnum.CheckingFile,
        extraParams = new[] { FileName }
    };

    private QueueStateStruct PrettyDescriptionHashing => new()
    {
        message = "Hashing File: {0}",
        queueState = QueueStateEnum.HashingFile,
        extraParams = new[] { FileName }
    };

    protected override void Process()
    {
        Logger.LogTrace("Checking File For Hashes: {Filename}", FileName);

        var (existing, vlocal, vlocalplace, folder) = GetVideoLocal();
        if (vlocal == null || vlocalplace == null)
        {
            Logger.LogTrace("Could not get or create VideoLocal. exiting");
            return;
        }

        var filename = vlocalplace.FileName;

        Logger.LogTrace("No existing hash in VideoLocal (or forced), checking XRefs");
        if (vlocal.HasAnyEmptyHashes() && !ForceHash)
        {
            // try getting the hash from the CrossRef
            if (TrySetHashFromXrefs(filename, vlocal))
                Logger.LogTrace("Found Hash in CrossRef_File_Episode: {Hash}", vlocal.Hash);
            else if (TrySetHashFromFileNameHash(filename, vlocal)) Logger.LogTrace("Found Hash in FileNameHash: {Hash}", vlocal.Hash);

            if (string.IsNullOrEmpty(vlocal.Hash) || string.IsNullOrEmpty(vlocal.CRC32) || string.IsNullOrEmpty(vlocal.MD5) ||
                string.IsNullOrEmpty(vlocal.SHA1))
                FillHashesAgainstVideoLocalRepo(vlocal);
        }

        if (!FillMissingHashes(vlocal, ForceHash) && existing)
        {
            Logger.LogTrace("Hashes were not necessary for file, so exiting: {File}, Hash: {Hash}", FileName, vlocal.Hash);
            return;
        }

        // We should have a hash by now
        // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)
        // TODO change this back to lookup by hash and filesize, but it'll need database migration and changes to other lookups
        var tlocal = RepoFactory.VideoLocal.GetByHash(vlocal.Hash);
        var changed = false;

        if (tlocal != null)
        {
            Logger.LogTrace("Found existing VideoLocal with hash, merging info from it");
            // Merge hashes and save, regardless of duplicate file
            changed = MergeInfoFrom(tlocal, vlocal);
            vlocal = tlocal;
        }

        // returns trinary state. null is return. true or false is duplicate
        var duplicate = ProcessDuplicates(vlocal, vlocalplace);
        if (duplicate == null) return;

        if (!duplicate.Value || changed)
        {
            Logger.LogTrace("Saving VideoLocal: Filename: {FileName}, Hash: {Hash}", FileName, vlocal.Hash);
            RepoFactory.VideoLocal.Save(vlocal, true);
        }

        vlocalplace.VideoLocalID = vlocal.VideoLocalID;
        RepoFactory.VideoLocalPlace.Save(vlocalplace);

        if (duplicate.Value)
        {
            _commandFactory.CreateAndSave<CommandRequest_ProcessFile>(
                c =>
                {
                    c.VideoLocalID = vlocal.VideoLocalID;
                    c.ForceAniDB = false;
                }
            );
            return;
        }

        SaveFileNameHash(filename, vlocal);

        if ((vlocal.Media?.GeneralStream?.Duration ?? 0) == 0 || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
            vlocal.RefreshMediaInfo(vlocalplace);

        ShokoEventHandler.Instance.OnFileHashed(folder, vlocalplace);

        // now add a command to process the file
        _commandFactory.CreateAndSave<CommandRequest_ProcessFile>(c =>
        {
            c.VideoLocalID = vlocal.VideoLocalID;
            c.ForceAniDB = ForceHash;
            c.SkipMyList = SkipMyList;
        });
    }

    //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
    private long CanAccessFile(string fileName, bool writeAccess, ref Exception e)
    {
        var accessType = writeAccess ? FileAccess.ReadWrite : FileAccess.Read;
        try
        {
            using (var fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite))
            {
                var size = fs.Seek(0, SeekOrigin.End);
                return size;
            }
        }
        catch (IOException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            // This shouldn't cause a recursion, as it'll throw if failing
            Logger.LogTrace("File {FileName} is Read-Only, unmarking", fileName);
            try
            {
                var info = new FileInfo(fileName);
                if (info.IsReadOnly)
                {
                    info.IsReadOnly = false;
                }

                // check to see if it stuck. On linux, we can't just winapi hack our way out, so don't recurse in that case, anyway
                if (!new FileInfo(fileName).IsReadOnly && !Utils.IsRunningOnLinuxOrMac())
                {
                    return CanAccessFile(fileName, writeAccess, ref e);
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

    private (bool existing, SVR_VideoLocal, SVR_VideoLocal_Place, SVR_ImportFolder) GetVideoLocal()
    {
        // hash and read media info for file
        var tup = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
        if (tup == null)
        {
            Logger.LogError("Unable to locate Import Folder for {FileName}", FileName);
            return default;
        }

        var folder = tup.Item1;
        var filePath = tup.Item2;
        long filesize = 0;
        Exception e = null;
        var existing = false;

        if (!File.Exists(FileName))
        {
            Logger.LogError("File does not exist: {Filename}", FileName);
            return default;
        }

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

            // if we failed to access the file, get ouuta here
            if (numAttempts >= 60 || filesize == 0)
            {
                Logger.LogError("Could not access file: {Filename}", FileName);
                return default;
            }
        }

        if (!File.Exists(FileName))
        {
            Logger.LogError("Could not access file: {Filename}", FileName);
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
                    vlocal.FileSize = filesize;
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
                DateTimeCreated = DateTimeUpdated,
                FileName = filename,
                FileSize = filesize,
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
                FilePath = filePath,
                ImportFolderID = nshareID,
                ImportFolderType = folder.ImportFolderType
            };
            // Make sure we have an ID
            RepoFactory.VideoLocalPlace.Save(vlocalplace);
        }

        return (existing, vlocal, vlocalplace, folder);
    }

    private bool TrySetHashFromXrefs(string filename, SVR_VideoLocal vlocal)
    {
        var crossRefs =
            RepoFactory.CrossRef_File_Episode.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (!crossRefs.Any()) return false;

        vlocal.Hash = crossRefs[0].Hash;
        vlocal.HashSource = (int)HashSource.DirectHash;
        Logger.LogTrace("Got hash from xrefs: {Filename} ({Hash})", FileName, crossRefs[0].Hash);
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

        Logger.LogTrace("Got hash from LOCAL cache: {Filename} ({Hash})", FileName, fnhashes[0].Hash);
        vlocal.Hash = fnhashes[0].Hash;
        vlocal.HashSource = (int)HashSource.WebCacheFileName;
        return true;

    }

    private bool FillMissingHashes(SVR_VideoLocal vlocal, bool force)
    {
        var hasherSettings = _settingsProvider.GetSettings().Import.Hasher;
        var needEd2k = string.IsNullOrEmpty(vlocal.Hash) || force;
        var needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes && force;
        var needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes && force;
        var needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes && force;
        if (needCRC32 || needMD5 || needSHA1) FillHashesAgainstVideoLocalRepo(vlocal);

        needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes && force;
        needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes && force;
        needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes && force;
        if (!needEd2k && !needCRC32 && !needMD5 && !needSHA1) return false;

        if (Processor != null)
            Processor.QueueState = PrettyDescriptionHashing;
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

        return true;
    }

    private static void FillHashesAgainstVideoLocalRepo(SVR_VideoLocal v)
    {
        if (!string.IsNullOrEmpty(v.ED2KHash))
        {
            var n = RepoFactory.VideoLocal.GetByHash(v.ED2KHash);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                }

                return;
            }
        }

        if (!string.IsNullOrEmpty(v.SHA1))
        {
            var n = RepoFactory.VideoLocal.GetBySHA1(v.SHA1);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(v.ED2KHash))
                {
                    v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                }

                return;
            }
        }

        if (!string.IsNullOrEmpty(v.MD5))
        {
            var n = RepoFactory.VideoLocal.GetByMD5(v.MD5);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(v.ED2KHash))
                {
                    v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                }
            }
        }
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

    private bool? ProcessDuplicates(SVR_VideoLocal vlocal, SVR_VideoLocal_Place vlocalplace)
    {
        if (vlocal == null) return null;
        // If the VideoLocalID == 0, then it's a new file that wasn't merged after hashing, so it can't be a dupe
        if (vlocal.VideoLocalID == 0) return false;

        var preps = vlocal.Places.Where(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
        foreach (var prep in preps)
        {
            if (prep == null) continue;

            // clean up, if there is a 'duplicate file' that is invalid, remove it.
            if (prep.FullServerPath == null)
            {
                RepoFactory.VideoLocalPlace.Delete(prep);
                continue;
            }

            if (File.Exists(prep.FullServerPath)) continue;
            RepoFactory.VideoLocalPlace.Delete(prep);
        }

        var dupPlace = vlocal.Places.FirstOrDefault(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath));

        if (dupPlace == null) return false;

        Logger.LogWarning("Found Duplicate File");
        Logger.LogWarning("---------------------------------------------");
        Logger.LogWarning("New File: {FullServerPath}", vlocalplace.FullServerPath);
        Logger.LogWarning("Existing File: {FullServerPath}", dupPlace.FullServerPath);
        Logger.LogWarning("---------------------------------------------");

        var settings = _settingsProvider.GetSettings();
        if (settings.Import.AutomaticallyDeleteDuplicatesOnImport)
        {
            vlocalplace.RemoveRecordAndDeletePhysicalFile();
            return null;
        }

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

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_HashFile_{FileName}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        FileName = docCreator.TryGetProperty("CommandRequest_HashFile", "FileName");
        ForceHash = bool.Parse(docCreator.TryGetProperty("CommandRequest_HashFile", "ForceHash"));
        SkipMyList = bool.Parse(docCreator.TryGetProperty("CommandRequest_HashFile", "SkipMyList"));

        return FileName.Trim().Length > 0;
    }

    public CommandRequest_HashFile(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_HashFile()
    {
    }
}
