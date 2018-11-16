using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.CommandQueue.Commands.Image
{
    public class CmdImageDownloadAllTvDB : BaseCommand, ICommand
    {
        public int TvDBSeriesID { get; set; }
        public bool ForceRefresh { get; set; }

       
        public string ParallelTag { get; set; } = "TvDBImages";
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 8;

        public string Id => $"TvDBDownloadImages_{TvDBSeriesID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.DownloadTvDBImages,
            ExtraParams = new[] {TvDBSeriesID.ToString(), ForceRefresh.ToString()}
        };

        public string WorkType => WorkTypes.Image;


        public CmdImageDownloadAllTvDB(string str) : base(str)
        {
        }


        public CmdImageDownloadAllTvDB(int tvDBSeriesID, bool forced)
        {
            TvDBSeriesID = tvDBSeriesID;
            ForceRefresh = forced;
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TvDBDownloadImages: {0}", TvDBSeriesID);

            try
            {
                ReportInit(progress);
                TvDBApiHelper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TvDBDownloadImages: {TvDBSeriesID} - {ex}", ex);
            }
        }

    }
}