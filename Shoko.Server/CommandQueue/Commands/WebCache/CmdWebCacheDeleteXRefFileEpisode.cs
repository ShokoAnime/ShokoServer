using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheDeleteXRefFileEpisode : BaseCommand<CmdWebCacheDeleteXRefFileEpisode>, ICommand
    {
        public string Hash { get; set; }
        public int EpisodeID { get; set; }

       

        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;


        public string Id => $"WebCacheDeleteXRefFileEpisode_{Hash}-{EpisodeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefFileEpisode,
            extraParams = new[] {Hash, EpisodeID.ToString()}
        };


        public CmdWebCacheDeleteXRefFileEpisode(string str) : base(str)
        {
        }

        public CmdWebCacheDeleteXRefFileEpisode(string hash, int aniDBEpisodeID)
        {
            Hash = hash;
            EpisodeID = aniDBEpisodeID;
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                AzureWebAPI.Delete_CrossRefFileEpisode(Hash);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheDeleteXRefFileEpisode {Hash} - {EpisodeID} - {ex}", ex);
            }
        }
       
    }
}