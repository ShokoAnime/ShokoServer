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

    public class CmdTraktCollectionEpisode : BaseCommand, ICommand
    {
        public int AnimeEpisodeID { get; set; }
        public int Action { get; set; }

        [JsonIgnore]
        public TraktSyncAction ActionEnum => (TraktSyncAction) Action;


        public string ParallelTag { get; set; } = WorkTypes.Trakt;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 9;

        public string Id => $"TraktCollectionEpisode_{AnimeEpisodeID}-{Action}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SyncTraktEpisodes,
            ExtraParams = new[] {AnimeEpisodeID.ToString(), ActionEnum.ToString()}
        };

        public string WorkType => WorkTypes.Trakt;

        public CmdTraktCollectionEpisode(string str) : base(str)
        {
        }

        public CmdTraktCollectionEpisode(int animeEpisodeID, TraktSyncAction action)
        {
            AnimeEpisodeID = animeEpisodeID;
            Action = (int) action;
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktCollectionEpisode: {0}-{1}", AnimeEpisodeID, Action);

            try
            {
                ReportInit(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    ReportFinish(progress);
                    return;
                }

                SVR_AnimeEpisode ep = Repo.Instance.AnimeEpisode.GetByID(AnimeEpisodeID);
                ReportUpdate(progress,50);
                if (ep != null)
                {
                    TraktSyncType syncType = TraktSyncType.CollectionAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.CollectionRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(ep, syncType);
                }

                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TraktCollectionEpisode: {AnimeEpisodeID} - {Action} - {ex}", ex);
            }
        }
    }
}