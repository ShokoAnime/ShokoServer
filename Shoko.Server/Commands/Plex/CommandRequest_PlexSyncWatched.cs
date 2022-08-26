using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
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

namespace Shoko.Server.Commands.Plex
{
    [Command(CommandRequestType.Plex_Sync)]
    class CommandRequest_PlexSyncWatched : CommandRequestImplementation
    {
        public JMMUser User;

        protected override void Process()
        {
            Logger.LogInformation("Syncing watched videos for: {Username}, if nothing happens make sure you have your libraries configured in Shoko", User.Username);

            foreach (var section in PlexHelper.GetForUser(User).GetDirectories())
            {
                if (!ServerSettings.Instance.Plex.Libraries.Contains(section.Key)) continue;

                var allSeries = ((SVR_Directory) section).GetShows();
                foreach (var series in allSeries)
                {
                    var episodes = ((SVR_PlexLibrary) series)?.GetEpisodes()?.Where(s => s != null);
                    if (episodes == null) continue;
                    foreach (var ep in episodes)
                    {
                        var episode = (SVR_Episode) ep;

                        var animeEpisode = episode.AnimeEpisode;
                        if (animeEpisode == null) continue;
                        var userRecord = animeEpisode.GetUserRecord(User.JMMUserID);
                        var isWatched = episode.ViewCount is > 0;
                        var lastWatched = userRecord?.WatchedDate;
                        if (userRecord?.WatchedCount == 0 && isWatched && episode.LastViewedAt != null)
                        {
                            lastWatched = FromUnixTime((long) episode.LastViewedAt);
                        }

                        var video = animeEpisode.GetVideoLocals()?.FirstOrDefault();
                        if (video == null) continue;
                        var alreadyWatched = animeEpisode.GetVideoLocals()
                            .Select(a => a.GetUserRecord(User.JMMUserID))
                            .Any(x => x.WatchedDate is not null || x.WatchedCount > 0);

                        if (!alreadyWatched && userRecord != null)
                            alreadyWatched = userRecord.IsWatched();

                        if (alreadyWatched && !isWatched) episode.Scrobble();

                        if (isWatched && !alreadyWatched)
                            video.ToggleWatchedStatus(true, true, lastWatched, true, User.JMMUserID, true, true);
                    }
                }
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"SyncPlex_{User.JMMUserID}";
        }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            message = "Syncing Plex for user: {0}",
            queueState = QueueStateEnum.SyncPlex,
            extraParams = new[] {User.Username}
        };

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;
            User = RepoFactory.JMMUser.GetByID(Convert.ToInt32(cq.CommandDetails));
            return true;
        }


        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = User.JMMUserID.ToString(CultureInfo.InvariantCulture),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }

        private DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(unixTime);
        }

        public CommandRequest_PlexSyncWatched(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }
    }
}