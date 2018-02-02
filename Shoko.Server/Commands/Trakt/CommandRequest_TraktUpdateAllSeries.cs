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
    public class CommandRequest_TraktUpdateAllSeries : CommandRequest
    {
        public virtual bool ForceRefresh { get; set; }


        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.UpdateTrakt, extraParams = new string[0]};

        public CommandRequest_TraktUpdateAllSeries()
        {
        }

        public CommandRequest_TraktUpdateAllSeries(bool forced)
        {
            CommandType = (int) CommandRequestType.Trakt_UpdateAllSeries;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktUpdateAllSeries");

            try
            {
                using (var usch = Repo.ScheduledUpdate.BeginAddOrUpdate(() => Repo.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate)))
                {
                    if (usch.Original != null)
                    {
                        int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_UpdateFrequency);

                        // if we have run this in the last xxx hours then exit
                        TimeSpan tsLastRun = DateTime.Now - usch.Entity.LastUpdate;
                        if (tsLastRun.TotalHours < freqHours)
                        {
                            if (!ForceRefresh) return;
                        }
                    }

                    usch.Entity.UpdateType = (int) ScheduledUpdateType.TraktUpdate;
                    usch.Entity.UpdateDetails = string.Empty;
                    usch.Entity.LastUpdate = DateTime.Now;
                    usch.Commit();
                }

                // update all info
                TraktTVHelper.UpdateAllInfo();

                // scan for new matches
                TraktTVHelper.ScanForMatches();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktUpdateAllSeries: {0}", ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_TraktUpdateAllSeries";
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
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktUpdateAllSeries", "ForceRefresh"));
            }

            return true;
        }
    }
}