using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendAnimeFull : BaseCommand<CmdWebCacheSendAnimeFull>, ICommand
    {
        public int AnimeID { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendAnimeFull_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnimeFull,
            extraParams = new[] {AnimeID.ToString()}
        };



        public CmdWebCacheSendAnimeFull(string str) : base(str)
        {
        }

        public CmdWebCacheSendAnimeFull(int animeID)
        {
            AnimeID = animeID;
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return new CommandResult();


                InitProgress(progress);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return ReportFinishAndGetResult(progress);
                if (anime.AllTags.ToUpper().Contains("18 RESTRICTED")) return ReportFinishAndGetResult(progress);
                UpdateAndReportProgress(progress,50);

                AzureWebAPI.Send_AnimeFull(anime);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheSendAnimeFull: {AnimeID} - {ex}", ex);
            }
        }
      
    }
}