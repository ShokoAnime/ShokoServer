using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.CommandQueue.Commands.Image
{
    public class CmdImageDownloadAllTvDB : BaseCommand<CmdImageDownloadAllTvDB>, ICommand
    {
        public int TvDBSeriesID { get; set; }
        public bool ForceRefresh { get; set; }

       
        public string ParallelTag { get; set; } = "TvDBImages";
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 8;

        public string Id => $"TvDBDownloadImages_{TvDBSeriesID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.DownloadTvDBImages,
            extraParams = new[] {TvDBSeriesID.ToString(), ForceRefresh.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.Image;


        public CmdImageDownloadAllTvDB(string str) : base(str)
        {
        }


        public CmdImageDownloadAllTvDB(int tvDBSeriesID, bool forced)
        {
            TvDBSeriesID = tvDBSeriesID;
            ForceRefresh = forced;
        }


        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TvDBDownloadImages: {0}", TvDBSeriesID);

            try
            {
                InitProgress(progress);
                TvDBApiHelper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_TvDBDownloadImages: {TvDBSeriesID} - {ex}", ex);
            }
        }

    }
}