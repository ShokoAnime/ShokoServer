using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.SortingSelectors;

public class LastAirDateSortingSelector : SortingExpression<DateTime>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public DateTime DefaultValue { get; set; }
    public override DateTime Evaluate(IFilterable f) => f.LastAirDate ?? DefaultValue;
}
