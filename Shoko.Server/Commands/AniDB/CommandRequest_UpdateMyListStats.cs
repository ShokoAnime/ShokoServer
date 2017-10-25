using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    public class CommandRequest_UpdateMyListStats : CommandRequestImplementation, ICommandRequest
    {
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UpdateMyListStats,
            extraParams = new string[0]
        };

        public CommandRequest_UpdateMyListStats()
        {
        }

        public CommandRequest_UpdateMyListStats(bool forced)
        {
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.AniDB_UpdateMylistStats;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_UpdateMylistStats");

            try
            {
                // we will always assume that an anime was downloaded via http first

                ScheduledUpdate sched =
                    RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMylistStats);
                if (sched == null)
                {
                    sched = new ScheduledUpdate
                    {
                        UpdateType = (int)ScheduledUpdateType.AniDBMylistStats,
                        UpdateDetails = string.Empty
                    };
                }
                else
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyListStats_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                sched.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(sched);

                ShokoService.AnidbProcessor.UpdateMyListStats();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_UpdateMylistStats: {0}", ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_UpdateMylistStats";
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
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_UpdateMylistStats", "ForceRefresh"));
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