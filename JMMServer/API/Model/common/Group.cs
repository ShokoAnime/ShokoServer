using System;
using System.Collections.Generic;
using System.Linq;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.API.Model.common
{
    public class Group
    {
        public List<Serie> series { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public List<Entities.AniDB_Anime_Title> titles { get; set; }
        public HashSet<string> videoqualities { get; set; }
        public DateTime added { get; set; }
        public DateTime edited { get; set; }
        public string summary { get; set; }
        public List<Tag> tags { get; set; }
        public string rating { get; set; }
        public string userrating { get; set; }
        public int size { get; set; }
        public ArtCollection art { get; set; }
        public readonly string type = "group";

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
            g.titles = ag.Titles;
            g.videoqualities = ag.VideoQualities;
            g.added = ag.DateTimeCreated;
            g.edited = ag.DateTimeUpdated;
            g.summary = ag.Description;

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

            if (level != 1)
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

            return g;
        }
    }
}
