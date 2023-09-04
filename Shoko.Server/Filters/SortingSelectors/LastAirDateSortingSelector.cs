using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastAirDateSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public DateTime DefaultValue { get; set; }
    public override object Evaluate(IFilterable f) => f.LastAirDate ?? DefaultValue;
}
