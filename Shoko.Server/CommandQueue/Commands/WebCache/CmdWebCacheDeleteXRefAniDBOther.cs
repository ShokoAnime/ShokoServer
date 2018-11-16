using System;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Providers.WebCache;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheDeleteXRefAniDBOther : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public int CrossRefType { get; set; }

        
        public string ParallelTag { get; set; } = WorkTypes.WebCache;
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public string WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheDeleteXRefAniDBOther_{AnimeID}-{CrossRefType}";

        public  QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheDeleteXRefAniDBOther,
            ExtraParams = new[] {AnimeID.ToString(), ((CrossRefType)CrossRefType).ToString()}
        };


        public CmdWebCacheDeleteXRefAniDBOther(string str) : base(str)
        {
        }

        public CmdWebCacheDeleteXRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            AnimeID = animeID;
            CrossRefType = (int) xrefType;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                WebCacheAPI.Delete_CrossRefAniDBOther(AnimeID, (CrossRefType) CrossRefType);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing WebCacheDeleteXRefAniDBOther: {AnimeID} - {CrossRefType} - {ex}", ex);
            }
        }
    }
}