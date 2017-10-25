using System;
using System.Collections.Generic;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.MAL
{
    [Serializable]
    [Command(CommandRequestType.MAL_UploadWatchedStates)]
    public class CommandRequest_MALUploadStatusToMAL : CommandRequestImplementation
    {
        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UploadMALWatched,
            extraParams = new string[0]
        };


        public CommandRequest_MALUploadStatusToMAL()
        {
            Priority = (int) DefaultPriority;

            GenerateCommandID();
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
                IReadOnlyList<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetAll();

                foreach (SVR_AniDB_Anime anime in animes)
                {
                    CommandRequest_MALUpdatedWatchedStatus cmd =
                        new CommandRequest_MALUpdatedWatchedStatus(anime.AnimeID);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALUploadStatusToMAL: {0}", ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_MALUploadStatusToMAL";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}