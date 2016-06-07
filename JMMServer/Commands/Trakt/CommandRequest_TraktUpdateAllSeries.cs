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
    public class CommandRequest_TraktUpdateAllSeries : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TraktUpdateAllSeries()
        {
        }

        public CommandRequest_TraktUpdateAllSeries(bool forced)
        {
            CommandType = (int)CommandRequestType.Trakt_UpdateAllSeries;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public bool ForceRefresh { get; set; }


        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_UpdateTrakt);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktUpdateAllSeries");

            try
            {
                var repSched = new ScheduledUpdateRepository();
                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
                if (sched == null)
                {
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.TraktUpdate;
                    sched.UpdateDetails = "";
                }
                else
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_UpdateFrequency);

                    // if we have run this in the last xxx hours then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }
                sched.LastUpdate = DateTime.Now;
                repSched.Save(sched);

                // update all info
                TraktTVHelper.UpdateAllInfo();

                // scan for new matches
                TraktTVHelper.ScanForMatches();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktUpdateAllSeries: {0}", ex.ToString());
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
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktUpdateAllSeries", "ForceRefresh"));
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_TraktUpdateAllSeries";
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