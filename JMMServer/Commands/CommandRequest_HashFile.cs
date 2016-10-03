using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using JMMContracts;
using JMMServer.Entities;
using JMMServer.FileHelper;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using Path = Pri.LongPath.Path;
using File = Pri.LongPath.File;
using FileInfo = Pri.LongPath.FileInfo;
using NutzCode.CloudFileSystem;
using CrossRef_File_Episode = JMMServer.Entities.CrossRef_File_Episode;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_HashFile : CommandRequestImplementation, ICommandRequest
    {
        public string FileName { get; set; }
        public bool ForceHash { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority4; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.HashingFile, extraParams = new string[] { FileName }  };
            }
        }

        public CommandRequest_HashFile()
        {
        }

        public CommandRequest_HashFile(string filename, bool force)
        {
            this.FileName = filename;
            this.ForceHash = force;
            this.CommandType = (int) CommandRequestType.HashFile;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Hashing File: {0}", FileName);

            try
            {
                ProcessFile_LocalInfo();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_ProcessFile: {0} - {1}", FileName, ex.ToString());
                return;
            }
        }

        //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
        private long CanAccessFile(string fileName)
        {
            try
            {
                using (FileStream fs = File.Open(fileName,FileMode.Open,FileAccess.Read,FileShare.None))
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

        private VideoLocal_Place ProcessFile_LocalInfo()
        {
            // hash and read media info for file
            int nshareID = -1;
            string filePath = "";


            Tuple<ImportFolder, string> tup = VideoLocal_PlaceRepository.GetFromFullPath(FileName);
            if (tup == null)
            {
                logger.Error($"Unable to locate file {FileName} inside the import folders");
                return null;
            }
            ImportFolder folder = tup.Item1;
            filePath = tup.Item2;
            IFileSystem f = tup.Item1.FileSystem;
            if (f == null)
            {
                logger.Error("Unable to open filesystem for : {0}", FileName);
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
    
                // Wait 3 minutes seconds before giving up on trying to access the file
                while ((filesize = CanAccessFile(FileName)) == 0 && (numAttempts < 180))
                {
                    numAttempts++;
                    Thread.Sleep(1000);
                    Console.WriteLine("Attempt # " + numAttempts.ToString());
                }

                // if we failed to access the file, get ouuta here
                if (numAttempts == 180)
                {
                    logger.Error("Could not access file: " + FileName);
                    return null;
                }
            }


            FileSystemResult<IObject> source = f.Resolve(FileName);
            if (source == null || !source.IsOk || (!(source.Result is IFile)))
            {
                logger.Error("Could not access file: " + FileName);
                return null;
            }
            IFile source_file = (IFile) source.Result;
            if (folder.CloudID.HasValue)
                filesize = source_file.Size;
            nshareID = folder.ImportFolderID;
            // check if we have already processed this file

         
            VideoLocal_Place vlocalplace = RepoFactory.VideoLocalPlace.GetByFilePathAndShareID(filePath, nshareID);
            VideoLocal vlocal;

            if (vlocalplace!=null)
            {
                vlocal = vlocalplace.VideoLocal;
                logger.Trace("VideoLocal record found in database: {0}", vlocal.VideoLocalID);

                if (ForceHash)
                {
                    vlocal.FileSize = filesize;
                    vlocal.DateTimeUpdated = DateTime.Now;
                }
            }
            else
            {                
                logger.Trace("VideoLocal, creating new record");
                vlocal = new VideoLocal();
                vlocal.DateTimeUpdated = DateTime.Now;
                vlocal.DateTimeCreated = vlocal.DateTimeUpdated;
                vlocal.FileName = Path.GetFileName(filePath);
                vlocal.FileSize = filesize;
                vlocal.Hash = string.Empty;
                vlocal.CRC32 = string.Empty;
                vlocal.MD5 = source_file.MD5.ToUpperInvariant() ?? string.Empty;
                vlocal.SHA1 = source_file.SHA1.ToUpperInvariant() ?? string.Empty;
                vlocal.IsIgnored = 0;
                vlocal.IsVariation = 0;
                vlocalplace=new VideoLocal_Place();
                vlocalplace.FilePath = filePath;
                vlocalplace.ImportFolderID = nshareID;
                vlocalplace.ImportFolderType = folder.ImportFolderType;
            }

            // check if we need to get a hash this file
            Hashes hashes = null;
            if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
            {
                // try getting the hash from the CrossRef
                if (!ForceHash)
                {
                    List<CrossRef_File_Episode> crossRefs = RepoFactory.CrossRef_File_Episode.GetByFileNameAndSize(vlocal.FileName,vlocal.FileSize);
                    if (crossRefs.Count == 1)
                    {
                        vlocal.Hash = crossRefs[0].Hash;
                        vlocal.HashSource = (int) HashSource.DirectHash;
                    }
                }

                // try getting the hash from the LOCAL cache
                if (!ForceHash && string.IsNullOrEmpty(vlocal.Hash))
                {
                    List<FileNameHash> fnhashes = RepoFactory.FileNameHash.GetByFileNameAndSize(vlocal.FileName,vlocal.FileSize);
                    if (fnhashes != null && fnhashes.Count > 1)
                    {
                        // if we have more than one record it probably means there is some sort of corruption
                        // lets delete the local records
                        foreach (FileNameHash fnh in fnhashes)
                        {
                            RepoFactory.FileNameHash.Delete(fnh.FileNameHashID);
                        }
                    }

                    if (fnhashes != null && fnhashes.Count == 1)
                    {
                        logger.Trace("Got hash from LOCAL cache: {0} ({1})", FileName, fnhashes[0].Hash);
                        vlocal.Hash = fnhashes[0].Hash;
                        vlocal.HashSource = (int) HashSource.WebCacheFileName;
                    }
                }
                if (string.IsNullOrEmpty(vlocal.Hash))
                    FillVideoHashes(vlocal);
                if (string.IsNullOrEmpty(vlocal.Hash) && folder.CloudID.HasValue)
                {
                    //Cloud and no hash, Nothing to do, except maybe Get the mediainfo....

                    RepoFactory.VideoLocal.Save(vlocal,false);
                    vlocalplace.VideoLocalID = vlocal.VideoLocalID;
                    RepoFactory.VideoLocalPlace.Save(vlocalplace);
                    if (vlocalplace.RefreshMediaInfo())
                        RepoFactory.VideoLocal.Save(vlocalplace.VideoLocal, true);
                    return vlocalplace;
                }
                // hash the file
                if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
                {
                    DateTime start = DateTime.Now;
                    logger.Trace("Calculating ED2K hashes for: {0}", FileName);
                    // update the VideoLocal record with the Hash, since cloud support we calculate everything
                    hashes = FileHashHelper.GetHashInfo(FileName.Replace("/","\\"), true, MainWindow.OnHashProgress, false, false, false);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Trace("Hashed file in {0} seconds --- {1} ({2})", ts.TotalSeconds.ToString("#0.0"), FileName, Utils.FormatByteSize(vlocal.FileSize));
                    vlocal.Hash = hashes.ed2k?.ToUpperInvariant();
                    vlocal.CRC32 = hashes.crc32?.ToUpperInvariant();
                    vlocal.MD5 = hashes.md5?.ToUpperInvariant();
                    vlocal.SHA1 = hashes.sha1?.ToUpperInvariant();
                    vlocal.HashSource = (int) HashSource.DirectHash;
                    FillVideoHashes(vlocal);
                    bool needcrc32 = string.IsNullOrEmpty(vlocal.CRC32);
                    bool needmd5 = string.IsNullOrEmpty(vlocal.MD5);
                    bool needsha1 = string.IsNullOrEmpty(vlocal.SHA1);
                    if (needcrc32 || needmd5 || needsha1)
                    {
                        List<string> tp = new List<string>();
                        if (needsha1)
                            tp.Add("SHA1");
                        if (needmd5)
                            tp.Add("MD5");
                        if (needcrc32)
                            tp.Add("CRC32");
                        logger.Trace("Calculating missing {1} hashes for: {0}", FileName, string.Join(",", tp));
                        // update the VideoLocal record with the Hash, since cloud support we calculate everything
                        hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", "\\"), true, MainWindow.OnHashProgress,
                            needcrc32, needmd5, needsha1);
                        ts = DateTime.Now - start;
                        logger.Trace("Hashed file in {0} seconds --- {1} ({2})", ts.TotalSeconds.ToString("#0.0"),
                            FileName, Utils.FormatByteSize(vlocal.FileSize));
                        if (needsha1)
                            vlocal.SHA1 = hashes.sha1?.ToUpperInvariant();
                        if (needmd5)
                            vlocal.MD5 = hashes.md5?.ToUpperInvariant();
                        if (needcrc32)
                            vlocal.CRC32 = hashes.crc32?.ToUpperInvariant();
                        AzureWebAPI.Send_FileHash(new List<VideoLocal> {vlocal});
                    }
                }

                // We should have a hash by now
                // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)

                VideoLocal tlocal = RepoFactory.VideoLocal.GetByHash(vlocal.Hash);

                bool intercloudfolder = false;
                VideoLocal_Place prep= tlocal?.Places.FirstOrDefault(a => a.ImportFolder.CloudID == folder.CloudID && vlocalplace.VideoLocal_Place_ID != a.VideoLocal_Place_ID);
                if (prep!=null)
                {
                    // delete the VideoLocal record
                    logger.Warn("Deleting duplicate video file record");
                    logger.Warn("---------------------------------------------");
                    logger.Warn($"Keeping record for: {vlocalplace.FullServerPath}");
                    logger.Warn($"Deleting record for: {prep.FullServerPath}");
                    logger.Warn("---------------------------------------------");

                    // check if we have a record of this in the database, if not create one
                    List<DuplicateFile> dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(vlocalplace.FilePath,
                        prep.FilePath,
                        vlocalplace.ImportFolderID, prep.ImportFolderID);
                    if (dupFiles.Count == 0)
                        dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(prep.FilePath, vlocalplace.FilePath, prep.ImportFolderID, vlocalplace.ImportFolderID);

                    if (dupFiles.Count == 0)
                    {
                        DuplicateFile dup = new DuplicateFile();
                        dup.DateTimeUpdated = DateTime.Now;
                        dup.FilePathFile1 = vlocalplace.FilePath;
                        dup.FilePathFile2 = prep.FilePath;
                        dup.ImportFolderIDFile1 = vlocalplace.ImportFolderID;
                        dup.ImportFolderIDFile2 = prep.ImportFolderID;
                        dup.Hash = vlocal.Hash;
                        RepoFactory.DuplicateFile.Save(dup);
                    }
                    //Notify duplicate, don't delete
                }
                else if (tlocal != null)
                {
                    vlocal = tlocal;
                    intercloudfolder = true;
                }


                if (!intercloudfolder)
                    RepoFactory.VideoLocal.Save(vlocal, true);

                vlocalplace.VideoLocalID = vlocal.VideoLocalID;
                RepoFactory.VideoLocalPlace.Save(vlocalplace);

                if (intercloudfolder)
                    return vlocalplace;

                // also save the filename to hash record
                // replace the existing records just in case it was corrupt
                FileNameHash fnhash = null;
                List<FileNameHash> fnhashes2 = RepoFactory.FileNameHash.GetByFileNameAndSize(vlocal.FileName,vlocal.FileSize);
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

                fnhash.FileName = vlocal.FileName;
                fnhash.FileSize = vlocal.FileSize;
                fnhash.Hash = vlocal.Hash;
                fnhash.DateTimeUpdated = DateTime.Now;
                RepoFactory.FileNameHash.Save(fnhash);

            }


            if ((vlocal.Media == null) || vlocal.MediaVersion < VideoLocal.MEDIA_VERSION || vlocal.Duration==0)
            {
                if (vlocalplace.RefreshMediaInfo()) 
                    RepoFactory.VideoLocal.Save(vlocalplace.VideoLocal,true);
            }
            // now add a command to process the file
            CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
            cr_procfile.Save();

            return vlocalplace;
        }

        private void FillVideoHashes(VideoLocal v)
        {
            if (!string.IsNullOrEmpty(v.ED2KHash))
            {
                VideoLocal n = RepoFactory.VideoLocal.GetByHash(v.ED2KHash);
                if (n != null)
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                    v.MD5 = n.MD5.ToUpperInvariant();
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                    return;
                }
                AniDB_File f = RepoFactory.AniDB_File.GetByHash(v.ED2KHash);
                if (f != null)
                {
                    v.CRC32 = f.CRC.ToUpperInvariant();
                    v.SHA1 = f.SHA1.ToUpperInvariant();
                    v.MD5 = f.MD5.ToUpperInvariant();
                    return;
                }
                List<FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.ED2K, v.ED2KHash) ?? new List<FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) && !string.IsNullOrEmpty(a.SHA1)).ToList();
                if (ls.Count > 0)
                {
                    v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                    v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    v.MD5 = ls[0].MD5.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.SHA1))
            {
                VideoLocal n = RepoFactory.VideoLocal.GetBySHA1(v.SHA1);
                if (n != null)
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                    v.MD5 = n.MD5.ToUpperInvariant();
                    v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                    return;
                }
                AniDB_File f = RepoFactory.AniDB_File.GetBySHA1(v.SHA1);
                if (f != null)
                {
                    v.CRC32 = f.CRC.ToUpperInvariant();
                    v.ED2KHash = f.Hash.ToUpperInvariant();
                    v.MD5 = f.MD5.ToUpperInvariant();
                    return;
                }
                List<FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.SHA1, v.SHA1) ?? new List<FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) && !string.IsNullOrEmpty(a.ED2K)).ToList();
                if (ls.Count > 0)
                {
                    v.ED2KHash = ls[0].ED2K.ToUpperInvariant();
                    v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    v.MD5 = ls[0].MD5.ToUpperInvariant();
                    return;
                }
            }
            if (!string.IsNullOrEmpty(v.MD5))
            {
                VideoLocal n = RepoFactory.VideoLocal.GetByMD5(v.MD5);
                if (n != null)
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                    v.ED2KHash = n.ED2KHash.ToUpperInvariant();
                    return;
                }
                AniDB_File f = RepoFactory.AniDB_File.GetByMD5(v.MD5);
                if (f != null)
                {
                    v.CRC32 = f.CRC.ToUpperInvariant();
                    v.ED2KHash = f.Hash.ToUpperInvariant();
                    v.SHA1 = f.SHA1.ToUpperInvariant();
                    return;
                }
                List<FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.MD5, v.MD5) ?? new List<FileHash>();
                ls = ls.Where(a => !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.SHA1) && !string.IsNullOrEmpty(a.ED2K)).ToList();
                if (ls.Count > 0)
                {
                    v.ED2KHash = ls[0].ED2K.ToUpperInvariant();
                    v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                    v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                }
            }
        }


        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = $"CommandRequest_HashFile_{FileName}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.FileName = TryGetProperty(docCreator, "CommandRequest_HashFile", "FileName");
                this.ForceHash = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "ForceHash"));
            }

            if (this.FileName.Trim().Length > 0)
                return true;
            else
                return false;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}