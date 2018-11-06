using System;
using System.Collections.Generic;
using System.Linq;
using AniDBAPI;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBGetReleaseGroupStatus : BaseCommand<CmdAniDBGetReleaseGroupStatus>, ICommand
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 5;
        public string Id => $"GetReleaseGroupStatus_{AnimeID}";
        public WorkTypes WorkType => WorkTypes.AniDB;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GetReleaseGroup,
            extraParams = new[] {AnimeID.ToString(), ForceRefresh.ToString()}
        };


        public CmdAniDBGetReleaseGroupStatus(string str) : base(str)
        {
        }

        public CmdAniDBGetReleaseGroupStatus(int aid, bool forced)
        {
            AnimeID = aid;
            ForceRefresh = forced;
        }

        public CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_GetReleaseGroupStatus: {0}", AnimeID);

            try
            {
                InitProgress(progress);
                // only get group status if we have an associated series
                SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByAnimeID(AnimeID);
                if (series == null) return ReportFinishAndGetResult(progress);
                UpdateAndReportProgress(progress,20);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return ReportFinishAndGetResult(progress);
                UpdateAndReportProgress(progress,40);

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
                                    Repo.Instance.AniDB_GroupStatus.GetByAnimeID(AnimeID);
                                UpdateAndReportProgress(progress,60);
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
                    return ReportFinishAndGetResult(progress);
                }

                GroupStatusCollection grpCol = ShokoService.AnidbProcessor.GetReleaseGroupStatusUDP(AnimeID);
                UpdateAndReportProgress(progress,80);
                if (ServerSettings.Instance.AniDb.DownloadReleaseGroups && grpCol != null && grpCol.Groups != null &&
                    grpCol.Groups.Count > 0)
                {
                    Queue.Instance.AddRange(grpCol.Groups.DistinctBy(a => a.GroupID).Select(a => new CmdAniDBGetReleaseGroup(a.GroupID, false)));
                }

                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Command AniDb.GetReleaseGroupStatus: {AnimeID} - {ex}", ex);
            }
        }
    }
}