using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.Trakt_SyncCollection)]
    public class CommandRequest_TraktSyncCollection : CommandRequestImplementation
    {
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.SyncTrakt, extraParams = new string[0]};

        public CommandRequest_TraktSyncCollection()
        {
        }

        public CommandRequest_TraktSyncCollection(bool forced)
        {
            Priority = (int) DefaultPriority;
            ForceRefresh = forced;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSyncCollection");

            try
            {
                if (!ServerSettings.Instance.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Instance.Trakt_AuthToken)) return;

                using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(
                    () => Repo.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync),
                    () => new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TraktSync, UpdateDetails = string.Empty }
                    ))
                {
                    if (upd.IsUpdate)
                    {
                        int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.Trakt_SyncFrequency);

                        // if we have run this in the last xxx hours then exit
                        TimeSpan tsLastRun = DateTime.Now - upd.Entity.LastUpdate;
                        if (tsLastRun.TotalHours < freqHours && !ForceRefresh)
                            return;
                    }
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }

                TraktTVHelper.SyncCollectionToTrakt();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktSyncCollection: {0}", ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_TraktSyncCollection";
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
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollection", "ForceRefresh"));
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