using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands.Azure
{
    public class CommandRequest_Azure_SendAnimeTitle : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_Azure_SendAnimeTitle()
        {
        }

        public CommandRequest_Azure_SendAnimeTitle(int animeID, string main, string titles)
        {
            AnimeID = animeID;
            MainTitle = main;
            Titles = titles;
            CommandType = (int)CommandRequestType.Azure_SendAnimeTitle;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public string MainTitle { get; set; }
        public string Titles { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority11; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SendAnimeTitle, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                var thisTitle = new AnimeIDTitle();
                thisTitle.AnimeIDTitleId = 0;
                thisTitle.MainTitle = MainTitle;
                thisTitle.AnimeID = AnimeID;
                thisTitle.Titles = Titles;

                AzureWebAPI.Send_AnimeTitle(thisTitle);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeTitle: {0} - {1}", AnimeID, ex.ToString());
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "AnimeID"));
                MainTitle = TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "MainTitle");
                Titles = TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeTitle", "Titles");
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_Azure_SendAnimeTitle_{0}", AnimeID);
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