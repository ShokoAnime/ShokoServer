using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NLog.Fluent;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Collection;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Plex.TVShow;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.Plex;

[Command(CommandRequestType.Plex_Sync)]
public class CommandRequest_PlexSyncWatched : CommandRequestImplementation
{
    private readonly ISettingsProvider _settingsProvider;
    public virtual JMMUser User { get; set; }

    protected override void Process()
    {
        Logger.LogInformation(
            "Syncing watched videos for: {Username}, if nothing happens make sure you have your libraries configured in Shoko",
            User.Username);

        var settings = _settingsProvider.GetSettings();
        foreach (var section in PlexHelper.GetForUser(User).GetDirectories())
        {
            if (!settings.Plex.Libraries.Contains(section.Key))
            {
                continue;
            }

            var allSeries = ((SVR_Directory)section).GetShows();
            foreach (var series in allSeries)
            {
                var episodes = ((SVR_PlexLibrary)series)?.GetEpisodes()?.Where(s => s != null);
                if (episodes == null)
                {
                    continue;
                }

                foreach (var ep in episodes)
                {
                    using (Logger.BeginScope(ep.Key))
                    {
                        var episode = (SVR_Episode)ep;

                        var animeEpisode = episode.AnimeEpisode;


                        Logger.LogTrace("Processing episode {title} of {seriesName}", episode.Title, series.Title);
                        if (animeEpisode == null)
                        {
                            var filePath = episode.Media[0].Part[0].File;
                            Logger.LogTrace("Episode not found in Shoko, skipping - {filename} ({filePath})", Path.GetFileName(filePath), filePath);
                            continue;
                        }

                        var userRecord = animeEpisode.GetUserRecord(User.JMMUserID);
                        var isWatched = episode.ViewCount is > 0;
                        var lastWatched = userRecord?.WatchedDate;
                        if (userRecord?.WatchedCount == 0 && isWatched && episode.LastViewedAt != null)
                        {
                            lastWatched = FromUnixTime((long)episode.LastViewedAt);
                            Logger.LogTrace("Last watched date is {lastWatched}", lastWatched);
                        }

                        var video = animeEpisode.GetVideoLocals()?.FirstOrDefault();
                        if (video == null)
                        {
                            continue;
                        }

                        var alreadyWatched = animeEpisode.GetVideoLocals()
                            .Select(a => a.GetUserRecord(User.JMMUserID))
                            .Where(a => a != null)
                            .Any(x => x.WatchedDate is not null || x.WatchedCount > 0);

                        if (!alreadyWatched && userRecord != null)
                        {
                            alreadyWatched = userRecord.IsWatched();
                        }

                        Logger.LogTrace("Already watched in shoko? {alreadyWatched} Has been watched in plex? {isWatched}", alreadyWatched, isWatched);

                        if (alreadyWatched && !isWatched)
                        {
                            Logger.LogInformation("Marking episode watched in plex");
                            episode.Scrobble();
                        }

                        if (isWatched && !alreadyWatched)
                        {
                            Logger.LogInformation("Marking episode watched in Shoko");
                            video.ToggleWatchedStatus(true, true, lastWatched, true, User.JMMUserID, true, true);
                        }
                    }
                }
            }
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = $"SyncPlex_{User.JMMUserID}";
    }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Syncing Plex for user: {0}",
        queueState = QueueStateEnum.SyncPlex,
        extraParams = new[] { User.Username }
    };

    protected override bool Load()
    {
        User = RepoFactory.JMMUser.GetByID(Convert.ToInt32(CommandDetails));
        return true;
    }


    protected override string GetCommandDetails()
    {
        return User.JMMUserID.ToString(CultureInfo.InvariantCulture);
    }

    private DateTime FromUnixTime(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(unixTime);
    }

    public CommandRequest_PlexSyncWatched(ILoggerFactory loggerFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_PlexSyncWatched()
    {
    }
}
