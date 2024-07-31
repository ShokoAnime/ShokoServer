using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Utils;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class Episode : BaseDirectory
{
    public override string type => string.Intern("ep");

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string season { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int view { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public DateTime? view_date { get; set; }

    [DataMember] public string eptype { get; set; }

    [DataMember] public int epnumber { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int aid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int eid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<RawFile> files { get; set; }

    internal static Episode GenerateFromAnimeEpisodeID(HttpContext ctx, int anime_episode_id, int uid, int level,
        int pic = 1)
    {
        var ep = new Episode();

        if (anime_episode_id > 0)
        {
            ep = GenerateFromAnimeEpisode(ctx, RepoFactory.AnimeEpisode.GetByID(anime_episode_id), uid,
                level, pic);
        }

        return ep;
    }

    internal static Episode GenerateFromAnimeEpisode(HttpContext ctx, SVR_AnimeEpisode aep, int uid, int level,
        int pic = 1)
    {
        var ep = new Episode { id = aep.AnimeEpisodeID, art = new ArtCollection() };

        if (aep.AniDB_Episode is not null)
        {
            ep.eptype = aep.EpisodeTypeEnum.ToString();
            ep.aid = aep.AniDB_Episode.AnimeID;
            ep.eid = aep.AniDB_Episode.EpisodeID;
        }

        var userRating = aep.UserRating;
        if (userRating > 0)
        {
            ep.userrating = userRating.ToString(CultureInfo.InvariantCulture);
        }

        if (double.TryParse(ep.rating, out var rating))
        {
            // 0.1 should be the absolute lowest rating
            if (rating > 10)
            {
                ep.rating = (rating / 100).ToString(CultureInfo.InvariantCulture);
            }
        }

        var epService = Utils.ServiceContainer.GetRequiredService<AnimeEpisodeService>();
        var cae = epService.GetV1Contract(aep, uid);
        if (cae != null)
        {
            ep.name = cae.AniDB_EnglishName;
            ep.summary = cae.Description;

            ep.year = cae.AniDB_AirDate?.Year.ToString(CultureInfo.InvariantCulture);
            ep.air = cae.AniDB_AirDate?.ToISO8601Date();

            ep.votes = cae.AniDB_Votes;
            ep.rating = cae.AniDB_Rating;

            ep.view = cae.WatchedDate != null ? 1 : 0;
            ep.view_date = cae.WatchedDate;
            ep.epnumber = cae.EpisodeNumber;
        }

        var tvdbEpisode = aep.TvDBEpisode;
        if (tvdbEpisode != null)
        {
            if (!string.IsNullOrEmpty(tvdbEpisode.EpisodeName))
            {
                ep.name = tvdbEpisode.EpisodeName;
            }

            if (pic > 0)
            {
                if (Misc.IsImageValid(tvdbEpisode.GetFullImagePath()))
                {
                    ep.art.thumb.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Thumbnail, DataSourceEnum.TvDB, tvdbEpisode.Id),
                    });
                }

                var fanarts = aep.AnimeSeries?.AniDB_Anime?.GetImages(ImageEntityType.Backdrop);
                if (fanarts is { Count: > 0 })
                {
                    var cont_image =
                        fanarts[new Random().Next(fanarts.Count)];
                    ep.art.fanart.Add(new Art
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, cont_image.ImageType, cont_image.Source, cont_image.ID),
                        index = 0
                    });
                }
                else
                {
                    ep.art.fanart.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Thumbnail, DataSourceEnum.TvDB, tvdbEpisode.Id),
                    });
                }
            }

            if (!string.IsNullOrEmpty(tvdbEpisode.Overview))
            {
                ep.summary = tvdbEpisode.Overview;
            }

            var zeroPadding = tvdbEpisode.EpisodeNumber.ToString().Length;
            var episodeNumber = tvdbEpisode.EpisodeNumber.ToString().PadLeft(zeroPadding, '0');
            zeroPadding = tvdbEpisode.SeasonNumber.ToString().Length;
            var seasonNumber = tvdbEpisode.SeasonNumber.ToString().PadLeft(zeroPadding, '0');

            ep.season = $"{seasonNumber}x{episodeNumber}";
            var airdate = tvdbEpisode.AirDate;
            if (airdate != null)
            {
                ep.air = airdate.Value.ToISO8601Date();
                ep.year = airdate.Value.Year.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (string.IsNullOrEmpty(ep.summary))
        {
            ep.summary = string.Intern("Episode Overview not Available");
        }

        if (pic > 0 && ep.art.thumb.Count == 0)
        {
            ep.art.thumb.Add(
                new Art { index = 0, url = APIV2Helper.ConstructSupportImageLink(ctx, "plex_404.png") });
            ep.art.fanart.Add(new Art { index = 0, url = APIV2Helper.ConstructSupportImageLink(ctx, "plex_404.png") });
        }

        if (string.IsNullOrEmpty(ep.year))
        {
            ep.year = aep.AnimeSeries.AirDate?.Year.ToString(CultureInfo.InvariantCulture) ?? "1";
        }

        if (level > 0)
        {
            var vls = aep.VideoLocals;
            if (vls.Count > 0)
            {
                ep.files = new List<RawFile>();
                foreach (var vl in vls)
                {
                    var file = new RawFile(ctx, vl, level - 1, uid, aep);
                    ep.files.Add(file);
                }
            }
        }

        return ep;
    }
}
