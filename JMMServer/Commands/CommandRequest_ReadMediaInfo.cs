using System;
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
    public class CommandRequest_ReadMediaInfo : CommandRequestImplementation, ICommandRequest
    {
        public int VideoLocalID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority4; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.ReadingMedia, extraParams = new string[] { VideoLocalID.ToString() } };
            }
        }

        public CommandRequest_ReadMediaInfo()
        {
        }

        public CommandRequest_ReadMediaInfo(int vidID)
        {
            this.VideoLocalID = vidID;
            this.CommandType = (int) CommandRequestType.ReadMediaInfo;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Reading Media Info for File: {0}", VideoLocalID);


            try
            {
                VideoLocalRepository repVids = new VideoLocalRepository();
                VideoLocal vlocal = repVids.GetByID(VideoLocalID);
                if (vlocal == null)
                {
                    logger.Error("Cound not find Video: {0}", VideoLocalID);
                    return;
                }

                if (!File.Exists(vlocal.FullServerPath))
                {
                    logger.Error("Cound not find physical file: {0}", vlocal.FullServerPath);
                    return;
                }

                int nshareID = -1;

                VideoInfoRepository repVidInfo = new VideoInfoRepository();
                VideoInfo vinfo = repVidInfo.GetByHash(vlocal.Hash);

                ImportFolderRepository repNS = new ImportFolderRepository();
                List<ImportFolder> shares = repNS.GetAll();

                string fileName = vlocal.FullServerPath;
                string filePath = "";
                DataAccessHelper.GetShareAndPath(fileName, shares, ref nshareID, ref filePath);

                FileInfo fi = new FileInfo(fileName);

                if (vinfo == null)
                {
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
                }


                logger.Trace("Getting media info for: {0}", fileName);
                MediaInfoResult mInfo = FileHashHelper.GetMediaInfo(fileName, true);

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
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_ReadMediaInfo: {0} - {1}", VideoLocalID, ex.ToString());
                return;
            }
        }


        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_ReadMediaInfo_{0}", this.VideoLocalID);
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
                this.VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_ReadMediaInfo", "VideoLocalID"));
            }

            return true;
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