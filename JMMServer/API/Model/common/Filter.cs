using System;
using System.Collections.Generic;
using System.Linq;
using JMMContracts.PlexAndKodi;

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
        public readonly string type = "filter";
        public List<Group> groups { get; set; }

        public Filter()
        {
            art = new ArtCollection();
            groups = new List<Group>();
        }

        internal Filter GenerateFromGroupFilter(Entities.GroupFilter gf, int uid, int nocast, int notag, int level, int all)
        {
            Filter filter = new Filter();
            filter.name = gf.GroupFilterName;
            filter.id = gf.GroupFilterID;
            filter.size = 0;

            if (gf.GroupsIds.ContainsKey(uid))
            {
                HashSet<int> groupsh = gf.GroupsIds[uid];
                if (groupsh.Count != 0)
                {
                    filter.size = groupsh.Count;

                    // Populate Random Art
                    int index = new Random().Next(groupsh.Count);
                    Entities.AnimeGroup randGrp = Repositories.RepoFactory.AnimeGroup.GetByID(groupsh.ToList()[index]);
                    Video contract = randGrp?.GetPlexContract(uid);
                    if (contract != null)
                    {
                        // contract.Fanarts can be null even if contract isn't
                        if (contract.Fanarts != null)
                        {
                            Random rand = new Random();
                            Contract_ImageDetails art = contract.Fanarts[rand.Next(contract.Fanarts.Count)];
                            filter.art.fanart.Add(new Art()
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                                index = 0
                            });
                            art = contract.Banners[rand.Next(contract.Banners.Count)];
                            filter.art.banner.Add(new Art()
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                                index = 0
                            });
                            if (!string.IsNullOrEmpty(contract.Thumb)) { filter.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(contract.Thumb), index = 0 }); }
                        }
                    }

                    if (level > 1)
                    {
                        foreach (int gp in groupsh)
                        {
                            Entities.AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(gp);

                            if (ag != null)
                            {
                                JMMContracts.PlexAndKodi.Video v = ag.GetPlexContract(uid);
                                groups.Add(new Group().GenerateFromAnimeGroup(ag, uid, nocast, notag, (level - 1), all,
                                    filter.id));
                            }
                        }
                    }
                    // save groups
                    if (groups.Count > 0)
                    {
                        filter.groups = groups;
                    }
                }
            }

            filter.viewed = 0;
            filter.url = APIHelper.ConstructFilterIdUrl(filter.id);

            return filter;
        }
    }
}
