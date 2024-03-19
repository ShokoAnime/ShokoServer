using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class VoteAniDBAnimeJob : BaseJob
{
    private readonly AniDBTitleHelper _titleHelper;
    private readonly IRequestFactory _requestFactory;
    private string _animeName;

    public int AnimeID { get; set; }
    public AniDBVoteType VoteType { get; set; }
    public decimal VoteValue { get; set; }

    public override void PostInit()
    {
        _animeName = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override string TypeName => "Rate Anime";

    public override string Title => "Sending AniDB Anime Vote";
    public override Dictionary<string, object> Details => new()
    {
        { "Anime", _animeName },
        { "Vote", VoteValue },
        { "Type", VoteType.ToString() }
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
    
    public VoteAniDBAnimeJob(IRequestFactory requestFactory, AniDBTitleHelper titleHelper)
    {
        _requestFactory = requestFactory;
        _titleHelper = titleHelper;
    }

    protected VoteAniDBAnimeJob() { }
}
