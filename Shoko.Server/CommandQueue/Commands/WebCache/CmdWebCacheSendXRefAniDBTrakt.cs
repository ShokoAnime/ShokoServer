using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{

    public class CmdWebCacheSendXRefAniDBTrakt : BaseCommand, ICommand
    {
        public int CrossRef_AniDB_TraktID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendXRefAniDBTrakt_{CrossRef_AniDB_TraktID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheSendXRefAniDBTrakt,
            ExtraParams = new[] {CrossRef_AniDB_TraktID.ToString()}
        };



        public CmdWebCacheSendXRefAniDBTrakt()
        {
        }

        public CmdWebCacheSendXRefAniDBTrakt(int xrefID)
        {
            CrossRef_AniDB_TraktID = xrefID;

        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                InitProgress(progress);
                CrossRef_AniDB_TraktV2 xref = Repo.Instance.CrossRef_AniDB_TraktV2.GetByID(CrossRef_AniDB_TraktID);
                if (xref == null)
                {
                    ReportFinishAndGetResult(progress);
                    return;
                }
                UpdateAndReportProgress(progress,25);
                Trakt_Show tvShow = Repo.Instance.Trakt_Show.GetByTraktSlug(xref.TraktID);
                if (tvShow == null)
                {
                    ReportFinishAndGetResult(progress);
                    return;
                }
                UpdateAndReportProgress(progress, 50);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime == null)
                {
                    ReportFinishAndGetResult(progress);
                    return;
                }
                UpdateAndReportProgress(progress, 75);
                WebCacheAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing WebCacheSendXRefAniDBTrakt {CrossRef_AniDB_TraktID} - {ex}", ex);
            }
        }
     
    }
}