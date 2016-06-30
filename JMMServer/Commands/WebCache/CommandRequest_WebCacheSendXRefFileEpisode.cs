using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheSendXRefFileEpisode : CommandRequestImplementation, ICommandRequest
    {
        public int CrossRef_File_EpisodeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.WebCacheSendXRefFileEpisode, extraParams = new string[] { CrossRef_File_EpisodeID.ToString() } };  
            }
        }

        public CommandRequest_WebCacheSendXRefFileEpisode()
        {
        }

        public CommandRequest_WebCacheSendXRefFileEpisode(int crossRef_File_EpisodeID)
        {
            this.CrossRef_File_EpisodeID = crossRef_File_EpisodeID;
            this.CommandType = (int) CommandRequestType.WebCache_SendXRefFileEpisode;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                CrossRef_File_EpisodeRepository repVids = new CrossRef_File_EpisodeRepository();
                CrossRef_File_Episode xref = repVids.GetByID(CrossRef_File_EpisodeID);
                if (xref == null) return;

                JMMServer.Providers.Azure.AzureWebAPI.Send_CrossRefFileEpisode(xref);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_WebCacheSendXRefFileEpisode: {0} - {1}",
                    CrossRef_File_EpisodeID,
                    ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_WebCacheSendXRefFileEpisode{0}", this.CrossRef_File_EpisodeID);
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
                this.CrossRef_File_EpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefFileEpisode",
                        "CrossRef_File_EpisodeID"));
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