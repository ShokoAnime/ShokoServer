using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerRefreshAnime : BaseCommand<CmdServerRefreshAnime>, ICommand
    {
        public int AnimeID { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Server.ToString();
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 8;

        public string Id => $"RefreshAnime_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.Refresh,
            extraParams = new[] {AnimeID.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.Server;

        public CmdServerRefreshAnime(int animeID)
        {
            AnimeID = animeID;
        }

        public CmdServerRefreshAnime(string str) : base(str)
        {
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing ServerRefreshAnime {AnimeID} - {ex}", ex);
            }
        }
    }
}