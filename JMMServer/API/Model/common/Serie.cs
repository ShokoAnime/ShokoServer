using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using System;
using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class Serie
    {
        public int id { get; set; }
        public ArtCollection art { get; set; }
        public string title { get; set; }
        public List<AnimeTitle> titles { get; set; }
        public string summary { get; set; }
        public string year { get; set; }
        public string air { get; set; }
	    public string season { get; set; }
        public string size { get; set; }
        public string localsize { get; set; }
        public string viewed { get; set; }
        public string rating { get; set; }
        public string userrating { get; set; }
        public List<Role> roles { get; set; }
        public List<Tag> tags { get; set; }
        public List<Episode> eps { get; set; }
        public readonly string type = "serie";

        public Serie()
        {
            art = new ArtCollection();
            roles = new List<Role>();
            tags = new List<Tag>();
        }

        public Serie GenerateFromVideoLocal(VideoLocal vl, int uid, int nocast, int notag, int level, int all)
        {
            Serie sr = new Serie();

            if (vl != null)
            {
                foreach (AnimeEpisode ep in vl.GetAnimeEpisodes())
                {
                    sr = GenerateFromAnimeSeries(ep.GetAnimeSeries(), uid, nocast, notag, level, all);
                }
            }

            return sr;
        }

        public Serie GenerateFromAnimeSeries(AnimeSeries ser, int uid, int nocast, int notag, int level, int all)
        {
            Serie sr = new Serie();

            Video nv = ser.GetPlexContract(uid);

            sr.id = ser.AnimeSeriesID;
            sr.summary = nv.Summary;
            sr.year = nv.Year;
            sr.air = nv.AirDate.ToString("dd-MM-yyyy");
            sr.size = nv.LeafCount;
            sr.localsize = nv.ChildCount;
            sr.viewed = nv.ViewedLeafCount;
            sr.rating = nv.Rating;
            sr.userrating = nv.UserRating;
            sr.titles = nv.Titles;
            sr.title = nv.Title;
	        sr.season = nv.Season;

            // until fanart refactor this will be good for start
            if (!String.IsNullOrEmpty(nv.Thumb)) { sr.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(nv.Thumb), index = 0 }); }
            if (!String.IsNullOrEmpty(nv.Banner)) { sr.art.banner.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(nv.Banner), index = 0 }); }
            if (!String.IsNullOrEmpty(nv.Art)) { sr.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(nv.Art), index = 0 }); }

            if (nocast == 0)
            {
                if (nv.Roles != null)
                {
                    foreach (RoleTag rtg in nv.Roles)
                    {
                        Role new_role = new Role();
                        if (!String.IsNullOrEmpty(rtg.Value)) { new_role.name = rtg.Value; } else { new_role.name = ""; }
                        if (!String.IsNullOrEmpty(rtg.TagPicture)) { new_role.namepic = APIHelper.ConstructImageLinkFromRest(rtg.TagPicture); } else { new_role.namepic = ""; }
                        if (!String.IsNullOrEmpty(rtg.Role)) { new_role.role = rtg.Role; } else { rtg.Role = ""; }
                        if (!String.IsNullOrEmpty(rtg.RoleDescription)) { new_role.roledesc = rtg.RoleDescription; } else { new_role.roledesc = ""; }
                        if (!String.IsNullOrEmpty(rtg.RolePicture)) { new_role.rolepic = APIHelper.ConstructImageLinkFromRest(rtg.RolePicture); } else { new_role.rolepic = ""; }
                        sr.roles.Add(new_role);
                    }
                }
            }

            if (notag == 0)
            {
                if (nv.Genres != null)
                {
                    foreach (JMMContracts.PlexAndKodi.Tag otg in nv.Genres)
                    {
                        Tag new_tag = new Tag();
                        new_tag.tag = otg.Value;
                        sr.tags.Add(new_tag);
                    }
                }
            }

            if (level != 1)
            {
                List<AnimeEpisode> ael = ser.GetAnimeEpisodes();
                if (ael.Count > 0)
                {
                    sr.eps = new List<Episode>();
                    foreach (AnimeEpisode ae in ael)
                    {
                        Episode new_ep = new Episode().GenerateFromAnimeEpisode(ae, uid, (level - 1), all);
                        if (new_ep != null)
                        {
                            sr.eps.Add(new_ep);
                        }
                    }
                }
            }

            return sr;
        }
    }
}
