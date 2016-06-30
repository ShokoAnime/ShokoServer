using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.TraktTV;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktUpdateInfoAndImages : CommandRequestImplementation, ICommandRequest
    {
        public string TraktID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.UpdateTraktData, extraParams = new string[] { TraktID.ToString() } };
            }
        }

        public CommandRequest_TraktUpdateInfoAndImages()
        {
        }

        public CommandRequest_TraktUpdateInfoAndImages(string traktID)
        {
            this.TraktID = traktID;
            this.CommandType = (int) CommandRequestType.Trakt_UpdateInfoImages;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }


        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktUpdateInfoAndImages: {0}", TraktID);

            try
            {
                TraktTVHelper.UpdateAllInfoAndImages(TraktID, false);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktUpdateInfoAndImages: {0} - {1}", TraktID,
                    ex.ToString());
                return;
            }
        }


        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_TraktUpdateInfoAndImages{0}", this.TraktID);
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
                this.TraktID = TryGetProperty(docCreator, "CommandRequest_TraktUpdateInfoAndImages", "TraktID");
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