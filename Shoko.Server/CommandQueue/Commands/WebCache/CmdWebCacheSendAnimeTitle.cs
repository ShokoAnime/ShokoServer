using System;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendAnimeTitle : BaseCommand<CmdWebCacheSendAnimeTitle>, ICommand
    {
        public int AnimeID { get; set; }
        public string MainTitle { get; set; }
        public string Titles { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendAnimeTitle_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnimeTitle,
            extraParams = new[] {AnimeID.ToString(), MainTitle, Titles}
        };


        public CmdWebCacheSendAnimeTitle(string str) : base(str)
        {
        }

        public CmdWebCacheSendAnimeTitle(int animeID, string main, string titles)
        {
            AnimeID = animeID;
            MainTitle = main;
            Titles = titles;

        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return new CommandResult();
                InitProgress(progress);
                Azure_AnimeIDTitle thisTitle = new Azure_AnimeIDTitle
                {
                    AnimeIDTitleId = 0,
                    MainTitle = MainTitle,
                    AnimeID = AnimeID,
                    Titles = Titles
                };
                AzureWebAPI.Send_AnimeTitle(thisTitle);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheSendAnimeTitle: {AnimeID} - {MainTitle} - {Titles} - {ex}", ex);
            }
        }
       
    }
}