using System.Collections.Generic;
using System.Linq;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using Raven.Client;

namespace JMMDatabase.Repos
{
    public class GroupFilterRepo : BaseRepo<GroupFilter>
    {
        
        public Dictionary<string, Dictionary<string, HashSet<string>>> FilterGroups { get; set; } = new Dictionary<string, Dictionary<string, HashSet<string>>>();

        public HashSet<string> GetFilterGroupsIds(string filterid, string userid)
        {
            if (!FilterGroups.ContainsKey(filterid))
                return new HashSet<string>();
            Dictionary<string, HashSet<string>> dic=FilterGroups.Find(filterid);
            return dic.Find(userid);
        }
        public List<AnimeGroup> GetFilterGroups(string filterid, string userid)
        {
            return GetFilterGroupsIds(filterid,userid).SelectOrDefault(a=>Store.AnimeGroupRepo.Find(a)).ToList();
        }



        internal override void InternalSave(GroupFilter gf, IDocumentSession s, UpdateType type = UpdateType.All)
        {
            foreach (AnimeGroup g in Store.AnimeGroupRepo.ToList())
            {
                bool change = false;
                foreach (GroupUserStats u in g.UsersStats)
                {
                    JMMUser us = Store.JmmUserRepo.Find(u.JMMUserId);
                    if ((us!=null) && (gf.EvaluateGroup(g, us)))
                    {
                        if (!u.GroupFilters.Contains(gf.Id))
                        {
                            u.GroupFilters.Add(gf.Id);
                            change = true;
                        }
                    }
                    else
                    {
                        if (u.GroupFilters.Contains(gf.Id))
                        {
                            u.GroupFilters.Remove(gf.Id);
                            change = true;
                        }
                    }
                }
                if (change)
                    Store.AnimeGroupRepo.Save(g,s,UpdateType.None);
            }
            Items[gf.Id] = gf;
        }

        internal override void InternalDelete(GroupFilter gf, IDocumentSession s)
        {
            foreach (AnimeGroup g in Store.AnimeGroupRepo.ToList())
            {
                bool change = false;
                foreach (GroupUserStats u in g.UsersStats)
                {
                    if (u.GroupFilters.Contains(gf.Id))
                    {
                        u.GroupFilters.Remove(gf.Id);
                        change = true;
                    }
                }
                if (change)
                    Store.AnimeGroupRepo.Save(g,s, UpdateType.None);
            }
            FilterGroups.Remove(gf.Id);
            Items.Remove(gf.Id);
        }



        public override void Populate(IDocumentSession session)
        {
            Items = session.GetAll<GroupFilter>().ToDictionary(a => a.Id, a => a);
            FilterGroups = Items.ToDictionary(a => a.Key, a => Store.JmmUserRepo.ToList().ToDictionary(b => b.Id, b => Store.AnimeGroupRepo.AsQueryable().Where(c => c.UsersStats.Any(d => d.JMMUserId == b.Id && d.GroupFilters.Contains(a.Value.Id))).Select(e => e.Id).ToHashSet()));
        }
    }
}
