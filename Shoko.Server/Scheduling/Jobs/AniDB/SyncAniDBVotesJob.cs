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
public class SyncAniDBVotesJob(IRequestFactory requestFactory, IQueueScheduler scheduler, IUserDataService userDataService, ISettingsProvider settingsProvider, AnimeEpisodeRepository animeEpisodes, AnimeEpisode_UserRepository animeEpisodeUsers, AnimeSeriesRepository animeSeries, AnimeSeries_UserRepository animeSeriesUsers, JMMUserRepository jmmUsers) : BaseJob
{
    public override string TypeName => "Import AniDB Votes";

    public override string Title => "Import AniDB Votes";

    public int UserID { get; set; }

    public bool Export { get; set; }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(SyncAniDBVotesJob));
        var user = jmmUsers.GetByID(UserID);
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

        var settings = settingsProvider.GetSettings();
        var request = requestFactory.Create<RequestVotes>(
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
        foreach (var userData in animeSeriesUsers.GetByUserID(user.JMMUserID))
        {
            if (animeSeries.GetByID(userData.AnimeSeriesID) is not { } series)
                continue;

            var vote = votes.Find(v => v.EntityID == series.AniDB_ID && v.VoteType is VoteType.AnimePermanent or VoteType.AnimeTemporary);
            if (vote is null && !userData.HasUserRating)
                continue;

            var voteType = vote.VoteType is VoteType.AnimePermanent
                ? SeriesVoteType.Permanent
                : SeriesVoteType.Temporary;
            if (vote is null)
            {
                await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
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
                await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AniDB_ID;
                    c.VoteValue = -1;
                    c.VoteType = VoteType.AnimePermanent;
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
                        ? VoteType.AnimePermanent
                        : VoteType.AnimeTemporary;
                });
            }
        }

        foreach (var userData in animeEpisodeUsers.GetByUserID(user.JMMUserID))
        {
            if (animeEpisodes.GetByID(userData.AnimeEpisodeID) is not { } episode)
                continue;

            var vote = votes.Find(v => v.EntityID == episode.AniDB_EpisodeID && v.VoteType is VoteType.Episode);
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
                case VoteType.Episode:
                {
                    if (animeEpisodes.GetByAniDBEpisodeID(vote.EntityID) is not { } episode)
                    {
                        _logger.LogInformation("Unable to find episode locally. Skipping import. (Episode={ID})", vote.EntityID);
                        continue;
                    }

                    await userDataService.ImportEpisodeUserData(episode, user, new() { UserRating = vote.VoteValue }, "AniDB");
                    break;
                }
                case VoteType.AnimePermanent:
                case VoteType.AnimeTemporary:
                {
                    if (animeSeries.GetByAnimeID(vote.EntityID) is not { } series)
                    {
                        _logger.LogInformation("Unable to find series locally. Skipping import. (Anime={ID})", vote.EntityID);
                        continue;
                    }

                    var voteType = vote.VoteType is VoteType.AnimePermanent
                        ? SeriesVoteType.Permanent
                        : SeriesVoteType.Temporary;
                    await userDataService.ImportSeriesUserData(series, user, new() { UserRating = vote.VoteValue, UserRatingVoteType = voteType }, "AniDB");
                    break;
                }
            }
        }

        _logger.LogInformation("Processed Votes: {Count} Items", votes.Count);
    }

}
