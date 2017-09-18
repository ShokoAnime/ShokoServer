using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Nancy;
using Shoko.Models.Enums;
using Shoko.Server.Models;

namespace Shoko.Server.API.v2.Models.common
{
    [DataContract]
    public class Serie : BaseDirectory, IComparable
    {
        public override string type
        {
            get { return "serie"; }
        }

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
            roles = new List<Role>();
            tags = new List<Tag>();
        }

        public static Serie GenerateFromVideoLocal(NancyContext ctx, SVR_VideoLocal vl, int uid, bool nocast, bool notag, int level, bool all, bool allpics, int pic, byte tagfilter)
        {
            Serie sr = new Serie();

            if (vl != null)
            {
                foreach (SVR_AnimeEpisode ep in vl.GetAnimeEpisodes())
                {
                    sr = GenerateFromAnimeSeries(ctx, ep.GetAnimeSeries(), uid, nocast, notag, level, all, allpics, pic, tagfilter);
                }
            }

            return sr;
        }

        public static Serie GenerateFromAnimeSeries(NancyContext ctx, SVR_AnimeSeries ser, int uid, bool nocast, bool notag, int level, bool all, bool allpics, int pic, byte tagfilter)
        {
            Serie sr = new Serie();

            Video nv = ser.GetPlexContract(uid);

            List<SVR_AnimeEpisode> ael = ser.GetAnimeEpisodes();

            sr.id = ser.AnimeSeriesID;
            sr.summary = nv.Summary;
            sr.year = nv.Year;
            sr.air = nv.AirDate.ToString("dd-MM-yyyy");

            GenerateSizes(sr, ael, uid);

            sr.rating = nv.Rating;
            sr.userrating = nv.UserRating;
            sr.titles = nv.Titles;
            sr.name = nv.Title;
            sr.season = nv.Season;
            if (nv.IsMovie)
            {
                sr.ismovie = 1;
            }

            #region Images
            var anime = ser.GetAnime();
            if (anime != null)
            {
                Random rand = new Random();
                if (allpics || pic > 1)
                {
                    if (allpics) { pic = 999; }
                    int pic_index = 0;
                    if (anime.AllPosters != null)
                        foreach (var cont_image in anime.AllPosters)
                        {
                            if (pic_index < pic)
                            {
                                sr.art.thumb.Add(new Art()
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
                    if (anime.Contract.AniDBAnime.Fanarts != null)
                        foreach (var cont_image in anime.Contract.AniDBAnime.Fanarts)
                        {
                            if (pic_index < pic)
                            {
                                sr.art.fanart.Add(new Art()
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
                    if (anime.Contract.AniDBAnime.Banners != null)
                        foreach (var cont_image in anime.Contract.AniDBAnime.Banners)
                        {
                            if (pic_index < pic)
                            {
                                sr.art.banner.Add(new Art()
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
                else
                {
                    sr.art.thumb.Add(new Art()
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int) ImageEntityType.AniDB_Cover,
                            anime.AnimeID),
                        index = 0
                    });

                    var fanarts = anime.Contract.AniDBAnime.Fanarts;
                    if (fanarts != null && fanarts.Count > 0)
                    {
                        var art = fanarts[rand.Next(fanarts.Count)];
                        sr.art.fanart.Add(new Art()
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
                        sr.art.banner.Add(new Art()
                        {
                            url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType,
                                art.AniDB_Anime_DefaultImageID),
                            index = 0
                        });
                    }
                }
            }
            #endregion

            if (!nocast)
            {
                if (nv.Roles != null)
                {
                    foreach (RoleTag rtg in nv.Roles)
                    {
                        Role new_role = new Role();
                        new_role.name = !string.IsNullOrEmpty(rtg.Value) ? rtg.Value : string.Empty;
                        new_role.namepic = !string.IsNullOrEmpty(rtg.TagPicture)
                            ? APIHelper.ConstructImageLinkFromRest(ctx, rtg.TagPicture)
                            : string.Empty;
                        new_role.role = !string.IsNullOrEmpty(rtg.Role) ? rtg.Role : string.Empty;
                        new_role.roledesc = !string.IsNullOrEmpty(rtg.RoleDescription)
                            ? rtg.RoleDescription
                            : string.Empty;
                        new_role.rolepic = !string.IsNullOrEmpty(rtg.RolePicture)
                            ? APIHelper.ConstructImageLinkFromRest(ctx, rtg.RolePicture)
                            : string.Empty;
                        sr.roles.Add(new_role);
                    }
                }
            }

            if (!notag)
            {
                if (nv.Genres != null)
                {
                    foreach (Shoko.Models.PlexAndKodi.Tag otg in nv.Genres)
                    {
                        Tag new_tag = new Tag
                        {
                            tag = otg.Value
                        };
                        sr.tags.Add(new_tag);
                    }
                }
            }

            if (level > 0)
            {
                if (ael.Count > 0)
                {
                    sr.eps = new List<Episode>();
                    foreach (SVR_AnimeEpisode ae in ael)
                    {
                        if (!all && (ae?.GetVideoLocals()?.Count ?? 0) == 0) continue;
                        Episode new_ep = Episode.GenerateFromAnimeEpisode(ctx, ae, uid, (level - 1));
                        if (new_ep != null)
                        {
                            sr.eps.Add(new_ep);
                        }
                        if (level - 1 > 0)
                        {
                            foreach (RawFile file in new_ep.files)
                            {
                                sr.filesize += file.size;
                            }
                        }
                    }
                    sr.eps = sr.eps.OrderBy(a => a.epnumber).ToList();
                }
            }

            return sr;
        }

        private static void GenerateSizes(Serie sr, List<SVR_AnimeEpisode> ael, int uid)
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

            sr.size = eps + credits + specials + trailers + parodies + others;
            sr.localsize = local_eps + local_credits + local_specials + local_trailers + local_parodies + local_others;
            sr.viewed = watched_eps + watched_credits + watched_specials + watched_trailers + watched_parodies + watched_others;

            sr.total_sizes = new Sizes()
            {
                Episodes = eps,
                Credits = credits,
                Specials = specials,
                Trailers = trailers,
                Parodies = parodies,
                Others = others
            };

            sr.local_sizes = new Sizes()
            {
                Episodes = local_eps,
                Credits = local_credits,
                Specials = local_specials,
                Trailers = local_trailers,
                Parodies = local_parodies,
                Others = local_others
            };

            sr.watched_sizes = new Sizes()
            {
                Episodes = watched_eps,
                Credits = watched_credits,
                Specials = watched_specials,
                Trailers = watched_trailers,
                Parodies = watched_parodies,
                Others = watched_others
            };
        }

        public int CompareTo(object obj)
        {
            Serie a = obj as Serie;
            if (a == null) return 1;
            // try year first, as it is more likely to have relevannt data
            if (int.TryParse(a.year, out int s1) && int.TryParse(year, out int s))
            {
                if (s < s1) return -1;
                if (s > s1) return 1;
            }
            // Does it have an air date? Sort by it
            if (!string.IsNullOrEmpty(a.air) && !a.air.Equals(DateTime.MinValue.ToString("dd-MM-yyyy")) &&
                !string.IsNullOrEmpty(air) && !air.Equals(DateTime.MinValue.ToString("dd-MM-yyyy")))
            {
                if (DateTime.TryParseExact(a.air, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out DateTime d1) &&
                    DateTime.TryParseExact(air, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
                {
                    if (d < d1) return -1;
                    if (d > d1) return 1;
                }
            }
            // I don't trust TvDB well enough to sort by them. Bakamonogatari...
            // Does it have a Season? Sort by it
            if (int.TryParse(a.season, out s1) && int.TryParse(season, out s))
            {
                // Only try if the season is valid
                if (s >= 0 && s1 >= 0)
                {
                    // Specials
                    if (s == 0 && s1 > 0) return 1;
                    if (s > 0 && s1 == 0) return -1;
                    // Normal
                    if (s < s1) return -1;
                    if (s > s1) return 1;
                }
            }
            return name.CompareTo(a.name);
        }
    }
}