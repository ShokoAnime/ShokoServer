using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.API.Model.common
{
    public class Group : BaseDirectory
    {
        public List<Serie> series { get; set; }

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

        public Group GenerateFromAnimeGroup(Entities.AnimeGroup ag, int uid, bool nocast, bool notag, int level, bool all, int filterid)
        {
            Group g = new Group();

            g.name = ag.GroupName;
            g.id = ag.AnimeGroupID;

            //g.videoqualities = ag.VideoQualities; <-- deadly trap
            g.added = ag.DateTimeCreated;
            g.edited = ag.DateTimeUpdated;

            JMMContracts.PlexAndKodi.Video vag = ag.GetPlexContract(uid);
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


            g.size = int.Parse(vag.ChildCount);
            g.rating = vag.Rating;
            g.userrating = vag.UserRating;

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
                foreach (Entities.AniDB_Anime ada in ag.Anime)
                {
                    if (series != null && series.Count > 0)
                    {
                        if (series.Contains(ada.AniDB_AnimeID)) continue;
                    }
                    g.series.Add(new Serie().GenerateFromAnimeSeries(Repositories.RepoFactory.AnimeSeries.GetByAnimeID(ada.AnimeID), uid,nocast, notag, (level-1), all));
                }
                g.series = g.series.OrderBy(a => a).ToList();
            }

            return g;
        }
    }
}
