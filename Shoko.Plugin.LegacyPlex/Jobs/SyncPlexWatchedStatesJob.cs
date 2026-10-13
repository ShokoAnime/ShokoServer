using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.User.Services;
using Shoko.Plugin.LegacyPlex.Collection;
using Shoko.Plugin.LegacyPlex.Libraries;
using Shoko.Plugin.LegacyPlex.TVShow;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Plugin.LegacyPlex.Jobs;

/// <summary>
///   Syncs a user's watched states with the picked Plex libraries, both ways:
///   what is watched on one side only is marked watched on the other.
/// </summary>
/// <param name="plexHelpers">The users' Plex clients.</param>
/// <param name="settingsProvider">The server settings, holding the picked libraries.</param>
/// <param name="userService">The users.</param>
/// <param name="userDataService">The users' watched states.</param>
[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(1, 1)]
[JobKeyGroup("Actions")]
public class SyncPlexWatchedStatesJob(
    PlexHelperProvider plexHelpers,
    ISettingsProvider settingsProvider,
    IUserService userService,
    IUserDataService userDataService
) : IQueueJob
{
    private ILogger _logger = null!;

    /// <summary>
    ///   The ID of the user to sync.
    /// </summary>
    public required int UserID { get; set; }

    /// <inheritdoc/>
    [JsonIgnore]
    public string TypeName => "Sync Plex States for User";

    /// <inheritdoc/>
    [JsonIgnore]
    public string Title => "Syncing Plex States for User";

    /// <inheritdoc/>
    [JsonIgnore]
    public Dictionary<string, object> Details => new()
    {
        { "User", userService.GetUserByID(UserID)?.Username ?? UserID.ToString() }
    };

    /// <inheritdoc/>
    public void Setup(IServiceProvider serviceProvider)
        => _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

    /// <inheritdoc/>
    public async Task Process()
    {
        if (userService.GetUserByID(UserID) is not { } user)
        {
            _logger.LogWarning("Skipping {Job}, the user {UserID} is gone", nameof(SyncPlexWatchedStatesJob), UserID);
            return;
        }

        _logger.LogInformation("Processing {Job} -> User: {Name}", nameof(SyncPlexWatchedStatesJob), user.Username);
        var libraries = settingsProvider.GetSettings().Plex.Libraries;
        foreach (var section in plexHelpers.GetForUser(user).GetDirectories().Where(a => libraries.Contains(a.Key)))
        {
            var allSeries = ((SVR_Directory)section).GetShows();
            foreach (var series in allSeries)
            {
                var episodes = ((SVR_PlexLibrary)series)?.GetEpisodes()?.WhereNotNull();
                if (episodes == null) continue;

                foreach (var ep in episodes)
                {
                    using var scope = _logger.BeginScope(ep.Key);
                    var episode = (SVR_Episode)ep;

                    var animeEpisode = episode.AnimeEpisode;

                    _logger.LogInformation("Processing episode {Title} of {SeriesName}", episode.Title, series!.Title);
                    if (animeEpisode == null)
                    {
                        var filePath = episode.Media[0].Part[0].File;
                        _logger.LogTrace("Episode not found in Shoko, skipping - {Filename} ({FilePath})", Path.GetFileName(filePath), filePath);
                        continue;
                    }

                    var userRecord = userDataService.GetEpisodeUserData(animeEpisode, user);
                    var isWatched = episode.ViewCount is > 0;
                    var lastWatched = userRecord.LastPlayedAt;
                    if (userRecord.PlaybackCount == 0 && isWatched && episode.LastViewedAt != null)
                    {
                        lastWatched = DateTime.UnixEpoch.AddSeconds((long)episode.LastViewedAt);
                        _logger.LogTrace("Last watched date is {LastWatched}", lastWatched);
                    }

                    var videos = animeEpisode.Videos;
                    if (videos.Count == 0) continue;

                    var alreadyWatched = videos
                        .Select(video => userDataService.GetVideoUserData(video, user))
                        .WhereNotNull()
                        .Any(userData => userData.IsWatched) || userRecord.IsWatched;

                    _logger.LogTrace("Already watched in shoko? {AlreadyWatched} Has been watched in plex? {IsWatched}", alreadyWatched, isWatched);

                    if (alreadyWatched && !isWatched)
                    {
                        _logger.LogInformation("Marking episode watched in plex");
                        episode.Scrobble();
                    }

                    if (isWatched && !alreadyWatched)
                    {
                        _logger.LogInformation("Marking episode watched in Shoko");
                        await userDataService.SaveVideoUserData(videos[0], user, new() { LastPlayedAt = lastWatched ?? DateTime.Now });
                    }
                }
            }
        }
    }
}
