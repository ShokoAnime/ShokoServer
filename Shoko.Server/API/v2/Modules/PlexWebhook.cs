using System;
using System.Linq;
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

namespace Shoko.Server.API.v2.Modules
{
    public class PlexWebhook : NancyModule
    {
        public PlexWebhook() : base("/plex")
        {
            Post["/"] = o => WebhookPost();
        }

        object WebhookPost()
        {
            PlexEvent eventData = JsonConvert.DeserializeObject<PlexEvent>(this.Context.Request.Form.payload, new JsonSerializerSettings(){ContractResolver = new CamelCasePropertyNamesContractResolver()});

            switch (eventData.Event)
            {
                case "media.scrobble":
                    Scrobble(eventData);
                    break;
            }

            return null;
        }

        #region Plex events

        private void Scrobble(PlexEvent data)
        {
            PlexEvent.PlexMetadata metadata = data.Metadata;
            if (!data.Metadata.Guid.StartsWith("com.plexapp.agents.shoko://")) return;

            string[] parts = metadata.Guid.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            int animeId = int.Parse(parts[1]);
            int series = int.Parse(parts[2]);

            var anime = RepoFactory.AnimeSeries.GetByID(animeId);

            enEpisodeType episodeType;
            switch (series) //I hate magic number's but this is just about how I can do this, also the rest of this is for later.
            {
                case -4:
                    episodeType  = enEpisodeType.Parody;
                    break;
                case -3:
                    episodeType = enEpisodeType.Trailer;
                    break;
                case -2:
                    episodeType = enEpisodeType.Other;
                    break;
                case -1:
                    episodeType = enEpisodeType.Credits;
                    break;
                case 0:
                    episodeType = enEpisodeType.Special;
                    break;
                default:
                    episodeType = enEpisodeType.Episode;
                    break;
            }

            if (episodeType != enEpisodeType.Episode || metadata.Index == 0) //metadata.index = 0 when it's something else.
                return; //right now no clean way to detect the episode. I could do by title.


            SVR_AnimeEpisode episode = anime
                .GetAnimeEpisodes()
                .Where(a => a.EpisodeTypeEnum == episodeType)
                .Where(a => metadata.Title.Equals(a?.PlexContract?.Title, StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault(a => a?.TvDBEpisode?.SeasonNumber == series);
            if (episode == null) return;

            var user = RepoFactory.JMMUser.GetAll().FirstOrDefault(u => data.Account.Title.FindIn(u.GetPlexUsers()));
            if (user == null)
                return; //At this point in time, we don't want to scrobble for unknown users.

            episode.ToggleWatchedStatus(true, true, FromUnixTime(metadata.LastViewedAt), false, false, user.JMMUserID, true);
            anime.UpdateStats(true, false, true);
        }

        #endregion

        public DateTime FromUnixTime(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixTime);
        }


        #region plexapi

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
            }
        }

        #endregion
    }

    public class PlexWebhookAuthenticated : NancyModule
    {
        public PlexWebhookAuthenticated() : base("/plex")
        {
            this.RequiresAuthentication();
            Get["/pin"] = o => CallPlexHelper(h => h.Authenticate());
            Get["/pin/authenticated"] = o => $"{CallPlexHelper(h => h.IsAuthenticated)}";
            Get["/token/invalidate"] = o => CallPlexHelper(h => {
                h.InvalidateToken();
                return true;
            });

#if DEBUG
            Get["/test/{id}"] = o => Response.AsJson(CallPlexHelper(h => h.GetPlexSeries((int)o.id)));
#endif
        }

        private object CallPlexHelper(Func<PlexHelper, object> act)
        {
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            return act(PlexHelper.GetForUser(user));
        }
    }
}
