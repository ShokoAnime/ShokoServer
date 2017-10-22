using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TraktTV;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TraktUpdateInfo : CommandRequestImplementation, ICommandRequest
    {
        public string TraktID { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UpdateTraktData,
            extraParams = new[] {TraktID}
        };

        public CommandRequest_TraktUpdateInfo()
        {
        }

        public CommandRequest_TraktUpdateInfo(string traktID)
        {
            TraktID = traktID;
            CommandType = (int) CommandRequestType.Trakt_UpdateInfo;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }


        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktUpdateInfoAndImages: {0}", TraktID);

            try
            {
                TraktTVHelper.UpdateAllInfo(TraktID, false);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktUpdateInfoAndImages: {0} - {1}", TraktID,
                    ex);
            }
        }


        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TraktUpdateInfoAndImages{TraktID}";
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
                TraktID = TryGetProperty(docCreator, "CommandRequest_TraktUpdateInfoAndImages", "TraktID");
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