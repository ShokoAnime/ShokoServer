using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.HashFile)]
    public class CommandRequest_HashFile : CommandRequestImplementation
    {
        public string FileName { get; set; }
        public bool ForceHash { get; set; }
        
        public bool SkipMyList { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.CheckingFile,
            extraParams = new[] {FileName}
        };

        public QueueStateStruct PrettyDescriptionHashing => new QueueStateStruct
        {
            queueState = QueueStateEnum.HashingFile,
            extraParams = new[] {FileName}
        };

        public CommandRequest_HashFile()
        {
        }

        public CommandRequest_HashFile(string filename, bool force, bool skipMyList = false)
        {
            FileName = filename;
            ForceHash = force;
            Priority = (int) DefaultPriority;
            SkipMyList = skipMyList;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Trace("Checking File For Hashes: {0}", FileName);

            try
            {
                ProcessFile_LocalInfo();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing file: {0}\n{1}", FileName, ex);
            }
        }

        //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
        private static long CanAccessFile(string fileName, bool writeAccess, ref Exception e)
        {
            var accessType = writeAccess ? FileAccess.ReadWrite : FileAccess.Read;
            try
            {
                using (FileStream fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite))
                {
                    long size = fs.Seek(0, SeekOrigin.End);
                    return size;
                }
            }
            catch (IOException ioex)
            {
                return 0;
            }
            catch (Exception ex)
            {
                // This shouldn't cause a recursion, as it'll throw if failing
                logger.Trace($"File {fileName} is Read-Only, unmarking");
                try
                {
                    FileInfo info = new FileInfo(fileName);
                    if (info.IsReadOnly)
                    {
                        info.IsReadOnly = false;
                    }

                    // check to see if it stuck. On linux, we can't just winapi hack our way out, so don't recurse in that case, anyway
                    if (!new FileInfo(fileName).IsReadOnly && !Utils.IsRunningOnLinuxOrMac())
                        return CanAccessFile(fileName, writeAccess, ref e);
                }
                catch
                {
                    // ignore, we tried
                }
                
                e = ex;
                return 0;
            }
        }

        //Used to check if file has been modified within the last X seconds.
        private static bool FileModified(string FileName, int Seconds, ref long lastFileSize, bool writeAccess, ref Exception e)
        {
            try
            {
                var lastWrite = File.GetLastWriteTime(FileName);
                var creation = File.GetCreationTime(FileName);
                var now = DateTime.Now;
                // check that the size is also equal, since some copy utilities apply the previous modified date
                var size = CanAccessFile(FileName, writeAccess, ref e);
                if (lastWrite <= now && lastWrite.AddSeconds(Seconds) >= now || 
                    creation <= now && creation.AddSeconds(Seconds) > now || 
                    lastFileSize != size)
                {
                    lastFileSize = size;
                    return true;
                }
            }
            catch (Exception ex)
            {
                e = ex;
            }
            return false;
        }

        private void ProcessFile_LocalInfo()
        {
            // hash and read media info for file
            int nshareID = -1;


            Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
            if (tup == null)
            {
                logger.Error($"Unable to locate Import Folder for {FileName}");
                return;
            }
            SVR_ImportFolder folder = tup.Item1;
            string filePath = tup.Item2;
            long filesize = 0;
            Exception e = null;

            if (!File.Exists(FileName))
            {
                logger.Error("File does not exist: {0}", FileName);
                return;
            }

            if (ServerSettings.Instance.Import.FileLockChecking)
            {
                int numAttempts = 0;
                bool writeAccess = folder.IsDropSource == 1;

                bool aggressive = ServerSettings.Instance.Import.AggressiveFileLockChecking;

                // At least 1s between to ensure that size has the chance to change
                int waitTime = ServerSettings.Instance.Import.FileLockWaitTimeMS;
                if (waitTime < 1000)
                {
                    waitTime = ServerSettings.Instance.Import.FileLockWaitTimeMS = 4000;
                    ServerSettings.Instance.SaveSettings();
                }

                if (!aggressive)
                {
                    // Wait 1 minute before giving up on trying to access the file
                    while ((filesize = CanAccessFile(FileName, writeAccess, ref e)) == 0 && (numAttempts < 60))
                    {
                        numAttempts++;
                        Thread.Sleep(waitTime);
                        logger.Trace($@"Failed to access, (or filesize is 0) Attempt # {numAttempts}, {FileName}");
                    }
                }
                else
                {
                    // Wait 1 minute before giving up on trying to access the file
                    // first only do read to not get in something's way
                    while ((filesize = CanAccessFile(FileName, false, ref e)) == 0 && (numAttempts < 60))
                    {
                        numAttempts++;
                        Thread.Sleep(1000);
                        logger.Trace($@"Failed to access, (or filesize is 0) Attempt # {numAttempts}, {FileName}");
                    }

                    // if we failed to access the file, get ouuta here
                    if (numAttempts >= 60)
                    {
                        logger.Error("Could not access file: " + FileName);
                        logger.Error(e);
                        return;
                    }

                    int seconds = ServerSettings.Instance.Import.AggressiveFileLockWaitTimeSeconds;
                    if (seconds < 0)
                    {
                        seconds = ServerSettings.Instance.Import.AggressiveFileLockWaitTimeSeconds = 8;
                        ServerSettings.Instance.SaveSettings();
                    }

                    numAttempts = 0;

                    //For systems with no locking
                    while (FileModified(FileName, seconds, ref filesize, writeAccess, ref e) && numAttempts < 60)
                    {
                        numAttempts++;
                        Thread.Sleep(waitTime);
                        // Only show if it's more than 'seconds' past
                        if (numAttempts != 0 && numAttempts * 2 % seconds == 0)
                            logger.Warn(
                                $@"The modified date is too soon. Waiting to ensure that no processes are writing to it. {numAttempts}/60 {FileName}"
                            );
                    }
                }

                // if we failed to access the file, get ouuta here
                if (numAttempts >= 60 || filesize == 0)
                {
                    logger.Error("Could not access file: " + FileName);
                    logger.Error(e);
                    return;
                }
            }

            if (!File.Exists(FileName))
            {
                logger.Error("Could not access file: " + FileName);
                return;
            }
            FileInfo sourceFile = new FileInfo(FileName);
            nshareID = folder.ImportFolderID;


            // check if we have already processed this file
            SVR_VideoLocal_Place vlocalplace = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(filePath, nshareID);
            SVR_VideoLocal vlocal = null;
            var filename = Path.GetFileName(filePath);

            if (vlocalplace != null)
            {
                vlocal = vlocalplace.VideoLocal;
                if (vlocal != null)
                {
                    logger.Trace("VideoLocal record found in database: {0}", FileName);

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
                logger.Trace("No existing VideoLocal, creating temporary record");
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
                logger.Trace("No existing VideoLocal_Place, creating a new record");
                vlocalplace = new SVR_VideoLocal_Place
                {
                    FilePath = filePath,
                    ImportFolderID = nshareID,
                    ImportFolderType = folder.ImportFolderType
                };
                // Make sure we have an ID
                RepoFactory.VideoLocalPlace.Save(vlocalplace);
            }

            // check if we need to get a hash this file
            if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
            {
                logger.Trace("No existing hash in VideoLocal, checking XRefs");
                if (!ForceHash)
                {
                    // try getting the hash from the CrossRef
                    List<CrossRef_File_Episode> crossRefs =
                        RepoFactory.CrossRef_File_Episode.GetByFileNameAndSize(filename, vlocal.FileSize);
                    if (crossRefs.Any())
                    {
                        vlocal.Hash = crossRefs[0].Hash;
                        vlocal.HashSource = (int) HashSource.DirectHash;
                    }
                }

                // try getting the hash from the LOCAL cache
                if (!ForceHash && string.IsNullOrEmpty(vlocal.Hash))
                {
                    List<FileNameHash> fnhashes =
                        RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
                    if (fnhashes != null && fnhashes.Count > 1)
                    {
                        // if we have more than one record it probably means there is some sort of corruption
                        // lets delete the local records
                        foreach (FileNameHash fnh in fnhashes)
                        {
                            RepoFactory.FileNameHash.Delete(fnh.FileNameHashID);
                        }
                    }
                    // reinit this to check if we erased them
                    fnhashes = RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);

                    if (fnhashes != null && fnhashes.Count == 1)
                    {
                        logger.Trace("Got hash from LOCAL cache: {0} ({1})", FileName, fnhashes[0].Hash);
                        vlocal.Hash = fnhashes[0].Hash;
                        vlocal.HashSource = (int) HashSource.WebCacheFileName;
                    }
                }

                if (string.IsNullOrEmpty(vlocal.Hash))
                    FillVideoHashes(vlocal);

                // hash the file
                if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
                {
                    logger.Info("Hashing File: {0}", FileName);
                    ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
                    DateTime start = DateTime.Now;
                    // update the VideoLocal record with the Hash, since cloud support we calculate everything
                    var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{Path.DirectorySeparatorChar}"), true, ShokoServer.OnHashProgress,
                        true, true, true);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Trace("Hashed file in {0:#0.0} seconds --- {1} ({2})", ts.TotalSeconds, FileName,
                        Utils.FormatByteSize(vlocal.FileSize));
                    vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
                    vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
                    vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
                    vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
                    vlocal.HashSource = (int) HashSource.DirectHash;
                }
                FillMissingHashes(vlocal);
                // We should have a hash by now
                // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)

                SVR_VideoLocal tlocal = RepoFactory.VideoLocal.GetByHash(vlocal.Hash);
                bool duplicate = false;
                bool changed = false;

                if (tlocal != null)
                {
                    logger.Trace("Found existing VideoLocal with hash, merging info from it");
                    // Aid with hashing cloud. Merge hashes and save, regardless of duplicate file
                    changed = tlocal.MergeInfoFrom(vlocal);
                    vlocal = tlocal;

                    List<SVR_VideoLocal_Place> preps = vlocal.Places.Where(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
                    foreach (var prep in preps)
                    {
                        if (prep == null) continue;
                        // clean up, if there is a 'duplicate file' that is invalid, remove it.
                        if (prep.FullServerPath == null)
                        {
                            RepoFactory.VideoLocalPlace.Delete(prep);
                        }
                        else
                        {
                            if (!File.Exists(prep.FullServerPath))
                                RepoFactory.VideoLocalPlace.Delete(prep);
                        }
                    }

                    var dupPlace = vlocal.Places.FirstOrDefault(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath));

                    if (dupPlace != null)
                    {
                        logger.Warn("Found Duplicate File");
                        logger.Warn("---------------------------------------------");
                        logger.Warn($"New File: {vlocalplace.FullServerPath}");
                        logger.Warn($"Existing File: {dupPlace.FullServerPath}");
                        logger.Warn("---------------------------------------------");

                        if (ServerSettings.Instance.Import.AutomaticallyDeleteDuplicatesOnImport)
                        {
                            vlocalplace.RemoveRecordAndDeletePhysicalFile();
                            return;
                        }
                        // check if we have a record of this in the database, if not create one
                        List<DuplicateFile> dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(
                            vlocalplace.FilePath,
                            dupPlace.FilePath,
                            vlocalplace.ImportFolderID, dupPlace.ImportFolderID);
                        if (dupFiles.Count == 0)
                            dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(dupPlace.FilePath,
                                vlocalplace.FilePath, dupPlace.ImportFolderID, vlocalplace.ImportFolderID);

                        if (dupFiles.Count == 0)
                        {
                            DuplicateFile dup = new DuplicateFile
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
                    RepoFactory.VideoLocal.Save(vlocal, true);

                vlocalplace.VideoLocalID = vlocal.VideoLocalID;
                RepoFactory.VideoLocalPlace.Save(vlocalplace);

                if (duplicate)
                {
                    CommandRequest_ProcessFile cr_procfile3 =
                        new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
                    cr_procfile3.Save();
                    return;
                }

                // also save the filename to hash record
                // replace the existing records just in case it was corrupt
                FileNameHash fnhash;
                List<FileNameHash> fnhashes2 =
                    RepoFactory.FileNameHash.GetByFileNameAndSize(filename, vlocal.FileSize);
                if (fnhashes2 != null && fnhashes2.Count > 1)
                {
                    // if we have more than one record it probably means there is some sort of corruption
                    // lets delete the local records
                    foreach (FileNameHash fnh in fnhashes2)
                    {
                        RepoFactory.FileNameHash.Delete(fnh.FileNameHashID);
                    }
                }

                if (fnhashes2 != null && fnhashes2.Count == 1)
                    fnhash = fnhashes2[0];
                else
                    fnhash = new FileNameHash();

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


            if (((vlocal.Media?.GeneralStream?.Duration ?? 0) == 0) || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
            {
                if (vlocalplace.RefreshMediaInfo())
                    RepoFactory.VideoLocal.Save(vlocalplace.VideoLocal, true);
            }

            ShokoEventHandler.Instance.OnFileHashed(folder, vlocalplace);

            // now add a command to process the file
            CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(vlocal.VideoLocalID, false, SkipMyList);
            cr_procfile.Save();
        }

        private void FillMissingHashes(SVR_VideoLocal vlocal)
        {
            bool needcrc32 = string.IsNullOrEmpty(vlocal.CRC32);
            bool needmd5 = string.IsNullOrEmpty(vlocal.MD5);
            bool needsha1 = string.IsNullOrEmpty(vlocal.SHA1);
            if (needcrc32 || needmd5 || needsha1)
                FillVideoHashes(vlocal);
            needcrc32 = string.IsNullOrEmpty(vlocal.CRC32);
            needmd5 = string.IsNullOrEmpty(vlocal.MD5);
            needsha1 = string.IsNullOrEmpty(vlocal.SHA1);
            if (needcrc32 || needmd5 || needsha1)
            {
                ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
                DateTime start = DateTime.Now;
                List<string> tp = new List<string>();
                if (needsha1)
                    tp.Add("SHA1");
                if (needmd5)
                    tp.Add("MD5");
                if (needcrc32)
                    tp.Add("CRC32");
                logger.Trace("Calculating missing {1} hashes for: {0}", FileName, string.Join(",", tp));
                // update the VideoLocal record with the Hash, since cloud support we calculate everything
                Hashes hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{Path.DirectorySeparatorChar}"), true, ShokoServer.OnHashProgress,
                    needcrc32, needmd5, needsha1);
                TimeSpan ts = DateTime.Now - start;
                logger.Trace("Hashed file in {0:#0.0} seconds --- {1} ({2})", ts.TotalSeconds,
                    FileName, Utils.FormatByteSize(vlocal.FileSize));
                if (string.IsNullOrEmpty(vlocal.Hash))
                    vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
                if (needsha1)
                    vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
                if (needmd5)
                    vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
                if (needcrc32)
                    vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
                if (ServerSettings.Instance.WebCache.Enabled) AzureWebAPI.Send_FileHash(new List<SVR_VideoLocal> {vlocal});
            }
        }

        private void FillHashesAgainstVideoLocalRepo(SVR_VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                SVR_VideoLocal n = RepoFactory.VideoLocal.GetByHash(v.ED2KHash);
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(n.CRC32))
                        v.CRC32 = n.CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.MD5))
                        v.MD5 = n.MD5.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.SHA1))
                        v.SHA1 = n.SHA1.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.SHA1))
            {
                SVR_VideoLocal n = RepoFactory.VideoLocal.GetBySHA1(v.SHA1);
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(n.CRC32))
                        v.CRC32 = n.CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.MD5))
                        v.MD5 = n.MD5.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(v.ED2KHash))
                        v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.MD5))
            {
                SVR_VideoLocal n = RepoFactory.VideoLocal.GetByMD5(v.MD5);
                if (n != null)
                {
                    if (!string.IsNullOrEmpty(n.CRC32))
                        v.CRC32 = n.CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(n.SHA1))
                        v.SHA1 = n.SHA1.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(v.ED2KHash))
                        v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                }
            }
        }

        private void FillHashesAgainstAniDBRepo(SVR_VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                SVR_AniDB_File f = RepoFactory.AniDB_File.GetByHash(v.ED2KHash);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.CRC))
                        v.CRC32 = f.CRC.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.SHA1))
                        v.SHA1 = f.SHA1.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.MD5))
                        v.MD5 = f.MD5.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.SHA1))
            {
                SVR_AniDB_File f = RepoFactory.AniDB_File.GetBySHA1(v.SHA1);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.CRC))
                        v.CRC32 = f.CRC.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.Hash))
                        v.ED2KHash = f.Hash.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.MD5))
                        v.MD5 = f.MD5.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.MD5))
            {
                SVR_AniDB_File f = RepoFactory.AniDB_File.GetByMD5(v.MD5);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.CRC))
                        v.CRC32 = f.CRC.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.Hash))
                        v.ED2KHash = f.Hash.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(f.SHA1))
                        v.SHA1 = f.SHA1.ToUpperInvariant();
                }
            }
        }

        private void FillHashesAgainstWebCache(SVR_VideoLocal v)
        {
            if (!ServerSettings.Instance.WebCache.Enabled) return;
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                List<Azure_FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.ED2K, v.ED2KHash) ??
                                          new List<Azure_FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) &&
                                   !string.IsNullOrEmpty(a.SHA1))
                    .ToList();
                if (ls.Count > 0)
                {
                    if (!string.IsNullOrEmpty(ls[0].SHA1))
                        v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].CRC32))
                        v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].MD5))
                        v.MD5 = ls[0].MD5.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.SHA1))
            {
                List<Azure_FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.SHA1, v.SHA1) ??
                                          new List<Azure_FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) &&
                                   !string.IsNullOrEmpty(a.ED2K))
                    .ToList();
                if (ls.Count > 0)
                {
                    if (!string.IsNullOrEmpty(ls[0].ED2K))
                        v.ED2KHash = ls[0].ED2K.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].CRC32))
                        v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].MD5))
                        v.MD5 = ls[0].MD5.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.MD5))
            {
                List<Azure_FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.MD5, v.MD5) ??
                                          new List<Azure_FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.SHA1) &&
                                   !string.IsNullOrEmpty(a.ED2K))
                    .ToList();
                if (ls.Count > 0)
                {
                    if (!string.IsNullOrEmpty(ls[0].ED2K))
                        v.ED2KHash = ls[0].ED2K.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].CRC32))
                        v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ls[0].SHA1))
                        v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                }
            }
        }

        private void FillVideoHashes(SVR_VideoLocal v)
        {
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstVideoLocalRepo(v);
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstAniDBRepo(v);
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstWebCache(v);
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                FileName = TryGetProperty(docCreator, "CommandRequest_HashFile", "FileName");
                ForceHash = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "ForceHash"));
                SkipMyList = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "SkipMyList"));
            }

            if (FileName.Trim().Length > 0)
                return true;
            return false;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
