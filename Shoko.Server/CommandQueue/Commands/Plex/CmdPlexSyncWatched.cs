using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.Plex.Collection;
using Shoko.Models.Plex.Libraries;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Collection;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Plex.TVShow;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Plex
{
    public class CmdPlexSyncWatched : BaseCommand, ICommand
    {
        public int UserId { get; set; }
        private readonly JMMUser _jmmuser;


        public string ParallelTag { get; set; } = WorkTypes.Plex;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 7;

        public string Id => $"SyncPlex_{UserId}";


        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SyncPlex,
            ExtraParams = new[] { _jmmuser.Username }
        };

        public string WorkType => WorkTypes.Plex;

        public CmdPlexSyncWatched(string str) : base(str)
        {
            _jmmuser = Repo.Instance.JMMUser.GetByID(UserId);
        }

        public CmdPlexSyncWatched(JMMUser jmmUser)
        {
            _jmmuser = jmmUser;
            UserId = jmmUser.JMMUserID;
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Syncing watched videos for: {_jmmuser.Username}, if nothing happens make sure you have your libraries configured in Shoko.");
            try
            {
                List<Directory> dirs = PlexHelper.GetForUser(_jmmuser).GetDirectories().Where(a => ServerSettings.Instance.Plex.Libraries.Contains(a.Key)).ToList();
                double add = 100D / dirs.Count;

                for (int x = 0; x < dirs.Count; x++)
                {
                    Directory section = dirs[x];
                    var allSeries = ((SVR_Directory)section).GetShows();
                    for(int y=0;y<allSeries.Length;y++)
                    {
                        PlexLibrary series = allSeries[y];
                        double pos = x * add + (y+1) * add / allSeries.Length;

                        var episodes = ((SVR_PlexLibrary)series)?.GetEpisodes()?.Where(s => s != null);
                        if (episodes == null) continue;
                        foreach (var ep in episodes)
                        {
                            var episode = (SVR_Episode)ep;

                            var animeEpisode = episode.AnimeEpisode;
                            if (animeEpisode == null) continue;
                            var userRecord = animeEpisode.GetUserRecord(_jmmuser.JMMUserID);
                            var isWatched = episode.ViewCount != null && episode.ViewCount > 0;
                            var lastWatched = userRecord?.WatchedDate;
                            if (userRecord?.WatchedCount == 0 && isWatched && episode.LastViewedAt != null)
                            {
                                lastWatched = FromUnixTime((long)episode.LastViewedAt);
                            }

                            SVR_VideoLocal video = animeEpisode.GetVideoLocals()?.FirstOrDefault();
                            if (video == null) continue;
                            var alreadyWatched = animeEpisode.GetVideoLocals()
                                .Where(a => a.GetAniDBFile() != null)
                                .Any(a => a.GetAniDBFile().IsWatched > 0);

                            if (alreadyWatched && !isWatched) episode.Scrobble();

                            if (isWatched && !alreadyWatched) video.ToggleWatchedStatus(true, true, lastWatched, true, _jmmuser.JMMUserID, true, true);
                            ReportUpdate(progress,pos);
                        }
                    }
                }

                ReportFinish(progress);
            }
            catch (Exception e)
            {
                ReportError(progress, $"Error processing Plex.PlexSyncWatched: {UserId} - {e}", e);
            }
           
        }


        private DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(unixTime);
        }
    }
}