using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using JMMFileHelper;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_ReadMediaInfo : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_ReadMediaInfo()
        {
        }

        public CommandRequest_ReadMediaInfo(int vidID)
        {
            VideoLocalID = vidID;
            CommandType = (int)CommandRequestType.ReadMediaInfo;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int VideoLocalID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority4; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_ReadingMedia, VideoLocalID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Reading Media Info for File: {0}", VideoLocalID);


            try
            {
                var repVids = new VideoLocalRepository();
                var vlocal = repVids.GetByID(VideoLocalID);
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

                var nshareID = -1;

                var repVidInfo = new VideoInfoRepository();
                var vinfo = repVidInfo.GetByHash(vlocal.Hash);

                var repNS = new ImportFolderRepository();
                var shares = repNS.GetAll();

                var fileName = vlocal.FullServerPath;
                var filePath = "";
                DataAccessHelper.GetShareAndPath(fileName, shares, ref nshareID, ref filePath);

                var fi = new FileInfo(fileName);

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
                var mInfo = FileHashHelper.GetMediaInfo(fileName, true);

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
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_ReadMediaInfo", "VideoLocalID"));
            }

            return true;
        }


        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_ReadMediaInfo_{0}", VideoLocalID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}