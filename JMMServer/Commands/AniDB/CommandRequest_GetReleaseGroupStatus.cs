using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetReleaseGroupStatus : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_GetReleaseGroupStatus()
        {
        }

        public CommandRequest_GetReleaseGroupStatus(int aid, bool forced)
        {
            AnimeID = aid;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.AniDB_GetReleaseGroupStatus;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority5; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_GetReleaseGroup, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetReleaseGroupStatus: {0}", AnimeID);

            try
            {
                // only get group status if we have an associated series
                var repSeries = new AnimeSeriesRepository();
                var series = repSeries.GetByAnimeID(AnimeID);
                if (series == null) return;

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                // don't get group status if the anime has already ended more than 50 days ago
                var skip = false;
                if (!ForceRefresh)
                {
                    if (anime.EndDate.HasValue)
                    {
                        if (anime.EndDate.Value < DateTime.Now)
                        {
                            var ts = DateTime.Now - anime.EndDate.Value;
                            if (ts.TotalDays > 50)
                            {
                                // don't skip if we have never downloaded this info before
                                var repGrpStatus = new AniDB_GroupStatusRepository();
                                var grpStatuses = repGrpStatus.GetByAnimeID(AnimeID);
                                if (grpStatuses != null && grpStatuses.Count > 0)
                                {
                                    skip = true;
                                }
                            }
                        }
                    }
                }

                if (skip)
                {
                    logger.Info("Skipping group status command because anime has already ended: {0}", anime.ToString());
                    return;
                }

                var grpCol = JMMService.AnidbProcessor.GetReleaseGroupStatusUDP(AnimeID);

                if (ServerSettings.AniDB_DownloadReleaseGroups)
                {
                    // save in bulk to improve performance
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        using (var transaction = session.BeginTransaction())
                        {
                            foreach (var grpStatus in grpCol.Groups)
                            {
                                var cmdRelgrp = new CommandRequest_GetReleaseGroup(grpStatus.GroupID, false);
                                cmdRelgrp.Save(session);
                            }

                            transaction.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetReleaseGroupStatus: {0} - {1}", AnimeID, ex.ToString());
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_GetReleaseGroupStatus_{0}", AnimeID);
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