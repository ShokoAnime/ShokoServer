using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktSyncCollection : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TraktSyncCollection()
        {
        }

        public CommandRequest_TraktSyncCollection(bool forced)
        {
            CommandType = (int)CommandRequestType.Trakt_SyncCollection;
            Priority = (int)DefaultPriority;
            ForceRefresh = forced;

            GenerateCommandID();
        }

        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SyncTrakt);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSyncCollection");

            try
            {
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

                var repSched = new ScheduledUpdateRepository();
                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
                if (sched == null)
                {
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.TraktSync;
                    sched.UpdateDetails = "";
                }
                else
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_SyncFrequency);

                    // if we have run this in the last xxx hours then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }
                sched.LastUpdate = DateTime.Now;
                repSched.Save(sched);

                TraktTVHelper.SyncCollectionToTrakt();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktSyncCollection: {0}", ex.ToString());
            }
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollection", "ForceRefresh"));
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_TraktSyncCollection";
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}