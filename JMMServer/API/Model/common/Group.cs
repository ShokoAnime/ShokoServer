using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.API.Model.common
{
    [DataContract]
    public class Group : BaseDirectory
    {
        // We need to rethink this. It doesn't support subgroups
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<Serie> series { get; set; }

        [DataMember]
        public override string type
        {
            get { return "group"; }
        }

        public Group()
        {
            series = new List<Serie>();
            art = new ArtCollection();
            tags = new List<Tag>();
        }

        public static Group GenerateFromAnimeGroup(Entities.AnimeGroup ag, int uid, bool nocast, bool notag, int level, bool all, int filterid)
        {
            Group g = new Group();

            g.name = ag.GroupName;
            g.id = ag.AnimeGroupID;

            //g.videoqualities = ag.VideoQualities; <-- deadly trap
            g.added = ag.DateTimeCreated;
            g.edited = ag.DateTimeUpdated;

            JMMContracts.PlexAndKodi.Video vag = ag.GetPlexContract(uid);

            if (vag != null)
            {
                g.air = vag.OriginallyAvailableAt;

                g.size = int.Parse(vag.LeafCount);
                g.viewed = int.Parse(vag.ViewedLeafCount);

                g.rating = vag.Rating;
                g.userrating = vag.UserRating;

                g.summary = vag.Summary;
                g.titles = vag.Titles;
                g.year = vag.Year;

                Random rand = new Random();
                Contract_ImageDetails art = new Contract_ImageDetails();
                // vag.Fanarts can be null even if contract isn't
                if (vag.Fanarts != null && vag.Fanarts.Count > 0)
                {
                    art = vag.Fanarts[rand.Next(vag.Fanarts.Count)];
                    g.art.fanart.Add(new Art()
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                        index = 0
                    });
                }

                if (vag.Banners != null && vag.Banners.Count > 0)
                {
                    art = vag.Banners[rand.Next(vag.Banners.Count)];
                    g.art.banner.Add(new Art()
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                        index = 0
                    });
                    if (!string.IsNullOrEmpty(vag.Thumb)) { g.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(vag.Thumb), index = 0 }); }
                }

                if (!nocast)
                {
                    if (vag.Roles != null)
                    {
                        g.roles = new List<Role>();
                        foreach (RoleTag rtg in vag.Roles)
                        {
                            Role new_role = new Role();
                            if (!String.IsNullOrEmpty(rtg.Value)) { new_role.name = rtg.Value; } else { new_role.name = ""; }
                            if (!String.IsNullOrEmpty(rtg.TagPicture)) { new_role.namepic = APIHelper.ConstructImageLinkFromRest(rtg.TagPicture); } else { new_role.namepic = ""; }
                            if (!String.IsNullOrEmpty(rtg.Role)) { new_role.role = rtg.Role; } else { rtg.Role = ""; }
                            if (!String.IsNullOrEmpty(rtg.RoleDescription)) { new_role.roledesc = rtg.RoleDescription; } else { new_role.roledesc = ""; }
                            if (!String.IsNullOrEmpty(rtg.RolePicture)) { new_role.rolepic = APIHelper.ConstructImageLinkFromRest(rtg.RolePicture); } else { new_role.rolepic = ""; }
                            g.roles.Add(new_role);
                        }
                    }
                }

                if (!notag)
                {
                    if (vag.Genres != null)
                    {
                        foreach (JMMContracts.PlexAndKodi.Tag otg in vag.Genres)
                        {
                            Tag new_tag = new Tag();
                            new_tag.tag = otg.Value;
                            g.tags.Add(new_tag);
                        }
                    }
                }
            }

            if (level > 0)
            {
                List<int> series = null;
                if (filterid > 0)
                {
                    GroupFilter filter = RepoFactory.GroupFilter.GetByID(filterid);
                    if (filter?.ApplyToSeries > 0)
                    {
                        if (filter.SeriesIds.ContainsKey(uid))
                            series = filter.SeriesIds[uid].ToList();
                    }
                }
                foreach (Entities.AnimeSeries ada in ag.GetSeries())
                {
                    if (series != null && series.Count > 0)
                    {
                        if (series.Contains(ada.AnimeSeriesID)) continue;
                    }
                    g.series.Add(Serie.GenerateFromAnimeSeries(ada, uid,nocast, notag, (level-1), all));
                }
                // This should be faster
                g.series.Sort();
            }

            return g;
        }
    }
}
