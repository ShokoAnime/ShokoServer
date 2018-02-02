using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_UpdateMyListStats : CommandRequest_AniDBBase
    {
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
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
                using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMylistStats)))
                {
                    upd.Entity.UpdateType = (int)ScheduledUpdateType.AniDBMylistStats;
                    upd.Entity.UpdateDetails = string.Empty;
                    int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyListStats_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - upd.Entity.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }

                ShokoService.AnidbProcessor.UpdateMyListStats();
            }
            catch (Exception ex)
            {
                logger.Error($"Error processing CommandRequest_UpdateMylistStats: {ex}");
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_UpdateMylistStats";
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
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_UpdateMylistStats", "ForceRefresh"));
            }

            return true;
        }
    }
}