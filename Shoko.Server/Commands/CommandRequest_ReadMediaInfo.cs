using System;
using System.Xml;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
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
                SVR_VideoLocal vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
                SVR_VideoLocal_Place place = vlocal?.GetBestVideoLocalPlace();
                if (place==null)
                {
                    logger.Error("Cound not find Video: {0}", VideoLocalID);
                    return;
                }
                if (place.RefreshMediaInfo())
                    RepoFactory.VideoLocal.Save(place.VideoLocal,true);
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
            this.CommandID = $"CommandRequest_ReadMediaInfo_{this.VideoLocalID}";
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