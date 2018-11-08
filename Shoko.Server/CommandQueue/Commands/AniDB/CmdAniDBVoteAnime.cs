using System;
using System.Globalization;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBVoteAnime : BaseCommand, ICommand
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

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.VoteAnime, ExtraParams = new[] {AnimeID.ToString(), VoteValue.ToString(CultureInfo.InvariantCulture), VoteType.ToString()}};


        public CmdAniDBVoteAnime(string str) : base(str)
        {
        }

        public CmdAniDBVoteAnime(int animeID, int voteType, decimal voteValue)
        {
            AnimeID = animeID;
            VoteType = voteType;
            VoteValue = voteValue;
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Processing CommandRequest_Vote: {Id}");


            try
            {
                ReportInit(progress);
                ShokoService.AnidbProcessor.VoteAnime(AnimeID, VoteValue, (AniDBVoteType) VoteType);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDB.Vote: {Id} - {ex}", ex);
            }
        }
    }
}