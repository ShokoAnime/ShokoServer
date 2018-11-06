using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheDeleteXRefAniDBTvDB : BaseCommand<CmdWebCacheDeleteXRefAniDBTvDB>, ICommand
    {
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheDeleteXRefAniDBTvDB_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDBTvDB,
            extraParams = new[] {AnimeID.ToString(), AniDBStartEpisodeType.ToString(), AniDBStartEpisodeNumber.ToString(), TvDBID.ToString(),TvDBSeasonNumber.ToString(), TvDBStartEpisodeNumber.ToString()}
        };



        public CmdWebCacheDeleteXRefAniDBTvDB(string str) : base(str)
        {
        }

        public CmdWebCacheDeleteXRefAniDBTvDB(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber,
            int tvDBID,
            int tvDBSeasonNumber, int tvDBStartEpisodeNumber)
        {
            AnimeID = animeID;
            AniDBStartEpisodeType = aniDBStartEpisodeType;
            AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            TvDBID = tvDBID;
            TvDBSeasonNumber = tvDBSeasonNumber;
            TvDBStartEpisodeNumber = tvDBStartEpisodeNumber;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                InitProgress(progress);
                AzureWebAPI.Delete_CrossRefAniDBTvDB(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TvDBID,
                    TvDBSeasonNumber, TvDBStartEpisodeNumber);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing WebCacheDeleteXRefAniDBTvDB: {AnimeID} - {TvDBID} - {ex}", ex);
            }
        }

    }
}