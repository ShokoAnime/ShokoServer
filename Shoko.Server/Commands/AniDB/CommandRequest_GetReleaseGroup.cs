using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetReleaseGroup)]
    public class CommandRequest_GetReleaseGroup : CommandRequestImplementation
    {
        public int GroupID { get; set; }
        public bool ForceRefresh { get; set; }

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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_GetReleaseGroup: {0}", GroupID);

            try
            {
                AniDB_ReleaseGroup relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID);

                if (ForceRefresh || relGroup == null)
                {
                    // redownload anime details from http ap so we can get an update character list
                    ShokoService.AniDBProcessor.GetReleaseGroupUDP(GroupID);
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
                GroupID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroup", "GroupID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroup", "ForceRefresh"));
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