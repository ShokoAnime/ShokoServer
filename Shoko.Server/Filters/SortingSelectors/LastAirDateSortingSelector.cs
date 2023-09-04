using System;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastAirDateSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(Filterable f)
    {
        return f.LastAirDate ?? DefaultValue;
    }
}
