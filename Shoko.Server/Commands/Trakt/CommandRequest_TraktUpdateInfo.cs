using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.Trakt_UpdateInfo)]
    public class CommandRequest_TraktUpdateInfo : CommandRequestImplementation
    {
        public string TraktID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }


        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_TraktUpdateInfo: {0}", TraktID);

            try
            {
                TraktTVHelper.UpdateAllInfo(TraktID);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktUpdateInfo: {0} - {1}", TraktID,
                    ex);
            }
        }


        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TraktUpdateInfo{TraktID}";
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
                TraktID = TryGetProperty(docCreator, "CommandRequest_TraktUpdateInfo", "TraktID");
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