using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Filters;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common;

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

    internal static Filters GenerateFromGroupFilter(HttpContext ctx, FilterPreset gf, int uid, bool nocast,
        bool notag, int level,
        bool all, bool allpic, int pic, TagFilter.Filter tagfilter, Dictionary<FilterPreset, IEnumerable<IGrouping<int, int>>> evaluatedResults = null)
    {
        var f = new Filters { id = gf.FilterPresetID, name = gf.Name };
        var hideCategories = ctx.GetUser().GetHideCategories();
        var gfs = RepoFactory.FilterPreset.GetByParentID(f.id).AsParallel().Where(a =>
            {
                if (a.Hidden) return false;
                // return true if it's not a tag
                if ((a.FilterType & GroupFilterType.Tag) == 0) return true;
                if (hideCategories.Contains(a.Name)) return false;
                return !TagFilter.IsTagBlackListed(a.Name, tagfilter);
            })
            .ToList();

        if (evaluatedResults == null)
        {
            var evaluator = ctx.RequestServices.GetRequiredService<FilterEvaluator>();
            evaluatedResults = evaluator.BatchEvaluateFilters(gfs, ctx.GetUser().JMMUserID);
            gfs = gfs.Where(a => evaluatedResults[a].Any()).ToList();
        }

        if (level > 0)
        {
            var filters = gfs.Select(cgf =>
                Filter.GenerateFromGroupFilter(ctx, cgf, uid, nocast, notag, level - 1, all, allpic, pic, tagfilter, evaluatedResults[cgf].ToList())).ToList();

            f.filters = gf.FilterType == (GroupFilterType.Season | GroupFilterType.Directory)
                ? filters.OrderBy(a => a.name, new SeasonComparator()).Cast<Filters>().ToList()
                : filters.OrderByNatural(a => a.name).Cast<Filters>().ToList();

            f.size = filters.Count;
        }
        else
        {
            f.size = gfs.Count;
        }

        f.url = APIV2Helper.ConstructFilterIdUrl(ctx, f.id);

        return f;
    }
}
