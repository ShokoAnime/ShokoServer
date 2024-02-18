using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBHttpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class SyncAniDBVotesJob : BaseJob
{
    // TODO make this use Quartz scheduling
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;

    public override string Name => "Sync AniDB Votes";
    public override QueueStateStruct Description => new()
    {
        message = "Upload Local Votes To AniDB",
        queueState = QueueStateEnum.Actions_SyncVotes,
        extraParams = Array.Empty<string>()
    };

    public override async Task Process()
    {
        // TODO rewrite this. It doesn't appear to do what anyone thought it did, at least functionally
        _logger.LogInformation("Processing {Job}", nameof(SyncAniDBVotesJob));

        var settings = _settingsProvider.GetSettings();
        var request = _requestFactory.Create<RequestVotes>(
            r =>
            {
                r.Username = settings.AniDb.Username;
                r.Password = settings.AniDb.Password;
            }
        );
        var response = request.Send();
        if (response.Response == null)
        {
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var myVote in response.Response)
        {
            var dbVotes = RepoFactory.AniDB_Vote.GetByEntity(myVote.EntityID);
            AniDB_Vote thisVote = null;
            foreach (var dbVote in dbVotes)
            {
                // we can only have anime permanent or anime temp but not both
                if (myVote.VoteType is AniDBVoteType.Anime or AniDBVoteType.AnimeTemp)
                {
                    if (dbVote.VoteType is (int)AniDBVoteType.Anime or (int)AniDBVoteType.AnimeTemp)
                    {
                        thisVote = dbVote;
                    }
                }
                else
                {
                    thisVote = dbVote;
                }
            }

            thisVote ??= new AniDB_Vote { EntityID = myVote.EntityID };
            thisVote.VoteType = (int)myVote.VoteType;
            thisVote.VoteValue = (int)(myVote.VoteValue * 100);

            RepoFactory.AniDB_Vote.Save(thisVote);

            if (myVote.VoteType is not (AniDBVoteType.Anime or AniDBVoteType.AnimeTemp)) continue;

            // download the anime info if the user doesn't already have it
            await scheduler.StartJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = thisVote.EntityID;
                c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
            });
        }

        _logger.LogInformation("Processed Votes: {Count} Items", response.Response.Count);
    }

    
    public SyncAniDBVotesJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
    }

    protected SyncAniDBVotesJob()
    {
    }
}
