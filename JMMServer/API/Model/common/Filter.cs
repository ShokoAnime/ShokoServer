using System;
using System.Collections.Generic;
using System.Linq;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Repositories;

namespace JMMServer.API.Model.common
{
    public class Filter : BaseDirectory
    {
        public override string type { get { return "filter"; } }
        public string url { get; set; }
        public List<Group> groups { get; set; }

        public Filter()
        {
            art = new ArtCollection();
            groups = new List<Group>();
        }

        internal Filter GenerateFromGroupFilter(Entities.GroupFilter gf, int uid, bool nocast, bool notag, int level, bool all)
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
                        Random rand = new Random();
                        Contract_ImageDetails art = new Contract_ImageDetails();
                        // contract.Fanarts can be null even if contract isn't
                        if (contract.Fanarts != null && contract.Fanarts.Count > 0)
                        {
                            art = contract.Fanarts[rand.Next(contract.Fanarts.Count)];
                            filter.art.fanart.Add(new Art()
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                                index = 0
                            });
                        }

                        if (contract.Banners != null && contract.Banners.Count > 0)
                        {
                            art = contract.Banners[rand.Next(contract.Banners.Count)];
                            filter.art.banner.Add(new Art()
                            {
                                url = APIHelper.ConstructImageLinkFromTypeAndId(art.ImageType, art.ImageID),
                                index = 0
                            });
                            if (!string.IsNullOrEmpty(contract.Thumb)) { filter.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(contract.Thumb), index = 0 }); }
                        }
                    }

                    Dictionary<Contract_AnimeGroup, Group> order = new Dictionary<Contract_AnimeGroup, Group>();
                    if (level > 0)
                    {
                        foreach (int gp in groupsh)
                        {
                            Entities.AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(gp);
                            if (ag == null) continue;
                            Group group =
                                new Group().GenerateFromAnimeGroup(ag, uid, nocast, notag, (level - 1), all,
                                    filter.id);
                            groups.Add(group);
                            order.Add(ag.GetUserContract(uid), group);
                        }
                    }
                    
                    if (groups.Count > 0)
                    {
                        // Proper Sorting!
                        IEnumerable<Contract_AnimeGroup> grps = order.Keys;
                        grps = gf.SortCriteriaList.Count != 0 ? GroupFilterHelper.Sort(grps, gf) : grps.OrderBy(a => a.GroupName);
                        groups = grps.Select(a => order[a]).ToList();
                    }
                }
            }

            filter.viewed = 0;
            filter.url = APIHelper.ConstructFilterIdUrl(filter.id);

            return filter;
        }
    }
}
