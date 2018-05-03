using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Nancy;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common
{
    [DataContract]
    public class Filters : BaseDirectory
    {
        public override string type
        {
            get { return "filters"; }
        }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<Filter> filters { get; set; }

        public Filters()
        {
            art = new ArtCollection();
            filters = new List<Filter>();
        }

        internal static Filters GenerateFromGroupFilter(NancyContext ctx, SVR_GroupFilter gf, int uid, bool nocast, bool notag, bool all,
            int level, bool allpic, int pic, byte tagfilter)
        {
            Filters f = new Filters
            {
                id = gf.GroupFilterID,
                name = gf.GroupFilterName
            };
            List<SVR_GroupFilter> allGfs = Repo.GroupFilter.GetByParentID(f.id)
                .Where(a => a.InvisibleInClients == 0 &&
                            ((a.GroupsIds.ContainsKey(uid) && a.GroupsIds[uid].Count > 0) ||
                             (a.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory))
                .ToList();
            List<Filter> filters = allGfs
                .Where(cgf =>
                    (cgf.FilterType & (int) GroupFilterType.Tag) != (int) GroupFilterType.Tag ||
                    TagFilter.ProcessTags(tagfilter, new List<string> {cgf.GroupFilterName}).Count != 0)
                .Select(cgf =>
                    Filter.GenerateFromGroupFilter(ctx, cgf, uid, nocast, notag, level - 1, all, allpic, pic,
                        tagfilter)).ToList();

            if (gf.FilterType == ((int)GroupFilterType.Season | (int)GroupFilterType.Directory))
                f.filters = filters.OrderBy(a => a.name, new SeasonComparator()).ToList();
            else
                f.filters = filters.OrderByNatural(a => a.name).ToList();

            f.size = f.filters.Count();
            f.url = APIHelper.ConstructFilterIdUrl(ctx, f.id);

            return f;
        }
    }
}