using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands.MAL
{
    [Serializable]
    public class CommandRequest_MALUploadStatusToMAL : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_MALUploadStatusToMAL()
        {
            CommandType = (int)CommandRequestType.MAL_UploadWatchedStates;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_UploadMALWatched);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MALUploadStatusToMAL");

            try
            {
                if (string.IsNullOrEmpty(ServerSettings.MAL_Username) ||
                    string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    return;

                // find the latest eps to update
                var repAnime = new AniDB_AnimeRepository();
                var animes = repAnime.GetAll();

                foreach (var anime in animes)
                {
                    var cmd = new CommandRequest_MALUpdatedWatchedStatus(anime.AnimeID);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALUploadStatusToMAL: {0}", ex.ToString());
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
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_MALUploadStatusToMAL";
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