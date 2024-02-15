using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class VoteAniDBAnimeJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;

    public int AnimeID { get; set; }
    public AniDBVoteType VoteType { get; set; }
    public decimal VoteValue { get; set; }

    public override string Name => "Rate Anime";
    public override QueueStateStruct Description => new()
    {
        message = "Voting: {0} - {1}",
        queueState = QueueStateEnum.VoteAnime,
        extraParams = new[] { AnimeID.ToString(), VoteValue.ToString(CultureInfo.InvariantCulture) }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} for {AnimeID} | {Type} | {Value}", nameof(VoteAniDBAnimeJob), AnimeID, VoteType, VoteValue);

        var vote = _requestFactory.Create<RequestVoteAnime>(
            r =>
            {
                r.Temporary = VoteType == AniDBVoteType.AnimeTemp;
                r.Value = Convert.ToDouble(VoteValue);
                r.AnimeID = AnimeID;
            }
        );
        vote.Send();
        return Task.CompletedTask;
    }
    
    public VoteAniDBAnimeJob(IRequestFactory requestFactory)
    {
        _requestFactory = requestFactory;
    }

    protected VoteAniDBAnimeJob()
    {
    }
}
