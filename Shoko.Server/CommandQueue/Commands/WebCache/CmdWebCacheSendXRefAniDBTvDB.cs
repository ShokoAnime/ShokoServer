using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendXRefAniDBTvDB : BaseCommand, ICommand
    {
        public int CrossRef_AniDB_TvDBID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendXRefAniDBTvDB_{CrossRef_AniDB_TvDBID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheSendXRefAniDBTvDB,
            ExtraParams = new[] {CrossRef_AniDB_TvDBID.ToString()}
        };


        public CmdWebCacheSendXRefAniDBTvDB(string str) : base(str)
        {
        }

        public CmdWebCacheSendXRefAniDBTvDB(int xrefID)
        {
            CrossRef_AniDB_TvDBID = xrefID;
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                //if (string.IsNullOrEmpty(ServerSettings.Instance.WebCache.AuthKey)) return;

                CrossRef_AniDB_TvDB xref = Repo.Instance.CrossRef_AniDB_TvDB.GetByID(CrossRef_AniDB_TvDBID);
                if (xref == null)
                {
                    ReportFinish(progress);
                    return;
                }
                ReportUpdate(progress,33);
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(xref.AniDBID);
                if (anime == null)
                {
                    ReportFinish(progress);
                    return;
                }
                ReportUpdate(progress, 66);
                WebCacheAPI.Send_CrossRefAniDBTvDB(xref.ToV2Model(), anime.MainTitle);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing WebCacheSendXRefAniDBTvDB {CrossRef_AniDB_TvDBID} - {ex}", ex);
            }
        }

      
    }
}
