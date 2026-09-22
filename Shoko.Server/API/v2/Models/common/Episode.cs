using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class Episode : BaseDirectory
{
    public override string type => string.Intern("ep");

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string season { get; set; } = null!;

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int view { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public DateTime? view_date { get; set; }

    [DataMember] public string eptype { get; set; } = null!;

    [DataMember] public int epnumber { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int aid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int eid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<RawFile> files { get; set; } = null!;

    internal static Episode GenerateFromAnimeEpisodeID(HttpContext ctx, int anime_episode_id, int uid, int level,
        int pic = 1)
    {
        var ep = new Episode();

        if (anime_episode_id > 0)
        {
            ep = GenerateFromAnimeEpisode(ctx, RepoFactory.AnimeEpisode.GetByID(anime_episode_id)!, uid,
                level, pic);
        }

        return ep;
    }

    internal static Episode GenerateFromAnimeEpisode(HttpContext ctx, AnimeEpisode aep, int uid, int level,
        int pic = 1)
    {
        var ep = new Episode { id = aep.AnimeEpisodeID, art = new ArtCollection() };

        if (aep.AniDB_Episode is { } anidbEpisode)
        {
            ep.eptype = anidbEpisode.EpisodeType.ToString();
            ep.aid = anidbEpisode.AnimeID;
            ep.eid = anidbEpisode.EpisodeID;
        }

        if (RepoFactory.AnimeEpisode_User.GetByUserAndEpisodeID(uid, aep.AnimeEpisodeID) is { HasUserRating: true } userData)
        {
            ep.userrating = userData.UserRating.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (double.TryParse(ep.rating, out var rating))
        {
            // 0.1 should be the absolute lowest rating
            if (rating > 10)
            {
                ep.rating = (rating / 100).ToString(CultureInfo.InvariantCulture);
            }
        }

        if (aep.AniDB_Episode is { } anidb)
        {
            var airDate = anidb.GetAirDateAsDate();
            var watchedDate = RepoFactory.AnimeEpisode_User.GetByUserAndEpisodeID(uid, aep.AnimeEpisodeID)?.WatchedDate;
            ep.name = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(anidb.EpisodeID, TitleLanguage.English).FirstOrDefault()?.Title!;
            ep.summary = anidb.Description;

            ep.year = airDate?.Year.ToString(CultureInfo.InvariantCulture)!;
            ep.air = airDate?.ToISO8601Date()!;

            ep.votes = anidb.Votes;
            ep.rating = anidb.Rating;

            ep.view = watchedDate != null ? 1 : 0;
            ep.view_date = watchedDate;
            ep.epnumber = anidb.EpisodeNumber;
        }

        if (pic > 0)
        {
            var backdrops = ((IWithImages)aep).GetImages(new() { ImageType = ImageEntityType.Backdrop });
            var backdropImage = backdrops.FirstOrDefault(x => x.IsPreferred)
                ?? backdrops.FirstOrDefault(x => x is { IsEnabled: true, IsAvailable: true });
            if (backdropImage is not null)
            {
                ep.art.thumb.Add(new Art
                {
                    index = 0,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, backdropImage),
                });
                ep.art.fanart.Add(new Art
                {
                    index = 0,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, backdropImage),
                });
            }
        }
        if (aep.TmdbEpisodes is { Count: > 0 } tmdbEpisodes)
        {
            var tmdbEpisode = tmdbEpisodes[0];
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
            ep.year = aep.AnimeSeries!.AirDate?.Year.ToString(CultureInfo.InvariantCulture) ?? "1";
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
