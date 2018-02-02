using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetFileMyListStatus : CommandRequest_AniDBBase
    {
        public virtual int AniFileID { get; set; }
        public virtual string FileName { get; set; }

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

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
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
    }
}