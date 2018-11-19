using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{

    public class CmdTraktLinkAniDB : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public string TraktID { get; set; }
        public bool AdditiveLink { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 5;

        public string Id => $"TraktLinkAniDB_{AnimeID}_{TraktID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.LinkAniDBTrakt,
            ExtraParams = new[] {AnimeID.ToString(), TraktID }
        };

        public string WorkType => WorkTypes.TvDB;

        public CmdTraktLinkAniDB(string str) : base(str)
        {
        }

        public CmdTraktLinkAniDB(int animeID, string traktID, bool additiveLink = false)
        {
            AnimeID = animeID;
            TraktID = traktID;
            AdditiveLink = additiveLink;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_LinkAniDBTrakt: {0}", AnimeID);

            try
            {
                ReportInit(progress);
                TraktTVHelper.LinkAniDBTrakt(AnimeID, TraktID, AdditiveLink);
                ReportUpdate(progress,50);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_LinkAniDBTrakt: {AnimeID} - {TraktID} - {ex}", ex);
            }
        }
    }
}
