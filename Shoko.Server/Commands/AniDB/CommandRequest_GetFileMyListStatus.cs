using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetMyListFile)]
    public class CommandRequest_GetFileMyListStatus : CommandRequestImplementation
    {
        public int AniFileID { get; set; }
        public string FileName { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info($"Processing CommandRequest_GetFileMyListStatus: {FileName} ({AniFileID})");


            try
            {
                ShokoService.AniDBProcessor.GetMyListFileStatus(AniFileID);
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