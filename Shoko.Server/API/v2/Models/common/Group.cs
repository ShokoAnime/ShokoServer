﻿using System;
 using System.Collections.Generic;
 using System.Globalization;
 using System.Linq;
 using System.Runtime.Serialization;
 using Nancy;
 using Shoko.Models.Enums;
 using Shoko.Models.PlexAndKodi;
 using Shoko.Server.Models;
 using Shoko.Server.PlexAndKodi;
 using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common
{
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
            tags = new List<Tag>();
        }

        public static Group GenerateFromAnimeGroup(NancyContext ctx, SVR_AnimeGroup ag, int uid, bool nocast, bool notag, int level,
            bool all, int filterid, bool allpic, int pic, byte tagfilter)
        {
            Group g = new Group
            {
                name = ag.GroupName,
                id = ag.AnimeGroupID,

                //g.videoqualities = ag.VideoQualities; <-- deadly trap
                added = ag.DateTimeCreated,
                edited = ag.DateTimeUpdated
            };

            var animes = ag.Anime?.OrderBy(a => a.BeginYear).ThenBy(a => a.AirDate ?? DateTime.MaxValue).ToList();
            if (animes != null && animes.Count > 0)
            {
                Random rand = new Random();
                if (allpic || pic > 1)
                {
                    if (allpic)
                    {
                        pic = 999;
                    }
                    int pic_index = 0;
                    foreach (var anime in animes)
                    {
                        foreach (var cont_image in anime.AllPosters)
                        {
                            if (pic_index < pic)
                            {
                                g.art.thumb.Add(new Art
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

                        pic_index = 0;
                        foreach (var cont_image in anime.Contract.AniDBAnime.Fanarts)
                        {
                            if (pic_index < pic)
                            {
                                g.art.fanart.Add(new Art
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

                        pic_index = 0;
                        foreach (var cont_image in anime.Contract.AniDBAnime.Banners)
                        {
                            if (pic_index < pic)
                            {
                                g.art.banner.Add(new Art
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
                }
                else
                {
                    var anime = animes.FirstOrDefault();
                    if (anime != null)
                    {
                        g.art.thumb.Add(new Art
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) ImageEntityType.AniDB_Cover,
                                anime.AnimeID),
                            index = 0
                        });

                        var fanarts = anime.Contract.AniDBAnime.Fanarts;
                        if (fanarts != null && fanarts.Count > 0)
                        {
                            var art = fanarts[rand.Next(fanarts.Count)];
                            g.art.fanart.Add(new Art
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType,
                                    art.AniDB_Anime_DefaultImageID),
                                index = 0
                            });
                        }

                        fanarts = anime.Contract.AniDBAnime.Banners;
                        if (fanarts != null && fanarts.Count > 0)
                        {
                            var art = fanarts[rand.Next(fanarts.Count)];
                            g.art.banner.Add(new Art
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType,
                                    art.AniDB_Anime_DefaultImageID),
                                index = 0
                            });
                        }
                    }
                }
            }
            List<SVR_AnimeEpisode> ael = ag.GetAllSeries().SelectMany(a => a.GetAnimeEpisodes()).ToList();
            GenerateSizes(g, ael, uid);

            g.air = (ag.Contract.Stat_AirDate_Min ?? animes?.FirstOrDefault()?.AirDate)?.ToPlexDate() ?? string.Empty;

            g.rating = Math.Round(ag.AniDBRating / 100, 1).ToString(CultureInfo.InvariantCulture);
            g.summary = ag.Contract.Description;
            g.titles = ag.Titles.ToAPIContract();
            g.year = ag.Contract.Stat_AllYears.Min().ToString(NumberFormatInfo.InvariantInfo);

            if (!notag && ag.Contract.Stat_AllTags != null)
                g.tags = TagFilter.ProcessTags(tagfilter, ag.Contract.Stat_AllTags.ToList())
                    .Select(value => new Tag {tag = value}).ToList();

            if (!nocast)
            {
                Video vag = ag.GetPlexContract(uid);
                if (vag?.Roles != null)
                {
                    g.roles = vag.Roles?.Select(rtg => new Role
                    {
                        name = !string.IsNullOrEmpty(rtg.Value) ? rtg.Value : string.Empty,
                        namepic = !string.IsNullOrEmpty(rtg.TagPicture)
                            ? APIHelper.ConstructImageLinkFromRest(ctx, rtg.TagPicture)
                            : string.Empty,
                        role = !string.IsNullOrEmpty(rtg.Role) ? rtg.Role : string.Empty,
                        roledesc = !string.IsNullOrEmpty(rtg.RoleDescription)
                            ? rtg.RoleDescription
                            : string.Empty,
                        rolepic = !string.IsNullOrEmpty(rtg.RolePicture)
                            ? APIHelper.ConstructImageLinkFromRest(ctx, rtg.RolePicture)
                            : string.Empty
                    }).ToList();
                }
            }

            if (level > 0)
            {
                List<int> series = null;
                if (filterid > 0)
                {
                    SVR_GroupFilter filter = RepoFactory.GroupFilter.GetByID(filterid);
                    if (filter?.ApplyToSeries > 0)
                    {
                        if (filter.SeriesIds.ContainsKey(uid))
                            series = filter.SeriesIds[uid].ToList();
                    }
                }
                foreach (SVR_AnimeSeries ada in ag.GetSeries())
                {
                    if (series != null && series.Count > 0 && !series.Contains(ada.AnimeSeriesID)) continue;
                    g.series.Add(Serie.GenerateFromAnimeSeries(ctx, ada, uid, nocast, notag, (level - 1), all, allpic,
                        pic, tagfilter));
                }
                // This should be faster
                g.series.Sort();
            }

            return g;
        }

        private static void GenerateSizes(Group grp, List<SVR_AnimeEpisode> ael, int uid)
        {
            int eps = 0;
            int credits = 0;
            int specials = 0;
            int trailers = 0;
            int parodies = 0;
            int others = 0;

            int local_eps = 0;
            int local_credits = 0;
            int local_specials = 0;
            int local_trailers = 0;
            int local_parodies = 0;
            int local_others = 0;

            int watched_eps = 0;
            int watched_credits = 0;
            int watched_specials = 0;
            int watched_trailers = 0;
            int watched_parodies = 0;
            int watched_others = 0;

            // single loop. Will help on long shows
            foreach (SVR_AnimeEpisode ep in ael)
            {
                if (ep == null) continue;
                var local = ep.GetVideoLocals().Any();
                switch (ep.EpisodeTypeEnum)
                {
                    case EpisodeType.Episode:
                    {
                        eps++;
                        if (local) local_eps++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_eps++;
                        break;
                    }
                    case EpisodeType.Credits:
                    {
                        credits++;
                        if (local) local_credits++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_credits++;
                        break;
                    }
                    case EpisodeType.Special:
                    {
                        specials++;
                        if (local) local_specials++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_specials++;
                        break;
                    }
                    case EpisodeType.Trailer:
                    {
                        trailers++;
                        if (local) local_trailers++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_trailers++;
                        break;
                    }
                    case EpisodeType.Parody:
                    {
                        parodies++;
                        if (local) local_parodies++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_parodies++;
                        break;
                    }
                    case EpisodeType.Other:
                    {
                        others++;
                        if (local) local_others++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_others++;
                        break;
                    }
                }
            }

            grp.size = eps + credits + specials + trailers + parodies + others;
            grp.localsize = local_eps + local_credits + local_specials + local_trailers + local_parodies + local_others;
            grp.viewed = watched_eps + watched_credits + watched_specials + watched_trailers + watched_parodies + watched_others;

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
}