using System.Collections.Generic;
using System.Linq;

namespace JMMServer.API.Model.common
{
    public class Filter
    {
        public int id { get; set; }
        public string name { get; set; }
        public ArtCollection art { get; set; }
        public int size { get; set; }
        public int viewed { get; set;}
        public string url { get; set; }
        public string type { get; set; }
        public List<Group> groups { get; set; }

        public Filter()
        {
            art = new ArtCollection();
            groups = new List<Group>();
        }

        internal Filter GenerateFromGroupFilter(Entities.GroupFilter gf, int uid, int nocast, int notag, int level)
        {
            Filter filter = new Filter();
            filter.name = gf.GroupFilterName;
            filter.id = gf.GroupFilterID;
            filter.size = 0;
            filter.type = gf.FilterType.ToString();

            if (gf.GroupsIds.ContainsKey(uid))
            {
                HashSet<int> groupsh = gf.GroupsIds[uid];
                if (groupsh.Count != 0)
                {
                    filter.size = groupsh.Count;
                    if (level != 1)
                    {
                        foreach (int gp in groupsh)
                        {
                            Entities.AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(groupsh.First<int>());
                            groups.Add(new Group().GenerateFromAnimeGroup(ag, uid, nocast, notag, (level - 1)));
                            if (ag != null)
                            {
                                JMMContracts.PlexAndKodi.Video v = ag.GetPlexContract(uid);
                                if (v.Banner != null)
                                {
                                    filter.art.banner.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(v.Banner), index = filter.art.banner.Count });
                                }

                                // we want to have same fanart and thumb
                                if (v.Art != null & v.Thumb != null)
                                {
                                    filter.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(v.Art), index = filter.art.fanart.Count });
                                    filter.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(v.Thumb), index = filter.art.thumb.Count });
                                }

                                if (filter.art.fanart.Count > 0 && filter.art.thumb.Count > 0)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            filter.viewed = 0;
            filter.url = APIHelper.ConstructFilterIdUrl(filter.id);

            return filter;
        }
    }
}
