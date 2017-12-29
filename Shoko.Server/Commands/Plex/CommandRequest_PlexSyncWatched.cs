using System;
using System.Linq;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_PlexSyncWatched : CommandRequest
    {
        private JMMUser _jmmuser;

        public override string CommandDetails => _jmmuser.JMMUserID.ToString();

        public CommandRequest_PlexSyncWatched()
        {
        }

        public CommandRequest_PlexSyncWatched(JMMUser jmmUser)
        {
            CommandType = (int) CommandRequestType.Plex_Sync;
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
                        var userRecord = animeEpisode.GetUserRecord(_jmmuser.JMMUserID);
                        var isWatched = episode.WatchCount > 0;
                        var lastWatched = userRecord.WatchedDate;
                        if (userRecord.WatchedCount == 0 && isWatched)
                        {
                            lastWatched = FromUnixTime(episode.LastWatched);
                        }
                        SVR_VideoLocal video = animeEpisode?.GetVideoLocals()?.FirstOrDefault();
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

        public override bool InitFromDB(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;
            _jmmuser = RepoFactory.JMMUser.GetByID(Convert.ToInt32(cq.CommandDetails));
            return true;
        }

        public virtual DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(unixTime);
        }
    }
}