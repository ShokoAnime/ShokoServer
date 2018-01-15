using System;
using System.Globalization;
using System.Linq;
using Shoko.Models.Plex.Libraries;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Collection;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Plex.TVShow;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.Plex
{
    [Command(CommandRequestType.Plex_Sync)]
    class CommandRequest_PlexSyncWatched : CommandRequestImplementation
    {
        private JMMUser _jmmuser;

        public CommandRequest_PlexSyncWatched()
        {
        }

        public CommandRequest_PlexSyncWatched(JMMUser jmmUser)
        {
            _jmmuser = jmmUser;
        }


        public override void ProcessCommand()
        {
            foreach (var section in PlexHelper.GetForUser(_jmmuser).GetDirectories().Where(d => ServerSettings.Plex_Libraries.Contains(d.Key)))
            {
                var allSeries = ((SVR_Directory)section).GetShows();
                foreach (var series in allSeries)
                {
                    if (series == null) continue; //I don't know why this occurs.
                    foreach (var ep in ((SVR_PlexLibrary)series).GetEpisodes())
                    {
                        var episode = (SVR_Episode) ep;

                        var animeEpisode = episode.AnimeEpisode;
                        if (animeEpisode == null) continue;
                        var userRecord = animeEpisode.GetUserRecord(_jmmuser.JMMUserID);
                        var isWatched = episode.ViewCount > 0;
                        var lastWatched = userRecord.WatchedDate;
                        if (userRecord.WatchedCount == 0 && isWatched && episode.LastViewedAt != null)
                        {
                            lastWatched = FromUnixTime((long) episode.LastViewedAt);
                        }
                        SVR_VideoLocal video = animeEpisode.GetVideoLocals()?.FirstOrDefault();
                        if (video == null) continue;
                        var alreadyWatched = animeEpisode.GetVideoLocals()
                            .Where(x => x.GetAniDBFile() != null)
                            .Any(x => x.GetAniDBFile().IsWatched > 0);

                        if (alreadyWatched && !isWatched)
                            episode.Scrobble();

                        if (isWatched && !alreadyWatched)
                            video.ToggleWatchedStatus(true, true, lastWatched, true,
                                _jmmuser.JMMUserID, true, true);
                    }
                }
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"SyncPlex_{_jmmuser.JMMUserID}";
        }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SyncPlex,
            extraParams = new[] {_jmmuser.Username}
        };

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;
            _jmmuser = RepoFactory.JMMUser.GetByID(Convert.ToInt32(cq.CommandDetails));
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
                CommandDetails = _jmmuser.JMMUserID.ToString(CultureInfo.InvariantCulture),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }

        private DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(unixTime);
        }
    }
}