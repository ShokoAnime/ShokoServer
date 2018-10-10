using System;
using System.Linq;
using System.Threading.Tasks;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Models.Server;
using Nancy.Security;
using NLog;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Plex;
using Shoko.Server.Plex.Libraries;

namespace Shoko.Server.API.v2.Modules
{
    public class PlexWebhook : NancyModule
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public PlexWebhook() : base("/plex")
        {
            Post["/", true] = async (x,ct) => await Task.Factory.StartNew(WebhookPost, ct);
        }

        object WebhookPost()
        {
            PlexEvent eventData = JsonConvert.DeserializeObject<PlexEvent>(this.Context.Request.Form.payload,
                new JsonSerializerSettings() {ContractResolver = new CamelCasePropertyNamesContractResolver()});

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

        private static void TraktScrobble(PlexEvent evt, ScrobblePlayingStatus type)
        {
            PlexEvent.PlexMetadata metadata = evt.Metadata;
            (SVR_AnimeEpisode episode, SVR_AnimeSeries anime) = GetEpisode(metadata);

            if (episode == null) return;

            var vl = RepoFactory.VideoLocal.GetByAniDBEpisodeID(episode.AniDB_EpisodeID).FirstOrDefault();

            float per = 100 * (metadata.ViewOffset / (float)vl.Duration); //this will be nice if plex would ever give me the duration, so I don't have to guess it.

            ScrobblePlayingType scrobbleType = episode.PlexContract.IsMovie ? ScrobblePlayingType.movie : ScrobblePlayingType.episode;

            TraktTVHelper.Scrobble(scrobbleType, episode.AnimeEpisodeID.ToString(), type, per);
        }

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

            var user = RepoFactory.JMMUser.GetAll().FirstOrDefault(u => data.Account.Title.FindIn(u.GetPlexUsers()));
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

        private static (SVR_AnimeEpisode, SVR_AnimeSeries) GetEpisode(PlexEvent.PlexMetadata metadata)
        {
            if (!metadata.Guid.StartsWith("com.plexapp.agents.shoko://")) return (null, null);

            string[] parts = metadata.Guid.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int animeId = int.Parse(parts[1]);
            int series = int.Parse(parts[2]);

            var anime = RepoFactory.AnimeSeries.GetByID(animeId);

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

        public DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
        }


        #region plexapi
        #pragma warning disable 0649
        internal class PlexEvent
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

    public class PlexWebhookAuthenticated : NancyModule
    {
        public PlexWebhookAuthenticated() : base("/plex")
        {
            this.RequiresAuthentication();
            //Get["/pin"] = o => CallPlexHelper(h => h.Authenticate());
            Get["/loginurl"] = o => CallPlexHelper(h => h.LoginUrl);
            Get["/pin/authenticated"] = o => $"{CallPlexHelper(h => h.IsAuthenticated)}";
            Get["/token/invalidate"] = o => CallPlexHelper(h =>
            {
                h.InvalidateToken();
                return true;
            });
            Get["/sync", true] = async (x, ct) => await Task.Factory.StartNew(() =>
            {
                new CommandRequest_PlexSyncWatched((JMMUser) this.Context.CurrentUser).Save();
                return APIStatus.OK();
            });
            Get["/sync/all", true] = async (x, ct) => await Task.Factory.StartNew(() =>
            {
                if (((JMMUser) this.Context.CurrentUser).IsAdmin != 1) return APIStatus.AdminNeeded();
                ShokoServer.Instance.SyncPlex();
                return APIStatus.OK();
            });

            Get["/sync/{id}", true] = async (x, ct) => await Task.Factory.StartNew(() =>
            {
                if (((JMMUser)this.Context.CurrentUser).IsAdmin != 1) return APIStatus.AdminNeeded();
                JMMUser user = RepoFactory.JMMUser.GetByID(x.id);
                ShokoServer.Instance.SyncPlex();
                return APIStatus.OK();
            });
#if DEBUG
            Get["/test/dir"] = o => Response.AsJson(CallPlexHelper(h => h.GetDirectories()));
            Get["/test/lib/{id}"] = o =>
                Response.AsJson(CallPlexHelper(h =>
                    ((SVR_Directory) h.GetDirectories().FirstOrDefault(d => d.Key == (int) o.id))?.GetShows()));
#endif
        }

        private object CallPlexHelper(Func<PlexHelper, object> act)
        {
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            return act(PlexHelper.GetForUser(user));
        }
    }
}