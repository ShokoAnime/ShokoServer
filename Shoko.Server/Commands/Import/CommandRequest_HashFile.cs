using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Repos;


namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_HashFile : CommandRequest
    {
        public virtual string FileName { get; set; }
        public virtual bool ForceHash { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.CheckingFile,
            extraParams = new[] {FileName}
        };

        public virtual QueueStateStruct PrettyDescriptionHashing => new QueueStateStruct
        {
            queueState = QueueStateEnum.HashingFile,
            extraParams = new[] {FileName}
        };

        public CommandRequest_HashFile()
        {
        }

        public CommandRequest_HashFile(string filename, bool force)
        {
            FileName = filename;
            ForceHash = force;
            CommandType = (int) CommandRequestType.HashFile;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
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
        private long CanAccessFile(string fileName)
        {
            try
            {
                using (FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    long size = fs.Seek(0, SeekOrigin.End);
                    fs.Close();
                    return size;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
        }

        private SVR_VideoLocal_Place ProcessFile_LocalInfo()
        {
            // hash and read media info for file
            int nshareID = -1;
            string filePath = string.Empty;


            (SVR_ImportFolder, string) tup = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
            if (tup.Item1 == null)
            {
                logger.Error($"Unable to locate Import Folder for {FileName}");
                return null;
            }
            SVR_ImportFolder folder = tup.Item1;
            filePath = tup.Item2;
            IFileSystem f = tup.Item1.FileSystem;
            if (f == null)
            {
                logger.Error("Unable to open filesystem for: {0}", FileName);
                return null;
            }
            long filesize = 0;
            if (folder.CloudID == null) // Local Access
            {
                if (!File.Exists(FileName))
                {
                    logger.Error("File does not exist: {0}", FileName);
                    return null;
                }

                int numAttempts = 0;

                // Wait 1 minute seconds before giving up on trying to access the file
                while ((filesize = CanAccessFile(FileName)) == 0 && (numAttempts < 60))
                {
                    numAttempts++;
                    Thread.Sleep(1000);
                    Console.WriteLine($@"Attempt # {numAttempts}");
                }

                // if we failed to access the file, get ouuta here
                if (numAttempts >= 60)
                {
                    logger.Error("Could not access file: " + FileName);
                    return null;
                }
            }


            IObject source = f.Resolve(FileName);
            if (source.Status!=Status.Ok || !(source is IFile))
            {
                logger.Error("Could not access file: " + FileName);
                return null;
            }
            IFile source_file = (IFile) source;
            if (folder.CloudID.HasValue)
                filesize = source_file.Size;
            nshareID = folder.ImportFolderID;
            string filename = source.Name;

            // check if we have already processed this file

            SVR_VideoLocal_Place vlocalplace = Repo.VideoLocal_Place.GetByFilePathAndShareID(filePath, nshareID);
            SVR_VideoLocal vlocal = null;

            if (vlocalplace != null)
            {
                vlocal = vlocalplace.VideoLocal;
                logger.Trace("VideoLocal record found in database: {0}", vlocal.VideoLocalID);
                if (vlocalplace.FullServerPath == null)
                {
                    if (vlocal.Places.Count == 1)
                    {
                        Repo.VideoLocal.Delete(vlocal);
                        vlocal = null;
                    }
                    Repo.VideoLocal_Place.Delete(vlocalplace);
                    vlocalplace = null;
                }
            }

            SVR_VideoLocal local=new SVR_VideoLocal();
            local.DateTimeUpdated = DateTime.Now;
            local.DateTimeCreated = DateTimeUpdated;
            local.FileSize = filesize;
            local.Hash = string.Empty;
            local.CRC32 = string.Empty;
            local.MD5 = source_file?.MD5?.ToUpperInvariant() ?? string.Empty;
            local.SHA1 = source_file?.SHA1?.ToUpperInvariant() ?? string.Empty;
            local.IsIgnored = 0;
            local.IsVariation = 0;


            if (string.IsNullOrEmpty(vlocal?.Hash) || ForceHash)
            {
                // try getting the hash from the CrossRef
                if (!ForceHash)
                {
                    List<CrossRef_File_Episode> crossRefs = Repo.CrossRef_File_Episode.GetByFileNameAndSize(filename, filesize);
                    if (crossRefs.Count >= 1)
                    {
                        local.Hash = crossRefs[0].Hash;
                        local.HashSource = (int) HashSource.DirectHash;
                    }
                    else
                    {
                        List<FileNameHash> fnhashes = Repo.FileNameHash.GetByFileNameAndSize(filename, filesize);
                        if (fnhashes != null && fnhashes.Count > 1)
                        {
                            // if we have more than one record it probably means there is some sort of corruption
                            // lets delete the local records
                            foreach (FileNameHash fnh in fnhashes)
                            {
                                Repo.FileNameHash.Delete(fnh.FileNameHashID);
                            }
                        }

                        // reinit this to check if we erased them
                        fnhashes = Repo.FileNameHash.GetByFileNameAndSize(filename, filesize);

                        if (fnhashes != null && fnhashes.Count == 1)
                        {
                            logger.Trace("Got hash from LOCAL cache: {0} ({1})", FileName, fnhashes[0].Hash);
                            local.Hash = fnhashes[0].Hash;
                            local.HashSource = (int) HashSource.WebCacheFileName;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(local.Hash))
                    FillVideoHashes_RA(local);

                //Cloud and no hash, Nothing to do, except maybe Get the mediainfo....
                if (string.IsNullOrEmpty(local.Hash) && folder.CloudID.HasValue)
                {
                    logger.Trace("No Hash found for cloud " + filename + " putting in videolocal table with empty ED2K");
                    using (var vupd = Repo.VideoLocal_Place.BeginAddOrUpdateWithLock(() => Repo.VideoLocal_Place.GetByFilePathAndShareID(filePath, nshareID)))
                    {
                        if (vupd.Original == null)
                        {
                            using (var vlupd = Repo.VideoLocal.BeginAdd(local))
                            {
                                local = vlupd.Commit();
                            }

                            vupd.Entity.FilePath = filePath;
                            vupd.Entity.ImportFolderID = nshareID;
                            vupd.Entity.ImportFolderType = folder.ImportFolderType;
                            vupd.Entity.VideoLocalID = local.VideoLocalID;
                            return vupd.Commit();
                        }
                    }
                }

                // hash the file
                if (string.IsNullOrEmpty(local.Hash) || ForceHash)
                {
                    logger.Info("Hashing File: {0}", FileName);
                    ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
                    DateTime start = DateTime.Now;
                    logger.Trace("Calculating ED2K hashes for: {0}", FileName);
                    // update the VideoLocal record with the Hash, since cloud support we calculate everything
                    var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}"), true, ShokoServer.OnHashProgress, true, true, true);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Trace("Hashed file in {0:#0.0} seconds --- {1} ({2})", ts.TotalSeconds, FileName, Utils.FormatByteSize(vlocal.FileSize));
                    local.Hash = hashes.ED2K?.ToUpperInvariant();
                    local.CRC32 = hashes.CRC32?.ToUpperInvariant();
                    local.MD5 = hashes.MD5?.ToUpperInvariant();
                    local.SHA1 = hashes.SHA1?.ToUpperInvariant();
                    local.HashSource = (int) HashSource.DirectHash;
                }

                FillMissingHashes(local);
                if (vlocalplace != null && ForceHash)
                {
                    using (var vupd = Repo.VideoLocal.BeginUpdate(vlocalplace.VideoLocal))
                    {
                        vupd.Entity.ForceMergeInfoFrom_RA(local);
                        vupd.Commit();
                    }
                }
                else if (vlocalplace == null)
                {
                    using (var vupd = Repo.VideoLocal.BeginAddOrUpdateWithLock(() => Repo.VideoLocal.GetByHashAndSize(local.Hash, filesize), local))
                    {
                        if (vupd.Original != null && ForceHash)
                        {
                            vupd.Entity.ForceMergeInfoFrom_RA(local);
                        }

                        vlocal = vupd.Commit();
                        using (var vlupd = Repo.VideoLocal_Place.BeginAdd())
                        {
                            vlupd.Entity.FilePath = filePath;
                            vlupd.Entity.ImportFolderID = nshareID;
                            vlupd.Entity.ImportFolderType = folder.ImportFolderType;
                            vlupd.Entity.VideoLocalID = vupd.Entity.VideoLocalID;
                            vlocalplace = vlupd.Commit();
                        }
                    }
                }

                bool duplicated = false;
                List<SVR_VideoLocal_Place> preps = vlocal.Places.Where(a => a.ImportFolder.CloudID == folder.CloudID && !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
                foreach (var prep in preps)
                {
                    if (prep == null) continue;
                    // clean up, if there is a 'duplicate file' that is invalid, remove it.
                    if (prep.FullServerPath == null)
                    {
                        Repo.VideoLocal_Place.Delete(prep);
                        preps.Remove(prep);
                    }
                    else
                    {
                        IObject dupFileSystemResult = prep.ImportFolder?.FileSystem?.Resolve(prep.FullServerPath);
                        if (dupFileSystemResult.Status != Status.Ok)
                        {
                            Repo.VideoLocal_Place.Delete(prep);
                            preps.Remove(prep);
                        }
                    }
                }

                foreach (var prep in preps)
                {
                    // delete the VideoLocal record
                    logger.Warn("Found Duplicate File");
                    logger.Warn("---------------------------------------------");
                    logger.Warn($"New File: {vlocalplace.FullServerPath}");
                    logger.Warn($"Existing File: {prep.FullServerPath}");
                    logger.Warn("---------------------------------------------");

                    List<DuplicateFile> dupFiles = Repo.DuplicateFile.GetByFilePathsAndImportFolderCheckBoth(vlocalplace.FilePath, prep.FilePath, vlocalplace.ImportFolderID, prep.ImportFolderID);
                    if (dupFiles.Count == 0)
                    {
                        using (var upd = Repo.DuplicateFile.BeginAdd())
                        {
                            upd.Entity.DateTimeUpdated = DateTime.Now;
                            upd.Entity.FilePathFile1 = vlocalplace.FilePath;
                            upd.Entity.FilePathFile2 = prep.FilePath;
                            upd.Entity.ImportFolderIDFile1 = vlocalplace.ImportFolderID;
                            upd.Entity.ImportFolderIDFile2 = prep.ImportFolderID;
                            upd.Entity.Hash = vlocal.Hash;
                            upd.Commit();
                        }
                    }

                    duplicated = true;
                }

                if (duplicated)
                {
                    CommandRequest_ProcessFile cr_procfile3 = new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
                    cr_procfile3.Save();
                    return vlocalplace;
                }

                // also save the filename to hash record
                // replace the existing records just in case it was corrupt

                using (var fupd = Repo.FileNameHash.BeginAddOrUpdateWithLock(() =>
                {
                    FileNameHash fnhash = null;
                    List<FileNameHash> fnhashes2 = Repo.FileNameHash.GetByFileNameAndSize(vlocal.FileName, vlocal.FileSize);
                    if (fnhashes2.Count > 0)
                    {
                        // if we have more than one record it probably means there is some sort of corruption
                        // lets delete the local records
                        if (fnhashes2.Count > 1)
                        {
                            for (int x = 1; x < fnhashes2.Count; x++)
                                Repo.FileNameHash.Delete(fnhashes2[x]);
                        }

                        fnhash = fnhashes2[2];
                    }

                    return fnhash;
                }))
                {
                    fupd.Entity.FileName = vlocal.FileName;
                    fupd.Entity.FileSize = vlocal.FileSize;
                    fupd.Entity.Hash = vlocal.Hash;
                    fupd.Entity.DateTimeUpdated = DateTime.Now;
                    fupd.Commit();
                }
            }
            else
            {
                FillMissingHashes(vlocal);
            }


            if ((vlocal.Media == null) || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || vlocal.Duration == 0)
            {
                using (var upd = Repo.VideoLocal.BeginUpdate(vlocal))
                {
                    if (vlocalplace.RefreshMediaInfo(upd.Entity))
                        upd.Commit(true);

                }
            }

            // now add a command to process the file
            CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
            cr_procfile.Save();

            return vlocalplace;
        }

        private void FillMissingHashes(SVR_VideoLocal vlocal)
        {
            bool needcrc32 = string.IsNullOrEmpty(vlocal.CRC32);
            bool needmd5 = string.IsNullOrEmpty(vlocal.MD5);
            bool needsha1 = string.IsNullOrEmpty(vlocal.SHA1);
            if (needcrc32 || needmd5 || needsha1)
                FillVideoHashes_RA(vlocal);
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
                Hashes hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}"), true, ShokoServer.OnHashProgress,
                    needcrc32, needmd5, needsha1);
                TimeSpan ts = DateTime.Now - start;
                logger.Trace("Hashed file in {0} seconds --- {1} ({2})", ts.TotalSeconds.ToString("#0.0"),
                    FileName, Utils.FormatByteSize(vlocal.FileSize));
                if (String.IsNullOrEmpty(vlocal.Hash))
                    vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
                if (needsha1)
                    vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
                if (needmd5)
                    vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
                if (needcrc32)
                    vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
                AzureWebAPI.Send_FileHash(new List<SVR_VideoLocal> {vlocal});
            }
        }

        private void FillHashesAgainstVideoLocalRepo(SVR_VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                SVR_VideoLocal n = Repo.VideoLocal.GetByHash(v.ED2KHash);
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
                SVR_VideoLocal n = Repo.VideoLocal.GetBySHA1(v.SHA1);
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
                SVR_VideoLocal n = Repo.VideoLocal.GetByMD5(v.MD5);
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
                SVR_AniDB_File f = Repo.AniDB_File.GetByHash(v.ED2KHash);
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
                SVR_AniDB_File f = Repo.AniDB_File.GetBySHA1(v.SHA1);
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
                SVR_AniDB_File f = Repo.AniDB_File.GetByMD5(v.MD5);
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

        private void FillVideoHashes_RA(SVR_VideoLocal v)
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

        public override bool InitFromDB(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
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
            }

            if (FileName.Trim().Length > 0)
                return true;
            return false;
        }
    }
}
