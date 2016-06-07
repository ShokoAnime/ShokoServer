using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands.Azure
{
    public class CommandRequest_Azure_SendAnimeFull : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_Azure_SendAnimeFull()
        {
        }

        public CommandRequest_Azure_SendAnimeFull(int animeID)
        {
            AnimeID = animeID;
            CommandType = (int)CommandRequestType.Azure_SendAnimeFull;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SendAnimeFull, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmmtesting", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                var rep = new AniDB_AnimeRepository();
                var anime = rep.GetByAnimeID(AnimeID);
                if (anime == null) return;

                if (anime.AllTags.ToUpper().Contains("18 RESTRICTED")) return;

                AzureWebAPI.Send_AnimeFull(anime);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeFull: {0} - {1}", AnimeID, ex.ToString());
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeFull", "AnimeID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_Azure_SendAnimeFull_{0}", AnimeID);
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