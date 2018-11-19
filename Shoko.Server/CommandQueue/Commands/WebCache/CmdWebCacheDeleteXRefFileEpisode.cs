using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.WebCache;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheDeleteXRefFileEpisode : BaseCommand, ICommand
    {
        public string Hash { get; set; }
        public int EpisodeID { get; set; }

       

        public string ParallelTag { get; set; } = WorkTypes.WebCache;
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public string WorkType => WorkTypes.WebCache;


        public string Id => $"WebCacheDeleteXRefFileEpisode_{Hash}-{EpisodeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheDeleteXRefFileEpisode,
            ExtraParams = new[] {Hash, EpisodeID.ToString()}
        };


        public CmdWebCacheDeleteXRefFileEpisode(string str) : base(str)
        {
        }

        public CmdWebCacheDeleteXRefFileEpisode(string hash, int aniDBEpisodeID)
        {
            Hash = hash;
            EpisodeID = aniDBEpisodeID;
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                WebCacheAPI.Instance.DeleteCrossRef_File_Episode(Hash, EpisodeID);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing WebCacheDeleteXRefFileEpisode {Hash} - {EpisodeID} - {ex}", ex);
            }
        }
       
    }
}