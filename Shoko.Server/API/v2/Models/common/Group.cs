using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class Group : BaseDirectory
{
    // We need to rethink this. It doesn't support subgroups
    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<Serie> series { get; set; }

    public override string type => "group";

    public Group()
    {
        series = [];
        art = new();
        tags = [];
        roles = [];
    }

    public static Group GenerateFromAnimeGroup(HttpContext ctx, SVR_AnimeGroup ag, int uid, bool noCast, bool noTag, int level,
        bool all, int filterID, bool allPic, int pic, TagFilter.Filter tagFilter, List<int> evaluatedSeriesIDs = null)
    {
        var g = new Group
        {
            name = ag.GroupName,
            id = ag.AnimeGroupID,
            added = ag.DateTimeCreated,
            edited = ag.DateTimeUpdated
        };

        if (filterID > 0 && evaluatedSeriesIDs == null)
        {
            var filter = RepoFactory.FilterPreset.GetByID(filterID);
            var evaluator = ctx.RequestServices.GetRequiredService<FilterEvaluator>();
            evaluatedSeriesIDs = evaluator.EvaluateFilter(filter, ctx.GetUser().JMMUserID).FirstOrDefault(a => a.Key == ag.AnimeGroupID)?.ToList();
        }

        var allAnime = evaluatedSeriesIDs is not null
            ? evaluatedSeriesIDs
                .Select(RepoFactory.AnimeSeries.GetByID)
                .WhereNotNull()
                .Select(ser => ser.AniDB_Anime).WhereNotNull()
                .OrderBy(a => a.BeginYear)
                .ThenBy(a => a.AirDate ?? DateTime.MaxValue)
                .ToList()
            : ag.Anime?.OrderBy(a => a.BeginYear).ThenBy(a => a.AirDate ?? DateTime.MaxValue).ToList();

        if (allAnime is not { Count: > 0 }) return g;

        var anime = allAnime.FirstOrDefault();
        if (anime == null) return g;

        PopulateArtFromAniDBAnime(ctx, allAnime, g, allPic, pic);

        List<SVR_AnimeEpisode> ael;
        if (evaluatedSeriesIDs is not null)
        {
            var series = evaluatedSeriesIDs.Select(id => RepoFactory.AnimeSeries.GetByID(id)).ToList();
            ael = series.SelectMany(ser => ser?.AnimeEpisodes).WhereNotNull().ToList();
            g.size = series.Count;
        }
        else
        {
            var series = ag.AllSeries;
            ael = series.SelectMany(a => a?.AnimeEpisodes).WhereNotNull().ToList();
            g.size = series.Count;
        }

        GenerateSizes(g, ael, uid);

        g.air = anime.AirDate?.ToISO8601Date() ?? string.Empty;

        g.rating = Math.Round(ag.AniDBRating / 100, 1).ToString(CultureInfo.InvariantCulture);
        g.summary = anime.Description ?? string.Empty;
        g.titles = anime.Titles.Select(s => new AnimeTitle
        {
            Type = s.TitleType.ToString().ToLower(),
            Language = s.LanguageCode,
            Title = s.Title
        }).ToList();
        g.year = anime.BeginYear.ToString();

        var tags = ag.Tags.Select(a => a.TagName).ToList();
        if (!noTag && tags.Count > 0)
        {
            g.tags = TagFilter.String.ProcessTags(tagFilter, tags);
        }

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
                    character_image = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Character, DataSourceEnum.Shoko, xref.CharacterID),
                    staff = staff.Name,
                    staff_image = APIHelper.ConstructImageLinkFromTypeAndId(ctx, ImageEntityType.Person, DataSourceEnum.Shoko, xref.CreatorID),
                    role = xref2.AppearanceType.ToString().Replace("_", " "),
                    type = "Seiyuu",
                };
                g.roles ??= [];

                g.roles.Add(role);
            }
        }

        if (level > 0)
        {
            // we already sorted allAnime, so no need to sort
            foreach (var ada in allAnime.Select(a => RepoFactory.AnimeSeries.GetByAnimeID(a.AnimeID)))
            {
                g.series.Add(Serie.GenerateFromAnimeSeries(ctx, ada, uid, noCast, noTag, level - 1, all, allPic, pic, tagFilter));
            }
        }

        return g;
    }

    private static IEnumerable<T> Randomize<T>(IEnumerable<T> source, int seed = -1)
    {
        var rnd = seed == -1 ? new Random() : new Random(seed);
        return source.OrderBy(item => rnd.Next());
    }

    public static void PopulateArtFromAniDBAnime(HttpContext ctx, IEnumerable<SVR_AniDB_Anime> allAnime, Group group, bool allPictures, int maxPictures)
    {
        var rand = new Random();
        foreach (var anime in Randomize(allAnime))
        {
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
                    group.art.thumb.Add(new Art
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
                    group.art.fanart.Add(new Art
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
                    group.art.banner.Add(new Art
                    {
                        index = pictureIndex++,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, banner.ImageType, banner.Source, banner.ID),
                    });
                }
            }
            else if (maxPictures > 0)
            {
                var poster = anime.PreferredOrDefaultPoster;
                group.art.thumb.Add(new Art
                {
                    index = 0,
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, poster.ImageType, poster.Source, poster.ID),
                });
                if (backdrops.Count > 0)
                {
                    if (anime.PreferredBackdrop is { } preferredBackdrop)
                    {
                        group.art.fanart.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, preferredBackdrop.ImageType, preferredBackdrop.ImageSource.ToDataSourceEnum(), preferredBackdrop.ImageID),
                            index = 0
                        });
                    }
                    else
                    {
                        var backdrop = backdrops[rand.Next(backdrops.Count)];
                        group.art.fanart.Add(new Art
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
                        group.art.banner.Add(new Art
                        {
                            index = 0,
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, preferredBanner.ImageType, preferredBanner.ImageSource.ToDataSourceEnum(), preferredBanner.ImageID),
                        });
                    }
                    else
                    {
                        var banner = banners[rand.Next(banners.Count)];
                        group.art.banner.Add(new Art
                        {
                            index = 0,
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, banner.ImageType, banner.Source, banner.ID),
                        });
                    }
                }
                break;
            }
        }
    }

    private static void GenerateSizes(Group grp, List<SVR_AnimeEpisode> ael, int uid)
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
            if (ep?.AniDB_Episode == null)
            {
                continue;
            }

            var local = ep.VideoLocals.Count != 0;
            switch (ep.EpisodeTypeEnum)
            {
                case EpisodeType.Episode:
                    {
                        eps++;
                        if (local)
                        {
                            local_eps++;
                        }

                        if (ep.GetUserRecord(uid)?.WatchedDate is not null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate is not null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate is not null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate is not null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate is not null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate is not null)
                        {
                            watched_others++;
                        }

                        break;
                    }
            }
        }

        grp.total_sizes = new Sizes
        {
            Episodes = eps,
            Credits = credits,
            Specials = specials,
            Trailers = trailers,
            Parodies = parodies,
            Others = others
        };

        grp.local_sizes = new Sizes
        {
            Episodes = local_eps,
            Credits = local_credits,
            Specials = local_specials,
            Trailers = local_trailers,
            Parodies = local_parodies,
            Others = local_others
        };

        grp.watched_sizes = new Sizes
        {
            Episodes = watched_eps,
            Credits = watched_credits,
            Specials = watched_specials,
            Trailers = watched_trailers,
            Parodies = watched_parodies,
            Others = watched_others
        };
    }
}
