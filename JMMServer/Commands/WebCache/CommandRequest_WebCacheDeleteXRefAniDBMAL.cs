using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands.WebCache
{
    public class CommandRequest_WebCacheDeleteXRefAniDBMAL : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get {
                return new QueueStateStruct() { queueState = QueueStateEnum.WebCacheDeleteXRefAniDBMAL, extraParams = new string[] { AnimeID.ToString() } };
            }
        }

        public CommandRequest_WebCacheDeleteXRefAniDBMAL()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBMAL(int animeID, int epType, int epNumber)
        {
            this.AnimeID = animeID;
            this.StartEpisodeType = epType;
            this.StartEpisodeNumber = epNumber;
            this.CommandType = (int) CommandRequestType.WebCache_DeleteXRefAniDBMAL;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBMAL(AnimeID, StartEpisodeType, StartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.ErrorException(
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBMAL: {0}" + ex.ToString(), ex);
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBMAL{0}", AnimeID);
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
                this.AnimeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL", "AnimeID"));
                this.StartEpisodeType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL", "StartEpisodeType"));
                this.StartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL",
                        "StartEpisodeNumber"));
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