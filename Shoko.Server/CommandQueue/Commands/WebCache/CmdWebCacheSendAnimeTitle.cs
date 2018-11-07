using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.WebCache;
using Shoko.Server.Providers.WebCache;

// ReSharper disable HeuristicUnreachableCode

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendAnimeTitle : BaseCommand, ICommand
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
            QueueState = QueueStateEnum.SendAnimeTitle,
            ExtraParams = new[] {AnimeID.ToString(), MainTitle, Titles}
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

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                bool process = false;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!process) return;
                InitProgress(progress);
                WebCache_AnimeIDTitle thisTitle = new WebCache_AnimeIDTitle
                {
                    AnimeIDTitleId = 0,
                    MainTitle = MainTitle,
                    AnimeID = AnimeID,
                    Titles = Titles
                };
                WebCacheAPI.Send_AnimeTitle(thisTitle);
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing WebCacheSendAnimeTitle: {AnimeID} - {MainTitle} - {Titles} - {ex}", ex);
            }
        }
       
    }
}