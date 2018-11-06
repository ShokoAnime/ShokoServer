using System;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Models.Server;
using NLog;

using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Libraries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Shoko.Models.Plex.Collection;
using Shoko.Models.Plex.Libraries;
using Shoko.Server.CommandQueue;
using Shoko.Server.CommandQueue.Commands.Plex;

namespace Shoko.Server.API.v2.Modules
{
    [ApiController]
    [Route("/plex")]
    public class PlexWebhook : Controller
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [HttpPost]
        public ActionResult WebhookPost([FromForm] PlexEvent eventData)
        {
            /*PlexEvent eventData = JsonConvert.DeserializeObject<PlexEvent>(this.Context.Request.Form.payload,
                new JsonSerializerSettings() {ContractResolver = new CamelCasePropertyNamesContractResolver()});*/

            logger.Trace($"{eventData.Event}: {eventData.Metadata.Guid}");
            switch (eventData.Event)
            {
                case "media.scrobble":
                    Scrobble(eventData);
                    break;
                case "media.resume":
                case "media.play":
                    TraktScrobble(eventData, ScrobblePlayingStatus.Start);
                    break;
                case "media.pause":
                    TraktScrobble(eventData, ScrobblePlayingStatus.Pause);
                    break;
                case "media.stop":
                    TraktScrobble(eventData, ScrobblePlayingStatus.Stop);
                    break;
            }

            return APIStatus.OK();
        }
        #region Plex events

        [NonAction]
        private static void TraktScrobble(PlexEvent evt, ScrobblePlayingStatus type)
        {
            PlexEvent.PlexMetadata metadata = evt.Metadata;
            (SVR_AnimeEpisode episode, SVR_AnimeSeries anime) = GetEpisode(metadata);

            if (episode == null) return;

            var vl = Repo.Instance.VideoLocal.GetByAniDBEpisodeID(episode.AniDB_EpisodeID).FirstOrDefault();

            float per = 100 * (metadata.ViewOffset / (float)vl.Duration); //this will be nice if plex would ever give me the duration, so I don't have to guess it.

            ScrobblePlayingType scrobbleType = episode.PlexContract.IsMovie ? ScrobblePlayingType.movie : ScrobblePlayingType.episode;

            TraktTVHelper.Scrobble(scrobbleType, episode.AnimeEpisodeID.ToString(), type, per);
        }

        [NonAction]
        private void Scrobble(PlexEvent data)
        {
            PlexEvent.PlexMetadata metadata = data.Metadata;
            (SVR_AnimeEpisode episode, SVR_AnimeSeries anime) = GetEpisode(metadata);
            if (episode == null)
            {
                logger.Info("No episode returned, aborting scrobble. This might not have been a ShokoMetadata library");
                return;
            }

            logger.Trace($"Got anime: {anime}, ep: {episode.PlexContract.EpisodeNumber}");

            var user = Repo.Instance.JMMUser.GetAll().FirstOrDefault(u => data.Account.Title.FindIn(u.GetPlexUsers()));
            if (user == null)
            {
                logger.Info($"Unable to determine who \"{data.Account.Title}\" is in Shoko, make sure this is set under user settings in Desktop");
                return; //At this point in time, we don't want to scrobble for unknown users
            }

            episode.ToggleWatchedStatus(true, true, FromUnixTime(metadata.LastViewedAt), false, user.JMMUserID,
                true);
            anime.UpdateStats(true, false, true);
        }

        #endregion

