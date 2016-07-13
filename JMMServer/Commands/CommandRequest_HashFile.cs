﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using JMMContracts;
using JMMFileHelper;
using JMMServer.Entities;
using JMMServer.Repositories;

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

            VideoLocal vlocal = null;
            try
            {
                vlocal = ProcessFile_LocalInfo();
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

        private VideoLocal ProcessFile_LocalInfo()
        {
            // hash and read media info for file
            int nshareID = -1;
            string filePath = "";

            ImportFolderRepository repNS = new ImportFolderRepository();
            List<ImportFolder> shares = repNS.GetAll();
            DataAccessHelper.GetShareAndPath(FileName, shares, ref nshareID, ref filePath);

            if (!File.Exists(FileName))
            {
                logger.Error("File does not exist: {0}", FileName);
                return null;
            }

            int numAttempts = 0;
            long filesize = 0;
            // Wait 3 minutes seconds before giving up on trying to access the file
            while ((filesize=CanAccessFile(FileName))==0 && (numAttempts < 180))
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


            // check if we have already processed this file
            VideoLocal vlocal = null;
            VideoLocalRepository repVidLocal = new VideoLocalRepository();
            FileNameHashRepository repFNHash = new FileNameHashRepository();

            List<VideoLocal> vidLocals = repVidLocal.GetByFilePathAndShareID(filePath, nshareID);
            FileInfo fi = new FileInfo(FileName);

            if (vidLocals.Count > 0)
            {
                vlocal = vidLocals[0];
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
                vlocal.FilePath = filePath;
                vlocal.FileSize = filesize;
                vlocal.ImportFolderID = nshareID;
                vlocal.Hash = "";
                vlocal.CRC32 = "";
                vlocal.MD5 = "";
                vlocal.SHA1 = "";
                vlocal.IsIgnored = 0;
                vlocal.IsVariation = 0;
            }

            // check if we need to get a hash this file
            Hashes hashes = null;
            if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
            {
                // try getting the hash from the CrossRef
                if (!ForceHash)
                {
                    CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();
                    List<CrossRef_File_Episode> crossRefs =
                        repCrossRefs.GetByFileNameAndSize(Path.GetFileName(vlocal.FilePath),
                            vlocal.FileSize);
                    if (crossRefs.Count == 1)
                    {
                        vlocal.Hash = crossRefs[0].Hash;
                        vlocal.HashSource = (int) HashSource.DirectHash;
                    }
                }

                // try getting the hash from the LOCAL cache
                if (!ForceHash && string.IsNullOrEmpty(vlocal.Hash))
                {
                    List<FileNameHash> fnhashes = repFNHash.GetByFileNameAndSize(Path.GetFileName(vlocal.FilePath),
                        vlocal.FileSize);
                    if (fnhashes != null && fnhashes.Count > 1)
                    {
                        // if we have more than one record it probably means there is some sort of corruption
                        // lets delete the local records
                        foreach (FileNameHash fnh in fnhashes)
                        {
                            repFNHash.Delete(fnh.FileNameHashID);
                        }
                    }

                    if (fnhashes != null && fnhashes.Count == 1)
                    {
                        logger.Trace("Got hash from LOCAL cache: {0} ({1})", FileName, fnhashes[0].Hash);
                        vlocal.Hash = fnhashes[0].Hash;
                        vlocal.HashSource = (int) HashSource.WebCacheFileName;
                    }
                }

                // hash the file
                if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
                {
                    DateTime start = DateTime.Now;
                    logger.Trace("Calculating hashes for: {0}", FileName);
                    // update the VideoLocal record with the Hash
                    hashes = FileHashHelper.GetHashInfo(FileName, true, MainWindow.OnHashProgress,
                        ServerSettings.Hash_CRC32,
                        ServerSettings.Hash_MD5, ServerSettings.Hash_SHA1);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Trace("Hashed file in {0} seconds --- {1} ({2})", ts.TotalSeconds.ToString("#0.0"), FileName,
                        Utils.FormatByteSize(vlocal.FileSize));

                    vlocal.Hash = hashes.ed2k;
                    vlocal.CRC32 = hashes.crc32;
                    vlocal.MD5 = hashes.md5;
                    vlocal.SHA1 = hashes.sha1;
                    vlocal.HashSource = (int) HashSource.DirectHash;
                }

                // We should have a hash by now
                // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)
                VideoLocal vidTemp = repVidLocal.GetByHash(vlocal.Hash);
                if (vidTemp != null)
                {
                    // don't delete it, if it is actually the same record
                    if (vidTemp.VideoLocalID != vlocal.VideoLocalID)
                    {
                        // delete the VideoLocal record
                        logger.Warn("Deleting duplicate video file record");
                        logger.Warn("---------------------------------------------");
                        logger.Warn("Keeping record for: {0}", vlocal.FullServerPath);
                        logger.Warn("Deleting record for: {0}", vidTemp.FullServerPath);
                        logger.Warn("---------------------------------------------");

                        // check if we have a record of this in the database, if not create one
                        DuplicateFileRepository repDups = new DuplicateFileRepository();
                        List<DuplicateFile> dupFiles = repDups.GetByFilePathsAndImportFolder(vlocal.FilePath,
                            vidTemp.FilePath,
                            vlocal.ImportFolderID, vidTemp.ImportFolderID);
                        if (dupFiles.Count == 0)
                            dupFiles = repDups.GetByFilePathsAndImportFolder(vidTemp.FilePath, vlocal.FilePath,
                                vidTemp.ImportFolderID,
                                vlocal.ImportFolderID);

                        if (dupFiles.Count == 0)
                        {
                            DuplicateFile dup = new DuplicateFile();
                            dup.DateTimeUpdated = DateTime.Now;
                            dup.FilePathFile1 = vlocal.FilePath;
                            dup.FilePathFile2 = vidTemp.FilePath;
                            dup.ImportFolderIDFile1 = vlocal.ImportFolderID;
                            dup.ImportFolderIDFile2 = vidTemp.ImportFolderID;
                            dup.Hash = vlocal.Hash;
                            repDups.Save(dup);
                        }

                        repVidLocal.Delete(vidTemp.VideoLocalID);
                    }
                }

                repVidLocal.Save(vlocal, true);

                // also save the filename to hash record
                // replace the existing records just in case it was corrupt
                FileNameHash fnhash = null;
                List<FileNameHash> fnhashes2 = repFNHash.GetByFileNameAndSize(Path.GetFileName(vlocal.FilePath),
                    vlocal.FileSize);
                if (fnhashes2 != null && fnhashes2.Count > 1)
                {
                    // if we have more than one record it probably means there is some sort of corruption
                    // lets delete the local records
                    foreach (FileNameHash fnh in fnhashes2)
                    {
                        repFNHash.Delete(fnh.FileNameHashID);
                    }
                }

                if (fnhashes2 != null && fnhashes2.Count == 1)
                    fnhash = fnhashes2[0];
                else
                    fnhash = new FileNameHash();

                fnhash.FileName = Path.GetFileName(vlocal.FilePath);
                fnhash.FileSize = vlocal.FileSize;
                fnhash.Hash = vlocal.Hash;
                fnhash.DateTimeUpdated = DateTime.Now;
                repFNHash.Save(fnhash);
            }


            // now check if we have stored a VideoInfo record
            bool refreshMediaInfo = false;

            VideoInfoRepository repVidInfo = new VideoInfoRepository();
            VideoInfo vinfo = repVidInfo.GetByHash(vlocal.Hash);

            if (vinfo == null)
            {
                refreshMediaInfo = true;

                vinfo = new VideoInfo();
                vinfo.Hash = vlocal.Hash;

                vinfo.Duration = 0;
                vinfo.FileSize = fi.Length;
                vinfo.DateTimeUpdated = DateTime.Now;
                vinfo.FileName = filePath;

                vinfo.AudioBitrate = "";
                vinfo.AudioCodec = "";
                vinfo.VideoBitrate = "";
                vinfo.VideoBitDepth = "";
                vinfo.VideoCodec = "";
                vinfo.VideoFrameRate = "";
                vinfo.VideoResolution = "";

                repVidInfo.Save(vinfo);
            }
            else
            {
                // check if we need to update the media info
                if (vinfo.VideoCodec.Trim().Length == 0) refreshMediaInfo = true;
                else refreshMediaInfo = false;
            }


            if (refreshMediaInfo)
            {
                logger.Trace("Getting media info for: {0}", FileName);
                MediaInfoResult mInfo = FileHashHelper.GetMediaInfo(FileName, true);

                vinfo.AudioBitrate = string.IsNullOrEmpty(mInfo.AudioBitrate) ? "" : mInfo.AudioBitrate;
                vinfo.AudioCodec = string.IsNullOrEmpty(mInfo.AudioCodec) ? "" : mInfo.AudioCodec;

                vinfo.DateTimeUpdated = vlocal.DateTimeUpdated;
                vinfo.Duration = mInfo.Duration;
                vinfo.FileName = filePath;
                vinfo.FileSize = fi.Length;

                vinfo.VideoBitrate = string.IsNullOrEmpty(mInfo.VideoBitrate) ? "" : mInfo.VideoBitrate;
                vinfo.VideoBitDepth = string.IsNullOrEmpty(mInfo.VideoBitDepth) ? "" : mInfo.VideoBitDepth;
                vinfo.VideoCodec = string.IsNullOrEmpty(mInfo.VideoCodec) ? "" : mInfo.VideoCodec;
                vinfo.VideoFrameRate = string.IsNullOrEmpty(mInfo.VideoFrameRate) ? "" : mInfo.VideoFrameRate;
                vinfo.VideoResolution = string.IsNullOrEmpty(mInfo.VideoResolution) ? "" : mInfo.VideoResolution;
                vinfo.FullInfo = string.IsNullOrEmpty(mInfo.FullInfo) ? "" : mInfo.FullInfo;
                repVidInfo.Save(vinfo);
            }
            //Resave videolocal, since it do not have media populated (videinfo was not created).
            repVidLocal.Save(vlocal, true);
            // now add a command to process the file
            CommandRequest_ProcessFile cr_procfile = new CommandRequest_ProcessFile(vlocal.VideoLocalID, false);
            cr_procfile.Save();

            return vlocal;
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_HashFile_{0}", this.FileName);
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