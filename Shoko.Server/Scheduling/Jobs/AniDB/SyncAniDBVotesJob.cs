using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Server.Models.Shoko;
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
    private readonly ISchedulerFactory _schedulerFactory;

    private readonly IRequestFactory _requestFactory;

    private readonly IUserDataService _userDataService;

    private readonly ISettingsProvider _settingsProvider;

    public override string TypeName => "Import AniDB Votes";

    public override string Title => "Import AniDB Votes";

    public int UserID { get; set; }

    public bool Export { get; set; }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(SyncAniDBVotesJob));
        var user = RepoFactory.JMMUser.GetByID(UserID);
        if (user == null)
        {
            _logger.LogInformation("User not found. Aborting.");
            return;
        }

        if (user.IsAniDBUser != 1)
        {
            _logger.LogInformation("User is not an AniDB user. Aborting.");
            return;
        }

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
            _logger.LogInformation("Unable to get votes from AniDB");
            return;
        }

        _logger.LogInformation("Got {Count} votes from AniDB", response.Response.Count);
        if (Export)
            await ExportVotes(user, response.Response);
        else
            ImportVotes(user, response.Response);
    }

    private async Task ExportVotes(JMMUser user, List<ResponseVote> votes)
    {
        _logger.LogInformation("Exporting votes for user {UserID} to AniDB.", user.JMMUserID);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var userData in RepoFactory.AnimeSeries_User.GetByUserID(user.JMMUserID))
        {
            if (RepoFactory.AnimeSeries.GetByID(userData.AnimeSeriesID) is not { } series)
                continue;

            var vote = votes.Find(v => v.EntityID == series.AniDB_ID && v.VoteType is Providers.AniDB.VoteType.AnimePermanent or Providers.AniDB.VoteType.AnimeTemporary);
            if (vote is null && !userData.HasUserRating)
                continue;

            var voteType = vote.VoteType is Providers.AniDB.VoteType.AnimePermanent
                ? SeriesVoteType.Permanent
                : SeriesVoteType.Temporary;
            if (vote is null)
            {
                await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = userData.UserRating.Value;
                    c.VoteType = userData.UserRatingVoteType.Value is SeriesVoteType.Permanent
                        ? Providers.AniDB.VoteType.AnimePermanent
                        : Providers.AniDB.VoteType.AnimeTemporary;
                });
            }
            else if (!userData.HasUserRating)
            {
                await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = -1;
                    c.VoteType = Providers.AniDB.VoteType.AnimePermanent;
                });
            }
            // TODO: Handle the unsetting of permanent vote to set a temporary vote if needed.
            else if (vote.VoteValue != userData.UserRating.Value || (voteType != userData.UserRatingVoteType.Value && voteType is not SeriesVoteType.Permanent))
            {
                await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = userData.UserRating.Value;
                    c.VoteType = userData.UserRatingVoteType.Value is SeriesVoteType.Permanent
                        ? Providers.AniDB.VoteType.AnimePermanent
                        : Providers.AniDB.VoteType.AnimeTemporary;
                });
            }
        }

        foreach (var userData in RepoFactory.AnimeEpisode_User.GetByUserID(user.JMMUserID))
        {
            if (RepoFactory.AnimeEpisode.GetByID(userData.AnimeEpisodeID) is not { } episode)
                continue;

            var vote = votes.Find(v => v.EntityID == episode.AniDB_EpisodeID && v.VoteType is Providers.AniDB.VoteType.Episode);
            if (vote is null && !userData.HasUserRating)
                continue;

            if (vote is null)
            {
                await scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
                {
                    c.EpisodeID = episode.AniDB_EpisodeID;
                    c.VoteValue = userData.UserRating.Value;
                });
            }
            else if (!userData.HasUserRating)
            {
                await scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
                {
                    c.EpisodeID = episode.AniDB_EpisodeID;
                    c.VoteValue = -1;
                });
            }
            else if (vote.VoteValue != userData.UserRating.Value)
            {
                await scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
                {
                    c.EpisodeID = episode.AniDB_EpisodeID;
                    c.VoteValue = userData.UserRating.Value;
                });
            }
        }
    }

    private async void ImportVotes(JMMUser user, List<ResponseVote> votes)
    {
        _logger.LogInformation("Importing votes for user {UserID} from AniDB.", user.JMMUserID);
        foreach (var vote in votes)
        {
            switch (vote.VoteType)
            {
                case Providers.AniDB.VoteType.Episode:
                {
                    if (RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(vote.EntityID) is not { } episode)
                    {
                        _logger.LogInformation("Unable to find episode locally. Skipping import. (Episode={ID})", vote.EntityID);
                        continue;
                    }

                    await _userDataService.ImportEpisodeUserData(episode, user, new() { UserRating = vote.VoteValue }, "AniDB");
                    break;
                }
                case Providers.AniDB.VoteType.AnimePermanent:
                case Providers.AniDB.VoteType.AnimeTemporary:
                {
                    if (RepoFactory.AnimeSeries.GetByAnimeID(vote.EntityID) is not { } series)
                    {
                        _logger.LogInformation("Unable to find series locally. Skipping import. (Anime={ID})", vote.EntityID);
                        continue;
                    }

                    var voteType = vote.VoteType is Providers.AniDB.VoteType.AnimePermanent
                        ? SeriesVoteType.Permanent
                        : SeriesVoteType.Temporary;
                    await _userDataService.ImportSeriesUserData(series, user, new() { UserRating = vote.VoteValue, UserRatingVoteType = voteType }, "AniDB");
                    break;
                }
            }
        }

        _logger.LogInformation("Processed Votes: {Count} Items", votes.Count);
    }

    public SyncAniDBVotesJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, IUserDataService userDataService, ISettingsProvider settingsProvider)
    {
        _schedulerFactory = schedulerFactory;
        _requestFactory = requestFactory;
        _userDataService = userDataService;
        _settingsProvider = settingsProvider;
    }

    protected SyncAniDBVotesJob()
    {
    }
}
