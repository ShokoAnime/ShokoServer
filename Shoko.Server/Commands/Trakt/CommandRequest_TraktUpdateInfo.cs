using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TraktTV;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TraktUpdateInfo : CommandRequest
    {
        public virtual string TraktID { get; set; }

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

        public override bool InitFromDB(CommandRequest cq)
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
    }
}