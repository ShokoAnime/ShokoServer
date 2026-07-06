using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class VoteAniDBAnimeJob(IRequestFactory requestFactory, AniDBTitleHelper titleHelper, AniDB_AnimeRepository anidbAnimes) : BaseJob
{
    private string _animeName;

    public int AnimeID { get; set; }
    public VoteType VoteType { get; set; }
    public double VoteValue { get; set; }

    public override void PostInit()
    {
        _animeName = anidbAnimes.GetByAnimeID(AnimeID)?.Title ?? titleHelper.SearchAnimeID(AnimeID)?.Title ?? AnimeID.ToString();
    }

    public override string TypeName => "Send AniDB Anime Rating";

    public override string Title => "Sending AniDB Anime Rating";
    public override Dictionary<string, object> Details => new()
    {
        { "Anime", _animeName },
        { "Vote", VoteValue },
        { "Type", VoteType.ToString() }
    };

    public override Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {AnimeID} | {Type} | {Value}", nameof(VoteAniDBAnimeJob), AnimeID, VoteType, VoteValue);

        var vote = requestFactory.Create<RequestVoteAnime>(
            r =>
            {
                r.Temporary = VoteType == VoteType.AnimeTemporary;
                r.Value = VoteValue;
                r.AnimeID = AnimeID;
            }
        );
        vote.Send();
        return Task.CompletedTask;
    }
}
