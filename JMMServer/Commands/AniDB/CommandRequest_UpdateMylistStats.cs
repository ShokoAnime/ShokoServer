using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands.AniDB
{
    [Serializable]
    public class CommandRequest_UpdateMylistStats : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_UpdateMylistStats()
        {
        }

        public CommandRequest_UpdateMylistStats(bool forced)
        {
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.AniDB_UpdateMylistStats;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority10; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_UpdateMyListStats);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_UpdateMylistStats");

            try
            {
                // we will always assume that an anime was downloaded via http first
                var repSched = new ScheduledUpdateRepository();
                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBMylistStats);
                if (sched == null)
                {
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.AniDBMylistStats;
                    sched.UpdateDetails = "";
                }
                else
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyListStats_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                sched.LastUpdate = DateTime.Now;
                repSched.Save(sched);

                JMMService.AnidbProcessor.UpdateMyListStats();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_UpdateMylistStats: {0}", ex.ToString());
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
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_UpdateMylistStats", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_UpdateMylistStats";
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