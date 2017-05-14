using System;
using System.Globalization;
using System.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Commands.Plex
{
    class CommandRequest_PlexSyncWatched : CommandRequestImplementation, ICommandRequest
    {
        private JMMUser _jmmuser;

        public CommandRequest_PlexSyncWatched()
        {
        }

        public CommandRequest_PlexSyncWatched(JMMUser jmmUser)
        {
            this.CommandType = (int) CommandRequestType.Plex_Sync;
            _jmmuser = jmmUser;
        }


        public override void ProcessCommand()
        {
            foreach (int section in ServerSettings.Plex_Libraries)
            {
                var allSeries = PlexHelper.GetForUser(_jmmuser).GetPlexSeries(section);
                foreach (PlexSeries series in allSeries)
                {
                    foreach (PlexEpisode episode in series.Episodes)
                    {
                        var animeEpisode = episode.AnimeEpisode;
                        var isWatched = episode.WatchCount > 0;
                        var lastWatched = FromUnixTime(episode.LastWatched);
                        SVR_VideoLocal video = animeEpisode?.GetVideoLocals()?.FirstOrDefault();
                        if (video == null) continue;
                        var alreadyWatched = animeEpisode.GetVideoLocals()
                            .Where(x => x.GetAniDBFile() != null)
                            .Any(x => x.GetAniDBFile().IsWatched > 0);

                        if (alreadyWatched && !isWatched)
                            episode.Scrobble();
                        if (isWatched && !alreadyWatched)
                            video.ToggleWatchedStatus(true, true, lastWatched, true, true,
                                _jmmuser.JMMUserID, true, true);
                    }
                }
            }
        }

        public override void GenerateCommandID() => CommandID = $"SyncPlex_{_jmmuser.JMMUserID}";
        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Default;

        public QueueStateStruct PrettyDescription => new QueueStateStruct()
        {
            queueState = QueueStateEnum.SyncPlex,
            extraParams = new[] {_jmmuser.Username}
        };

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;
            _jmmuser = Repositories.RepoFactory.JMMUser.GetByID(Convert.ToInt32(cq.CommandDetails));
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

        public DateTime FromUnixTime(long unixTime) => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMilliseconds(unixTime);
    }
}