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

        public Filter()
        {
            art = new ArtCollection();
        }

        internal Filter GenerateFromGroupFilter(Entities.GroupFilter gf, int uid)
        {
            Filter filter = new Filter();
            filter.name = gf.GroupFilterName;
            filter.id = gf.GroupFilterID;
            filter.size = 0;
            filter.type = gf.FilterType.ToString();

            if (gf.GroupsIds.ContainsKey(uid))
            {
                System.Collections.Generic.HashSet<int> groups = gf.GroupsIds[uid];
                if (groups.Count != 0)
                {
                    filter.size = groups.Count;

                    foreach (int gp in groups)
                    {
                        Entities.AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(groups.First<int>());
                        if (ag != null)
                        {
                            if (ag.GetPlexContract(uid).Banner != null)
                            {
                                filter.art.banner.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(ag.GetPlexContract(uid).Banner), index = filter.art.banner.Count });
                            }
                            if (ag.GetPlexContract(uid).Art != null & ag.GetPlexContract(uid).Thumb != null)
                            {
                                filter.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(ag.GetPlexContract(uid).Art), index = filter.art.fanart.Count });
                            }
                            if (ag.GetPlexContract(uid).Thumb != null & ag.GetPlexContract(uid).Art != null)
                            {
                                filter.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(ag.GetPlexContract(uid).Thumb), index = filter.art.thumb.Count });
                            }

                            if (filter.art.fanart.Count > 0 && filter.art.thumb.Count > 0)
                            {
                                break;
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
