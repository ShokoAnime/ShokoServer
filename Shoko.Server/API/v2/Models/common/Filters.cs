using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

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
        bool all, bool allpic, int pic, TagFilter.Filter tagfilter, IReadOnlyDictionary<FilterPreset, IReadOnlyList<IGrouping<int, int>>> evaluatedResults = null)
    {
        var f = new Filters { id = gf.FilterPresetID, name = gf.Name };
        var hideCategories = ctx.GetUser().GetHideCategories();
        var children = f.id < 0
            ? RepoFactory.FilterPreset.GetAllFiltersForLegacy().AsParallel().Where(a => a.ParentFilterPresetID == f.id)
            : RepoFactory.FilterPreset.GetByParentID(f.id).AsParallel();
        var gfs = children.Where(a =>
            {
                if (a.Hidden) return false;
                // return true if it's not a tag
                if (!a.FilterType.HasFlag(FilterPresetType.Tag)) return true;
                if (hideCategories.Contains(a.Name)) return false;
                return !TagFilter.IsTagBlackListed(a.Name, tagfilter);
            })
            .ToList();

        if (evaluatedResults == null)
        {
            var evaluator = ctx.RequestServices.GetRequiredService<IFilterEvaluator>();
            evaluatedResults = evaluator.BatchPrepareFilters(gfs, ctx.GetUser());
            gfs = gfs.Where(a => evaluatedResults[a].Any()).ToList();
        }

        if (level > 0)
        {
            var filters = gfs.Select(cgf =>
                Filter.GenerateFromGroupFilter(ctx, cgf, uid, nocast, notag, level - 1, all, allpic, pic, tagfilter, evaluatedResults[cgf].ToList())).ToList();

            f.filters = gf.FilterType == (FilterPresetType.Season | FilterPresetType.Directory)
                ? filters.OrderBy(a => a.name, new CL_SeasonComparator()).Cast<Filters>().ToList()
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
