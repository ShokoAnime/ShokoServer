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

        public static Serie GenerateFromVideoLocal(NancyContext ctx, SVR_VideoLocal vl, int uid, bool nocast, bool notag, int level, bool all, bool allpic, int pic)
        {
            Serie sr = new Serie();

            if (vl != null)
            {
                foreach (SVR_AnimeEpisode ep in vl.GetAnimeEpisodes())
                {
                    sr = GenerateFromAnimeSeries(ctx, ep.GetAnimeSeries(), uid, nocast, notag, level, all, allpic, pic);
                }
            }

            return sr;
        }

        public static Serie GenerateFromAnimeSeries(NancyContext ctx, SVR_AnimeSeries ser, int uid, bool nocast, bool notag, int level, bool all, bool allpic, int pic)
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
            Random rand = new Random();
            Contract_ImageDetails art;
            if (nv.Fanarts != null && nv.Fanarts.Count > 0)
            {
                if (allpic || pic > 1)
                {
                    int pic_index = 0;
                    foreach (Contract_ImageDetails cont_image in nv.Fanarts)
                    {
                        if (pic_index < pic)
                        {
                            sr.art.fanart.Add(new Art()
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, cont_image.ImageType, cont_image.ImageID),
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
                    art = nv.Fanarts[rand.Next(nv.Fanarts.Count)];
                    sr.art.fanart.Add(new Art()
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType, art.ImageID),
                        index = 0
                    });
                }
            }

            if (nv.Banners != null && nv.Banners.Count > 0)
            {
                if (allpic || pic > 1)
                {
                    int pic_index = 0;
                    foreach (Contract_ImageDetails cont_image in nv.Banners)
                    {
                        if (pic_index < pic)
                        {
                            sr.art.banner.Add(new Art()
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, cont_image.ImageType, cont_image.ImageID),
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
                    art = nv.Banners[rand.Next(nv.Banners.Count)];
                    sr.art.banner.Add(new Art()
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType, art.ImageID),
                        index = 0
                    });
                }
            }

            if (!string.IsNullOrEmpty(nv.Thumb))
            {
                sr.art.thumb.Add(new Art() {url = APIHelper.ConstructImageLinkFromRest(ctx, nv.Thumb), index = 0});
            }
            #endregion

            if (!nocast)
            {
                if (nv.Roles != null)
                {
                    foreach (RoleTag rtg in nv.Roles)
                    {
                        Role new_role = new Role();
                        if (!String.IsNullOrEmpty(rtg.Value))
                        {
                            new_role.name = rtg.Value;
                        }
                        else
                        {
                            new_role.name = "";
                        }
                        if (!String.IsNullOrEmpty(rtg.TagPicture))
                        {
                            new_role.namepic = APIHelper.ConstructImageLinkFromRest(ctx, rtg.TagPicture);
                        }
                        else
                        {
                            new_role.namepic = "";
                        }
                        if (!String.IsNullOrEmpty(rtg.Role))
                        {
                            new_role.role = rtg.Role;
                        }
                        else
                        {
                            rtg.Role = "";
                        }
                        if (!String.IsNullOrEmpty(rtg.RoleDescription))
                        {
                            new_role.roledesc = rtg.RoleDescription;
                        }
                        else
                        {
                            new_role.roledesc = "";
                        }
                        if (!String.IsNullOrEmpty(rtg.RolePicture))
                        {
                            new_role.rolepic = APIHelper.ConstructImageLinkFromRest(ctx, rtg.RolePicture);
                        }
                        else
                        {
                            new_role.rolepic = "";
                        }
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
                switch (ep.EpisodeTypeEnum)
                {
                    case enEpisodeType.Episode:
                    {
                        eps++;
                        if (ep.PlexContract?.Medias?.Any() ?? false) local_eps++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_eps++;
                        break;
                    }
                    case enEpisodeType.Credits:
                    {
                        credits++;
                        if (ep.PlexContract?.Medias?.Any() ?? false) local_credits++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_credits++;
                        break;
                    }
                    case enEpisodeType.Special:
                    {
                        specials++;
                        if (ep.PlexContract?.Medias?.Any() ?? false) local_specials++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_specials++;
                        break;
                    }
                    case enEpisodeType.Trailer:
                    {
                        trailers++;
                        if (ep.PlexContract?.Medias?.Any() ?? false) local_trailers++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_trailers++;
                        break;
                    }
                    case enEpisodeType.Parody:
                    {
                        parodies++;
                        if (ep.PlexContract?.Medias?.Any() ?? false) local_parodies++;
                        if ((ep.GetUserRecord(uid)?.WatchedCount ?? 0) > 0) watched_parodies++;
                        break;
                    }
                    case enEpisodeType.Other:
                    {
                        others++;
                        if (ep.PlexContract?.Medias?.Any() ?? false) local_others++;
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