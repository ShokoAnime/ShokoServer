using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendXRefFileEpisode : BaseCommand, ICommand
    {
        public int CrossRef_File_EpisodeID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendXRefFileEpisode_{CrossRef_File_EpisodeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheSendXRefFileEpisode,
            ExtraParams = new[] {CrossRef_File_EpisodeID.ToString()}
        };



        public CmdWebCacheSendXRefFileEpisode(string str) : base(str)
        {
        }

        public CmdWebCacheSendXRefFileEpisode(int crossRef_File_EpisodeID)
        {
            CrossRef_File_EpisodeID = crossRef_File_EpisodeID;        
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                InitProgress(progress);
                CrossRef_File_Episode xref = Repo.Instance.CrossRef_File_Episode.GetByID(CrossRef_File_EpisodeID);
                if (xref == null)
                {
                    ReportFinishAndGetResult(progress);
                    return;
                } 
                UpdateAndReportProgress(progress,50);
                WebCacheAPI.Send_CrossRefFileEpisode(xref);
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing WebCacheDeleteXRefAniDBOther: {CrossRef_File_EpisodeID} - {ex}", ex);
            }
        }
      
    }
}