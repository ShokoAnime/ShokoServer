using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
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
        series = new List<Serie>();
        art = new ArtCollection();
        tags = new List<string>();
        roles = new List<Role>();
    }

    public static Group GenerateFromAnimeGroup(HttpContext ctx, SVR_AnimeGroup ag, int uid, bool nocast, bool notag,
        int level,
        bool all, int filterid, bool allpic, int pic, TagFilter.Filter tagfilter, List<int> evaluatedSeriesIDs = null)
    {
        var g = new Group
        {
            name = ag.GroupName,
            id = ag.AnimeGroupID,

            //g.videoqualities = ag.VideoQualities; <-- deadly trap
            added = ag.DateTimeCreated,
            edited = ag.DateTimeUpdated
        };

        if (filterid > 0 && evaluatedSeriesIDs == null)
        {
            var filter = RepoFactory.FilterPreset.GetByID(filterid);
            var evaluator = ctx.RequestServices.GetRequiredService<FilterEvaluator>();
            evaluatedSeriesIDs = evaluator.EvaluateFilter(filter, ctx.GetUser().JMMUserID).FirstOrDefault(a => a.Key == ag.AnimeGroupID)?.ToList();
        }

        var animes = evaluatedSeriesIDs != null
            ? evaluatedSeriesIDs.Select(id => RepoFactory.AnimeSeries.GetByID(id)).Select(ser => ser.AniDB_Anime).Where(a => a != null).OrderBy(a => a.BeginYear)
                .ThenBy(a => a.AirDate ?? DateTime.MaxValue).ToList()
            : ag.Anime?.OrderBy(a => a.BeginYear).ThenBy(a => a.AirDate ?? DateTime.MaxValue).ToList();

        if (animes is not { Count: > 0 }) return g;

        var anime = animes.FirstOrDefault();
        if (anime == null) return g;

        PopulateArtFromAniDBAnime(ctx, animes, g, allpic, pic);

        List<SVR_AnimeEpisode> ael;
        if (evaluatedSeriesIDs != null)
        {
            var series = evaluatedSeriesIDs.Select(id => RepoFactory.AnimeSeries.GetByID(id)).ToList();
            ael = series.SelectMany(ser => ser?.AnimeEpisodes).Where(a => a != null).ToList();
            g.size = series.Count;
        }
        else
        {
            var series = ag.AllSeries;
            ael = series.SelectMany(a => a?.AnimeEpisodes).Where(a => a != null).ToList();
            g.size = series.Count;
        }

        GenerateSizes(g, ael, uid);

        g.air = anime.AirDate?.ToISO8601Date() ?? string.Empty;

        g.rating = Math.Round(ag.AniDBRating / 100, 1).ToString(CultureInfo.InvariantCulture);
        g.summary = anime.Description ?? string.Empty;
        g.titles = anime.Titles.Select(s => new AnimeTitle
        {
            Type = s.TitleType.ToString().ToLower(), Language = s.LanguageCode, Title = s.Title
        }).ToList();
        g.year = anime.BeginYear.ToString();

        var tags = ag.Tags.Select(a => a.TagName).ToList();
        if (!notag && tags.Count > 0)
        {
            g.tags = TagFilter.String.ProcessTags(tagfilter, tags);
        }

        if (!nocast)
        {
            var xref_animestaff = RepoFactory.CrossRef_Anime_Staff.GetByAnimeIDAndRoleType(anime.AnimeID, StaffRoleType.Seiyuu);
            foreach (var xref in xref_animestaff)
            {
                if (xref.RoleID == null) continue;

                var character = RepoFactory.AnimeCharacter.GetByID(xref.RoleID.Value);
                if (character == null) continue;

                var staff = RepoFactory.AnimeStaff.GetByID(xref.StaffID);
                if (staff == null) continue;

                var role = new Role
                {
                    character = character.Name,
                    character_image = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.Character, xref.RoleID.Value),
                    staff = staff.Name,
                    staff_image = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.Staff, xref.StaffID),
                    role = xref.Role,
                    type = ((StaffRoleType)xref.RoleType).ToString()
                };
                g.roles ??= new List<Role>();

                g.roles.Add(role);
            }
        }

        if (level > 0)
        {
            foreach (var ada in animes.Select(a => RepoFactory.AnimeSeries.GetByAnimeID(a.AnimeID)))
            {
                g.series.Add(Serie.GenerateFromAnimeSeries(ctx, ada, uid, nocast, notag, level - 1, all, allpic,
                    pic, tagfilter));
            }
            // we already sorted animes, so no need to sort
        }

        return g;
    }

    private static IEnumerable<T> Randomize<T>(IEnumerable<T> source, int seed = -1)
    {
        var rnd = seed == -1 ? new Random() : new Random(seed);
        return source.OrderBy(item => rnd.Next());
    }

    public static void PopulateArtFromAniDBAnime(HttpContext ctx, IEnumerable<SVR_AniDB_Anime> animes, Group grp,
        bool allpics, int pic)
    {
        var rand = new Random();

        foreach (var anime in Randomize(animes))
        {
            var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(anime.AnimeID).ToList();
            var fanarts = tvdbIDs
                .SelectMany(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID)).ToList();
            var banners = tvdbIDs
                .SelectMany(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID)).ToList();

            var posters = anime.AllPosters;
            if (allpics || pic > 1)
            {
                if (allpics)
                {
                    pic = 999;
                }

                var pic_index = 0;
                if (posters != null)
                {
                    foreach (var cont_image in posters)
                    {
                        if (pic_index < pic)
                        {
                            grp.art.thumb.Add(new Art
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, cont_image.ImageType,
                                    cont_image.AniDB_Anime_DefaultImageID),
                                index = pic_index
                            });
                            pic_index++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                pic_index = 0;
                foreach (var cont_image in fanarts)
                {
                    if (pic_index < pic)
                    {
                        grp.art.fanart.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_FanArt,
                                cont_image.TvDB_ImageFanartID),
                            index = pic_index
                        });
                        pic_index++;
                    }
                    else
                    {
                        break;
                    }
                }

                pic_index = 0;
                foreach (var cont_image in banners)
                {
                    if (pic_index < pic)
                    {
                        grp.art.banner.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_Banner,
                                cont_image.TvDB_ImageWideBannerID),
                            index = pic_index
                        });
                        pic_index++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else if (pic > 0)
            {
                var poster = anime.GetDefaultPosterDetailsNoBlanks();
                grp.art.thumb.Add(new Art
                {
                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)poster.ImageType, poster.ImageID),
                    index = 0
                });

                if (fanarts.Count > 0)
                {
                    var default_fanart = anime.DefaultFanart;

                    if (default_fanart != null)
                    {
                        grp.art.fanart.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, default_fanart.ImageType,
                                default_fanart.AniDB_Anime_DefaultImageID),
                            index = 0
                        });
                    }
                    else
                    {
                        var tvdbart = fanarts[rand.Next(fanarts.Count)];
                        grp.art.fanart.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_FanArt,
                                tvdbart.TvDB_ImageFanartID),
                            index = 0
                        });
                    }
                }

                if (banners.Count > 0)
                {
                    var default_fanart = anime.DefaultWideBanner;

                    if (default_fanart != null)
                    {
                        grp.art.banner.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, default_fanart.ImageType,
                                default_fanart.AniDB_Anime_DefaultImageID),
                            index = 0
                        });
                    }
                    else
                    {
                        var tvdbart = banners[rand.Next(banners.Count)];
                        grp.art.banner.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_Banner,
                                tvdbart.TvDB_ImageWideBannerID),
                            index = 0
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

            var local = ep.VideoLocals?.Any() ?? false;
            switch (ep.EpisodeTypeEnum)
            {
                case EpisodeType.Episode:
                    {
                        eps++;
                        if (local)
                        {
                            local_eps++;
                        }

                        if (ep.GetUserRecord(uid)?.WatchedDate != null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate != null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate != null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate != null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate != null)
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

                        if (ep.GetUserRecord(uid)?.WatchedDate != null)
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
