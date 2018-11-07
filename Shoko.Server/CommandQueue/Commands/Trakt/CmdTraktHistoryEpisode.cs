using System;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{

    public class CmdTraktHistoryEpisode : BaseCommand, ICommand
    {
        public int AnimeEpisodeID { get; set; }
        public int Action { get; set; }

        [JsonIgnore]
        public TraktSyncAction ActionEnum => (TraktSyncAction) Action;
        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 9;

        public string Id => $"TraktHistoryEpisode{AnimeEpisodeID}-{Action}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.TraktAddHistory,
            ExtraParams = new[] {AnimeEpisodeID.ToString(), ActionEnum.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.Trakt;

        public CmdTraktHistoryEpisode(string str) : base(str)
        {
        }

        public CmdTraktHistoryEpisode(int animeEpisodeID, TraktSyncAction action)
        {
            AnimeEpisodeID = animeEpisodeID;
            Action = (int) action;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktHistoryEpisode: {0}-{1}", AnimeEpisodeID, Action);

            try
            {
                InitProgress(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    ReportFinishAndGetResult(progress);
                    return;
                }

                SVR_AnimeEpisode ep = Repo.Instance.AnimeEpisode.GetByID(AnimeEpisodeID);
                UpdateAndReportProgress(progress,50);
                if (ep != null)
                {
                    TraktSyncType syncType = TraktSyncType.HistoryAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.HistoryRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(ep, syncType);
                }

                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing CommandRequest_TraktHistoryEpisode: {AnimeEpisodeID} - {Action} - {ex}", ex);
            }
        }
    }
}