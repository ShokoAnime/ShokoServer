using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Nancy;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.API.v2.Models.common
{
    [DataContract]
    public class Episode : BaseDirectory
    {
        public override string type => string.Intern("ep");

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string season { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string votes { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int view { get; set; }

        [DataMember]
        public string eptype { get; set; }

        [DataMember]
        public int epnumber { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<RawFile> files { get; set; }

        public Episode()
        {
        }

        internal static Episode GenerateFromAnimeEpisodeID(NancyContext ctx, int anime_episode_id, int uid, int level)
        {
            Episode ep = new Episode();

            if (anime_episode_id > 0)
            {
                ep = GenerateFromAnimeEpisode(ctx, Repositories.RepoFactory.AnimeEpisode.GetByID(anime_episode_id), uid,
                    level);
            }

            return ep;
        }

        internal static Episode GenerateFromAnimeEpisode(NancyContext ctx, SVR_AnimeEpisode aep, int uid, int level)
        {
            Episode ep = new Episode();
            CL_AnimeEpisode_User cae = aep?.GetUserContract(uid);
            if (cae != null)
            {
                TvDB_Episode tvep = aep.TvDBEpisode;
                ep.id = aep.AnimeEpisodeID;
                ep.art = new ArtCollection();
                ep.name = cae.AniDB_EnglishName;

                ep.year = cae.AniDB_AirDate?.Year.ToString(CultureInfo.InvariantCulture);
                ep.air = cae.AniDB_AirDate?.ToPlexDate();

                ep.votes = cae.AniDB_Votes;
                ep.rating = cae.AniDB_Rating;
                var userrating = aep.UserRating;
                if (userrating > 0) ep.userrating = userrating.ToString(CultureInfo.InvariantCulture);
                if (double.TryParse(ep.rating, out double rating))
                {
                    // 0.1 should be the absolute lowest rating
                    if (rating > 10) ep.rating = (rating / 100).ToString(CultureInfo.InvariantCulture);
                }

                ep.view = cae.IsWatched() ? 1 : 0;
                ep.epnumber = cae.EpisodeNumber;
                ep.eptype = aep.EpisodeTypeEnum.ToString();

                ep.art = new ArtCollection();

                if (tvep != null)
                {
                    ep.name = tvep.EpisodeName;
                    ep.art.thumb.Add(new Art {index = 0, url = tvep.GenPoster(null)});
                    ep.art.fanart.Add(new Art {index = 0, url = tvep.GenPoster(null)});
                    ep.summary = tvep.Overview;

                    int zeroPadding = tvep.EpisodeNumber.ToString().Length;
                    string episodeNumber = tvep.EpisodeNumber.ToString().PadLeft(zeroPadding, '0');
                    zeroPadding = tvep.SeasonNumber.ToString().Length;
                    string seasonNumber = tvep.SeasonNumber.ToString().PadLeft(zeroPadding, '0');

                    ep.season = $"{seasonNumber}x{episodeNumber}";
                    var airdate = tvep.AirDate;
                    if (airdate != null)
                    {
                        ep.air = airdate.Value.ToPlexDate();
                        ep.year = airdate.Value.Year.ToString(CultureInfo.InvariantCulture);
                    }
                }
                if (string.IsNullOrEmpty(ep.summary)) ep.summary = string.Intern("Episode Overview not Available");
                if (ep.art.thumb.Count == 0)
                {
                    ep.art.thumb.Add(
                        new Art {index = 0, url = APIHelper.ConstructSupportImageLink(ctx, "plex_404.png")});
                    ep.art.fanart.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructSupportImageLink(ctx, "plex_404.png")
                    });
                }
                if (string.IsNullOrEmpty(ep.year))
                    ep.year = aep.GetAnimeSeries().AirDate.Year.ToString(CultureInfo.InvariantCulture);

                if (level > 0)
                {
                    List<SVR_VideoLocal> vls = aep.GetVideoLocals();
                    if (vls.Count > 0)
                    {
                        ep.files = new List<RawFile>();
                        foreach (SVR_VideoLocal vl in vls)
                        {
                            RawFile file = new RawFile(ctx, vl, (level - 1), uid);
                            ep.files.Add(file);
                        }
                    }
                }
            }

            return ep;
        }
    }
}