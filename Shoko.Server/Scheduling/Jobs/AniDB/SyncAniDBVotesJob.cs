using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
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
    private readonly IAnidbService _anidbService;
    private readonly ISettingsProvider _settingsProvider;

    public override string TypeName => "Sync AniDB Votes";

    public override string Title => "Syncing AniDB Votes";

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

            // only download the anime info if the user doesn't already have it
            if (RepoFactory.AniDB_Anime.GetByAnimeID(thisVote.EntityID) is not null)
                continue;

            var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful;
            if (settings.AniDb.AutomaticallyImportSeries)
                refreshMethod |= AnidbRefreshMethod.CreateShokoSeries;
            await _anidbService.ScheduleRefreshByID(thisVote.EntityID, refreshMethod).ConfigureAwait(false);
        }

        _logger.LogInformation("Processed Votes: {Count} Items", response.Response.Count);
    }

    public SyncAniDBVotesJob(IRequestFactory requestFactory, IAnidbService anidbService, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _anidbService = anidbService;
        _settingsProvider = settingsProvider;
    }

    protected SyncAniDBVotesJob()
    {
    }
}
