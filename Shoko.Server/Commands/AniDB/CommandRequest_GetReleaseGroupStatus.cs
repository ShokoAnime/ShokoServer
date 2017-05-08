using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using Shoko.Models.Queue;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetReleaseGroupStatus : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority5; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.GetReleaseGroup,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_GetReleaseGroupStatus()
        {
        }

        public CommandRequest_GetReleaseGroupStatus(int aid, bool forced)
        {
            this.AnimeID = aid;
            this.ForceRefresh = forced;
            this.CommandType = (int) CommandRequestType.AniDB_GetReleaseGroupStatus;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetReleaseGroupStatus: {0}", AnimeID);

            try
            {
                // only get group status if we have an associated series
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
                if (series == null) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                // don't get group status if the anime has already ended more than 50 days ago
                bool skip = false;
                if (!ForceRefresh)
                {
                    if (anime.EndDate.HasValue)
                    {
                        if (anime.EndDate.Value < DateTime.Now)
                        {
                            TimeSpan ts = DateTime.Now - anime.EndDate.Value;
                            if (ts.TotalDays > 50)
                            {
                                // don't skip if we have never downloaded this info before
                                List<AniDB_GroupStatus> grpStatuses =
                                    RepoFactory.AniDB_GroupStatus.GetByAnimeID(AnimeID);
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

                GroupStatusCollection grpCol = ShokoService.AnidbProcessor.GetReleaseGroupStatusUDP(AnimeID);

                if (ServerSettings.AniDB_DownloadReleaseGroups && grpCol != null && grpCol.Groups != null &&
                    grpCol.Groups.Count > 0)
                {
                    // save in bulk to improve performance
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        foreach (Raw_AniDB_GroupStatus grpStatus in grpCol.Groups)
                        {
                            CommandRequest_GetReleaseGroup cmdRelgrp =
                                new CommandRequest_GetReleaseGroup(grpStatus.GroupID, false);
                            cmdRelgrp.Save(session);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetReleaseGroupStatus: {0} - {1}", AnimeID,
                    ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_GetReleaseGroupStatus_{0}", this.AnimeID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "AnimeID"));
                this.ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}