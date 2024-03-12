using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Collection;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Plex.TVShow;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Plex;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SyncPlexWatchedStatesJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    public JMMUser User { get; set; }

    public override string TypeName => "Sync Series to Trakt Collection";

    public override string Title => "Sync Plex States for User";
    public override Dictionary<string, object> Details => new()
    {
        { "User", User.Username }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> User: {Name}", nameof(SyncTraktCollectionSeriesJob), User.Username);
        var settings = _settingsProvider.GetSettings();
        foreach (var section in PlexHelper.GetForUser(User).GetDirectories().Where(a => settings.Plex.Libraries.Contains(a.Key)))
        {
            var allSeries = ((SVR_Directory)section).GetShows();
            foreach (var series in allSeries)
            {
                var episodes = ((SVR_PlexLibrary)series)?.GetEpisodes()?.Where(s => s != null);
                if (episodes == null) continue;

                foreach (var ep in episodes)
                {
                    using var scope = _logger.BeginScope(ep.Key);
                    var episode = (SVR_Episode)ep;

                    var animeEpisode = episode.AnimeEpisode;


                    _logger.LogInformation("Processing episode {Title} of {SeriesName}", episode.Title, series.Title);
                    if (animeEpisode == null)
                    {
                        var filePath = episode.Media[0].Part[0].File;
                        _logger.LogTrace("Episode not found in Shoko, skipping - {Filename} ({FilePath})", Path.GetFileName(filePath), filePath);
                        continue;
                    }

                    var userRecord = animeEpisode.GetUserRecord(User.JMMUserID);
                    var isWatched = episode.ViewCount is > 0;
                    var lastWatched = userRecord?.WatchedDate;
                    if ((userRecord?.WatchedCount ?? 0) == 0 && isWatched && episode.LastViewedAt != null)
                    {
                        lastWatched = FromUnixTime((long)episode.LastViewedAt);
                        _logger.LogTrace("Last watched date is {LastWatched}", lastWatched);
                    }

                    var video = animeEpisode.GetVideoLocals()?.FirstOrDefault();
                    if (video == null) continue;

                    var alreadyWatched = animeEpisode.GetVideoLocals()
                        .Select(a => a.GetUserRecord(User.JMMUserID))
                        .Where(a => a != null)
                        .Any(x => x.WatchedDate is not null || x.WatchedCount > 0);

                    if (!alreadyWatched && userRecord != null)
                    {
                        alreadyWatched = userRecord.IsWatched();
                    }

                    _logger.LogTrace("Already watched in shoko? {AlreadyWatched} Has been watched in plex? {IsWatched}", alreadyWatched, isWatched);

                    if (alreadyWatched && !isWatched)
                    {
                        _logger.LogInformation("Marking episode watched in plex");
                        episode.Scrobble();
                    }

                    if (isWatched && !alreadyWatched)
                    {
                        _logger.LogInformation("Marking episode watched in Shoko");
                        video.ToggleWatchedStatus(true, true, lastWatched ?? DateTime.Now, true, User.JMMUserID, true, true);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private DateTime FromUnixTime(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(unixTime);
    }

    public SyncPlexWatchedStatesJob(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    protected SyncPlexWatchedStatesJob() { }
}
