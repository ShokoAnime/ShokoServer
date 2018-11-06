using System;
using System.Globalization;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBVoteAnime : BaseCommand<CmdAniDBVoteAnime>, ICommand
    {


        public int AnimeID { get; set; }
        public int VoteType { get; set; }
        public decimal VoteValue { get; set; }


        public string Info => AnimeID.ToString();
        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 6;
        public string Id => $"Vote_{AnimeID}_{VoteType}_{VoteValue}";
        public WorkTypes WorkType => WorkTypes.AniDB;

        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.VoteAnime, extraParams = new[] {AnimeID.ToString(), VoteValue.ToString(CultureInfo.InvariantCulture), VoteType.ToString()}};


        public CmdAniDBVoteAnime(string str) : base(str)
        {
        }

        public CmdAniDBVoteAnime(int animeID, int voteType, decimal voteValue)
        {
            AnimeID = animeID;
            VoteType = voteType;
            VoteValue = voteValue;
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info($"Processing CommandRequest_Vote: {Id}");


            try
            {
                InitProgress(progress);
                ShokoService.AnidbProcessor.VoteAnime(AnimeID, VoteValue, (AniDBVoteType) VoteType);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Command AniDB.Vote: {Id} - {ex}", ex);
            }
        }
    }
}