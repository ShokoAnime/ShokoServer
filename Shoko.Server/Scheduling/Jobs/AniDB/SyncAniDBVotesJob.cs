using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories.Cached;
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
    private readonly IQueueScheduler _scheduler;

    private readonly IRequestFactory _requestFactory;

    private readonly IUserDataService _userDataService;

    private readonly ISettingsProvider _settingsProvider;

    public override string TypeName => "Import AniDB Votes";

    public override string Title => "Import AniDB Votes";

    public int UserID { get; set; }

    public bool Export { get; set; }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(SyncAniDBVotesJob));
        var user = _jmmUsers.GetByID(UserID);
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
        foreach (var userData in _animeSeriesUsers.GetByUserID(user.JMMUserID))
        {
            if (_animeSeries.GetByID(userData.AnimeSeriesID) is not { } series)
                continue;

            var vote = votes.Find(v => v.EntityID == series.AniDB_ID && v.VoteType is VoteType.AnimePermanent or VoteType.AnimeTemporary);
            if (vote is null && !userData.HasUserRating)
                continue;

            var voteType = vote.VoteType is VoteType.AnimePermanent
                ? SeriesVoteType.Permanent
                : SeriesVoteType.Temporary;
            if (vote is null)
            {
                await _scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = userData.UserRating.Value;
                    c.VoteType = userData.UserRatingVoteType.Value is SeriesVoteType.Permanent
                        ? VoteType.AnimePermanent
                        : VoteType.AnimeTemporary;
                });
            }
            else if (!userData.HasUserRating)
            {
                await _scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = -1;
                    c.VoteType = VoteType.AnimePermanent;
                });
            }
            // TODO: Handle the unsetting of permanent vote to set a temporary vote if needed.
            else if (vote.VoteValue != userData.UserRating.Value || (voteType != userData.UserRatingVoteType.Value && voteType is not SeriesVoteType.Permanent))
            {
                await _scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = userData.UserRating.Value;
                    c.VoteType = userData.UserRatingVoteType.Value is SeriesVoteType.Permanent
                        ? VoteType.AnimePermanent
                        : VoteType.AnimeTemporary;
                });
            }
        }

        foreach (var userData in _animeEpisodeUsers.GetByUserID(user.JMMUserID))
        {
            if (_animeEpisodes.GetByID(userData.AnimeEpisodeID) is not { } episode)
                continue;

            var vote = votes.Find(v => v.EntityID == episode.AniDB_EpisodeID && v.VoteType is VoteType.Episode);
            if (vote is null && !userData.HasUserRating)
                continue;

            if (vote is null)
            {
                await _scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
                {
                    c.EpisodeID = episode.AniDB_EpisodeID;
                    c.VoteValue = userData.UserRating.Value;
                });
            }
            else if (!userData.HasUserRating)
            {
                await _scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
                {
                    c.EpisodeID = episode.AniDB_EpisodeID;
                    c.VoteValue = -1;
                });
            }
            else if (vote.VoteValue != userData.UserRating.Value)
            {
                await _scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
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
                case VoteType.Episode:
                {
                    if (_animeEpisodes.GetByAniDBEpisodeID(vote.EntityID) is not { } episode)
                    {
                        _logger.LogInformation("Unable to find episode locally. Skipping import. (Episode={ID})", vote.EntityID);
                        continue;
                    }

                    await _userDataService.ImportEpisodeUserData(episode, user, new() { UserRating = vote.VoteValue }, "AniDB");
                    break;
                }
                case VoteType.AnimePermanent:
                case VoteType.AnimeTemporary:
                {
                    if (_animeSeries.GetByAnimeID(vote.EntityID) is not { } series)
                    {
                        _logger.LogInformation("Unable to find series locally. Skipping import. (Anime={ID})", vote.EntityID);
                        continue;
                    }

                    var voteType = vote.VoteType is VoteType.AnimePermanent
                        ? SeriesVoteType.Permanent
                        : SeriesVoteType.Temporary;
                    await _userDataService.ImportSeriesUserData(series, user, new() { UserRating = vote.VoteValue, UserRatingVoteType = voteType }, "AniDB");
                    break;
                }
            }
        }

        _logger.LogInformation("Processed Votes: {Count} Items", votes.Count);
    }

    private readonly AnimeEpisodeRepository _animeEpisodes;
    private readonly AnimeEpisode_UserRepository _animeEpisodeUsers;
    private readonly AnimeSeriesRepository _animeSeries;
    private readonly AnimeSeries_UserRepository _animeSeriesUsers;
    private readonly JMMUserRepository _jmmUsers;
    public SyncAniDBVotesJob(IRequestFactory requestFactory, IQueueScheduler schedulerFactory, IUserDataService userDataService, ISettingsProvider settingsProvider,
        AnimeEpisodeRepository animeEpisodes,
        AnimeEpisode_UserRepository animeEpisodeUsers,
        AnimeSeriesRepository animeSeries,
        AnimeSeries_UserRepository animeSeriesUsers,
        JMMUserRepository jmmUsers
    )
    {
        _scheduler = schedulerFactory;
        _requestFactory = requestFactory;
        _userDataService = userDataService;
        _settingsProvider = settingsProvider;
        _animeEpisodes = animeEpisodes;
        _animeEpisodeUsers = animeEpisodeUsers;
        _animeSeries = animeSeries;
        _animeSeriesUsers = animeSeriesUsers;
        _jmmUsers = jmmUsers;

    }

    protected SyncAniDBVotesJob()
    {
    }
}
