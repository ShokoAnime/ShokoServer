using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendAnimeFull : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache;
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public string WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendAnimeFull_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SendAnimeFull,
            ExtraParams = new[] {AnimeID.ToString()}
        };



        public CmdWebCacheSendAnimeFull(string str) : base(str)
        {
        }

        public CmdWebCacheSendAnimeFull(int animeID)
        {
            AnimeID = animeID;
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return;


                ReportInit(progress);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null)
                {
                    ReportFinish(progress);
                    return;
                }

                if (anime.AllTags.ToUpper().Contains("18 RESTRICTED"))
                {
                    ReportFinish(progress);
                    return;
                }
                ReportUpdate(progress,50);

                WebCacheAPI.Send_AnimeFull(anime);
               ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing WebCacheSendAnimeFull: {AnimeID} - {ex}", ex);
            }
        }
      
    }
}