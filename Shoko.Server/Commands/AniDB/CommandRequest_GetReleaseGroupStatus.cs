using System;
using System.Collections.Generic;
using System.Xml;
using AniDBAPI;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetReleaseGroupStatus : CommandRequest_AniDBBase
    {
        public virtual int AnimeID { get; set; }
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GetReleaseGroup,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_GetReleaseGroupStatus()
        {
        }

        public CommandRequest_GetReleaseGroupStatus(int aid, bool forced)
        {
            AnimeID = aid;
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.AniDB_GetReleaseGroupStatus;
            Priority = (int) DefaultPriority;

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
                    logger.Info("Skipping group status command because anime has already ended: {0}", anime);
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
                    ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetReleaseGroupStatus_{AnimeID}";
        }

        public override bool InitFromDB(CommandRequest cq)
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroupStatus", "ForceRefresh"));
            }

            return true;
        }
    }
}