using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class VoteAniDBEpisodeJob(IRequestFactory requestFactory, AnimeEpisodeRepository animeEpisodes) : BaseJob
{
    private string _animeName;
    private string _episodeName;

    public int EpisodeID { get; set; }
    public double VoteValue { get; set; }

    public override void PostInit()
    {
        var episode = animeEpisodes.GetByID(EpisodeID);
        _animeName = episode?.AnimeSeries?.Title ?? EpisodeID.ToString();
        _episodeName = episode?.Title ?? EpisodeID.ToString();
    }

    public override string TypeName => "Send AniDB Episode Rating";

    public override string Title => "Sending AniDB Episode Rating";
    public override Dictionary<string, object> Details => new()
    {
        { "Anime", _animeName },
        { "Episode", _episodeName },
        { "Vote", VoteValue },
    };

    public override Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {EpisodeID} | {Value}", nameof(VoteAniDBEpisodeJob), EpisodeID, VoteValue);

        var vote = requestFactory.Create<RequestVoteEpisode>(
            r =>
            {
                r.EpisodeID = EpisodeID;
                r.Value = VoteValue;
            }
        );
        vote.Send();
        return Task.CompletedTask;
    }
}
