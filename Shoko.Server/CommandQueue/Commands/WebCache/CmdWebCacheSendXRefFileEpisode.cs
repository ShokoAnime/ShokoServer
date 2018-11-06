using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendXRefFileEpisode : BaseCommand<CmdWebCacheSendXRefFileEpisode>, ICommand
    {
        public int CrossRef_File_EpisodeID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendXRefFileEpisode_{CrossRef_File_EpisodeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefFileEpisode,
            extraParams = new[] {CrossRef_File_EpisodeID.ToString()}
        };



        public CmdWebCacheSendXRefFileEpisode(string str) : base(str)
        {
        }

        public CmdWebCacheSendXRefFileEpisode(int crossRef_File_EpisodeID)
        {
            CrossRef_File_EpisodeID = crossRef_File_EpisodeID;        
        }


        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                CrossRef_File_Episode xref = Repo.Instance.CrossRef_File_Episode.GetByID(CrossRef_File_EpisodeID);
                if (xref == null) return ReportFinishAndGetResult(progress); 
                UpdateAndReportProgress(progress,50);
                AzureWebAPI.Send_CrossRefFileEpisode(xref);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheDeleteXRefAniDBOther: {CrossRef_File_EpisodeID} - {ex}", ex);
            }
        }
      
    }
}