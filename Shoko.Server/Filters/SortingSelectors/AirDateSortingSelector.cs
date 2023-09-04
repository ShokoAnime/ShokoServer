using System;

namespace Shoko.Server.Filters.SortingSelectors;

public class AirDateSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public DateTime DefaultValue { get; set; }

    public override object Evaluate(Filterable f)
    {
        return f.AirDate ?? DefaultValue;
    }
}
