using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
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

        public Serie()
        {
            art = new ArtCollection();
            roles = new List<Role>();
            tags = new List<Tag>();
        }

        public static Serie GenerateFromVideoLocal(SVR_VideoLocal vl, int uid, bool nocast, bool notag, int level,
            bool all)
        {
            Serie sr = new Serie();

            if (vl != null)
            {
                foreach (SVR_AnimeEpisode ep in vl.GetAnimeEpisodes())
                {
                    sr = GenerateFromAnimeSeries(ep.GetAnimeSeries(), uid, nocast, notag, level, all);
                }
            }

            return sr;
        }

        public static Serie GenerateFromAnimeSeries(SVR_AnimeSeries ser, int uid, bool nocast, bool notag, int level,
            bool all)
        {
            Serie sr = new Serie();

            Video nv = ser.GetPlexContract(uid);

            sr.id = ser.AnimeSeriesID;
            sr.summary = nv.Summary;
            sr.year = nv.Year;
            sr.air = nv.AirDate.ToString("dd-MM-yyyy");
            sr.size = int.Parse(nv.LeafCount);
            sr.localsize = int.Parse(nv.ChildCount);
            sr.viewed = int.Parse(nv.ViewedLeafCount);
            sr.rating = nv.Rating;
            sr.userrating = nv.UserRating;
            sr.titles = nv.Titles;
            sr.name = nv.Title;
            sr.season = nv.Season;
            if (nv.IsMovie)
            {
                sr.ismovie = 1;
            }

            Random rand = new Random();
            Contract_ImageDetails art = new Contract_ImageDetails();
            if (nv.Fanarts != null && nv.Fanarts.Count > 0)
            {
                art = nv.Fanarts[rand.Next(nv.Fanarts.Count)];
                sr.art.fanart.Add(new Art()
                {
                    url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                    index = 0
                });
            }

            if (nv.Banner != null && nv.Fanarts.Count > 0)
            {
                art = nv.Banners[rand.Next(nv.Banners.Count)];

                sr.art.banner.Add(new Art()
                {
                    url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                    index = 0
                });
            }

            if (!string.IsNullOrEmpty(nv.Thumb))
            {
                sr.art.thumb.Add(new Art() {url = APIHelper.ConstructImageLinkFromRest(nv.Thumb), index = 0});
            }

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
                            new_role.namepic = APIHelper.ConstructImageLinkFromRest(rtg.TagPicture);
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
                            new_role.rolepic = APIHelper.ConstructImageLinkFromRest(rtg.RolePicture);
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
                        Tag new_tag = new Tag();
                        new_tag.tag = otg.Value;
                        sr.tags.Add(new_tag);
                    }
                }
            }

            if (level > 0)
            {
                List<SVR_AnimeEpisode> ael = ser.GetAnimeEpisodes();
                if (ael.Count > 0)
                {
                    sr.eps = new List<Episode>();
                    foreach (SVR_AnimeEpisode ae in ael)
                    {
                        if (!all && (ae?.GetVideoLocals()?.Count ?? 0) == 0) continue;
                        Episode new_ep = Episode.GenerateFromAnimeEpisode(ae, uid, (level - 1));
                        if (new_ep != null)
                        {
                            sr.eps.Add(new_ep);
                        }
                    }
                    sr.eps = sr.eps.OrderBy(a => a.epnumber).ToList();
                }
            }

            return sr;
        }

        public int CompareTo(object obj)
        {
            Serie a = obj as Serie;
            if (a == null) return 1;
            int s, s1;
            // try year first, as it is more likely to have relevannt data
            if (int.TryParse(a.year, out s1) && int.TryParse(year, out s))
            {
                if (s < s1) return -1;
                if (s > s1) return 1;
            }
            // Does it have an air date? Sort by it
            if (!string.IsNullOrEmpty(a.air) && !a.air.Equals(DateTime.MinValue.ToString("dd-MM-yyyy")) &&
                !string.IsNullOrEmpty(air) && !air.Equals(DateTime.MinValue.ToString("dd-MM-yyyy")))
            {
                DateTime d, d1;
                if (DateTime.TryParseExact(a.air, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out d1) &&
                    DateTime.TryParseExact(air, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
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