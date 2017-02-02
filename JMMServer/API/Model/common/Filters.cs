using JMMServer.Entities;
using JMMServer.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace JMMServer.API.Model.common
{
    public class Filters : BaseDirectory
    {
        public override string type { get { return "filters"; } }
        public List<Filter> filters { get; set; }

        public Filters()
        {
            art = new ArtCollection();
            filters = new List<Filter>();
        }

        internal static Filters GenerateFromGroupFilter(Entities.GroupFilter gf, int uid, bool nocast, bool notag, bool all, int level)
        {
            Filters f = new Filters();
            f.id = gf.GroupFilterID;
            f.name = gf.GroupFilterName;

            List<Filter> filters = new List<Filter>();
            List<GroupFilter> allGfs = RepoFactory.GroupFilter.GetByParentID(f.id).Where(a => a.InvisibleInClients == 0 && ((a.GroupsIds.ContainsKey(uid) && a.GroupsIds[uid].Count > 0) || (a.FilterType & (int)GroupFilterType.Directory) == (int)GroupFilterType.Directory)).ToList();
            foreach (GroupFilter cgf in allGfs)
            {
                // any level higher than 1 can drain cpu
                filters.Add(Filter.GenerateFromGroupFilter(cgf, uid, nocast, notag, level - 1, all));
            }

            f.filters = filters.OrderBy(a => a.name).ToList<Filter>();
            f.size = f.filters.Count();
            f.url = APIHelper.ConstructFilterIdUrl(f.id);

            return f;
        }
    }
}