using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.CommandQueue.Commands.TvDB
{

    public class CmdTvDBLinkAniDB : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public bool AdditiveLink { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.TvDB;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 5;

        public string Id => $"TvDBLinkAniDB_{AnimeID}_{TvDBID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.LinkAniDBTvDB,
            ExtraParams = new[] {AnimeID.ToString(), TvDBID.ToString()}
        };

        public string WorkType => WorkTypes.TvDB;

        public CmdTvDBLinkAniDB(string str) : base(str)
        {
        }

        public CmdTvDBLinkAniDB(int animeID, int tvDBID, bool additiveLink = false)
        {
            AnimeID = animeID;
            TvDBID = tvDBID;
            AdditiveLink = additiveLink;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_LinkAniDBTvDB: {0}", AnimeID);

            try
            {
                ReportInit(progress);
                TvDBApiHelper.LinkAniDBTvDB(AnimeID, TvDBID, AdditiveLink);
                ReportUpdate(progress,50);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_LinkAniDBTvDB: {AnimeID} - {TvDBID} - {ex}", ex);
            }
        }
    }
}
