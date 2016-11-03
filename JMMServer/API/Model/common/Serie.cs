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
        public string url { get; set; }
        public string type { get; set; }
        public List<AnimeTitle> titles { get; set; }
        public string summary { get; set; }
        public string year { get; set; }
        public string air { get; set; }
        // why not int ?
        public string size { get; set; }
        public string viewed { get; set; }
        public string season { get; set; }
        // rename later + why not int ?
        public string childcount { get; set; }

        public string rating { get; set; }
        public List<Role> roles { get; set; }
        public List<Tag> tags { get; set; }

        public Serie()
        {

        }

        public Serie(AnimeSeries ser, int uid)
        {
            Video nv = ser.GetPlexContract(uid);

            int Id = 0;
            if (Int32.TryParse(nv.Id, out Id)) { id = Id; }
            url = nv.Key;
            type = nv.Type;
            summary = nv.Summary;
            year = nv.Year;
            air = nv.AirDate.ToString();
            size = nv.LeafCount;
            viewed = nv.ViewedLeafCount;
            rating = nv.Rating;
            season = nv.Season;
            childcount = nv.ChildCount;
            titles = nv.Titles;
            art = new ArtCollection();
            roles = new List<Role>();
            tags = new List<Tag>();

            // until fanart refactor this will be good for start
            art.thumb.Add(new Art(){ url = nv.Thumb, index = 0});
            art.banner.Add(new Art() { url = nv.Banner, index = 0 });
            art.fanart.Add(new Art() { url = nv.Art, index = 0 });

            if (nv.Roles != null)
            {
                foreach (RoleTag rtg in nv.Roles)
                {
                    Role new_role = new Role();
                    new_role.name = rtg.Value;
                    new_role.namepic = rtg.TagPicture;
                    new_role.role = rtg.Role;
                    new_role.roledesc = rtg.RoleDescription;
                    new_role.rolepic = rtg.RolePicture;
                    roles.Add(new_role);
                }
            }

            if (nv.Genres != null)
            {
                foreach (JMMContracts.PlexAndKodi.Tag otg in nv.Genres)
                {
                    Tag new_tag = new Tag();
                    new_tag.tag = otg.Value;
                    tags.Add(new_tag);
                }
            }
        }
    }
}
