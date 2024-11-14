using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
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

        if (aep.AniDB_Episode is { } anidbEpisode)
        {
            ep.eptype = anidbEpisode.EpisodeTypeEnum.ToString();
            ep.aid = anidbEpisode.AnimeID;
            ep.eid = anidbEpisode.EpisodeID;
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
        if (epService.GetV1Contract(aep, uid) is { } cae)
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

        if (aep.TmdbEpisodes is { Count: > 0 } tmdbEpisodes)
        {
            TMDB_Image thumbnail = null;
            var tmdbEpisode = tmdbEpisodes[0];
            if (pic > 0 && tmdbEpisode.GetImages(ImageEntityType.Thumbnail) is { } thumbnailImages && thumbnailImages.Count > 0)
            {
                thumbnail = thumbnailImages
                    .Where(image => image.ImageType == ImageEntityType.Thumbnail && image.IsLocalAvailable)
                    .OrderByDescending(image => image.IsPreferred)
                    .FirstOrDefault();
                if (thumbnail is not null)
                {
                    ep.art.thumb.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, thumbnail.ImageType, thumbnail.Source, thumbnail.ID),
                    });
                }
            }
            if (pic > 0 && tmdbEpisode.GetImages(ImageEntityType.Backdrop) is { } backdropImages && backdropImages.Count > 0)
            {
                var backdrop = backdropImages
                    .Where(image => image.ImageType == ImageEntityType.Backdrop && image.IsLocalAvailable)
                    .OrderByDescending(image => image.IsPreferred)
                    .FirstOrDefault();
                if (backdrop is not null)
                {
                    backdrop ??= thumbnail;
                    ep.art.fanart.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, backdrop.ImageType, backdrop.Source, backdrop.ID),
                    });
                }
            }

            if (!string.IsNullOrEmpty(tmdbEpisode.EnglishTitle))
            {
                ep.name = tmdbEpisode.EnglishTitle;
            }

            if (!string.IsNullOrEmpty(tmdbEpisode.EnglishOverview))
            {
                ep.summary = tmdbEpisode.EnglishOverview;
            }

            var zeroPadding = tmdbEpisode.EpisodeNumber.ToString().Length;
            var episodeNumber = tmdbEpisode.EpisodeNumber.ToString().PadLeft(zeroPadding, '0');
            zeroPadding = tmdbEpisode.SeasonNumber.ToString().Length;
            var seasonNumber = tmdbEpisode.SeasonNumber.ToString().PadLeft(zeroPadding, '0');

            ep.season = $"{seasonNumber}x{episodeNumber}";
            var airdate = tmdbEpisode.AiredAt;
            if (airdate != null)
            {
                ep.air = airdate.Value.ToDateTime().ToISO8601Date();
                ep.year = airdate.Value.Year.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (string.IsNullOrEmpty(ep.summary))
        {
            ep.summary = string.Intern("Episode Overview not Available");
        }

        if (pic > 0 && ep.art.thumb.Count == 0)
        {
            ep.art.thumb.Add(new Art { index = 0, url = APIV2Helper.ConstructSupportImageLink(ctx, "plex_404.png") });
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
