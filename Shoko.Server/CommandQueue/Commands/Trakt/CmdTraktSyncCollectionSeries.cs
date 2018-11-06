using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{
    public class CmdTraktSyncCollectionSeries : BaseCommand<CmdTraktSyncCollectionSeries>, ICommand
    {
        public int AnimeSeriesID { get; set; }
        public string SeriesName { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 9;

        public string Id => $"TraktSyncCollectionSeries_{AnimeSeriesID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SyncTraktSeries,
            extraParams = new[] {SeriesName}
        };

        public WorkTypes WorkType => WorkTypes.Trakt;

        public CmdTraktSyncCollectionSeries(string str) : base(str)
        {
        }

        public CmdTraktSyncCollectionSeries(int animeSeriesID, string seriesName)
        {
            AnimeSeriesID = animeSeriesID;
            SeriesName = seriesName;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktSyncCollectionSeries");

            try
            {
                InitProgress(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return ReportFinishAndGetResult(progress);

                SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByID(AnimeSeriesID);
                if (series == null)
                {
                    return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Could not find anime series: {AnimeSeriesID}");
                }

                UpdateAndReportProgress(progress,50);
                TraktTVHelper.SyncCollectionToTrakt_Series(series);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Trakt.TraktSyncCollectionSeries: {AnimeSeriesID} - {ex}", ex);
            }
        }
    }
}