using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.CommandQueue.Commands.TvDB
{

    public class CmdTvDBLinkAniDB : BaseCommand<CmdTvDBLinkAniDB>, ICommand
    {
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public bool AdditiveLink { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.TvDB.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 5;

        public string Id => $"TvDBLinkAniDB_{AnimeID}_{TvDBID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.LinkAniDBTvDB,
            extraParams = new[] {AnimeID.ToString(), TvDBID.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.TvDB;

        public CmdTvDBLinkAniDB(string str) : base(str)
        {
        }

        public CmdTvDBLinkAniDB(int animeID, int tvDBID, bool additiveLink = false)
        {
            AnimeID = animeID;
            TvDBID = tvDBID;
            AdditiveLink = additiveLink;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_LinkAniDBTvDB: {0}", AnimeID);

            try
            {
                InitProgress(progress);
                TvDBApiHelper.LinkAniDBTvDB(AnimeID, TvDBID, AdditiveLink);
                UpdateAndReportProgress(progress,50);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_LinkAniDBTvDB: {AnimeID} - {TvDBID} - {ex}", ex);
            }
        }
    }
}
