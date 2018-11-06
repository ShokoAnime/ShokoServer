using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheDeleteXRefAniDBTrakt : BaseCommand<CmdWebCacheDeleteXRefAniDBTrakt>, ICommand
    {
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }




        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheDeleteXRefAniDBTrakt_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDBTrakt,
            extraParams = new[] {AnimeID.ToString(), AniDBStartEpisodeType.ToString(), AniDBStartEpisodeNumber.ToString(), TraktID, TraktSeasonNumber.ToString(), TraktStartEpisodeNumber.ToString()}
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

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                AzureWebAPI.Delete_CrossRefAniDBTrakt(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TraktID,
                    TraktSeasonNumber, TraktStartEpisodeNumber);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheDeleteXRefAniDBOther: {AnimeID} - {TraktID} - {ex}", ex);
            }


        }

    }
}