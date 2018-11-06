using System;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.TvDB
{

    public class CmdTvDBUpdateEpisode : BaseCommand<CmdTvDBUpdateEpisode>, ICommand
    {
        public int TvDBEpisodeID { get; set; }
        public bool ForceRefresh { get; set; }
        public bool DownloadImages { get; set; }
        public string InfoString { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.TvDB.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;

        public string Id => $"TvDBUpdateEpisode_{TvDBEpisodeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GettingTvDBEpisode,
            extraParams = new[] {$"{InfoString} ({TvDBEpisodeID})", DownloadImages.ToString(),ForceRefresh.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.TvDB;

        public CmdTvDBUpdateEpisode(string str) : base(str)
        {
        }

        public CmdTvDBUpdateEpisode(int tvDbEpisodeID, string infoString, bool downloadImages, bool forced)
        {
            TvDBEpisodeID = tvDbEpisodeID;
            ForceRefresh = forced;
            DownloadImages = downloadImages;
            InfoString = infoString;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TvDBUpdateEpisode: {0} ({1})", InfoString, TvDBEpisodeID);

            try
            {
                InitProgress(progress);
                var ep = TvDBApiHelper.UpdateEpisode(TvDBEpisodeID, DownloadImages, ForceRefresh);
                if (ep == null) return ReportFinishAndGetResult(progress);
                UpdateAndReportProgress(progress,25);
                var xref = Repo.Instance.CrossRef_AniDB_TvDB.GetByTvDBID(ep.SeriesID).DistinctBy(a => a.AniDBID);
                if (xref == null) return ReportFinishAndGetResult(progress);
                UpdateAndReportProgress(progress, 50);
                foreach (var crossRefAniDbTvDbv2 in xref)
                {
                    var anime = Repo.Instance.AnimeSeries.GetByAnimeID(crossRefAniDbTvDbv2.AniDBID);
                    if (anime == null) continue;
                    var episodes = Repo.Instance.AnimeEpisode.GetBySeriesID(anime.AnimeSeriesID);
                    foreach (SVR_AnimeEpisode episode in episodes)
                    {
                        // Save
                        if ((episode.TvDBEpisode?.Id ?? TvDBEpisodeID) != TvDBEpisodeID) continue;
                        Repo.Instance.AnimeEpisode.Touch(() => episode);
                    }
                    anime.QueueUpdateStats();
                }

                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error Processing CommandRequest_TvDBUpdateEpisode: {InfoString} ({TvDBEpisodeID})", ex);
            }
        }

    }
}
