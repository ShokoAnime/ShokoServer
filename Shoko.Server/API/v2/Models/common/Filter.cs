using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Nancy;
using Shoko.Models.Client;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.Models;

namespace Shoko.Server.API.v2.Models.common
{
    [DataContract]
    public class Filter : BaseDirectory
    {
        public override string type
        {
            get { return "filter"; }
        }

        // We need to rethink this
        // There is too much duplicated info.
        // example:
        // groups { { name="the series" air="a date" year="2017" ... series { { name="the series" air="a date" year="2017" ... }, {...} } }
        // my plan is:
        // public List<BaseDirectory> subdirs;
        // structure:
        // subdirs { { type="group" name="the group" ... series {...} }, { type="serie" name="the series" ... eps {...} } }
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<Group> groups { get; set; }

        public Filter()
        {
            art = new ArtCollection();
            groups = new List<Group>();
        }

        internal static Filter GenerateFromGroupFilter(NancyContext ctx, SVR_GroupFilter gf, int uid, bool nocast, bool notag, int level,
            bool all)
        {
            List<Group> groups = new List<Group>();
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

                    int rand_art_iteration = 0;

                    // Populate Random Art
                    while (rand_art_iteration < 3)
                    {
                        int index = new Random().Next(groupsh.Count);
                        SVR_AnimeGroup randGrp = Repositories.RepoFactory.AnimeGroup.GetByID(groupsh.ToList()[index]);
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
                                    url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType, art.ImageID),
                                    index = 0
                                });
                                rand_art_iteration = 3;

                                // we only want banner if we have fanart, other way will desync images
                                if (contract.Banners != null && contract.Banners.Count > 0)
                                {
                                    art = contract.Banners[rand.Next(contract.Banners.Count)];
                                    filter.art.banner.Add(new Art()
                                    {
                                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, art.ImageType, art.ImageID),
                                        index = 0
                                    });
                                    if (!string.IsNullOrEmpty(contract.Thumb))
                                    {
                                        filter.art.thumb.Add(new Art()
                                        {
                                            url = APIHelper.ConstructImageLinkFromRest(ctx, contract.Thumb),
                                            index = 0
                                        });
                                    }
                                }
                            }
                        }
                        rand_art_iteration++;
                    }

                    Dictionary<CL_AnimeGroup_User, Group> order = new Dictionary<CL_AnimeGroup_User, Group>();
                    if (level > 0)
                    {
                        foreach (int gp in groupsh)
                        {
                            SVR_AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(gp);
                            if (ag == null) continue;
                            Group group =
                                Group.GenerateFromAnimeGroup(ctx, ag, uid, nocast, notag, (level - 1), all,
                                    filter.id);
                            groups.Add(group);
                            order.Add(ag.GetUserContract(uid), group);
                        }
                    }

                    if (groups.Count > 0)
                    {
                        // Proper Sorting!
                        IEnumerable<CL_AnimeGroup_User> grps = order.Keys;
                        grps = gf.SortCriteriaList.Count != 0
                            ? GroupFilterHelper.Sort(grps, gf)
                            : grps.OrderBy(a => a.GroupName);
                        groups = grps.Select(a => order[a]).ToList();
                        filter.groups = groups;
                    }
                }
            }

            filter.viewed = 0;
            filter.url = APIHelper.ConstructFilterIdUrl(ctx, filter.id);

            return filter;
        }
    }
}