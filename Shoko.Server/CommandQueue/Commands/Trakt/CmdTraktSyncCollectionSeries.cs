using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{
    public class CmdTraktSyncCollectionSeries : BaseCommand, ICommand
    {
        public int AnimeSeriesID { get; set; }
        public string SeriesName { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 9;

        public string Id => $"TraktSyncCollectionSeries_{AnimeSeriesID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SyncTraktSeries,
            ExtraParams = new[] {SeriesName}
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

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktSyncCollectionSeries");

            try
            {
                ReportInit(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    ReportFinish(progress);
                    return;
                }

                SVR_AnimeSeries series = Repo.Instance.AnimeSeries.GetByID(AnimeSeriesID);
                if (series == null)
                {
                    ReportError(progress, $"Could not find anime series: {AnimeSeriesID}");
                    return;
                }

                ReportUpdate(progress,50);
                TraktTVHelper.SyncCollectionToTrakt_Series(series);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Trakt.TraktSyncCollectionSeries: {AnimeSeriesID} - {ex}", ex);
            }
        }
    }
}