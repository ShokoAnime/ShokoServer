using System;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{

    public class CmdTraktCollectionEpisode : BaseCommand<CmdTraktCollectionEpisode>, ICommand
    {
        public int AnimeEpisodeID { get; set; }
        public int Action { get; set; }

        [JsonIgnore]
        public TraktSyncAction ActionEnum => (TraktSyncAction) Action;


        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 9;

        public string Id => $"TraktCollectionEpisode_{AnimeEpisodeID}-{Action}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SyncTraktEpisodes,
            extraParams = new[] {AnimeEpisodeID.ToString(), ActionEnum.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.Trakt;

        public CmdTraktCollectionEpisode(string str) : base(str)
        {
        }

        public CmdTraktCollectionEpisode(int animeEpisodeID, TraktSyncAction action)
        {
            AnimeEpisodeID = animeEpisodeID;
            Action = (int) action;
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktCollectionEpisode: {0}-{1}", AnimeEpisodeID, Action);

            try
            {
                InitProgress(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return ReportFinishAndGetResult(progress);

                SVR_AnimeEpisode ep = Repo.Instance.AnimeEpisode.GetByID(AnimeEpisodeID);
                UpdateAndReportProgress(progress,50);
                if (ep != null)
                {
                    TraktSyncType syncType = TraktSyncType.CollectionAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.CollectionRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(ep, syncType);
                }

                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_TraktCollectionEpisode: {AnimeEpisodeID} - {Action} - {ex}", ex);
            }
        }
    }
}