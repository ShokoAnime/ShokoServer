using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Shoko.Server.Workers.Commands.Hashing;
using Shoko.Server.Workers.WorkUnits.Hashing;



namespace Shoko.Server.Workers.Commands.Import
{
    public class CMD_HashVideoLocalPlace : CMD_HashFile
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

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
        public override async Task<WorkResult<HashFile>> RunAsync(HashFile workunit, IProgress<IWorkProgress<HashFile>> progress = null, CancellationToken token = default(CancellationToken))
        {
            //TODO WIP, first (database refactor)
            throw new NotImplementedException();
            /*
            int nshareID = -1;
            string filePath = string.Empty;


            Tuple<SVR_ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(workunit.File.FullName);
            if (tup == null)
                return new WorkResult<HashFile>(WorkResultStatus.Error, logger, $"Unable to locate Import Folder for {workunit.File.FullName}");
            SVR_ImportFolder folder = tup.Item1;
            filePath = tup.Item2;
            IFileSystem f = tup.Item1.FileSystem;
            if (f == null)
                return new WorkResult<HashFile>(WorkResultStatus.Error, logger, $"Unable to open filesystem for: {workunit.File.FullName}");
            long filesize = 0;
            if (folder.CloudID == null) // Local Access
            {
                if (!File.Exists(workunit.File.FullName))
                    return new WorkResult<HashFile>(WorkResultStatus.Error, logger, $"File does not exist: {workunit.File.FullName}");
                int numAttempts = 0;

                // Wait 1 minute seconds before giving up on trying to access the file
                while ((filesize = CanAccessFile(workunit.File.FullName)) == 0 && (numAttempts < 10))
                {
                    numAttempts++;
                    await Task.Delay(6000, token);
                }

                // if we failed to access the file, get ouuta here
                if (numAttempts >= 60)
                    return new WorkResult<HashFile>(WorkResultStatus.Error, logger, $"Could not access file: {workunit.File.FullName}");
            }

            IFile source_file = workunit.File;
            if (folder.CloudID.HasValue)
                filesize = source_file.Size;
            nshareID = folder.ImportFolderID;
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
                if (vlocal != null && workunit.Force)
                {
                    vlocal.FileSize = filesize;
                    vlocal.DateTimeUpdated = DateTime.Now;
                }
            }

            if (vlocal == null)
            {
                logger.Trace("VideoLocal, creating temporary record");
                vlocal = new SVR_VideoLocal
                {
                    DateTimeUpdated = DateTime.Now,
                    DateTimeCreated = DateTime.Now,
                    FileName = Path.GetFileName(filePath),
                    FileSize = filesize,
                    Hash = string.Empty,
                    CRC32 = string.Empty,
                    MD5 = source_file?.MD5?.ToUpperInvariant() ?? string.Empty,
                    SHA1 = source_file?.SHA1?.ToUpperInvariant() ?? string.Empty,
                    IsIgnored = 0,
                    IsVariation = 0
                };
            }

            if (vlocalplace == null)
            {
                vlocalplace = new SVR_VideoLocal_Place
                {
                    FilePath = filePath,
                    ImportFolderID = nshareID,
                    ImportFolderType = folder.ImportFolderType
                };
                // Make sure we have an ID
                Repo.VideoLocal_Place.Save(vlocalplace);
            }
            // check if we need to get a hash this file
            if (string.IsNullOrEmpty(vlocal.Hash) || workunit.Force)
            {
                // try getting the hash from the CrossRef
                if (!workunit.Force)
                {
                    List<CrossRef_File_Episode> crossRefs =
                        Repo.CrossRef_File_Episode.GetByFileNameAndSize(vlocal.FileName, vlocal.FileSize);
                    if (crossRefs.Count == 1)
                    {
                        vlocal.Hash = crossRefs[0].Hash;
                        vlocal.HashSource = (int)HashSource.DirectHash;
                    }
                }

                // try getting the hash from the LOCAL cache
                if (!workunit.Force && string.IsNullOrEmpty(vlocal.Hash))
                {
                    List<FileNameHash> fnhashes =
                        Repo.FileNameHash.GetByFileNameAndSize(vlocal.FileName, vlocal.FileSize);
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
                    fnhashes = Repo.FileNameHash.GetByFileNameAndSize(vlocal.FileName, vlocal.FileSize);

                    if (fnhashes != null && fnhashes.Count == 1)
                    {
                        logger.Trace("Got hash from LOCAL cache: {0} ({1})", workunit.File.FullName, fnhashes[0].Hash);
                        vlocal.Hash = fnhashes[0].Hash;
                        vlocal.HashSource = (int)HashSource.WebCacheFileName;
                    }
                }
                if (string.IsNullOrEmpty(vlocal.Hash))
                    FillVideoHashes(vlocal);

                //Cloud and no hash, Nothing to do, except maybe Get the mediainfo....
                if (string.IsNullOrEmpty(vlocal.Hash) && folder.CloudID.HasValue)
                {
                    logger.Trace("No Hash found for cloud " + vlocal.FileName +
                                 " putting in videolocal table with empty ED2K");
                    Repo.VideoLocal.Save(vlocal, false);
                    vlocalplace.VideoLocalID = vlocal.VideoLocalID;
                    Repo.VideoLocal_Place.Save(vlocalplace);
                    if (vlocalplace.RefreshMediaInfo())
                        Repo.VideoLocal.Save(vlocalplace.VideoLocal, true);
                    return new WorkResult<HashFile>(workunit);
                }

                // hash the file
                if (string.IsNullOrEmpty(vlocal.Hash) || workunit.Force)
                {
                    logger.Info("Hashing File: {0}", FileName);
                    ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
                    DateTime start = DateTime.Now;
                    logger.Trace("Calculating ED2K hashes for: {0}", FileName);
                    // update the VideoLocal record with the Hash, since cloud support we calculate everything
                    var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{System.IO.Path.DirectorySeparatorChar}"), true, ShokoServer.OnHashProgress,
                        true, true, true);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Trace("Hashed file in {0:#0.0} seconds --- {1} ({2})", ts.TotalSeconds, FileName,
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

                SVR_VideoLocal tlocal = Repo.VideoLocal.GetByHash(vlocal.Hash);
                bool duplicate = false;
                bool changed = false;

                if (tlocal != null)
                {
                    // Aid with hashing cloud. Merge hashes and save, regardless of duplicate file
                    changed = tlocal.MergeInfoFrom(vlocal);
                    vlocal = tlocal;

                    List<SVR_VideoLocal_Place> preps = vlocal.Places.Where(
                        a => a.ImportFolder.CloudID == folder.CloudID &&
                             !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
                    foreach (var prep in preps)
                    {
                        if (prep == null) continue;
                        // clean up, if there is a 'duplicate file' that is invalid, remove it.
                        if (prep.FullServerPath == null)
                        {
                            Repo.VideoLocal_Place.Delete(prep);
                        }
                        else
                        {
                            FileSystemResult dupFileSystemResult =
                                prep.ImportFolder?.FileSystem?.Resolve(prep.FullServerPath);
                            if (dupFileSystemResult == null || !dupFileSystemResult.IsOk)
                                Repo.VideoLocal_Place.Delete(prep);
                        }
                    }

                    var dupPlace = vlocal.Places.FirstOrDefault(
                        a => a.ImportFolder.CloudID == folder.CloudID &&
                             !vlocalplace.FullServerPath.Equals(a.FullServerPath));

                    if (dupPlace != null)
                    {
                        // delete the VideoLocal record
                        logger.Warn("Found Duplicate File");
                        logger.Warn("---------------------------------------------");
                        logger.Warn($"New File: {vlocalplace.FullServerPath}");
                        logger.Warn($"Existing File: {dupPlace.FullServerPath}");
                        logger.Warn("---------------------------------------------");

                        // check if we have a record of this in the database, if not create one
                        List<DuplicateFile> dupFiles = Repo.DuplicateFile.GetByFilePathsAndImportFolder(
                            vlocalplace.FilePath,
                            dupPlace.FilePath,
                            vlocalplace.ImportFolderID, dupPlace.ImportFolderID);
                        if (dupFiles.Count == 0)
                            dupFiles = Repo.DuplicateFile.GetByFilePathsAndImportFolder(dupPlace.FilePath,
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
                            Repo.DuplicateFile.Save(dup);
                        }
                        //Notify duplicate, don't delete
                        duplicate = true;
                    }
                }

                if (!duplicate || changed)
                    Repo.VideoLocal.Save(vlocal, true);

                vlocalplace.VideoLocalID = vlocal.VideoLocalID;
                Repo.VideoLocal_Place.Save(vlocalplace);

                if (duplicate)
                {
                    CommandRequest_ProcessFile cr_procfile3 =
                        new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
                    cr_procfile3.Save();
                    return vlocalplace;
                }

                // also save the filename to hash record
                // replace the existing records just in case it was corrupt
                FileNameHash fnhash;
                List<FileNameHash> fnhashes2 =
                    Repo.FileNameHash.GetByFileNameAndSize(vlocal.FileName, vlocal.FileSize);
                if (fnhashes2 != null && fnhashes2.Count > 1)
                {
                    // if we have more than one record it probably means there is some sort of corruption
                    // lets delete the local records
                    foreach (FileNameHash fnh in fnhashes2)
                    {
                        Repo.FileNameHash.Delete(fnh.FileNameHashID);
                    }
                }

                if (fnhashes2 != null && fnhashes2.Count == 1)
                    fnhash = fnhashes2[0];
                else
                    fnhash = new FileNameHash();

                fnhash.FileName = vlocal.FileName;
                fnhash.FileSize = vlocal.FileSize;
                fnhash.Hash = vlocal.Hash;
                fnhash.DateTimeUpdated = DateTime.Now;
                Repo.FileNameHash.Save(fnhash);
            }
            else
            {
                FillMissingHashes(vlocal);
            }


            if ((vlocal.Media == null) || vlocal.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || vlocal.Duration == 0)
            {
                if (vlocalplace.RefreshMediaInfo())
                    Repo.VideoLocal.Save(vlocalplace.VideoLocal, true);
            }
            // now add a command to process the file
            CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
            cr_procfile.Save();

            return vlocalplace;        */
        }
        /*
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
                AzureWebAPI.Send_FileHash(new List<SVR_VideoLocal> { vlocal });
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

        private void FillVideoHashes(SVR_VideoLocal v)
        {
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstVideoLocalRepo(v);
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstAniDBRepo(v);
            if (string.IsNullOrEmpty(v.CRC32) || string.IsNullOrEmpty(v.MD5) || string.IsNullOrEmpty(v.SHA1))
                FillHashesAgainstWebCache(v);
        }
        */
    }
}
