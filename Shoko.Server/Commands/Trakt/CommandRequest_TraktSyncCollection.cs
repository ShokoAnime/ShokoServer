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
    public class CommandRequest_TraktSyncCollection : CommandRequest
    {
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.SyncTrakt, extraParams = new string[0]};

        public CommandRequest_TraktSyncCollection()
        {
        }

        public CommandRequest_TraktSyncCollection(bool forced)
        {
            CommandType = (int) CommandRequestType.Trakt_SyncCollection;
            Priority = (int) DefaultPriority;
            ForceRefresh = forced;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSyncCollection");

            try
            {
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

                using (var usch = Repo.ScheduledUpdate.BeginAddOrUpdate(()=> Repo.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync)))
                {
                    if (usch.Original != null)
                    {
                        int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_SyncFrequency);

                        // if we have run this in the last xxx hours then exit
                        TimeSpan tsLastRun = DateTime.Now - usch.Entity.LastUpdate;
                        if (tsLastRun.TotalHours < freqHours)
                        {
                            if (!ForceRefresh) return;
                        }
                    }
                    usch.Entity.UpdateType = (int) ScheduledUpdateType.TraktSync;
                    usch.Entity.UpdateDetails = string.Empty;
                    usch.Entity.LastUpdate = DateTime.Now;
                    usch.Commit();
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
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollection", "ForceRefresh"));
            }

            return true;
        }
    }
}