        [NonAction]
        private static (SVR_AnimeEpisode, SVR_AnimeSeries) GetEpisode(PlexEvent.PlexMetadata metadata)
        {
            if (!metadata.Guid.StartsWith("com.plexapp.agents.shoko://")) return (null, null);

            string[] parts = metadata.Guid.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int animeId = int.Parse(parts[1]);
            int series = int.Parse(parts[2]);

            var anime = Repo.Instance.AnimeSeries.GetByID(animeId);

            EpisodeType episodeType;
            switch (series) //I hate magic number's but this is just about how I can do this, also the rest of this is for later.
            {
                case -4:
                    episodeType = EpisodeType.Parody;
                    break;
                case -3:
                    episodeType = EpisodeType.Trailer;
                    break;
                case -2:
                    episodeType = EpisodeType.Other;
                    break;
                case -1:
                    episodeType = EpisodeType.Credits;
                    break;
                case 0:
                    episodeType = EpisodeType.Special;
                    break;
                default:
                    episodeType = EpisodeType.Episode;
                    break;
            }

            if (episodeType != EpisodeType.Episode ||
                metadata.Index == 0) //metadata.index = 0 when it's something else.
                return (null, anime); //right now no clean way to detect the episode. I could do by title.


            return (anime
                .GetAnimeEpisodes()
                .Where(a => a.AniDB_Episode != null)
                .Where(a => a.EpisodeTypeEnum == episodeType)
                .Where(a => metadata.Title.Equals(a?.PlexContract?.Title, StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault(a => a?.TvDBEpisode?.SeasonNumber == series), anime);
        }

        [NonAction]
        public DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
        }

        [Authorize]
        [HttpGet("loginurl")]
        public string GetLoginUrl() => CallPlexHelper(h => h.LoginUrl);

        [Authorize]
        [HttpGet("pin/authenticated")]
        public bool IsAuthenticated() => CallPlexHelper(h => h.IsAuthenticated);

        [Authorize]
        [HttpGet("token/invalidate")]
        public bool InvalidateToken() => CallPlexHelper(h => { h.InvalidateToken(); return true; });

        [Authorize]
        [HttpGet("sync")]
        public ActionResult Sync()
        {
            Queue.Instance.Add(new CmdPlexSyncWatched(HttpContext.GetUser()));
            return APIStatus.OK();
        }

        [Authorize("admin")]
        [HttpGet("sync/all")]
        public ActionResult SyncAll()
        {
            ShokoServer.Instance.SyncPlex();
            return APIStatus.OK();
        }

        [Authorize("admin")]
        [HttpGet("sync/{id}")]
        public ActionResult SyncForUser(int uid)
        {
            JMMUser user = Repo.Instance.JMMUser.GetByID(uid);
            ShokoServer.Instance.SyncPlex();
            return APIStatus.OK();
        }


#if DEBUG
        [Authorize]
        [HttpGet("test/dir")]
        public Directory[] GetDirectories() => CallPlexHelper(h => h.GetDirectories());

        [Authorize]
        [HttpGet("test/lib/{id}")]
        public PlexLibrary[] GetShowsForDirectory(int id) => CallPlexHelper(h => ((SVR_Directory)h.GetDirectories().FirstOrDefault(d => d.Key == id))?.GetShows());
#endif

        [NonAction]
        private T CallPlexHelper<T>(Func<PlexHelper, T> act)
        {
            JMMUser user = HttpContext.GetUser();
            return act(PlexHelper.GetForUser(user));
        }

        #region plexapi
#pragma warning disable 0649
        public class PlexEvent
        {
            public string Event;
            public bool User;
            public bool Owner;

            public PlexAccount Account;
            public PlexBasicInfo Server;
            public PlexPlayerInfo Player;
            public PlexMetadata Metadata;

            public class PlexAccount
            {
                public int Id;
                public string Thumb;
                public string Title;
            }

            public class PlexBasicInfo
            {
                public string Title;
                public string Uuid;
            }

            public class PlexPlayerInfo : PlexBasicInfo
            {
                public bool Local;
                public string PublicAddress;
            }

            public class PlexMetadata
            {
                public string LibrarySectionType;
                public string RatingKey;
                public string Key;
                public string ParentRatingKey;
                public string GrandparentRatingKey;
                public string Guid;
                public int LibrarySectionId;
                public string Type;
                public string Title;
                public string GrandparentKey;
                public string ParentKey;
                public string GranparentTitle;
                public string Summary;
                public int Index;
                public int ParentIndex;
                public int RatingCount;
                public string Thumb;
                public string Art;
                public string ParentThumb;
                public string GrandparentThumb;
                public string GrandparentArt;
                public int LastViewedAt;
                public int AddedAt;
                public int UpdatedAt;
                public int ViewOffset;
            }
        }
         #pragma warning restore 0649
        #endregion
    }
}