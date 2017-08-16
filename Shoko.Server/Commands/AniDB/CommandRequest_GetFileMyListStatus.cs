using System;
using System.Collections.Generic;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetFileMyListStatus : CommandRequestImplementation, ICommandRequest
    {
        public int AniFileID { get; set; }
        public string FileName { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AniDB_MyListGetFile,
            extraParams = new[] { FileName, AniFileID.ToString() }
        };

        public CommandRequest_GetFileMyListStatus()
        {
        }

        public CommandRequest_GetFileMyListStatus(int aniFileID, string fileName)
        {
            AniFileID = aniFileID;
            FileName = fileName;
            CommandType = (int) CommandRequestType.AniDB_GetMyListFile;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info($"Processing CommandRequest_GetFileMyListStatus: {FileName} ({AniFileID})");


            try
            {
                ShokoService.AnidbProcessor.GetMyListFileStatus(AniFileID);
            }
            catch (Exception ex)
            {
                logger.Error($"Error processing CommandRequest_GetFileMyListStatus: {FileName} ({AniFileID}) - {ex}");
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetFileMyListStatus_{AniFileID}";
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                if (!int.TryParse(TryGetProperty(docCreator, "CommandRequest_GetFileMyListStatus", "AniFileID"),
                    out int ID)) return false;
                AniFileID = ID;

                FileName = TryGetProperty(docCreator, "CommandRequest_GetFileMyListStatus", "FileName");

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