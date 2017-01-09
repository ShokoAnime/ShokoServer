using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Providers.Azure;
using Shoko.Models.Azure;
using Shoko.Models.Server;

namespace JMMServer.Commands.Azure
{
    public class CommandRequest_Azure_SendAnimeTitle : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public string MainTitle { get; set; }
        public string Titles { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority11; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.SendAnimeTitle, extraParams = new string[] { AnimeID.ToString() } };
            }
        }

        public CommandRequest_Azure_SendAnimeTitle()
        {
        }

        public CommandRequest_Azure_SendAnimeTitle(int animeID, string main, string titles)
        {
            this.AnimeID = animeID;
            this.MainTitle = main;
            this.Titles = titles;
            this.CommandType = (int) CommandRequestType.Azure_SendAnimeTitle;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                bool process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                Azure_AnimeIDTitle thisTitle = new Azure_AnimeIDTitle();
                thisTitle.AnimeIDTitleId = 0;
                thisTitle.MainTitle = MainTitle;
                thisTitle.AnimeID = AnimeID;
                thisTitle.Titles = Titles;

                AzureWebAPI.Send_AnimeTitle(thisTitle);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeTitle: {0} - {1}", AnimeID, ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_Azure_SendAnimeTitle_{0}", this.AnimeID);
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
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "AnimeID"));
                this.MainTitle = TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "MainTitle");
                this.Titles = TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "Titles");
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