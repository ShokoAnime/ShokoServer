using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.WebCache;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheDeleteXRefAniDBTrakt : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }




        public string ParallelTag { get; set; } = WorkTypes.WebCache;
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public string WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheDeleteXRefAniDBTrakt_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheDeleteXRefAniDBTrakt,
            ExtraParams = new[] {AnimeID.ToString(), AniDBStartEpisodeType.ToString(), AniDBStartEpisodeNumber.ToString(), TraktID, TraktSeasonNumber.ToString(), TraktStartEpisodeNumber.ToString()}
        };


        public CmdWebCacheDeleteXRefAniDBTrakt(string str) : base(str)
        {
        }

        public CmdWebCacheDeleteXRefAniDBTrakt(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber,
            string traktID,
            int traktSeasonNumber, int traktStartEpisodeNumber)
        {
            AnimeID = animeID;
            AniDBStartEpisodeType = aniDBStartEpisodeType;
            AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            TraktID = traktID;
            TraktSeasonNumber = traktSeasonNumber;
            TraktStartEpisodeNumber = traktStartEpisodeNumber;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                WebCacheAPI.Delete_CrossRefAniDBTrakt(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TraktID,
                    TraktSeasonNumber, TraktStartEpisodeNumber);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing WebCacheDeleteXRefAniDBOther: {AnimeID} - {TraktID} - {ex}", ex);
            }


        }

    }
}