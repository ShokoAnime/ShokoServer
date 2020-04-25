using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
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
        public override string type => string.Intern("filters");

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<Filters> filters { get; set; }

        public Filters()
        {
            art = new ArtCollection();
            filters = new List<Filters>();
        }

        internal static Filters GenerateFromGroupFilter(HttpContext ctx, SVR_GroupFilter gf, int uid, bool nocast, bool notag, int level,
            bool all, bool allpic, int pic, TagFilter.Filter tagfilter)
        {
            Filters f = new Filters
            {
                id = gf.GroupFilterID,
                name = gf.GroupFilterName
            };

            var _ = new List<string>();
            var gfs = RepoFactory.GroupFilter.GetByParentID(f.id).AsParallel()
                // Not invisible in clients
                .Where(a => a.InvisibleInClients == 0 &&
                            // and Has groups or is a directory
                            ((a.GroupsIds.ContainsKey(uid) && a.GroupsIds[uid].Count > 0) ||
                             (a.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory) &&
                            // and is not a blacklisted tag
                            !((a.FilterType & (int) GroupFilterType.Tag) != 0 &&
                            TagFilter.IsTagBlackListed(a.GroupFilterName, tagfilter, ref _)));

            if (_.Count > 0)
            {
                gfs = gfs.Concat(_.Select(tag => RepoFactory.GroupFilter.GetAll().FirstOrDefault(a =>
                {
                    if (a.FilterType != (int) GroupFilterType.Tag) return false;
                    if (tag.Equals("Original Work"))
                        return a.GroupFilterName.Equals("new");

                    return a.GroupFilterName.Equals(tag, StringComparison.InvariantCultureIgnoreCase);
                })).AsParallel());
            }

            if (level > 0)
            {
                var filters = gfs.Select(cgf =>
                    Filter.GenerateFromGroupFilter(ctx, cgf, uid, nocast, notag, level - 1, all, allpic, pic,
                        tagfilter)).ToList();

                if (gf.FilterType == ((int)GroupFilterType.Season | (int)GroupFilterType.Directory))
                    f.filters = filters.OrderBy(a => a.name, new SeasonComparator()).Cast<Filters>().ToList();
                else
                    f.filters = filters.OrderByNatural(a => a.name).Cast<Filters>().ToList();
                f.size = filters.Count;
            }
            else
                f.size = gfs.Count();

            f.url = APIV2Helper.ConstructFilterIdUrl(ctx, f.id);

            return f;
        }
    }
}