using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetReleaseGroup : CommandRequest_AniDBBase
    {
        public virtual int GroupID { get; set; }
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GetReleaseInfo,
            extraParams = new[] {GroupID.ToString()}
        };

        public CommandRequest_GetReleaseGroup()
        {
        }

        public CommandRequest_GetReleaseGroup(int grpid, bool forced)
        {
            GroupID = grpid;
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.AniDB_GetReleaseGroup;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetReleaseGroup: {0}", GroupID);

            try
            {
                AniDB_ReleaseGroup relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID);

                if (ForceRefresh || relGroup == null)
                {
                    // redownload anime details from http ap so we can get an update character list
                    ShokoService.AnidbProcessor.GetReleaseGroupUDP(GroupID);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetReleaseGroup: {0} - {1}", GroupID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetReleaseGroup_{GroupID}";
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
                GroupID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroup", "GroupID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroup", "ForceRefresh"));
            }

            return true;
        }
    }
}