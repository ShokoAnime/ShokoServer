using System;
using System.Collections.Generic;

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


        public Group GenerateFromAnimeGroup(Entities.AnimeGroup ag, int uid, int nocast, int notag, int level, int all)
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

            if (!String.IsNullOrEmpty(vag.Thumb)) { g.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(vag.Thumb), index = 0 }); }
            if (!String.IsNullOrEmpty(vag.Banner)) { g.art.banner.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(vag.Banner), index = 0 }); }
            if (!String.IsNullOrEmpty(vag.Art)) { g.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(vag.Art), index = 0 }); }

            g.size = int.Parse(vag.ChildCount);
            g.rating = vag.Rating;
            g.userrating = vag.UserRating;

            if (level != 1)
            {
                foreach (Entities.AniDB_Anime ada in ag.Anime)
                {
                    g.series.Add(new Serie().GenerateFromAnimeSeries(Repositories.RepoFactory.AnimeSeries.GetByAnimeID(ada.AnimeID), uid,nocast, notag, (level-1), all));
                }
            }

            if (notag == 0)
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
