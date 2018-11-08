using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.TvDB
{
    public class CmdTvDBUpdateSeries : BaseCommand, ICommand
    {
        public int TvDBSeriesID { get; set; }
        public bool ForceRefresh { get; set; }
        public string SeriesTitle { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.TvDB.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;

        public string Id => $"TvDBUpdateSeries_{TvDBSeriesID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.GettingTvDBSeries,
            ExtraParams = new[] {$"{SeriesTitle} ({TvDBSeriesID})", ForceRefresh.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.TvDB;

        public CmdTvDBUpdateSeries(string str) : base(str)
        {
            SeriesTitle = Repo.Instance.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ?? string.Intern("Name not Available");
        }

        public CmdTvDBUpdateSeries(int tvDBSeriesID, bool forced)
        {
            TvDBSeriesID = tvDBSeriesID;
            ForceRefresh = forced;
            SeriesTitle = Repo.Instance.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ?? string.Intern("Name not Available");
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TvDBUpdateSeries: {0}", TvDBSeriesID);

            try
            {
                ReportInit(progress);
                TvDBApiHelper.UpdateSeriesInfoAndImages(TvDBSeriesID, ForceRefresh, true);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TvDBUpdateSeries: {TvDBSeriesID} - {ex}", ex);
            }
        }

    }
}