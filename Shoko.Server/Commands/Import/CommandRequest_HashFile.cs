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
    public string FileName { get; set; }
    public bool ForceHash { get; set; }

    public bool SkipMyList { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Checking File for Hashes: {0}",
        queueState = QueueStateEnum.CheckingFile,
        extraParams = new[] { FileName }
    };

    public QueueStateStruct PrettyDescriptionHashing => new()
    {
        message = "Hashing File: {0}", queueState = QueueStateEnum.HashingFile, extraParams = new[] { FileName }
    };

    protected override void Process()
    {
        Logger.LogTrace("Checking File For Hashes: {Filename}", FileName);

        try
        {
            ProcessFile_LocalInfo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing file: {Filename}\n{Ex}", FileName, ex);
        }
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

    private void ProcessFile_LocalInfo()
    {
        // hash and read media info for file
        int nshareID;

        var tup = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
        if (tup == null)
        {
            Logger.LogError("Unable to locate Import Folder for {FileName}", FileName);
            return;
        }

        var folder = tup.Item1;
        var filePath = tup.Item2;
        long filesize = 0;
        Exception e = null;

        if (!File.Exists(FileName))
        {
            Logger.LogError("File does not exist: {Filename}", FileName);
            return;
        }

        if (ServerSettings.Instance.Import.FileLockChecking)
        {
            var numAttempts = 0;
            var writeAccess = folder.IsDropSource == 1;

            // At least 1s between to ensure that size has the chance to change
            var waitTime = ServerSettings.Instance.Import.FileLockWaitTimeMS;
            if (waitTime < 1000)
            {
                waitTime = ServerSettings.Instance.Import.FileLockWaitTimeMS = 4000;
                ServerSettings.Instance.SaveSettings();
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
                return;
            }
        }

        if (!File.Exists(FileName))
        {
            Logger.LogError("Could not access file: {Filename}", FileName);
            return;
        }

        nshareID = folder.ImportFolderID;


        // check if we have already processed this file
        var vlocalplace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(filePath, nshareID);
        SVR_VideoLocal vlocal = null;
        var filename = Path.GetFileName(filePath);

        if (vlocalplace != null)
        {
            vlocal = vlocalplace.VideoLocal;
            if (vlocal != null)
            {
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
            // TODO support reading MD5 and SHA1 from files via the standard way
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
                IsIgnored = 0,
                IsVariation = 0
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

        // check if we need to get a hash this file
        if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
        {
            Logger.LogTrace("No existing hash in VideoLocal, checking XRefs");
            if (!ForceHash)
            {
                // try getting the hash from the CrossRef
                var crossRefs =
                    RepoFactory.CrossRef_File_Episode.GetByFileNameAndSize(filename, vlocal.FileSize);
                if (crossRefs.Any())
                {
                    vlocal.Hash = crossRefs[0].Hash;
                    vlocal.HashSource = (int)HashSource.DirectHash;
                }
            }

            // try getting the hash from the LOCAL cache
            if (!ForceHash && string.IsNullOrEmpty(vlocal.Hash))
            {
                var fnhashes =
                    RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
                if (fnhashes != null && fnhashes.Count > 1)
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

                if (fnhashes != null && fnhashes.Count == 1)
                {
                    Logger.LogTrace("Got hash from LOCAL cache: {Filename} ({Hash})", FileName, fnhashes[0].Hash);
                    vlocal.Hash = fnhashes[0].Hash;
                    vlocal.HashSource = (int)HashSource.WebCacheFileName;
                }
            }

            if (string.IsNullOrEmpty(vlocal.Hash))
            {
                FillVideoHashes(vlocal);
            }

            // hash the file
            if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
            {
                Logger.LogInformation("Hashing File: {Filename}", FileName);
                ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
                var start = DateTime.Now;
                // update the VideoLocal record with the Hash, since cloud support we calculate everything
                var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{Path.DirectorySeparatorChar}"), true,
                    ShokoServer.OnHashProgress,
                    true, true, true);
                var ts = DateTime.Now - start;
                Logger.LogTrace("Hashed file in {Seconds:#0.0} seconds --- {Filename} ({Size})", ts.TotalSeconds,
                    FileName,
                    Utils.FormatByteSize(vlocal.FileSize));
                vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
                vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
                vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
                vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
                vlocal.HashSource = (int)HashSource.DirectHash;
            }

            FillMissingHashes(vlocal);
            // We should have a hash by now
            // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)

            var tlocal = RepoFactory.VideoLocal.GetByHash(vlocal.Hash);
            var duplicate = false;
            var changed = false;

            if (tlocal != null)
            {
                Logger.LogTrace("Found existing VideoLocal with hash, merging info from it");
                // Aid with hashing cloud. Merge hashes and save, regardless of duplicate file
                changed = tlocal.MergeInfoFrom(vlocal);
                vlocal = tlocal;

                var preps = vlocal.Places.Where(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
                foreach (var prep in preps)
                {
                    if (prep == null)
                    {
                        continue;
                    }

                    // clean up, if there is a 'duplicate file' that is invalid, remove it.
                    if (prep.FullServerPath == null)
                    {
                        RepoFactory.VideoLocalPlace.Delete(prep);
                    }
                    else
                    {
                        if (!File.Exists(prep.FullServerPath))
                        {
                            RepoFactory.VideoLocalPlace.Delete(prep);
                        }
                    }
                }

                var dupPlace = vlocal.Places.FirstOrDefault(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath));

                if (dupPlace != null)
                {
                    Logger.LogWarning("Found Duplicate File");
                    Logger.LogWarning("---------------------------------------------");
                    Logger.LogWarning("New File: {FullServerPath}", vlocalplace.FullServerPath);
                    Logger.LogWarning("Existing File: {FullServerPath}", dupPlace.FullServerPath);
                    Logger.LogWarning("---------------------------------------------");

                    if (ServerSettings.Instance.Import.AutomaticallyDeleteDuplicatesOnImport)
                    {
                        vlocalplace.RemoveRecordAndDeletePhysicalFile();
                        return;
                    }

                    // check if we have a record of this in the database, if not create one
                    var dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(
                        vlocalplace.FilePath,
                        dupPlace.FilePath,
                        vlocalplace.ImportFolderID, dupPlace.ImportFolderID);
                    if (dupFiles.Count == 0)
                    {
                        dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(dupPlace.FilePath,
                            vlocalplace.FilePath, dupPlace.ImportFolderID, vlocalplace.ImportFolderID);
                    }

                    if (dupFiles.Count == 0)
                    {
                        var dup = new DuplicateFile
                        {
                            DateTimeUpdated = DateTime.Now,
                            FilePathFile1 = vlocalplace.FilePath,
                            FilePathFile2 = dupPlace.FilePath,
                            ImportFolderIDFile1 = vlocalplace.ImportFolderID,
                            ImportFolderIDFile2 = dupPlace.ImportFolderID,
                            Hash = vlocal.Hash
                        };
                        RepoFactory.DuplicateFile.Save(dup);
                    }

                    //Notify duplicate, don't delete
                    duplicate = true;
                }
            }

            if (!duplicate || changed)
            {
                RepoFactory.VideoLocal.Save(vlocal, true);
            }

            vlocalplace.VideoLocalID = vlocal.VideoLocalID;
            RepoFactory.VideoLocalPlace.Save(vlocalplace);

            if (duplicate)
            {
                var crProcfile3 = _commandFactory.Create<CommandRequest_ProcessFile>(
                    c =>
                    {
                        c.VideoLocalID = vlocal.VideoLocalID;
                        c.ForceAniDB = false;
                    }
                );
                crProcfile3.Save();
                return;
            }

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
        else
        {
            FillMissingHashes(vlocal);
        }


        if ((vlocal.Media?.GeneralStream?.Duration ?? 0) == 0 || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
        {
            if (vlocalplace.RefreshMediaInfo())
            {
                RepoFactory.VideoLocal.Save(vlocalplace.VideoLocal, true);
            }
        }

        ShokoEventHandler.Instance.OnFileHashed(folder, vlocalplace);

        // now add a command to process the file
        var crProcFile = _commandFactory.Create<CommandRequest_ProcessFile>(c =>
        {
            c.VideoLocalID = vlocal.VideoLocalID;
            c.ForceAniDB = false;
            c.SkipMyList = SkipMyList;
        });
        crProcFile.Save();
    }

    private void FillMissingHashes(SVR_VideoLocal vlocal)
    {
        var needcrc32 = string.IsNullOrEmpty(vlocal.CRC32);
        var needmd5 = string.IsNullOrEmpty(vlocal.MD5);
        var needsha1 = string.IsNullOrEmpty(vlocal.SHA1);
        if (needcrc32 || needmd5 || needsha1)
        {
            FillVideoHashes(vlocal);
        }

        needcrc32 = string.IsNullOrEmpty(vlocal.CRC32);
        needmd5 = string.IsNullOrEmpty(vlocal.MD5);
        needsha1 = string.IsNullOrEmpty(vlocal.SHA1);
        if (!needcrc32 && !needmd5 && !needsha1) return;

        ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
        var start = DateTime.Now;
        var tp = new List<string>();
        if (needsha1)
        {
            tp.Add("SHA1");
        }

        if (needmd5)
        {
            tp.Add("MD5");
        }

        if (needcrc32)
        {
            tp.Add("CRC32");
        }

        Logger.LogTrace("Calculating missing {Filename} hashes for: {Types}", FileName, string.Join(",", tp));
        // update the VideoLocal record with the Hash, since cloud support we calculate everything
        var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{Path.DirectorySeparatorChar}"), true,
            ShokoServer.OnHashProgress,
            needcrc32, needmd5, needsha1);
        var ts = DateTime.Now - start;
        Logger.LogTrace("Hashed file in {TotalSeconds:#0.0} seconds --- {Filename} ({Size})", ts.TotalSeconds,
            FileName, Utils.FormatByteSize(vlocal.FileSize));
        if (string.IsNullOrEmpty(vlocal.Hash))
        {
            vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
        }

        if (needsha1)
        {
            vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
        }

        if (needmd5)
        {
            vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
        }

        if (needcrc32)
        {
            vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
        }
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

    private void FillVideoHashes(SVR_VideoLocal v)
    {
        if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
        {
            FillHashesAgainstVideoLocalRepo(v);
        }
    }


    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_HashFile_{FileName}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length > 0)
        {
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            FileName = TryGetProperty(docCreator, "CommandRequest_HashFile", "FileName");
            ForceHash = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "ForceHash"));
            SkipMyList = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "SkipMyList"));
        }

        if (FileName.Trim().Length > 0)
        {
            return true;
        }

        return false;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_HashFile(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
    }

    protected CommandRequest_HashFile()
    {
    }
}
