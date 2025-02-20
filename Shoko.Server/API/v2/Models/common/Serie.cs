using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;

#pragma warning disable CA2012
#pragma warning disable IDE1006
namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class Serie : BaseDirectory, IComparable
{
    public override string type => "serie";

    [DataMember(IsRequired = true, EmitDefaultValue = true)]
    public int aid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string season { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<Episode> eps { get; set; }

    [DataMember(IsRequired = true, EmitDefaultValue = true)]
    public int ismovie { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public long filesize { get; set; }

    public Serie()
    {
        art = new ArtCollection();
        roles = [];
        tags = [];
    }

    public static Serie GenerateFromVideoLocal(HttpContext ctx, SVR_VideoLocal vl, int uid, bool noCast, bool noTag,
        int level, bool all, bool allPictures, int pic, TagFilter.Filter tagFilter)
    {
        if (vl is null)
            return new();

        var ser = vl.AnimeEpisodes.FirstOrDefault()?.AnimeSeries;
        if (ser is null)
            return new();

        return GenerateFromAnimeSeries(ctx, ser, uid, noCast, noTag, level, all, allPictures, pic, tagFilter);
    }

    public static Serie GenerateFromBookmark(HttpContext ctx, BookmarkedAnime bookmark, int uid, bool noCast,
        bool noTag, int level, bool all, bool allPictures, int pic, TagFilter.Filter tagFilter)
    {
        var series = RepoFactory.AnimeSeries.GetByAnimeID(bookmark.AnimeID);
        if (series is not null)
            return GenerateFromAnimeSeries(ctx, series, uid, noCast, noTag, level, all, allPictures, pic, tagFilter);

        var aniDB_Anime = RepoFactory.AniDB_Anime.GetByAnimeID(bookmark.AnimeID);
        if (aniDB_Anime is null)
        {
            var scheduler = ctx.RequestServices.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
            scheduler.StartJob<GetAniDBAnimeJob>(
                c =>
                {
                    c.AnimeID = bookmark.AnimeID;
                    c.ForceRefresh = true;
                }
            ).GetAwaiter().GetResult();

            return new Serie { id = -1, name = "GetAnimeInfoHTTP", aid = bookmark.AnimeID };
        }

        return GenerateFromAniDBAnime(ctx, aniDB_Anime, noCast, noTag, allPictures, pic, tagFilter);
    }

    public static Serie GenerateFromAniDBAnime(HttpContext ctx, SVR_AniDB_Anime anime, bool noCast, bool noTag,
        bool allPictures, int pic, TagFilter.Filter tagFilter)
    {
        var sr = new Serie
        {
            // 0 will load all
            id = -1,
            aid = anime.AnimeID,
            summary = anime.Description,
            rating = Math.Round(anime.Rating / 100D, 1)
                .ToString(CultureInfo.InvariantCulture),
            votes = anime.VoteCount.ToString(),
            name = anime.MainTitle,
            ismovie = anime.AnimeType == (int)AnimeType.Movie ? 1 : 0
        };

        if (anime.AirDate.HasValue)
        {
            sr.year = anime.AirDate.Value.Year.ToString();
            var airdate = anime.AirDate.Value;
            if (airdate != DateTime.MinValue)
            {
                sr.air = airdate.ToISO8601Date();
            }
        }

        var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime) ??
                   RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp);
        if (vote is not null)
        {
            sr.userrating = Math.Round(vote.VoteValue / 100D, 1).ToString(CultureInfo.InvariantCulture);
        }

        sr.titles = anime.Titles.Select(title =>
            new AnimeTitle
            {
                Language = title.LanguageCode,
                Title = title.Title,
                Type = title.TitleType.ToString().ToLower(),
            }).ToList();

        PopulateArtFromAniDBAnime(ctx, anime, sr, allPictures, pic);

        if (!noCast)
        {
            var xrefAnimeStaff = RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(anime.AnimeID);
            foreach (var xref in xrefAnimeStaff)
            {
                var character = RepoFactory.AniDB_Character.GetByID(xref.CharacterID);
                if (character == null) continue;

                var staff = RepoFactory.AniDB_Creator.GetByID(xref.CreatorID);
                if (staff == null) continue;

                var xref2 = xref.CharacterCrossReference;
                if (xref2 == null) continue;

                var role = new Role
                {
                    character = character.Name,
                    character_image = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Character, DataSourceEnum.AniDB, xref.CharacterID),
                    staff = staff.Name,
                    staff_image = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Person, DataSourceEnum.AniDB, xref.CreatorID),
                    role = xref2.AppearanceType.ToString().Replace("_", " "),
                    type = "Seiyuu",
                };
                sr.roles ??= [];
                sr.roles.Add(role);
            }
        }

        if (!noTag)
        {
            var tags = anime.GetAllTags();
            if (tags is not null)
            {
                sr.tags = TagFilter.String.ProcessTags(tagFilter, tags.ToList());
            }
        }

        return sr;
    }

    public static Serie GenerateFromAnimeSeries(HttpContext ctx, SVR_AnimeSeries ser, int uid, bool noCast, bool noTag,
        int level, bool all, bool allPictures, int maxPictures, TagFilter.Filter tagFilter)
    {
        var sr = GenerateFromAniDBAnime(ctx, ser.AniDB_Anime, noCast, noTag, allPictures, maxPictures, tagFilter);

        var ael = ser.AnimeEpisodes;

        sr.id = ser.AnimeSeriesID;
        sr.name = ser.PreferredTitle;
        GenerateSizes(sr, ael, uid);

        var season = ael.FirstOrDefault(a =>
                a.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode &&
                a.AniDB_Episode.EpisodeNumber == 1)
            ?.TmdbEpisodes.FirstOrDefault()?.SeasonNumber;
        if (season is not null)
        {
            sr.season = season.Value.ToString();
        }

        var tmdbShow = ael.SelectMany(a => a.TmdbEpisodes)
            .DistinctBy(a => a.TmdbEpisodeID)
            .GroupBy(a => a.TmdbShowID)
            .MaxBy(a => a.Count())
            ?.First().Show;
        if (tmdbShow is not null)
        {
            sr.titles.Add(new()
            {
                Language = "EN",
                Title = tmdbShow.EnglishTitle,
                Type = "TMDB",
            });
        }

        if (!noTag)
        {
            var tags = ser.AniDB_Anime.GetAllTags();
            if (tags is not null)
            {
                sr.tags = TagFilter.String.ProcessTags(tagFilter, tags.ToList());
            }
        }

        if (level > 0)
        {
            if (ael.Count > 0)
            {
                sr.eps = [];
                foreach (var ae in ael)
                {
                    if (!all && (ae?.VideoLocals?.Count ?? 0) == 0)
                    {
                        continue;
                    }

                    var new_ep = Episode.GenerateFromAnimeEpisode(ctx, ae, uid, level - 1, maxPictures);
                    if (new_ep is null)
                    {
                        continue;
                    }

                    sr.eps.Add(new_ep);

                    if (level - 1 <= 0)
                    {
                        continue;
                    }

                    foreach (var file in new_ep.files)
                    {
                        sr.filesize += file.size;
                    }
                }

                sr.eps = sr.eps.OrderBy(a => a.epnumber).ToList();
            }
        }

        return sr;
    }

    private static void GenerateSizes(Serie sr, IEnumerable<SVR_AnimeEpisode> ael, int uid)
    {
        var eps = 0;
        var credits = 0;
        var specials = 0;
        var trailers = 0;
        var parodies = 0;
        var others = 0;

        var local_eps = 0;
        var local_credits = 0;
        var local_specials = 0;
        var local_trailers = 0;
        var local_parodies = 0;
        var local_others = 0;

        var watched_eps = 0;
        var watched_credits = 0;
        var watched_specials = 0;
        var watched_trailers = 0;
        var watched_parodies = 0;
        var watched_others = 0;

        // single loop. Will help on long shows
        foreach (var ep in ael)
        {
            if (ep?.AniDB_Episode is null)
            {
                continue;
            }

            var local = ep.VideoLocals.Count != 0;
            var watched = ep.GetUserRecord(uid)?.WatchedDate is not null;
            switch (ep.EpisodeTypeEnum)
            {
                case EpisodeType.Episode:
                    {
                        eps++;
                        if (local)
                        {
                            local_eps++;
                        }

                        if (watched)
                        {
                            watched_eps++;
                        }

                        break;
                    }
                case EpisodeType.Credits:
                    {
                        credits++;
                        if (local)
                        {
                            local_credits++;
                        }

                        if (watched)
                        {
                            watched_credits++;
                        }

                        break;
                    }
                case EpisodeType.Special:
                    {
                        specials++;
                        if (local)
                        {
                            local_specials++;
                        }

                        if (watched)
                        {
                            watched_specials++;
                        }

                        break;
                    }
                case EpisodeType.Trailer:
                    {
                        trailers++;
                        if (local)
                        {
                            local_trailers++;
                        }

                        if (watched)
                        {
                            watched_trailers++;
                        }

                        break;
                    }
                case EpisodeType.Parody:
                    {
                        parodies++;
                        if (local)
                        {
                            local_parodies++;
                        }

                        if (watched)
                        {
                            watched_parodies++;
                        }

                        break;
                    }
                case EpisodeType.Other:
                    {
                        others++;
                        if (local)
                        {
                            local_others++;
                        }

                        if (watched)
                        {
                            watched_others++;
                        }

                        break;
                    }
            }
        }

        sr.size = eps + credits + specials + trailers + parodies + others;
        sr.localsize = local_eps + local_credits + local_specials + local_trailers + local_parodies + local_others;
        sr.viewed = watched_eps + watched_credits + watched_specials + watched_trailers + watched_parodies +
                    watched_others;

        sr.total_sizes = new Sizes
        {
            Episodes = eps,
            Credits = credits,
            Specials = specials,
            Trailers = trailers,
            Parodies = parodies,
            Others = others
        };

        sr.local_sizes = new Sizes
        {
            Episodes = local_eps,
            Credits = local_credits,
            Specials = local_specials,
            Trailers = local_trailers,
            Parodies = local_parodies,
            Others = local_others
        };

        sr.watched_sizes = new Sizes
        {
            Episodes = watched_eps,
            Credits = watched_credits,
            Specials = watched_specials,
            Trailers = watched_trailers,
            Parodies = watched_parodies,
            Others = watched_others
        };
    }

    public static void PopulateArtFromAniDBAnime(HttpContext ctx, SVR_AniDB_Anime anime, Serie sr, bool allPictures,
        int maxPictures)
    {
        var rand = (Random)ctx.Items["Random"];
        var backdrops = anime.GetImages(ImageEntityType.Backdrop);
        var banners = anime.GetImages(ImageEntityType.Banner);
        var posters = anime.GetImages(ImageEntityType.Poster);
        if (allPictures || maxPictures > 1)
        {
            if (allPictures)
                maxPictures = 999;
            var pictureIndex = 0;
            foreach (var poster in posters)
            {
                if (pictureIndex >= maxPictures)
                    break;
                sr.art.thumb.Add(new Art
                {
                    index = pictureIndex++,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, poster.ImageType, poster.Source, poster.ID),
                });
            }
            pictureIndex = 0;
            foreach (var backdrop in backdrops)
            {
                if (pictureIndex >= maxPictures)
                    break;
                sr.art.fanart.Add(new Art
                {
                    index = pictureIndex++,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, backdrop.ImageType, backdrop.Source, backdrop.ID),
                });
            }
            pictureIndex = 0;
            foreach (var banner in banners)
            {
                if (pictureIndex >= maxPictures)
                    break;
                sr.art.banner.Add(new Art
                {
                    index = pictureIndex++,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, banner.ImageType, banner.Source, banner.ID),
                });
            }
        }
        else if (maxPictures > 0)
        {
            var poster = anime.PreferredOrDefaultPoster;
            sr.art.thumb.Add(new Art
            {
                index = 0,
                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, poster.ImageType, poster.Source, poster.ID),
            });
            if (backdrops.Count > 0)
            {
                if (anime.PreferredBackdrop is { } preferredBackdrop)
                {
                    sr.art.fanart.Add(new Art
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, preferredBackdrop.ImageType, preferredBackdrop.ImageSource.ToDataSourceEnum(), preferredBackdrop.ImageID),
                        index = 0
                    });
                }
                else
                {
                    var backdrop = backdrops[rand.Next(backdrops.Count)];
                    sr.art.fanart.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, backdrop.ImageType, backdrop.Source, backdrop.ID),
                    });
                }
            }
            if (banners.Count > 0)
            {
                var preferredBanner = anime.PreferredBanner;
                if (preferredBanner is not null)
                {
                    sr.art.banner.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, preferredBanner.ImageType, preferredBanner.ImageSource.ToDataSourceEnum(), preferredBanner.ImageID),
                    });
                }
                else
                {
                    var banner = banners[rand.Next(banners.Count)];
                    sr.art.banner.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, banner.ImageType, banner.Source, banner.ID),
                    });
                }
            }
        }
    }

    public int CompareTo(object obj)
    {
        if (obj is not Serie a)
        {
            return 1;
        }

        // try year first, as it is more likely to have relevant data
        if (int.TryParse(a.year, out var s1) && int.TryParse(year, out var s))
        {
            if (s < s1)
            {
                return -1;
            }

            if (s > s1)
            {
                return 1;
            }
        }

        // Does it have an air date? Sort by it
        if (!string.IsNullOrEmpty(a.air) && !a.air.Equals(DateTime.MinValue.ToISO8601Date()) &&
            !string.IsNullOrEmpty(air) && !air.Equals(DateTime.MinValue.ToISO8601Date()))
        {
            if (DateTime.TryParseExact(a.air, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var d1) &&
                DateTime.TryParseExact(air, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                if (d < d1)
                {
                    return -1;
                }

                if (d > d1)
                {
                    return 1;
                }
            }
        }

        // Does it have a Season? Sort by it
        if (int.TryParse(a.season, out s1) && int.TryParse(season, out s))
        {
            // Only try if the season is valid
            if (s >= 0 && s1 >= 0)
            {
                // Specials
                if (s == 0 && s1 > 0)
                {
                    return 1;
                }

                if (s > 0 && s1 == 0)
                {
                    return -1;
                }

                // Normal
                if (s < s1)
                {
                    return -1;
                }

                if (s > s1)
                {
                    return 1;
                }
            }
        }

        return string.Compare(name, a.name, StringComparison.InvariantCultureIgnoreCase);
    }
}
